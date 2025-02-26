---
layout: post
title: AOT compilation issues
date: 2025-02-20 00:00:00 -0500
categories: general
---

A look at the issues involved in restoring AOT compilation to ClojureCLR.

## Background

ClojureCLR initially was developed on what is now called .NET Framework, currently in some 4.8.x version. 
(The first version of ClojureCLR supported 3.5.)
Under Framework, ClojureCLR was able to do ahead-of-time (AOT) compilation and save to disk DLL files containing compiled Clojure source code.  
This allowed the distribution of pre-compiled versions of all Clojure runtime supplied as Clojure source -- `core.clj` and the rest.
Startup-times were significantly reduced by loading DLLs versus loading the source files from scratch.

When .Net Core was introduced,  saving dynamic assemblies to file was no longer supported.  The official reason:

> The `AssemblyBuilder.Save` API wasn't originally ported to .NET (Core) because the implementation depended heavily on Windows-specific native code that also wasn't ported. ([Source](https://learn.microsoft.com/en-us/dotnet/fundamentals/runtime-libraries/system-reflection-emit-persistedassemblybuilder))

Since then, I have periodically looked for alternatives.  I'll talk more about some of them at the end of this post.

.Net 9 restores the key method:  `System.Reflection.Emit.AssemblyBuilder.Save()`. Almost.  What it provides is a new version of the assembly builder functionality, in the class `System.Reflection.Emit.PersistedAssemblyBuilder`.  There are significant differences between `AssemblyBuilder` and `PersistedAssemblyBuilder` that have direct (dire?) consequences for code generation in ClojureCLR. For this reason, re-enabling AOT compilation in ClojureCLR in .NET 9 is not simply a matter of reactivating the code that worked for Framework.

Understanding the issues requires some knowlege of how ClojureCLR generates code. 

## Code generation in ClojureCLR

In Clojure (JVM or CLR), the reading and evaluation of Clojure source code results in the analysis of that code and eventual generation of IL code (Java bytecodes on the JVM, MSIL on the CLR).  Whether that code is written to a file -- for the JVM, in `.class` files; for the CLR, an assembly -- depends on whether the `*compile-files*` flag is set.

If you working at the REPL, a form is read, analyzed and evaluated.  Leaving out some special case handling (more on that some other day), roughly the following happens.  Say you have read in the form:

```clojure
(defn f [x] (inc x))
```

This macroexpands to something like:

```clojure
(def f (fn* [x] (inc x)))
```

The evaluation process for the `fn*` sub-form results in the creation of a type -- let's call it `My.F` -- that implements the `IFn` interface.  It will have an implementation of `IFn.invoke(arg1)` that returns its argument increased by one.  

Ultimately, we then evaluate the `def` form.  This results in:

1. a mapping for the symbol `f` in the current namespace (call it `my.fs`) to
2. a newly created `Var` `#'my.ns/f` that has its value
3. the result of calling `new My.F()`.

Of great importance here is that the Clojure environment is modified progressively.
When the next form is read and evaluated, it can see `f`'s binding, grab the value of the associated `Var` and proceed accordingly.

Even when you don't see `fn` as directly as here, most forms being evaluated will result in the creation of at least one new type.  In the CLR, these types must have a home.  In the non-compile environment, we keep a dynamic assembly (named `eval`) for this purpose.  Fortunately, dynamic types in dynamic assemblies can be used immediately.  We don't have to wait for the assembly to be saved to disk.

When you are sitting and typing at the REPL, you just keeping doing the process above over and over.  When you load a Clojure source file, via a call to `load`, `require`, or similar, exactly the same process happens.  Those functions are just a REPL (Read-Eval-Print Loop) without the `Print`. 

## AOT compilation in ClojureCLR

AOT compilation means saving the generated code -- to `.class` files on the JVM or to an assembly written to disk on the CLR.  This would typically be done by calling the `compile` function:

>  Usage: (compile lib)
>
>  Compiles the namespace named by the symbol lib into a set of
classfiles. The source for the lib must be in a proper
classpath-relative directory. The output files will go into the
directory specified by *compile-path*, and that directory too must
be in the classpath.

`compile` is defined as:

```clojure
(defn compile
  [lib]
  (binding [*compile-files* true]
    (load-one lib true true))
  lib)
  ```

`load-one`  also underlies `load` and `require`.  It calls some runtime code (defined in Java/C#) that does different things depending on whether `*compile-files*` is true or not.

Focusing on ClojureCLR and on .NET Framework,  when we compile a source file, we first generate a _saveable_ assembly -- the official terminology seems to be _persisted_ --  to use in place of the `eval` assembly that we use for interactive work. We then go through essentially the same process as above.  Read a form, analyze it, create types as necessary and generate the appropriate IL code.  In addition, we create an _initialization_ type that contains a static method to which we write code that will do the same steps as the REPL loop -- create `Var`s, map symbols to `Var`s, etc.

At the end of the process, we save the assembly to disk. When we are loading a library and find a DLL file -- say we are trying to load load library `clojure.uuid` and we find `clojure.uuid.dll` -- we load the DLL and call the initialization method.  

## The primary issue for AOT compilation in .NET 9

The most important point in all of the above is the _progressive updating of the Clojure environment_: Each form _must_ be evaluated before the next form is read.  

In .NET Framework, an assembly that can be saved allows the dynamic usage of finalized types.  You can use the types before the save. 

This is not the case with the new `PersistedAssemblyBuilder`. We cannot use the types we have created until the assembly as a whole is saved. And then you must load it it before using any of the types.  __We cannot do progressive evaluation.__ This is a show-stopper for ClojureCLR.

The documentation does make this explicit, if you look hard enough.
We find:

 > A dynamic assembly is an assembly that is created using the Reflection Emit APIs. A dynamic assembly can reference types defined in another dynamic or static assembly. You can use AssemblyBuilder to generate dynamic assemblies in memory and execute their code during the same application run. In .NET 9 we added a new `PersistedAssemblyBuilder` with fully managed implementation of reflection emit that allows you save the assembly into a file. In .NET Framework, you can do both—run the dynamic assembly and save it to a file. ( [Source](https://learn.microsoft.com/en-us/dotnet/fundamentals/runtime-libraries/system-reflection-emit-assemblybuilder))

And:

> New in .NET 9, the `PersistedAssemblyBuilder` class adds a fully managed `Reflection.Emit` implementation that supports saving. This implementation has no dependency on the pre-existing, runtime-specific `Reflection.Emit` implementation. That is, now there are two different implementations in .NET, runnable and persisted. To run the persisted assembly, first save it into a memory stream or a file, then load it back. ([Source](https://learn.microsoft.com/en-us/dotnet/fundamentals/runtime-libraries/system-reflection-emit-persistedassemblybuilder)).

## How to proceed

The only solution I've come up with so far is doing all the code generation twice.  
Syntactic and semantic analysis  -- the generation of the AST from which we do code generation -- need only be done once.  
For each form, we then create two types, one in the `eval` dynamic assembly and one in the persisted assembly that we will save to file.  
We generate code into each type and evaluate the one from the dynamic assembly.

Even this is not as easy as it sounds. (If it sounds easy.)  The design of the current compiler has code generation happening _during_ syntactic analysis. 
Specifically, those things that are 'functions' -- typically what you get from `defn`, `deftype`, direct uses of `fn`, etc. -- actually have their types generated and finalized during the first pass.
It even goes a little further.  If you do something at the REPL (or in a source file) such as:

```clojure
(def x (let [y f(12)] (inc y)))
```

it so happens that 'naked' `let`s get wrapped with a `fn` -- don't ask -- so even in something as simple as this, we have a type generated during syntactic analysis.
I _think_ that this very little effect at that point in the process -- but most likely I will have to do double generation even during syntactic analysis.  
 Emitting twice, once for saving and once for progressive evaluation,  might work.  But there are complications I have not figured out yet. 

 [Edit: The first version of this post had a digression on direct linking here.  I've moved that to a [separate post]({{site.baseurl}}{% post_url 2025-02-20-AOT-compilation-issues %}).]

The biggest task to start is to go over line of code in the compiler to see if there is any chance that we could cross-reference from  dynamic (eval) assembly types to/from persisted assembly types.  Either direction, the code won't run.  (If the dynamic code references something in the persisteed assembly, we can't run it -- the persistent assembly type can't be run.  If the persisted code references a something in the eval assembly, we won't be able to save the assembly;  been there, done that, when first writing the compiler code.)

## Not so fast: platform dependence

There are some additional problems in the brave new world of post-Framework .NET.  One is the platform dependence.

> To create a `PersistedAssemblyBuilder`` instance, use the PersistedAssemblyBuilder(AssemblyName, Assembly, IEnumerable<CustomAttributeBuilder>)` constructor. The` coreAssembly` parameter is used to resolve base runtime types and can be used for resolving reference assembly versioning:
>
> - If `Reflection.Emit` is used to generate an assembly that will only be executed on the same runtime version as the runtime version that the compiler is running on (typically in-proc), the core assembly can be simply `typeof(object).Assembly`.
>
> _[example deleted]_
>
> If `Reflection.Emit` is used to generate an assembly that targets a specific TFM, open the reference assemblies for the given TFM using `MetadataLoadContext` and use the value of the `MetadataLoadContext.CoreAssembly` property for `coreAssembly`. This value allows the generator to run on one .NET runtime version and target a different .NET runtime version. 

We may be in a situation where supporting multiple TFMs (target frameworks) is required. Given that TFMs can relate not just to .NET version but also operating system specificity, we have a nightmare.

It could be that we have to do AOT-compilation of things like `core.clj` as part of the installation process. 

I'm not feeling well.

## Other solutions

A number of other solutions have been suggested over the years.

1. Find an package that can save dynamic assemblies in post-Framework verions of .NET.  Some of the options I've looked at:

    - [Mono.Cecil](https://www.mono-project.com/docs/tools+libraries/libraries/Mono.Cecil/) -- heavily used, very reliable.  But I couldn't find good examples of how to do use it for dynamic code gen in the way we need it. 
    - [Lokad.ILPack](https://github.com/Lokad/ILPack) -- I actually tried to use it a few years ago.  I ran into bugs, mostly incomplete features.   The project is owned by a company that uses it for its own work.  They will take patches, but new releases are few and far between.  I could fork and try to fix them.  But that looked like a lot of work. 

2. The Roslyn compiler.  The choices seem to be: write C# code as a string, read it, compile it; or create a syntax tree programmatically.  Either way, I'd have to figure out how to translate all Clojure compilation tricks into C# terms.  That seems daunting. And I think we still have the progressive evaluation problem -- I don't believe the technology supports dynamic code generation.

3. Just don't do it.  Yeah, that's been my approach so far.

4. Go halfway.  Rather than generating IL code, develop an intermediate language that expresses what is in the AST tree.  Loading it would be faster than reading and analyzing the source code.  But we'd still have to create the types and generate IL at runtime.  This would be  slower than loading a DLL.  Sounds like a lot of work.


## Open to suggestions

If you have other ideas, let me know.  I've started a [discussion](https://github.com/dmiller/clojure-clr-next/discussions/8) seeking inspiration.



