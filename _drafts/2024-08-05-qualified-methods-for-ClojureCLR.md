---
layout: post
title: Qualfied methods -- for ClojureCLR
date: 2024-08-05 00:00:00 -0500
categories: general
---

Clojure has introduced a new _qualified methods_ feature allows for Java methods to be passed to higher-order functions.  This feature also provides alternative ways to invoke methods and constructors, and new ways to specify type hints.  We need to enhance this mechanism for ClojureCLR in the same way we enhanced 'classic' interop. 

I'll start with a quick review of 'classic' interop, including the additions made for interop with the CLR.
I'll introduce the new qualified methods mechanism.  
I'll conclude with looking at how the CLR extras will be incorporated in the new mechanism.

# 'Classic' interop

I'm going to assume basic familiarity with interoperabiltiy with the JVM/CLR, at least prior to the introduction of qualified methods.  If you need a refresher, you can look at the [Java interop section](https://clojure.org/reference/java_interop) of the Clojure reference.  

There are several pages on the ClojureCLR wiki that talk about the additional features of CLR interop:

- [Basic CLR interop](https://github.com/clojure/clojure-clr/wiki/Basic-CLR-interop).
- [`ByRef` and `params`](https://github.com/clojure/clojure-clr/wiki/ByRef-and-params)
- [Calling generic methods](https://github.com/clojure/clojure-clr/wiki/Calling-generic-methods)


## Class access

Symbols that represent the names of types are resolved to `Type` objects.


```clojure
-> System.String     ; => System.String
-> String            ; => System.String
-> (class String)    ; => System.RuntimeType
```

Classes can be `import`ed into a Clojure namespece so that the namespace of the type can be omitted, as with `String` above. (There is a default set of imports that are always available.  See the note at the end for how that set is computed.)

There are types in the CLR that can not be named by symbols.  (I guess Java does not yet have this problem.) See the note at the end for a few comments about this.

## Member access

The classic list of ways to access members of a class are:

```clojure
(.instanceMember instance args*)
(.instanceMember Classname args*)
(.-instanceField instance)
(Classname/staticMethod args*)
Classname/staticField
```

The Lisp reader is tasked with translating these into the _dot_ special form; read all about it [here](https://clojure.org/reference/java_interop#_the_dot_special_form).   Generally, you should use the forms above rather than using the _dot_ special form directly.

## CLR augmentations

For CLR interop, we had to add some additional functionality, primarily for calling generic methods and for working with `ByRef` and `params` arguments.

If you are familiar with C#, you have seen `ref`, `in`, and `out` used in method signatures.  There is no distinction of these at the CLR level.  C# adds `in` and `out` for additional compile-time analysis.  Given that we don't have uninitialized variables in Clojure and that CLR doesn't distinguish, ClojureCLR only provide a `by-ref` mechanism.  The example given on the wiki page looks at a class defined by:

```C#
public class C1
{
    public int m3(int x) { return x; }
    public int m3(ref int x) { x = x + 1; return x+20; }
    public string m5(string x, ref int y) { y = y + 10;  return x + y.ToString(); }
    public int m5(int x, ref int y) { y = y + 100; return x+y; }
}
```

To call `m3` with a `ref` argument, you would use:

```clojure
(let [n (int n)  ]
  (.m3 c (by-ref n)))
```

The type hint provided by the `(int n)` is required -- otherwise the it will try to match a `ref object` parameter.

The `by-ref` is a syntactic form that can only be used at the top-level of interop calls, as shown here. It can only wrap a local variable. (`by-ref` can also be used in `definterface`, `deftype`, and the like.)  And yes, the value of the local variable `n` is updated by the call -- yep, that binding is not immutable.  You do not want to know how this is done.

For `params`, consider the class:

```C#
namespace dm.interop
{
  public class C6
  {
    public static int sm1(int x, params object[] ys)
    {
        return x + ys.Length;
    }
   public static int sm1(int x, params string[] ys)
    {
        int count = x;
        foreach (String y in ys)
            count += y.Length;
        return count;
    }
   public static int m2(ref int x, params object[] ys)
   {
        x += ys.Length;
        return ys.Length;
    }
  }
}
```

Method `sm1` is overloaded on the `params` argument.
You can access the first overload with either of these:

```clojure
(dm.interop.C6/sm1 12 #^objects (into-array Object [1 2 3] ))
(dm.interop.C6/sm1 12 #^"System.Object[]" (into-array Object  [1 2 3]))
```

The second overload is accessed with:

```clojure
(dm.interop.C6/sm1 12 #^"System.String[]" (into-array String ["abc" "de" "f"]))
(dm.interop.C6/sm1 12 #^"System.String[]" (into-array ["abc" "de" "f"]))
```

Make me one with everything.

```clojure
(defn c6m2 [x] 
  (let [n (int x)
        v (dm.interop.C6/m2 (by-ref n) #^objects (into-array Object [1 2 3 4]))]
    [n v]))
```

## Generic methods

We are talking here about methods with type paremeters, not methods that are part of a generic type.
Often you don't need to do anything special:

```clojure
(import 'System.Linq.Enumerable)
(seq (Enumerable/Where [1 2 3 4 5] even?))   ; => (2 4)
```

There are actually two overloads of `Where` in `Enumerable`. 

```C#
public static IEnumerable<TSource> Where<TSource>(
        this IEnumerable<TSource> source,
        Func<TSource, bool> predicate
)

public static IEnumerable<TSource> Where<TSource>(
        this IEnumerable<TSource> source,
        Func<TSource, int, bool> predicate
)
```

 The type inferencing mechanism built into ClojureCLR can figure out that `even?` supports one argument but not two, so it can be coerced to a `Func<Object,bool>` but not a `Func<Object,int,bool>`.   Thus, it can select the first overload. 

 The wiki page for this discusses how to deal with situations where the function might not be so accommodating.  Some of these techniques involve macros to define `System.Func` and `System.Action` delegates from Clojure functions.  This is a bit of a pain, but it is not too bad.

 But there are still situations where more is required.

 ```clojure 
(def r3 (Enumerable/Repeat 2 5))  ;; fails with

Execution error (InvalidOperationException) at System.Diagnostics.StackFrame/ThrowNoInvokeException (NO_FILE:0).
Late bound operations cannot be performed on types or methods for which ContainsGenericParameters is true.
```

We need to know the type parameters for the generic method `Enumerable/Repeat` at compile (load) time.  The `type-args` macro can be used to supply type arguments for the method:

```clojure
(seq (Enumerable/Repeat (type-args Int32) 2 5))  ;=> (2 2 2 2 2)
```

# The new qualified methods feature

The new qualified methods feature is used to provide methods to higher-order functions, for example, passing a Java method to `map`.
The new feature is described [here](https://clojure.org/news/2024/04/28/clojure-1-12-alpha10#method_values).

Qualified methods are specified as follows:

- `Classname/method` -- refers to a static method
- `Classname/.method` -- refers to an instance method
- `Classname/new` -- refers to a constructor

These can be used as values (e.g., passed to higher-order functions) or as invocations.

By invocation we mean appearing as the first element in a `(func args)` form.  Thus

```clojure
(String/Format format-str arg1)   ;; static method invocation
(String/.ToUpper s)               ;; instance method invocation
(String/new init-char count)      ;; constructor invocation
```

For static methods, this is our original syntax.  For instance methods, this would compare to `(.ToUpper ^String s)`.
The third example is equivalent to `(String. \a 12)`.   We gain the ability to directly specify the type on the instance method.
(Though see `:param-tags` below for a bonus.)

Using qualified methods as values is the more interesting case.  Rather than needing to wrap a method in a function, as in

```clojure
(map #(.ToUpper ^String %) ["abc" "def" "ghi"])
```

you can just use the qualified method directly:

```clojure
(map String/.ToUpper ["abc" "def" "ghi"])
```
Note that we no longer need the type hint on the parameter to avoid reflection.

## `:param-tags`

Using qualified methods gives us the benefit of a type hint on the instance variable for instance method invocation. In fact, the type that the qualified method gives (`String` in the case of `String/.ToUpper`) overrides any type hint on the instance argument.

For invocations, further disambiguation of method signature can be made by providing type hints on the other arguments.   
However, for use in value positions, we cannot type hint in this way.

Consider trying to map `Math/Abs` over a sequence.  In the old `IFn`-wrapper style

```clojure
 (map #(Math/Abs %) [1.23 -3.14])
 ```

 This will get a reflection warning.  You can fix this with a type hint.

 ```clojure
 (map #(Math/Abs ^double %) [1.23 -3.14])
 ```

Now consider using a qualified method:

 ```clojure
 (map Math/Abs [1.23 -3.14])
```

You will get a reflection warning.  And there is no place to put a traditional type hint.

Enter `:param-tags`.  You can add `:param-tags` metadata to the qualified method to provide type hints.  The easiest way to  The easiest way is to use new `^[...]` metadata reader syntax.

```clojure
(map ^[double] Math/Abs [1.23 -3.14]))
```

You need to put as many types as there are arguments in the method you wish to select.
If you don't need to type a specific argument, you can use an underscore, as in

```clojure
(^[_ _] clojure.lang.Tuple/create 1 2)  ; => (1 2)
```
Here, we just need to indicate that the two-argument version of the `Tuple/create` method is required.

By the way, `:param-tags` can be used in invocations also as an alternative way to specify argument typing. Compare

```clojure
(Math/Abs ^double x)
```

to

```clojure
(^[double] Math/Abs x)
```
For one argument, not a big deal.  But with, say, 3 arguments, it might help to pull all the type info in one place

```clojure
(.Method ^Typename (...) ^T1 (...) ^T2 (...) ^T3 (...) )
([T1 T2 T3]  Typename/.Method  (....) (....) (....) (....))
```

At least you have a choice.

## the add-ons

We need to allow adding `(type-args ...)` and `(by-ref ...)` to our `:param-tags`.
Simply, we just put them into position.

```clojure
#[(type-args T1 T2) T3 T4 (by-ref T5)] T/.m
```

would specify the instance method 

```C#
class T 
{
   Object m<T1,T2>(T3 arg1, T4 arg2, ref T4 arg3) {...}
}
```

I don't know if there is any utility for `by-ref` in things like `map` calls.  You would have no way to get any changed value in a `by-ref` parameter.
A fallback to the classic interop using a function wrapper seems best for this situation.


## Availability 

The new qualified methods feature is available in ClojureCLR 1.12-alpha10 and later. 

# Notes

## Note: Default imports

  At system startup, we go through all loaded assemblies and create an default import list of all types that satisfy the following conditions:

- the type's namespace is `System`
- the type is a class, interface, or value type
- the type is public
- the type is not a generic type definition  (meaning an uintantiated generic type)
- the type's name does not start with "_" or "<".

In addition, strictly for my own convenience for dealing with 'core.clj' and other startup files, I add `System.Text.StringBuilder`, `clojure.lang.BigInteger` and `clojure.lang.BigDecimal`.  (I'll change `clojure.lang.BigInteger` to `System.Numerics.BigInteger` in the ClojureCLR.Next.)


## Note: Specifying type names

 The 
[specifying types](https://github.com/clojure/clojure-clr/wiki/Specifying-types) page explains how we get around this.   It is just ugly.  The mechanism ties directly into the CLR's type naming and resolution machinery, thus:

```clojure
|System.Collections.Generic.IList`1[System.Int32]|
```

You have to include fully-qualified type names, generics include a backtick and the number of type arguments, and the type arguments are enclosed in square brackets. 

I plan to introduce a new syntax for this in ClojureCLR.Next, that would take advantage of imports and be otherwise nice.
When I designed the `|...|` syntax (stolen from CommonLisp), Clojure did not yet have _tagged literals_.  Now we might be able to do something like

#type "IList<int>"

(If you are interested in helping to design this, please let me know.)

