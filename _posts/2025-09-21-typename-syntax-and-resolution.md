---
layout: post
title: Typename syntax and resolution in ClojureCLR
date: 2025-09-21 00:00:00 -0500
categories: general
---

Wherein we describe the significant enhancements to typename syntax and resolution in ClojureCLR effective with version 1.12.3-alpha2.

## TL;DR

Several significant improvements have been made to typename syntax and resolution in ClojureCLR.

- Discover and automatically load assemblies that the application depends on and assemblies that are part of the .NET runtime, so that types in those assemblies can be found without explicit assembly loading.  You will never write `(assembly-load "System.Text.Json")` again; if you execute `(System.Text.Json.JsonSerializer/Serialize ...)`, the assembly will be loaded automatically if not alread loaded.

- You can define type aliases for any type, including generic types.
- You can use type aliases at the top level or embedded as generic type parameters.
- You can use the built-in Clojure primitive type names such as `int`, `long`, `shorts`, etc. as generic type parameters.
- In many places, you no longer need to include the arity of the generic type in the name.

With these changes, you can write code like this:

```clojure
(add-type-alias 'List |System.Collections.Generic.List`1|) 
(add-type-alias 'IntList |List[int]|) 
(def il (IntList/new)) 
(defn f [^IntList xs) ... )
```

## ClojureCLR typename syntax and resolution: _status quo ante_

I wrote previously about  typename resolution in ClojureCLR in [Are you my type?]({{site.baseurl}}{% post_url 2025-03-01-are-you-my-type %}).  That post described the strategies used to look up types by name, and some of the tradeoffs involved.

Two prominent pain points in dealing with typenames and resolving them are:

- the need to call `assembly-load` or related functions to load assemblies before types in them can be resolved, when the assemblies of interest could be automatically discovered and loaded.

- the need to use fully namespace-qualifed names, explicit number of generic parameter counts, and other syntactic burdens when referring to types.

The first is self-explanatory.  For the second, a poster child of the problem is

```clojure
|System.Collections.Generic.Dictionary`2[System.String,System.Collections.Generic.List`1[System.Int64]]|
```

- Why must we write `System.Int64` instead of just `int`?
- Why must we write `System.String` instead of just `String`?  We can just write `String` when used direclty as a type hint.  Why not here?
- Why must we write ``Dictionary`2`` instead of just `Dictionary`?  We have the context to infer the arity of the generic type definition.

One has to make direct reference by name to underlying platform types in various places in Clojure(JVM or CLR) code.  Type hints to avoid reflection are one example.  There are some places where a string can be used instead of a symbol to refer to a type, but often a symbol must be used.  Which presented a problem for ClojureCLR given the complexity of CLR types.  

For a variety of reasons, I decided to use the syntax of fully-qualified type names used by the CLR itself.  This is the syntax used by methods such as `Type.GetType()` and `Assembly.GetType()`. 

> A detailed specfication for fully qualified type names can be found here:  [Specify fully qualified type names](https://learn.microsoft.com/en-us/dotnet/fundamentals/reflection/specifying-fully-qualified-type-names).  You might also want to take a peek at [Type.AssemblyQualifiedName Property](https://learn.microsoft.com/en-us/dotnet/api/system.type.assemblyqualifiedname).  I'll have occasion to look at a few of the its more obscure details later.

A non-trivial problem with that choice; the syntax uses characters such as backquotes, commas and square brackets that are not valid in Clojure symbols.  So I had to come up with a way to write a symbol using characters that the Lisp reader would not normally accept.  (Any alternative syntax likely would have had the same problem.)

Other Lisps have solutions to this problem.  I decided to use a simplified version of the symbol syntax used in CommonLisp.  This is the `|`-quotiing used by the Clojure Lisp reader.  Read about it in [Reader extension: `|`-quoting](https://github.com/clojure/clojure-clr/wiki/Reader-extension:-%7C%E2%80%90quoting).

Thus we end up with the aforementioned

```clojure
|System.Collections.Generic.Dictionary`2[System.String,System.Collections.Generic.List`1[System.Int64]]|
```

The type resolution code in ClojureCLR passes the name of this symbol directly to methods such as `Type.GetType()` and `Assembly.GetType()`. 

Unfortunately, this syntax is not very pleasant to write. 

### Aliases in the before world

Why not just use `import` and `ns` declarations to define type aliases?

Namespaces already supply a mechanism for mapping symbols to types.

> Namespaces are __mappings from simple (unqualified) symbols to Vars and/or Classes__. 
> Vars can be interned in a namespace, using def or any of its variants, 
> in which case they have a simple symbol for a name and a reference to their containing namespace, 
> and the namespace maps that symbol to the same var. 
> A namespace can also contain mappings from symbols to vars interned in other namespaces by using refer or use, 
> or __from symbols to Class objects by using import__.   [ Emphasis mine. Reference: [Namespaces](https://clojure.org/reference/namespaces) ]

Use of the symbol-to-type map is embedded all over the Clojure interpreter/compiler code.  I've written a little about this:

- [C4: Symbolic of what?]({{site.baseurl}}{% post_url 2025-09-02-symbolic-of-what %}) - A little digression on what symbols represent
- [Are you my type?]({{site.baseurl}}{% post_url 2025-03-01-are-you-my-type %}).


Every namespace comes pre-loaded with a set of type aliases for all the public types in the `System` namespace in assemblies that are loaded during Clojure initialization.  This is why you have been able to write

```clojure
(.ToLower ^String s)
``` 

There is an entry in the namespace map associating `String` with the type `System.String`.  That mapping is found when the type hint `^String` is processed.

Clojure provides a mechanism for users to define type aliases:  `import`.  Though one can call import directly, it is more commonly encountered in `:import` clauses in `ns` declarations.  `import` and `(ns ... (:import ...))` can do some of twhat we want, but is not tied into the underlying CLR type resolution mechanism.   For example, you can write:

```clojure 
(ns  my.stuff
  (:import 
     (System.IO FileInfo Path)
     (System.Text Encoding)))
```

to introduce aliases `FileInfo` for `System.IO.FileInfo`, etc.
And these will work standalone.

```clojure
(defn f [file-info]
  (.FullName ^FileInfo file-info))
```
But, before the changes described below, this would not work:

```clojure
|System.Collections.Generic.List`1[FileInfo]|
```

The underlying CLR typename resolution algorithm is not aware of Clojure aliases; it requires fully-qualified names.  Instead of `FileInfo`, you must write `System.IO.FileInfo`.  

In addition, `import` does not do what is needed for generic types.  Though you can do

```clojure
(ns my.stuff
  (:import (System.Collections.Generic List)))
```

the definition of `import` is that the alias is the name of the type, which is actually ``List`1``.  If you were to write

```clojure
(ns my.stuff2
  (:import (System.Collections.Generic |List`1[System.String]|
                                       |List`1[System.Int64]|)))
```

you would get an error about defining a second alias for ``List`1``.  

Let's turn to the improvements.


## Improved assembly discovery

There are assemblies that should be checked and automatically loaded when looking up types.  These include assemblies that the application itself directly depends on as--they might not be loaded yet--and assemblies that are part of the .NET runtime, such as `System.Collections.Concurrent.dll`.

The new version of `RT.classForName()` (see the aforementioned _Are you my type?_) automatically discovers and loads these assemblies.  It uses the `DependencyContext` class to find the entry assembly's dependencies.  It also discovers assemblies that are part of the shared .NET runtime via `AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")`.

Note that the latter is not available in .NET Framework 4.6.2.  I added a build of ClojureCLR for .NET Framework 4.8.1 which does have access to `AppContext.GetData`.   If you are on Framework and want the improved typename resolution, use that build.

The algorithm uses several heuristics to identify assemblies that should be inspected.  There might be some improvements in these heuristics in the future.  But the current version seems to work well in practice.  One user reported on the `#clr` channel on Clojurians Slack that they were able to remove the following `assembly-load` calls in one of their files.

```Clojure
(assembly-load "System.Net.Http")
(assembly-load "System.Web.HttpUtility")
(assembly-load "System.Text.Json")
(assembly-load "System.Collections")
(assembly-load "System.Collections.Concurrent")
(assembly-load "System.Threading.Channels")
(assembly-load "System.Threading.Tasks")
(assembly-load "System.Security.Cryptography")
```

Sweet.

## Typename syntax and resolution -- improvements

One yearns for the simplicity of just writing names along the lines of C#'s;

```C#
Dictionary<String, List<long>>
```

And now we have a close approximation of that in ClojureCLR.  The improvements are:

- type aliases
- use of the standard built-in Clojure typenames such as `int` and `shorts` for things like generic type parameters
- automatic calculation of generic type arities (in most circumstances)


In _ClojureCLR: Brave new world_, we can now define aliases such as:

```clojure
(alias-type Dictionary |System.Collections.Generic.Dictionary`2|)
(alias-type List |System.Collections.Generic.List`1|)
(alias-type IntList |List[int]|)
```

and then refer to types

```clojure
|Dictionary[String, List<long>]|
|Dictionary[String, IntList]|
```

and create expressions such as:

```clojure
(def my-list (IntList/new))
(IntList/.Add my-list 42)
```

I'll describe each of the three improvements in turn.

### Type aliases

There are two aspects: providing a way to define type aliases that fixes the problems with `import`, and integrating type aliasing into the typename resolution mechanism.

To define aliases, I have introduced a new function and a new macro.  They have the same functionality, but the macro does not require quoting.  The following are equivalent:

```clojure
(alias-type Dictionary |System.Collections.Generic.Dictionary`2|)
(add-type-alias 'Dictionary |System.Collections.Generic.Dictionary`2|)
```

The second argument must evaluate to a `Type` object. With type aliasing fully incorporated into typename resolution, you can now write:

```clojure
(alias-type List |System.Collections.Generic.List`1|)  ;; List maps to the generic type definition
(alias-type IntList |List[int]|)                       ;; aliases List and int are both recognized
(def il (IntList/new))                                 ;; You can use an alias where type is expected
(defn f [^IntList xs) ... )
```

Note that we cannot write just

```clojure
(alias-type Dictionary |System.Collections.Generic.Dictionary|)
```

The backquote-arity suffix is required; it is the true name of the type.  In this circumstance, we do not have a context to compute the arity.

I considered ways to provide that context.  C# would allow this reference

```C#
Dictionary<,>
```

But I'm not sure that is really better than ``Dictionary`2``.    And I very much prefer

```Clojure
|System.Func`6|
```

to

```C#
|System.Func<,,,,,>|
```
The need to provide backquote-arity suffixes only occurs when referring to the generic type definition itself. When you provide type arguments, the arity can be inferred, as in

```clojure
|System.Collections.Generic.List[int]|
```


### A note on the built-in special types

Clojure provides special handling for names identifying built-in primitive numeric types:  `int`, `long`, `shorts`, etc.
ClojureCLR adds a few for primitive numeric types that are unique to ClojureCLR, such as `uint` and `sbytes`.
Note that these do not use the type alias mechanism.  They are special-cased to be recognized only in certain places, such as the processing of type hints.  Consider:

```clojure
(alias-type Long System.Int64)   ;; esablish an alias
Long                             ;; => System.Int64 -- evaluates to a type
long                             ;; => #object[core$long 0x28993d0 "clojure.core$long"] -- the Clojure function 'long`
(defn f [^long x] ...)           ;; interprets ^long as a type hint for System.Int64
                                 ;; on the JVM, this is a type hint for the primitive numeric type 'long'
```

The typename resolution code will recognize `int` and friends when they appear appear in type definitions as generic type parameters only.  Of course, the pre-existing usage in type hints is unaffected.

Note: I tried to allow expressions like `|int*[]|` to be used at the top level, something way down deep in the compiler had a problem with that.  I decided it wasn't worth the effort to find a solution -- for now.  You can still use `|System.Int32*[]|` or define an alias.


### Inferring generic type arity

When in C# you write
```C#
Dictionary<String, List<long>> mylist = [];
```

(assuming appropriate `using` directives), the compiler does a lot of work for you behind the scenes. It knows first of all that for `Dictionary` it is looking for a generic type with two type parameters.  For that reason, it looks for a type named ``Dictionary`2``: generic type definition names must have a suffix of a backquote and the arity of the generic.

>  However, to allow such overloading on generic arity at the source 
language level, CLS Rule 43 is defined to map generic type names to unique CIL names. That Rule 
states that the CLS-compliant name of a type C having one or more generic parameters, shall have a 
suffix of the form `n, where n is a decimal integer constant (without leading zeros) representing the 
number of generic parameters that C has. [Source:  [ECMA-335, Common Language Infrastructure (CLI), Partition II, section 9](https://www.ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf)]


Looking through the namespaces mentioned in `using`s, the C# compiler finds ``System.Collections.Generic.Dictionary`2``.   Similarly, for ``List`1``.

Our enhancements to typename resolution in ClojureCLR now allow you to omit the arity when generic type arguments are supplied.  However, you still must supply the backquote-arity in the name when no generic type arguments are given, i.e., when you are referring to the generic type definition itself.

## Nested types and generic type definitions -- Beware!

CLR supports nested classes.  Referring to these is very straightforward -- unless generics are involved.  For a simple case such as

```C#
namespace MyNamespace;
public class Outer
{
    public class Inner { }
}
```
you could refer to `MyNamespace.Outer` and `MyNamespace.Outer+Inner`.  You could import `MyNamespace+Outer` and then refer just to `Outer+Inner`.  All fine and dandy.

Now consider:

```C#
namespace MyNamespace;
public class GenParent<T1, T2>
{   
    public class Child
    {
        public class GrandChild<T3>
        {
            public class GreatGrandChild<T4, T5>
            {

            }
        }
    }
}
```

Working in C#, you could refer to any of these (assuming `using MyNamespace;` is in effect):

```C#
GenParent<,>                     // Generic type definition
GenParent<int, string>           // constructed generic type
GenParent<,>.Child               // nested type -- this is also a generic type definition
GenParent<,>.Child.GrandChild<>  // nested type -- constructed generic type
GenParent<int string,>.Child.GrandChild<double>  // constructed generic type
// etc.
```

However, if you were to print the fully-qualified name of the type

```C#
GenParent<int, string>.Child.GrandChild<double>.GreatGrandChild<int, long>
```

you might be surprised to find that you obtain (leaving out assembly information):

```C#
GenParent`2+Child+GrandChild`1+GreatGrandChild`2[System.Int32,System.String,System.Double,System.Int32,System.Int64]
```

Yup.  the actual generic type definition is

```C#
GenParent`2+Child+GrandChild`1+GreatGrandChild`2
```

and it takes _five_ type parameters.  

We have most of flexibility of the C# syntax in ClojureCLR, except: if you have generic type definitions, as opposed to constructed generic types (those with type arguments supplied), you must provide the backquote-arity suffixes for all generic type definitions in the nesting hierarchy.  For the C# examples given above:

```C#
GenParent<,>                      // Generic type definition
GenParent<int, string>            // constructed generic type
GenParent<,>.Child                // nested type -- this is also a generic type definition
GenParent<,>.Child.GrandChild<>  // nested type -- constructed generic type
GenParent<int string,>.Child.GrandChild<double>  // constructed generic type
// etc.
```

we can write in ClojureCLR:

```clojure
|MyNamespace.GenParent`2|                         ;; Generic type definition, backquote-arity required
|MyNamespace.GenParent[int, String]|              ;; No need for `2 here
|MyNamespace.GenParent`2+Child|                   ;; Nested generic type definition; must provide `2   
|MyNamespace.GenParent`2+Child+GrandChild`1|      ;; Nested generic type definition; must provide `2 and `1
|MyNamespace.GenParent[int,String]+Child+GrandChild[double]| ;; No need for `2 or `1 here
```

If you introduce type aliases, the same rules apply.

```clojure
(alias-type GP |MyNamespace.GenParent`2|)
(alias-type GPC |GP+Child|)                    ;; we know the arity from the alias for GP
```

We can then refer to

```clojure
|GP[int, string]|            ;; constructed generic type;
|GPC[long, double]|          ;; constructed generic type; 
```
|

### The triple identity of square brackets

When I first worked with the CLR typename syntax, one thing I found confusing was that square brackets (`[` and `]`) are used in three different ways:

- To delimit the list of type arguments to a generic type definition.  Example:  ``Dictionary`2[System.String,System.Int32]``.
- To indicate an array type.  Example:  `System.String[]` is an array of strings.
- To delimit assembly names in assembly-qualified type names.  

For the last case, you do not need brackets around the string if the assembly is for the top level name, such as

```C#
System.String, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
```

The comma after `System.String` indicates that an assembly name follows. However, if you are supplying an assembly name for a generic type parameters, you need the brackets.  Example:

```C#
System.Collections.Generic.List`1[[System.Int64, System.Private.CoreLib, Version=9.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e]], System.Private.CoreLib, Version=9.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e]
```

which is deconstructed as

```
typename[  ...type argument ... ], ...assembly-specifier 
```

(note: not brackets around this), and where the type argument is

```
[typename,assembly-specifier]
```

(Note the brackets.)

I hope you never have to deal with nested generic types and assembly-qualified names. 

## Conclusion

I hope this has been helpful in understanding the improvements to typename syntax and resolution in ClojureCLR.  I think these changes make type referencing more pleasant to use and easier to understand.  