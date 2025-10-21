---
layout: post
title: C4 - Is there a protocol for that?
date: 2025-10-20 00:00:00 -0500
categories: general
---

We take a look at how Clojure protocols are implemented.

## Introduction

Familiarity with Clojure protocols is assumed.  If you need a refresher, see the [Clojure protocols documentation](https://clojure.org/reference/protocols).

As explained in that document, protocols provide an alternative to interfaces for defining polymorphic behavior.  No inheritance hierarchy.  Any type can be extended to implement a protocol.

To determine how protocols are implemented requires reverse-engineering the relevant code in the Clojure core and Clojure compiler. There is no documentation. I did that work. Fifteen years after porting the code, I finally understand all of it. (Do not underestimate my ability to port code I don't understand. It is a skill.)

I will start with an overview of the data design and basic semantics of protocols, then move on to implementation details.

## Overview 

### Protocol data design and basic semantics

Let's start with a simple protocol definition with some extensions.

```clojure
(ns test.proto)

(defprotocol MyProto 
   "my prototype" 
   :extend-via-metadata true 
   (bar [this a b] "bar doc") 
   (baz [this a] [this a b] "baz docs"))

;; One might combine the following two extend calls into a single extend-protocol call.
;; But we will focus on extend in our discussion later, so let's stick with that.

(extend String
  MyProto
  { :bar (fn [this a b] (str "String: " this " " a " " b))
    :baz (fn 
		  ([this a] (str "String baz 1: " this " " a))
		  ([this a b] (str "String baz 2: " this " " a " " b)))
  }
  ;; Additional protocol + method-map pairs could go here
  ) 

(extend Object
  MyProto
  { :bar (fn [this a b] (str "Object: " this " " a " " b))
    :baz (fn 
		  ([this a] (str "Object baz 1: " this " " a))
		  ([this a b] (str "Object baz 2: " this " " a " " b)))
  }
  ;; Additional protocol + method-map pairs could go here
  )
```

This will macroexpand into code that does several things. 

- `(defonce MyProto {})` - defines a Var named `MyProto` in the `test.proto` namespace.  The initial value is an empty map.  We'll add to this in a minute.
- Generates an interface matching the protocol methods.  Primitive type hints are not allowed in protocol method signatures. In this case, there are no type hints, so everything is `Object`.

```clojure    
 (gen-interface
  :name  user.MyProto
  :methods
  ([bar [Object Object] Object]
   [baz [Object] Object]
   [baz [Object Object] Object]))
```

- Attach the doc string as metadata to the Var `#'MyProto`.
- Set the value of the `Var` `#'MyProto` to a map that contains all the protocol information.  We'll call this the _protocol map_.  The protocol map gets passed to a various functions implementing protocol functionality.  The protocol map contains the following key/value pairs:

| Key | Value | Description |
|-----|-------|:-------------|
| `:on` | `user.MyProto` | I believe no longer used |
| `:on-interface` | `test.proto.MyProto` | The interface type generated for the protocol |
| `:doc` | `"my prototype"` | The doc string for the protocol |
| `:extend-via-metadata` | `true` | The only supported option for now. |
| `:var` | `#'MyProto` | The Var holding the protocol map itself.   |
| `:method-map` | `{:baz :baz, :bar :bar}` | Used by the compiler.  | 
| `:sigs` | See below | Signatures of the protocol methods. |
| `:method-builders` | See below | Functions that help build the method dispatching code. |
| `:impls` | See below | Map of types implementing the protocol and their method implementations. |

All of these are filled in during the `defprotocol` expansion except for `:impls`, which is built up as types are extended to implement the protocol.
The value for `:sigs` in this example is:

```clojure
{:bar
    {:tag nil, :name bar, :arglists ([this a b]), :doc "bar doc"},
    :baz
    {:tag nil,
    :name baz,
    :arglists ([this a] [this a b]),
    :doc "baz docs"}}
```

I can't find any place it is used.  You are welcome to use it as you please.

The value for `:method-builders` is a map.  The keys are the `Var`s holding the protocol method functions, such as `bar` and `baz`.  The values are functions that help build the method dispatching code for each function in the protocol interface.   When those `Var`s are created they have metadata attached.  The metadata is mostly standard function metadata, such as `:arglists` and `:doc`.  The important part is the `:protocol` key in the metadata: the value is the protocol `Var` itself. If we see a `:protocol` entry, we know the function is a protocol method and the protocol is is associated with.

The value for `:impls` is built up as types are extended to implement the protocol.  After the two `extend` calls above, the value of `:impls` in the protocol map will be:

```clojure
 {System.String
  {:bar (fn [this a b] (str "String: " this " " a " " b))
   :baz (fn 
		  ([this a] (str "String baz 1: " this " " a))
		  ([this a b] (str "String baz 2: " this " " a " " b)))
  System.Object
  {:bar (fn [this a b] (str "Object: " this " " a " " b))
   :baz (fn 
          ([this a] (str "Object baz 1: " this " " a))
          ([this a b] (str "Object baz 2: " this " " a " " b)))}
```

### The endpoints: protocol functions

A protocol function, such as `bar` in our example, is just a regular Clojure function.  It uses its first argument to determin the actual implementation function to call.  The algorithm is mention in [Protocols, Extend via Metadata ](https://clojure.org/reference/protocols#_extend_via_metadata):    
    
Protocol implementations are checked first for direct definitions (defrecord, deftype, reify), then metadata definitions, then external extensions (extend, extend-type, extend-protocol).

"Direct definition" means that the type of the first argument implements the protocol interface. Typically this is via the methods indicated, though one supposes other mechanisms could be used.

Regarding metadata definitions:

    When :extend-via-metadata is true, values can extend protocols by adding metadata where keys are fully-qualified protocol function symbols and values are function implementations. 

This extension is for individual objects, not types; the other two mechanisms are type-based. Similar to the example in the documentation, we can do this:

```clojure
(def thingy (with-meta {:name "fred" } 
                       {`bar (fn [this a b] 
                                (str "Thingy: " (:name this) " " a " " b))}))
```
`` `bar`` evaluates to `test.proto/bar`, the fully-qualified symbol for the `bar` function.  Now we can call:

```clojure
(bar thingy 1 2)  ; => "Thingy: fred 1 2"
```

Each implementation function maintains a cache of method implementations for the types it has seen. The cache, of type `MethodImplCache`, is stored in a private field of type `AFunction`, the base type for all the function types created by the Clojure compiler.  This cache is only for types that have extended.  The caches are created when during the `defprotocol` expansion and invalidated when new the protocol is extended to new types.


### The compiler

The compiler has some optimizations for dealing with protocol function calls.  When analyzing an invocation

```clojure
(fexpr arg1 arg2 ...)
```

after we've eliminated all the special cases and know that `fexpr` is a `Var` referrring to a regular function, 
we check the `Var` to see if it has `:protocol` metadata.  If so, we know it is a protocol function.  We can then generate optimized code to do the protocol dispatch. We mark the invocation as a protocol invocation, record the protocol the function is associated with.  It verifies that the name of the function is in `:method-map` map of the protocol map; if not, it raises an error.  It also records the method info from the corresponding method in the interface type associated with the protocol.  This 'protocol callsite' is registered with function expression. The function class will allocate static fields to hold the most recently used type the function has been called for.  The invocation expression generates code to check for the mostly recently used type, to check for the type in the call implementing the interface type and calling the corresponding method, correctly, and finally acccessing the `Var`'s value to get the function to call.


## Implementation details

If you are interested in looking a little deeper at the code behind all of this, read on.  Else, class dismissed.

### defprotocol

`defprotocol` is a monster of macro magic.  Rather than puzzling our way through the code, to look at what it produces.   I took the macroexpansion of the `defprotocol` above call and did some renaming of gensym'd variables from the the backquote templates to make it more readable.  And cut out some bits. And added some comments.


```clojure
;; Several things going on here.
(do

 ;; def the name MyProto to have an initial empty map (if does not already have a value)
 ;; we will augment this map below.
 (defonce MyProto {}) 
 
 ;; Create the interface for the protocol
 ;; Fairly easy to generate this from the method signatures given in the defprotocol call.
 (gen-interface
  :name  user.MyProto
  :methods
  ([bar [Object Object] Object]
   [baz [Object] Object]
   [baz [Object Object] Object]))
   
 // Attach the doc string to the protocol var  
 (alter-meta! #'MyProto
    assoc
    :doc "my prototype")  
  
 ;; assert-same-protocol is private (the #' in front is a clue).
 ;; It warns if bar or baz are defined for a different protocol or are already bound
 (#'assert-same-protocol #'MyProto '(bar baz))
 
 ;; The value of Var MyProto is a map that carries the protocol data.
 ;; That map is constructed by the assoc call below.
 ;; There are a lot of moving parts here; we'll discuss them below.
 (alter-var-root
  #'MyProto
  merge
  
  (assoc
  
   ;; Basic data.  See the comments above on these keys/values.
   {:on 'user.MyProto, 
    :on-interface test.proto.MyProto, 
    :doc "my prototype", 
    :extend-via-metadata true}
	
   ;; Signatures of the protocol methods.
  
   :sigs
   '{:bar
     {:tag nil, :name bar, :arglists ([this a b]), :doc "bar doc"},
     :baz
     {:tag nil,
      :name baz,
      :arglists ([this a] [this a b]),
      :doc "baz docs"}}
	  
   ;; The var holding the protocol map itself.   
   :var
   #'MyProto
   
   ;; Used by the compiler.  See below.
   :method-map
   {:baz :baz, :bar :bar}
   
   ;; This is the killer piece.
   ;; The value for :method-builders is a map.
   ;; key = the Var holding the function definition for a protocal method, such as bar or baz
   ;; value = a function that helps build the method dispatching code.

   :method-builders
   {
   
   ;; Key for 'bar'
   ;; the intern maps the symbol 'bar' to a Var in the current namespace.
   ;; We add the standard function metadata to the Var created for the protocol method 'bar'
   ;; Note the :protocol key in the metadata -- this is how we know this is a protocol method.
   (intern *ns*
     (with-meta 'bar
       (merge
         '{:tag nil, :name bar, :arglists ([this a b]), :doc "bar doc"}
         {:protocol #'MyProto})))

    ;; value = builder for 'bar'   
    ;; For now, just note that this function creates a function with a cache and returns it.
    (fn [cache]
     (let  [direct-access  (fn ([^test.proto.MyProto this a b]  (. this (bar a b))))
            new-fn
              (fn myfn
                ([this a b]
                  ;; some very gnarly code we'll look at later.
                ))]
        (set! (.__methodImplCache new-fn) cache)
        new-fn)),

    ;; additional key/value entries for other protocol methods (namely, baz) would appear here.  
    }))
	  
 ;; Magic invocation to use the builders to set up the method dispatching.
 ;; This will use the builders above to create the functions for bar, baz.
 (-reset-methods MyProto)
 
 ;; Let's get out of here.
 'MyProto)
```

There is no `:impls` entry yet; that gets built up as types are extended to implement the protocol.

Translating from the code of the `defprotocol` invocation to this macroexpansion is tedious and not all that entertaining or edifying.  Let's move on.


### Extending a protocol

There are three functions to extend types to implement protocols: `extend`, `extend-type`, and `extend-protocol`.
`extend-protocol`  macroexpands to calls to `extend-type`; `extend-type` macroexpands to calls to `extend`.  That is fairly straightforward  macro work.  Let's look at `extend`.  Per the documentation, `extend` takes a type and protocol + method-map pairs:

```clojure
(extend String
  MyProto
  { :bar (fn [this a b] (str "String: " this " " a " " b))
    :baz (fn 
		  ([this a] (str "String baz 1: " this " " a))
		  ([this a b] (str "String baz 2: " this " " a " " b)))
  }
  ;; Additional protocol + method-map pairs could go here
  )
  
```

Note that `extend` is a regular function, not a macro. Arguments are evaluated.
The `String` symbol is evaluated;  it resolves to `System.String`.
The `MyProto` symbol resolves to the to the protocol `Var`;  that `Var`'s value, the protocol map, is passed into `extend`.

The method maps are keyed by the keyword-ized names of methods in the interface.  The values are functions that implement the methods.  We just iterate through each pair.  There are some checks for actually having a protocol and for the type not being already extended to this protocol.
As a side note, the private function `protocol?` is used to check whether the first argument is a protocol map: if the map has an `:on-interface` key, it is thought to be a protocol map.

```clojure
(defn- protocol?
  [maybe-p]
  (boolean (:on-interface maybe-p)))
```

After making the validity checks, the key piece of code in `extend` is this:

```clojure
 (-reset-methods (alter-var-root (:var proto) assoc-in [:impls atype] mmap))
```
`proto` is the protocol map; `(:var proto)` grabs the `Var` for the protocol.  We are going to alter its root value.  `atype` is the type being extended, here `System.String`.  `mmap` is the method map passed into the call.  So this code creates a modified protocol map , adding an entry to the `:impls` map for the type being extended.   

 `-reset-methods` is passed the altered protocol map.  It needs to set up the method dispatching code for each protocol method.  (You will recall that `-reset-methods` is also called as the last step in the `defprotocol` work.  It sets up the method functions with no types being extended yet.) Here is the code:

```clojure
(defn -reset-methods [protocol]
  (doseq [[^clojure.lang.Var v build] (:method-builders protocol)]
   (let [cache (clojure.lang.MethodImplCache. (symbol v) protocol (keyword (.sym v)))]
      (.bindRoot v (build cache)))))
```

We have just entered the Cave of Complexity.  Let us perform reasoning.
- Our protocol map contains a `:method-builders` key.  The value is a sequence of pairs.  
- `v` = the first element of a pair, a `Var`, one of our protocol method `Var`s, such as `bar` or `baz`.
- `build` = the second element of pair; by inspection, a function that takes a `MethodImplCache` (whatever that is) instance.
- We create a new `MethodImplCache` from `v` and the protocol map. The values passed for `bar` will be:

```clojure
(symbol v)         =  test.proto/bar
protocol           =   <the protocol map>
(keyword (.sym v)) = :bar
```

`MethodImplCache` is defined in the compiler code.  We'll get to it later.

That cache object is passed into the builder function.  We're still not quite ready to look at that code.  Patience.

## Accessing protocol information

There are functions that that provide information about a given protocol:  `extends?` is used to test whether a type implements a protocol;  `satisfies?` is used to test whether an object implements a protocol; and  `extenders` returns a list of the types explicitly implementing a protocol.  All of these functions are defined in the `clojure.core` source code; look in `core_deftype.clj`.

We can find out which types have been extended by calling `extenders`.  For our example above, after the two `extend` calls, we have:

```clojure
(extenders MyProto)  ; => (System.String System.Object)
```

`extenders` is defined by 

```clojure
(defn extenders 
  "Returns a collection of the types explicitly extending protocol"
  {:added "1.2"} 
  [protocol]
  (keys (:impls protocol)))
  ```

Simply, the keys of the `:impls` map in the protocol map are the types that have been extended.

The test for a type extending a protocol is done by `extends?`:

```clojure
(defn extends? 
  "Returns true if atype  extends protocol"
  {:added "1.2"} 
  [protocol atype]
  (boolean (or (implements? protocol atype)
               (get (:impls protocol) atype))))
```

The last clause here checks the `:impls` map for the type.  The `implements?` function is a private function:

```clojure
(defn- implements? [protocol atype]
  (and atype (.IsAssignableFrom ^Type (:on-interface protocol) atype)))  
```

Looking at the protocol map, we see that `(:on-interface protocol)` is the interface type created for the protocol, `test.proto.MyProto`.  `IsAssignableFrom` checks whether `atype` implements that interface.  This allows for types that implement the protocol interface directly, without going through `extend`.

`extends?` asks about a type.  Extension by metadata is per-object, not per-type, so that is not involved here.

The last function in this group of tests is `satisfies?`:

```clojure  
(defn satisfies? 
  "Returns true if x satisfies the protocol"
  {:added "1.2"} 
  [protocol x]
  (boolean (find-protocol-impl protocol x)))
```

This is value-oriented, not type-oriented.  However it still does not check for extension by metadata.  It calls `find-protocol-impl` to do the real work.

```clojure
;; not private, but not advertised
(defn find-protocol-impl [protocol x]
  (if (instance? (:on-interface protocol) x)
    x
    (let [c (class x)
          impl #(get (:impls protocol) %)]
      (or (impl c)
          (and c (or (first (remove nil? (map impl (butlast (super-chain c)))))
                     (when-let [t (reduce1 pref (filter impl (disj (supers c) Object)))]
                       (impl t))
                     (impl Object)))))))

(defn- super-chain [^Type c] 
  (when c
    (cons c (super-chain (.BaseType c)))))  
``` 

`superchain` returns a sequence of the type and its supertypes, up to `Object`.

 The `or`s in `find-protocol-impl` make the first match win.  
 - If `x`  is an instance of the protocol interface, return `x`.  Direct implementation of the interface wins over extensions.
 - If the class of `x` is in the `:impls` map, return the corresponding value.  Direct extension wins over inherited extensions.
 - Look up through the superchain.  Closest direct extension of a base class wins.
 - Look also through interfaces supported by `x`'s class.  (The difference between `super-chain` and `supers`.)  Implemention of an interface of `x`'s class comes last.  If more than one interface supports it, there is no priority ordering.  Whichever is first in the list wins.

## MethodImplCache

We still have to deal with `-reset-methods` and the method builders.  To do that, we need to start moving into the compiler code.  The first place to to look is `clojure.lang.MethodImplCache`. 

```c#
public sealed class MethodImplCache
{
    // an entry in _map (below)
    public sealed class Entry
    {
        readonly Type _t;
        public Type T => _t;

        readonly IFn _fn;
        public IFn Fn => _fn;

        public Entry(Type t, IFn fn)
        {
            _t = t;
            _fn = fn;
        }
    }

    // Fields

    private readonly IPersistentMap _protocol;  // The protocol this cache is for 
    private readonly Keyword _methodk;          // The keyword for the method this cache is for
    private readonly Symbol _sym;               // The symbolized name of the protcol
    public readonly int _shift;                 // Hash table parameters (see below)
    public readonly int _mask;
    private readonly object[] _table;   // simple hash table mapping Type to entry, stored in an array (no collision resolution)
    public readonly IDictionary _map;   // alternative mapping from Type to entry (used when we cannot min-hash the keys)
    Entry _mre;                         // The most recently used entry

    // Accessors for fields
    // ...

    // Constructors

    // This constructor is used only by defprotocol to create an empty cache.
    public MethodImplCache(Symbol sym, IPersistentMap protocol, Keyword methodk)
        : this(sym, protocol, methodk, 0, 0, RT.EmptyObjectArray) { 

    // This constructor is called by expand-method-impl-cache when we can min-hash the keys.
    public MethodImplCache(Symbol sym, IPersistentMap protocol, Keyword methodk, int shift, int mask, Object[] table)
    { /* Initialize fields */ }

    // This constructor is called by expand-method-impl-cache when we cannot min-hash the keys. 
    public MethodImplCache(Symbol sym, IPersistentMap protocol, Keyword methodk, IDictionary map)
   { /* Initialize fields */ }

    // implementation of fnFor and FindFnFor:  See below.
}
```

We know in rough terms that a `MethodImplCache` instance is going to be attached to a function, based on code we saw earlier:

```clojure
(set! (.__methodImplCache new-fn) cache)
```

The base class for the functions created by the compiler is `AFunction`. The only instance field it adds is for this purpose:

```c#
  public abstract class AFunction : AFn, IObj, Fn, IComparer
  {
      [NonSerialized]
      public volatile MethodImplCache __methodImplCache;

      // ... more code ...
  } 
  ```

  So the implementation for `bar` will have a ``MethodImplCache` instance attached to it.  It must supply the mapping from types to method implementations for `bar`.  `MethodImplCache.fnFor` will be used to retrieve the appropriate method implementation based on the argument types.

```C#
public IFn fnFor(Type t)
{
    Entry last = _mre;
    if (last != null && last.T == t)
        return last.Fn;
    return FindFnFor(t);
}
```

Field `_mre` caches the most recently used entry.  If the requested type matches that, we have a quick hit.  Otherwise, we call `FindFnFor`.

```C#  
IFn FindFnFor(Type t)
{
    if (_map != null)
    {
        Entry e = (Entry)_map[t];
        _mre = e;
        return e?.Fn;
    }
    else
    {
        int idx = ((Util.hash(t) >> _shift) & _mask) << 1;
        if (idx < _table.Length && ((Type)_table[idx]) == t)
        {
            Entry e = ((Entry)table[idx + 1]);
            _mre = e;
            return e?.Fn;
        }
        return null;
    }
}
```

We have two possible ways to look up the type.  We have `_map`, a `Dictionary` mapping `Type` to `Entry`.  We also have `_table`, an array used as a hash table.  Either the `_map` or the `_table` will be used; the other will be `null`.  The array approach is more compact and faster, but it requires that the types being mapped can be (min-)hashed without collisions.  If that is not possible, we fall back to the dictionary approach.  We have two constructors for `MethodImplCache` to handle these two cases.  Which is used is determined by `expand-method-impl-cache`, which is called whenever we want to add a new entry to the cache.  Specifically, it is called by the builders.

The piece of code in `expand-method-impl-cache` that decides which constructor to use is this:

```clojure
;; cache - the method impl cache to expand
;; c     - the type to add
;; f     - the function implementing the method for type c
;; Returns: a new MethodImplCache with the new entry added

(defn- expand-method-impl-cache [^clojure.lang.MethodImplCache cache c f]
  (if (.map cache)

    ;; incoming cache is using a dictionary.  We will continue to use a dictionary.
    ;; create new dictionary with new entry added and create new MethodImplCache from that
    (let [cs (assoc (.map cache) c (clojure.lang.MethodImplCache+Entry. c f))] 
      (clojure.lang.MethodImplCache. (.sym cache) (.protocol cache) (.methodk cache) cs))

    ;; incoming cache is using a table.  We may or may not be able to continue using a table.
    ;; create a map of existing entries plus the new entry
    (let [cs (into1 {} (remove (fn [[c e]] (nil? e)) (map vec (partition 2 (.table cache)))))
          cs (assoc cs c (clojure.lang.MethodImplCache+Entry. c f))] 

      ;; Can we min-hash the keys?
      (if-let [[shift mask] (maybe-min-hash (map hash (keys cs)))]

        ;; We can min-hash the keys.  Build the table and call the table-based constructor.
        (let [table (make-array Object (* 2 (inc mask)))
              table (reduce1 (fn [^objects t [c e]]
                               (let [i (* 2 (int (shift-mask shift mask (hash c))))]
                                 (aset t i c)
                                 (aset t (inc i) e)
                                 t))
                             table cs)]
          (clojure.lang.MethodImplCache. (.sym cache) (.protocol cache) (.methodk cache) shift mask table))

        ;; We cannot min-hash the keys.  Build the dictionary and call the dictionary-based constructor.
        (clojure.lang.MethodImplCache. (.sym cache) (.protocol cache) (.methodk cache) cs)))))
```

The only other place `maybe-min-hash` makes an appearance is in the code that implements the `case` macro.  Doc string: "takes a collection of hashes and returns [shift mask] or nil if none found".  It examines the hashes of the keys (the types) and tries to find a shift and mask on the hashes that will produce unique indices into a hash table.  If it can find such a pair, we can use the array-based approach.  If not, we have to use the dictionary-based approach.


## The builders

Finally, we can look at the builder functions. ecall the context in which the builders are called:

```clojure
(defn -reset-methods [protocol]
  (doseq [[^clojure.lang.Var v build] (:method-builders protocol)]
   (let [cache (clojure.lang.MethodImplCache. (symbol v) protocol (keyword (.sym v)))]
      (.bindRoot v (build cache)))))
```

`-reset-method` really is a _reset_: it wipes out all caches for all protocol method functions.  `-reset-methods` is called from `defprotocol` -- we are initializing so there is nothing to wipe out.  `-reset-methods` is also called from `extend` -- we have added a new type to the protocol.  Adding a new type potentially invalidates cache entries.


There is one builder function for each protocol method.
The builder needs to return a properly prepared function for the protocol method, say `bar`.  That function will need a `MethodImplCache` instance attached to it.  The builder is passed a new, blank `MethodImplCache` instance set up for `bar`.  It will need to be added to the function we are creating., which it needs to attach to the function it creates.

A builder function has two possible forms, simpler when `:extend-via-metadata` is false or absent, more complex when it is true. Here is a simpler version for the `bar` method.


```clojure
(fn [cache]
    (let [direct-access (fn ([^test.proto.MyProtothis a b] (. this (bar a b))))
          new-fn
          (fn myfn ([this a b]
            (let [mfyn-cache (.__methodImplCache myfn)
                  fn-for (.fnFor mfyn-cache (clojure.lang.Util/classOf this))]
            (if fn-for
              (fn-for this a b)
              ((-cache-protocol-fn myfn this test.proto.MyProto2 direct-access) this a b)))))]
    (set! (.__methodImplCache new-fn) cache)
    new-fn))
```

Let's work outside in.   The builder takes in a `MethodImplCache` instance, `cache`. 


```clojure
    (fn [cache]
      
      ;; code that defines a function, bound to new-fn

      ;; install the cache we were given on the new function
      (set! (.__methodImplCache new-fn) cache)
      
      ;; return the new function
      new-fn))
```

Next we look at the function being created, `new-fn`.  What' important to realize is this: it is the same function every time!  The only thing that changes is the `MethodImplCache` instance attached to it. 

```clojure
(fn myfn ([this a b]
    (let [mfyn-cache (.__methodImplCache myfn)
            fn-for (.fnFor mfyn-cache (clojure.lang.Util/classOf this))]
    (if fn-for
        (fn-for this a b)
        ((-cache-protocol-fn myfn this test.proto.MyProto direct-access) this a b)))))
```

If you look carefully at this code, you don't even see a direct mention of `bar`.  The `MethodImplCache` instance carries that information.  Also `direct-access` is an accessor for the `bar` method on the interface type: `(fn ([^test.proto.MyProto this a b] (. this (bar a b))))`.  (BTW: you won't see that type tag on `this` in the macroexpansion.  It is attached during macroexpansion.)

How does this function `myfn` work? When we call `(bar "abc" 1 2)`, `this` will be be `"abc"`, `a` will be `1`, and `b` will be `2`.  The first thing we do is retrieve the `MethodImplCache` instance attached to `myfn`.  Then we call its `fnFor` method, passing in the class of `this`, which is `System.String`.  If we have an implementation for that type recorded in the cache, we get back a function, which we call with the arguments.  If not, we call `-cache-protocol-fn` to find and cache the appropriate implementation.  Note the parentheses around the call to `-cache-protocol-fn`:  it returns the function to call, which we then call with the arguments.
Here is `-cache-protocol-fn`, with some comments added:

```clojure
;; What is passed in:
;; pf = the protocol function being called (bar) -- has the MethodImplCache attached
;; x  = the 'this' argument (the object the protocol method is being called on)
; c  = the protocol interface type (test.proto.MyProto)
;; interf = direct-access = function that calls the protocol method directly on the object
;;        = (fn ([this a b] (. this (bar a b))))
;; Keep in mind -- the only reasone we are calling this is because the cache does not have entry
;; We are going to try to find an implementation, update the cache with it, and return it.
(defn -cache-protocol-fn [^clojure.lang.AFunction pf x ^Type c ^clojure.lang.IFn interf]
  (let [cache  (.__methodImplCache pf)
        ;; If the class of x implements the protocol interface, use the direct access function
        ;; Otherwise, try to find a method implementing the type of x.
        f (if (.IsInstanceOfType c x)
            interf
            (find-protocol-method (.protocol cache) (.methodk cache) x))]
    (when-not f
      (throw (ArgumentException. (str "No implementation of method: " (.methodk cache)
                                             " of protocol: " (:var (.protocol cache)) 
                                             " found for class: " (if (nil? x) "nil" (.Name (class x)))))))
    ;; Side-effect!!  Install a cache augmented with the new entry on our method function.
    (set! (.__methodImplCache pf) (expand-method-impl-cache cache (class x) f))

    ;; Return the function we found -- it's going to get called.
    f))


(defn find-protocol-method [protocol methodk x]
  ;; We covered find-protocol-impl above.
  (get (find-protocol-impl protocol x) methodk))    
```



```clojure
(fn [cache]
    (let  [direct-access  (fn ([^test.proto.MyProto this a b]  (. this (bar a b))))
        new-fn
            (fn myfn
            ([this a b]
                (let [myfn-cache (.__methodImplCache myfn)
                      fnfor (.fnFor myfn-cache (clojure.lang.Util/classOf this))]
                    (if (identical? fnfor direct-access)
                    (fnfor this a b)
                    (if-let [meta-fn
                                (when-let [meta_2 (meta this)]
                                ((.sym myfn-cache) meta_2))]
                        (meta-fn this a b)
                        (if cached-fnfor
                          (cached-fnfor this a b)
                          ((-cache-protocol-fn myfn this test.proto.MyProto direct-access) this a b)))))))]
    (set! (.__methodImplCache new-fn) cache)
    new-fn))
```

This encodes implementation lookup via: direct implementation (testing getting back the `direct-access` function from the cache), metadata-based implementation (looking up the method in the metadata of `this`), cached implementation (looking up the method in the cache), and finally, finding and caching the implementation via `-cache-protocol-fn`.

### Compiler hackery

In the overview, we mentioned that `InvokeExpr` detects calls on protocol functions and generates optimized code for them. 

Consider

```clojure
(defn caller [x y z]
  (bar x y z))
```

In the class implementing `caller`, the compiler will create a static field of type `Type` to hold the most recently encountered type for `x`.  Looking at the code generated for the `invokeStatic` method -- well, good luck.  ILSpy decompiles to not-very-readable C#.  The Java decompiler I was using actually translated the code incorrectly; it ommitted the branch of code that calls the `bar` function!
Here is my hand-translation into pseudocode.

```
// x = first argument to caller, the discriminant for the protocol dispatch
// y, z = other arguments to caller
// cachedClass = static field holding most recently seen type for x

if cachedClass == typeof(x)
   goto make_call;

if x is an instance of interface test.proto.MyProto
   return (x as test.proto.MyProto).bar(y, z);

cachedClass = typeof(x);

make_call:
    // invoke the #'bar function
    let f = (#'test.proto/bar.)GetRawRoot() as AFunction
    return f.invoke(x,y,y);
```

And with that, let's get out of here.
