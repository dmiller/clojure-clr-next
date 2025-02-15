---
layout: post
title: Symbolic of what?
date: 2023-01-06 00:00:00 -0500
categories: general
---

An exercise in symbology.


## Introduction 

I remember trying to figure out symbols when I first learned Lisp.  
My predecessor languages (Fortran, Basic, Pascal) had not prepared me.  
(You might guess from that list that my first encounter was some years ago.)  
I was in good shape with symbols across multiple dialects of Lisp over the years, 
though certainly there was non-trivial variation. 

Clojure forced yet another re-calibration.  
Symbols are a simple construct, but are given meaning by a complex web of interactions
among the Lisp reader, namespaces, the Clojure compiler and the Clojure runtime.
I hope to document here where meaning arises.



## Background

One should go the documentation.

- [Learn Clojure -Syntax: Symbols and idents](https://www.clojure.org/guides/learn/syntax#_symbols_and_idents)
- [ The Reader: Reader forms: Symbols](https://clojure.org/reference/reader#_symbols)
- [Data Structures - Symbols](https://clojure.org/reference/data_structures#Symbols)


Apparently that is not enough for some.

- [The Relationship Between Clojure Functions, Symbols, Vars, and Namespaces](https://8thlight.com/insights/the-relationship-between-clojure-functions-symbols-vars-and-namespaces) by Aaron Lahey.  (Kudos for the Oxford comma.)
- [What are symbols in Clojure?](https://www.reddit.com/r/Clojure/comments/j3b5hc/what_are_symbols_in_clojure/?rdt=63497)
- [Explain Clojure Symbols](https://stackoverflow.com/questions/1175920/explain-clojure-symbols)

The first article is especially releveant. 

So much for preparation.

# Naked symbolism

The `Symbol` data structure is relatively simple -- it has an optional namespace and a name, both strings.  
`Symbol` does support metadata.

```F#
type Symbol private (_meta: IPersistentMap, _ns: string, _name: string) =
    inherit AFn()

    // cached hashcode value, lazy
    let mutable hasheq = 0

    // cached string representation, lazy
    let mutable _str: string = null

    private new(ns, name) = Symbol(null, ns, name)
```

The constructors are private. One uses the `intern` methods to create `Symbol`s.

```F#
    /// Intern a symbol with the given name  and namespace-name
    static member intern(ns: string, name: string) = Symbol(null, ns, name)

    /// Intern a symbol with the given name (extracting the namespace if name is of the form ns/name)
    static member intern(nsname: string) =
        let i = nsname.IndexOf('/')

        if i = -1 || nsname.Equals("/") then
            Symbol(null, nsname)
        else
            Symbol(nsname.Substring(0, i), nsname.Substring(i + 1))
```

And that's it.  Well, other than the pieces needed to fit into the ecosystem: 
overrides for `Equals`, `GetHashCode`, `ToString`; 
and implementations for the `IComparable`, `IHashEq`, `IMeta`, `IObj`, `Named`, and `IFn` interfaces.
Simplifying the last of these is why we have `Symbol` inherit from `AFn`.
Oh, and some efficiency hacks, such as (lazily) caching the hashcode and string representation.

One can create `Symbol`s programmatically in Clojure code with the []`symbol` function](https://clojuredocs.org/clojure.core/symbol).
But clearly most `Symbol` creation is done the Reader.

## The Reader and Symbols

One should look again at [the section on Symbols in the Reader reference](https://clojure.org/reference/reader#_symbols).

The Reader processes a sequence of characters and constructs a data structure.  
Conceptually, it is rather straightforward.  
In implementation, not so much.  The F# source is almost 1700 lines of code.
Some of the complexity arises from the need to handle various error conditions 
-- end-of-stream when not complete, invalid characters, etc. -- in an intelligent matter.
The reader is inherently recursive -- that adds a few twists.
But most of the complexity arises from all the special cases and special goodies that the reader supports, such as 

- `{` _maps_ `}`
- `#{` _sets_ `}`
- `[` _vectors_ `]`
- `^`_metadata on  something_
- `#` ( _functions with_ `%` _args_ `)`
- `` `(`` _backquote forms with_ `~` _unquote and_ `~@` _unquote-splicing_ `)`

and more.

Let's stick with the conceptually simple. 
The reader looks at the next character in the input and decides what to do.

- If the next character is a digit, or a `+` or `-` followed by a digit, read a number.
- If it is one of the so-called _macro_ characters 
 -- that would include `(` (read a list) and  `{` (read a map), 
   but also `"` (read a string) and `\` (read a character) -- then call the special reader for that thing.
- otherwise, we have a _token_.

For tokens, we accummulate characters until we hit the end of the input or a charact that can't be in a token.
Characters that can't be in a token are whitespace  or terminating macro character (that includes characters like `(` and `)`)`).
For the JVM version of the reader, that is entirely the definition of a token.
On the CLR, we added `|`-escaping to make it possible to enter CLR typenames that have otherwise unacceptable (terminating) characters; 
this complicates token reading just a bit.

Once the token has been delineated, the reader interprets it. 
The reader handles the tokens `nil`, `true`, and `false` as special cases.
There are three possible outcomes:

- Symbol
- Keyword
- Don't know (throws an exception)

And that's it.  Almost.

Some of the specialized reader methods must go further and _interpret_ symbols that are encountered during their processing.
One thinks of _interpretation_ typically as the domain of the evaluator/compiler, not the reader.  But in the Clojure reader,
it cannot be avoided.  The Clojure(JVM) and ClojureCLR code for the reader makes this quite apparent;
there are calls to methods defined over in the `Compiler` and `HostExpr` classes. 
For ClojureCLR.Next, I wanted the reader to be defined before I got around to the compiler.  
In particular, because of F# circularity restrictions, 
I didn't want to have put the reader and at least the parser pass of the compiler into one massive file.
I ended up duplicating the compiler methods used by the reader in the reader code itself.
These duplicates could be simplified -- they don't have to deal with some compiler-specific issues such as local binding scopes.

Where does symbol interpretation arise in the reader?  Primarily in [syntax quote](https://clojure.org/reference/reader#syntax-quote)

> For Symbols, syntax-quote _resolves_ the symbol in the current context, yielding a fully-qualified symbol (i.e. namespace/name or fully.qualified.Classname). If a symbol is non-namespace-qualified and ends with '#', it is resolved to a generated symbol with the same name to which '_' and a unique id have been appended. e.g. x# will resolve to x_123. All references to that symbol within a syntax-quoted expression resolve to the same generated symbol.

Here, _resolves_ is what I was calling _interprets_.  For example:

```clojure
(ns my.long.namespace.name)
(def something 7)
(in-ns 'user)
(alias `mlnn my.long.namespace.name)

mlnn/something   ; => 7
`mlnn/something  ; => my.long.namespace.name/something

`Int64           ; => System.Int64
```

[NB: I discovered that the current version of ClojureCLR did not do the last line correctly.  For the last 15 years.  
By the time you read this, the fix will be in.]

These operations require interpretation of symbols in the context of namespace aliases and type mappings.
The first step on the road to interpretation begins with namespaces.

## Namespaces

Best to go read up on [namespaces](https://clojure.org/reference/namespaces).

> Namespaces are mappings from simple (unqualified) symbols to Vars and/or Classes. Vars can be interned in a namespace, using def or any of its variants, in which case they have a simple symbol for a name and a reference to their containing namespace, and the namespace maps that symbol to the same var. A namespace can also contain mappings from symbols to vars interned in other namespaces by using refer or use, or from symbols to Class objects by using import. 


The code for `Namespace` is considerably longer than that for `Symbol`.  But the data structure itelf is simple enough.

```F#
type Namespace(_name: Symbol) =
    inherit AReference((_name :> IMeta).meta ())

    // variable-to-value map
    let mappings: AtomicReference<IPersistentMap> =
        AtomicReference(DefaultImports.imports)

    // variable-to-namespace alias map
    let aliases: AtomicReference<IPersistentMap> =
        AtomicReference(PersistentArrayMap.Empty)

    // All namespaces, keyed by Symbol
    static let namespaces = ConcurrentDictionary<Symbol, Namespace>()

    static let clojureNamespace = Namespace.findOrCreate (Symbol.intern "clojure.core")

    // Some accessors
    member _.Name = _name
    member _.Aliases = aliases.Get()
    member _.Mappings = mappings.Get()
```

We automatically create a namespace for `clojure.core` when the `Namespace` type is initialized.
The two maps are set in `AtomicReference`s to allow for multithreaded access.  
In particular, updates to these maps are potentially occasions for race conditions to occur.
That is dealt with in the updating code.  There are a lot of rules and special cases in the code for adding mapping and aliases.
For our purposes here, we can ignore that.  What _is_ important here is finding what a symbol 'means' relative to a namespace.
The entry points for that are the following.

```F#

    // Get the value a symbol maps to.  Typically a Var or a Type.
    member this.getMapping(sym: Symbol) = this.Mappings.valAt (sym)

    // Find the Var mapped to a Symbol.  When we want only a Var.
    member this.findInternedVar(sym: Symbol) =
        match this.Mappings.valAt (sym) with
        | :? Var as v when Object.ReferenceEquals(v.Namespace, this) -> v
        | _ -> null

    // Find a Namespace aliased by a Symbol.
    member this.lookupAlias(alias: Symbol) =
        this.Aliases.valAt (alias) :?> Namespace        
```

## Evaluation

Most interpretation of symbols is done during evaluation/compilation. Looking at the traditional phases of interpreters/compilers,
the lisp reader performs the role of lexical analysis and partial parsing.  
The reader does more than just tokenizing, in the sense of producing a linear stream of tokens.
It has already organized that into the nesting of lists, vectors, and other structural elements.
This is handed to the evaluator/compiler, which does the rest of the parsing, type checking, and code generation.
Most of the work of symbol interpretation is done during the parsing phase, which produces an abstract syntax tree (AST).

Let us contemplate the following situation. 

```Clojure
(ns big.deal.namespace)

(defn g [z] (inc z))
(defn h [x] (g x))

(ns ns1 
  (:require [big.deal.namespace :as ns2]))   ; unnecessary in real life because this import is done automatically

(defn f [x y z] [z y x])
```

Now consider the situation where we have read the following code and are evaluating it when `ns1` is the current namespace.

```Clojure
(fn* [x] 
   (let* [y  7]
      (f (ns2/g Int64/MaxValue y) 
         (String/.ToUpper x)
         (big.deal.namespace/h System.Text.StringBuilder))))
```
(Note: the parser would see `fn*` instead of `fn` -- the latter is macro that expands to the former. Similarly for `let*`.)

The reader has the easy job.  No backquoting in there, so symbols are just symbols to it.  The parse will do the heavy lifting.

Looking at the call to `f` only, we are interpreting that form in a lexical binding scope where `x` and `y` are bound.
We need to interpret each symbol in that form:

```Clojure
f  x  y  ns2/g  big.deal.namespace/h  System.Int64  String/ToUpper System.Text.StringBuilder
```

`x` and `y` are easy.  They do not have a namespace, so the first step is look to see if they are currently bound in the lexical scope.
They are, so they resolve to local bindings expressions.

`f` also does not have namespace.  However, it not bound in the current lexical scope.  
It does not have a namespace, so it does not refer to directly or indirectly (via an alias) to a namespace.
The remaining option is that it has a mapping in the current namespace.  It does, to a `Var` and that is what we use.

`ns2/g` is a bit more complicated.  It has a namespace, so it won't be a local.  We need to determine what namespace `ns2` stands for.
The `NamespaceFor` method is used:

```F#
    static member NamespaceFor(inns: Namespace, sym: Symbol) : Namespace =
        //note, presumes non-nil sym.ns
        // first check against currentNS' aliases...
        let nsSym = Symbol.intern (sym.Namespace)

        match inns.lookupAlias (nsSym) with
        | null -> Namespace.find (nsSym)
        | _ as ns -> ns
```

In this context, the current namespace is `ns1` and `ns1` is an alias for `big.deal.namespace`.  So we look up `g` in `big.deal.namspace`, finding a `Var`.

Next up is `Int64/MaxValue`. It does have a namespace, so it won't be a local.  
When we call `NamespaceFor` on `Int64`, no namespace is found. (`null` is returned.)
If you were to try to find a type `Int64`, you would fail.  There is a type named `System.Int64`.
Fortunately, all namespaces are set up with a mappings from unqualified names of types to their types.
Thus, there is a mapping from `Int64` to `System.Int64` in the `ns1` namespace.
When we have something of the form `Type/Member`, we look for the `Member` in as a property or field in that type.  
In this case, there is a property `MaxValue` in `System.Int64`, so we turn this into a static method call node.

`String/.ToUpper` is similar.  In this case, because this symbol appears in the functional position of function invocation, 
given that `String` maps to `System.String`, we look for methods also. Beacause the name starts with a period, we look for an instance method, and find one.

Finally, we have `System.Text.StringBuilder`.  When we have a symbol with no namespace and periods in the name, we look for a type.
In this case, we do find a type.  If it didn't name a type, we would go on and treat the same as a symbol with no periods.


I wrote a debug printer for ASTs.  Here is the output of parsing the form above.

```Clojure
Fn ns1$fn__1
  invoke [ x ] 
    
    Let [ y
           = 7 (PrimNumeric)  ]
      
      Invoke: 
          Var: #'ns1/f
          Invoke: 
              Var: #'big.deal.namespace/g
              InteropCall: FieldOrProperty: System.Int64.MaxValue Static
              <y>
          InteropCall: InstanceZeroArityCall: System.String.ToUpper Instance
              <x>
          Invoke: 
              Var: #'big.deal.namespace/h
              = System.Text.StringBuilder (Other)
```

## There's more

There are a lot more rules and special cases.  The test suite for the parse has almost 60 tests for symbol interpretation. 
This includes tests for looking up / resolving symbols in the context of namespaces and aliases and types, 
plus tests for the various AST nodes that can be created from symbols.

Here is a sample of test descriptions.  
The first two lists are for symbol lookup and resolution, used in parsing, 
but not looking at what AST node would be created.
Do you know all of these rules?

These are when the symbol has namespace:

- ns/name, ns is namespace alias, no var found for name (throws)
- ns/name, ns is namespace alias, not current namespace, var found, var is private, privates not allows (throws)
- ns/name, ns is namespace alias, not current namespace, var found, var is private, privates allowed (var returned)
- ns/name, ns is namespace alias, not current namespace, var found, var is public (var returned)
- ns/digit, but ns is not a type (throws)  -- this is something like `BadType/7`
- ns/digit, ns is a type (return array type)  -- this is something like `String/1`

These are when the symbol does not have a namespace:

- name has . in it, names type (return type) 
- name has . in it, does not resolve to a type (throws)  -- note that in the parser, we catch the exception and move on
- `in-ns` -- treated as a special case -- always found
- `ns` -- treated as a special case -- always found
- name found in current namespace (return var)  (there are variants in the resolve/lookup code that will create the `Var` if not found)

Several kinds of AST nodes can be created from symbols.  The details of node types is bit beyond where we can go here,
but perhaps you can get the gist:

- ns/name, ns names a `Type`, that type has a field or property with the given name  => InteropCall, type = FieldOrProperty, static
- ns/name ns names a `Type`, no field or property found, name does not start with a period  => QualifiedMethod, Static 
- ns/.name ns names a `Type`, no field or property found, name starts with a period  => QualifiedMethod, Instance 
- ^NotAType TypeName/FieldName, FieldName not in type TypeName => throws because the tag is not a type
- ^IsAType TypeName/FieldName, FieldName not in type TypeName => QualifiedMethod, Static, IsAType set as tag.
- ^[...types...] TypeName/FieldName, FieldName not in type TypeName => QualifiedMethod, Static, SignatureHint set

Without a namespace:

- name - has a local binding => Expr.LocalBinding
- not local, not a type, resolves to a Var, Var is macro => throws
- not local, not a type, resolves to a Var, Var is has `:sonst true` metadata  => Expr.Literal with Var as value
- not local, not a type, resolves to a Var, Var is not macro, not const => Expr.Var
- not local, not a type, does not resolve, allow-unresolved = true => Expr.UnresolvedVar
- not local, not a type, does not resolve, allow-unresolved = false => throws

You may thank me for not including the code that implements this.
Or even the tests.


## Creating the example

At the time of writing, ClojureCLR.Next cannot read and evaluate code.  It has the reader and it has the parser.
I set up the example as a unit test.  I didn't bother actually defining the functions `f`, `g`, and `h`; 
all that's needed is that those symbols map to `Var`s in their namespaces.  
The structure has to be created by hand by calling methods on the underlying data structures.
Here's what it looks like:

```F#
let ns1Name = "ns1"
let ns2Name = "big.deal.namespace"

let ns1 = Namespace.findOrCreate (Symbol.intern (ns1Name))
let ns2 = Namespace.findOrCreate (Symbol.intern (ns2Name))

let ns2Sym = Symbol.intern "ns2"
ns1.addAlias(ns2Sym, ns2)

let fSym = Symbol.intern "f"
let gSym = Symbol.intern "g"
let hSym = Symbol.intern "h"

ns1.intern (fSym) |> ignore
ns2.intern (gSym) |> ignore
ns2.intern (hSym) |> ignore

// Set the current namespace by binding *ns* 
Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

try

    let form = ReadFromString "(fn* [x] 
                                    (let* [y  7]
                                    (f (ns2/g Int64/MaxValue y) 
                                        (String/.ToUpper x)
                                        (big.deal.namespace/h System.Text.StringBuilder))))"
    let ast = Parser.Analyze(CompilerEnv.Create(Expression), form)

    // tests would go here.  I also printed the AST to a string so I could grab it in the debugger.
Finally
    Var.popThreadBindings()
```