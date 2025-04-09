---
layout: post
title: C4 - Classic Clojure Compiler Contemplation
date: 2025-04-15 00:00:00 -0500
categories: general
---

The first in a series of posts analyzing the Clojure (JVM/CLR) compiler.
In this post, we look at the overall structure of the compiler.  

The other posts in the series are:

- [C4: AST me anything]({{site.baseurl}}{% post_url 2025-04-16-AST-me-anything}) - A tour of the AST nodes produced by the compiler
- [C4: Symbolic of what?]({{site.baseurl}}{% post_url 2025-04-17-symbolic-of-what}) - A little digression on what symbols represent
- [C4: ISeq clarity]({{site.baseurl}}{% post_url 2025-04-18-iseq-clarity}) - How to analyze an `ISeq`
- [C4: Functional anatomy]({{site.baseurl}}{% post_url 2025-04-19-functional-anatomy}) - How functions are implemented in Clojure
- [C4: How type-ical]({{site.baseurl}}{% post_url 2025-04-20-how-type-ical})- Type analysis by the compiler
- [C4: I have something to emit]({{site.baseurl}}{% post_url 2025-04-21-i-have-something-to-emit}) - Code generation

## The terms of discussion

Languages are often classified as interpreted or compiled.  Languages with interpreters tend to process one form or statement at time.  The interpreter reads a statement, analyzes it to produce a fairly high-level representation of the statemnt, typically in the form of an abstract syntax tree (AST).  It then evaluates the statement by traversing the AST.
Evaluation often makes incremental changes to the running environment, such as defining functions or creating bindings for variables.

Compiled languages typically process an entire compilation unit--a file or perhaps multiple files making up a project.  Analysis of the code produces an AST on which various semantic checks and embellishments are made.  The compiler then generates code for an abstract machine, which is then optimized and translated into into some lower-level language, such as machine code or Java bytecodes or MSIL instructions.  That output can be directly executed by the execution engine (the CPU or some excution engine such as the JVM or CLR).  

In either case, the compiler or interpreter share certain steps:

- Lexical analysis: take the stream of characters of the source code and output a stream of 'tokens'.
- Syntax analysis: Take the stream of tokens and determine its hierarchical structure, outputing an abstract syntax tree (AST).
- Semantic analysis: check the semantic consistency of the AST, including making sure that referenced elements exist, types are correct, etc.

They differ in the what follows this analysis.  An interpreter typically performs a traversal of the AST to evaluate it directly. A compiler uses the AST to generate code, usually in several stages:

- Intermediate code generation: take the typed AST and generate code for an abstract machine
- Code optimization: optimize the intermediate code
- Code generation: generate code for the target machine

Clojure fits a bit uncomfortably into this framework.  

- Clojure is a Lisp and exhibits common Lisp-y features.  

   - A runtime environment that is incrementally modified by the evaluation of forms.  
   - Homoiconicity.  To quote [Wikipedia/Homoiconicity](https://en.wikipedia.org/wiki/Homoiconicity): "In a homoiconic language, the primary representation of programs is also a data structure in a primitive type of the language itself."  The Lisp reader translates from source text to Clojure data structures.  The evaluation and compiler components do not deal with source text directly.

- Clojure runs on an execution engine, such as the JVM or the CLR.  Code generation goes only to the platform level, with the hard work of code optimization and machine code generation delegated to the JITter of the platform.  (I'm restricting attention to Clojure(JVM) and ClojureCLR.  I cannot speak to some of the other implementations.)

- Clojure is only partially evaluated.  There are constructs for which it does not try to directly evaluate in any standard notion of the term.  It will wrap the construct into a function, compile the function, and then call the function to execute the construct.

- Clojure compilation involves evaluation.  Discard the notion of compilation as a separate application that takes in source code and churns out a binary, the sole computational effect being translation.  During compilation, one has to evaluation each form in the current Clojure environment, likely cause side-effects in the environment.

Given all this, perhaps we should clearly establish the terms we will use to describe Clojure evaluation and compilation.

- _code_: There is no need to refer to the source text.  By _code_ we mean the Clojure data structure that the Lisp reader produces and that we will pass to the interpreter or compiler.
- _analyze_: The process of taking the code and producing an AST.  This is a syntactic analysis of the code, but also includes semantic analysis.  The result is an AST that is annotated with type information and other semantic information.
- _evaluate_: The process of traversing the AST and directly performing computation from it.
- _code-gen_: Creation of platform classes and methods and the generation of bytecode/MSIL instructions for the methods.  This could be either dynamic or _persisted_ (generated into files, either Java .class files or .NET assemblies, for later loading).  This can occur during the _analyze_ phase or during _AOT-compilation_.
- _AOT-compilation_: Doing persisted code-gen from Clojure code.


## Let's evaluate

The primary interaction with Clojure is to read a form, analyze it, and evaluate it.  This could happen directly at the REPL, or through REPL-feeding from your editor.  It could be by loading a file of Clojure code by any of a number of means. (calling `load` or `require` or starting Clojure with command to `main` to load a file).  (We'll leave  AOT-compilation out of the mix for now.)   These actions at root come to down to calls to `clojure.core/eval` or `clojure.core/load`, which themselves ultimately call methods in the `clojure.lang.Compiler` class, written in the base language (Java, C#, whatever).

`clojure.lang.Compiler.load` really is just repeated to calls to `Compiler.eval`.  Leaving out a few details, it is just

```C#
while ((form = LispReader.read(lntr, false, eofVal, false, readerOpts)) != eofVal)
{
      ret = eval(form);
}

return ret;
```

(`lntr` is a line-numbering text reader; the other parameters to the reader need not concern us.)

`Compiler.eval` should be this:


```C#
form = Macroexpand(form)
Expr expr = Analyze(pconEval, form);
return expr.Eval();
```

`Expr` is the interface implemented by all AST nodes. 

```C#
public interface Expr
{
   object Eval();
   void Emit(RHC rhc, ObjExpr objx, CljILGen ilg);

   // ... three other methods relating to details of code-gen
}
```

`Eval` does evalution.  `Emit` does code-gen. In the `eval` code:

```C#
form = Macroexpand(form)
Expr expr = Analyze(pconEval, form);
return expr.Eval();
```

`Compiler.Analyze` starts by macroexpanding the form it is given.  It then analyzes the resulting form and constructs an AST representing it.  (The `pconEval` is a parameter that indicates the context in which form is being analyzed.  Here, it is set to indicate we are calling in an evaluation context.  More on contexts later.)  Once we have the AST root node, we can call its `eval` method to perform the computation.

The careful reader will notice that I should `Compiler.eval` _should_ be like this.
Actually, it's more complicated.  Two cases have special handling. The first case deals with macroexpansion. When a macro needs to expand into multiple forms, the standard practice is to wrap the forms in a `do` form.

```Clojure
(m ...args...)  =>  (do form1 form2 ...)
```

With the incremental update model of Clojure loading, we can run into a problem treating the `do` expression monolithically;  it might be necessary to evaluate  `form1` before even beginning the analysis of `form2`.  An example of this is the so-called "Gilardi scenario"; you can find it discussed [here](https://technomancy.us/143). 

> The Gilardi Scenario refers to a case where you want to evaluate some code that both loads in a new var and refers to that var in the same piece of code

The first special case catches a `do` form  resulting from a macro expansion and evaluates each of forms in the `do` in sequence, returning the value computed from the last form as required by the semantics of `do`.

The second special case is hard to even state, much less justify.  There are two conditions that trigger it:

- the form is an `IType`.  `IType` is a marker interface that is only used for types defined by `deftype` (directly, or indirectly -- `defrecord`, for example, uses `deftype` to do its business).  I have no idea why this type is tested for.
- the form looks like `(f1 f2 ...)` and `f1` is not a symbol with a name that starts with `"def"`.

In this case, we do the following:

```C#
ObjExpr objx = (ObjExpr)Analyze(pconExpr, RT.list(FnSym, PersistentVector.EMPTY, form), "eval" + RT.nextID());
IFn fn = (IFn)objx.Eval();
return fn.invoke();
```

In other words, we take the `form` and make it the body of a function with no arguments.  Analyzing that will produce an AST with the root node being an `FnExpr`. (The eagle-eyed will note that the analysis context is no longer `pconEval` -- that's important and will be explained later.)  The analysis has a side-effect: it creates a new class that implements `IFn` and has a method `invoke()` into which we _code-gen_ the form.  `ObjExpr.eval()` returns an instance of that class, i.e., an object on which we can call `invoke()`.  And that's what we do.  The `invoke()` performs the calculation defined by `form`.

In this case, we see code-gen happening during analysis.  By wrapping the form in an `fn*` form, we end up compiling `form` all the way down to bytecode/MSIL instructions. 

Why?  Some AST nodes do not support evaluation.  Perhaps because doing so just wasn't worth the effort.  For example, evaluating a `let` form would require managing local variables scopes, the nesting of scoeps, shadowing of names, and then closing over non-local variable bindings in nested functions.  So, just wrap the form in a function to provide the local binding scope machinery and compile it.  At least, that's my guess.

The default case is actually just the simple case described above:

```C#
Expr expr = Analyze(pconEval, form);
return expr.Eval();
```

What makes it through to this case?  Numbers, strings, and characters. Symbols. A few other things.
Also, forms that look like `(defXXX ....)`.

One ends up with some odd distinctions.  Consider:

```clojure
(def not-sure 12)
(def maybe 13) 
(def definitely 14)
[not-sure maybe] ; => [12 13], but causes a function class to be created, instantiated, and invoked.
[definitely maybe] ; => [14 13], but no function class involved.
```






 