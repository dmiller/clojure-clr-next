---
layout: post
title: C4 - Classic Clojure Compiler Contemplation
date: 2025-08-31 00:00:00 -0500
categories: general
---

The first in a series of posts analyzing the Clojure (JVM/CLR) compiler.
In this post, we look at the overall structure of the compiler.  

The other posts in the series are:

- [C4: AST me anything]({{site.baseurl}}{% post_url 2025-09-01-AST-me-anything}) - A tour of the AST nodes produced by the compiler
- [C4: Symbolic of what?]({{site.baseurl}}{% post_url 2025-09-02-symbolic-of-what}) - A little digression on what symbols represent
- [C4: ISeq clarity]({{site.baseurl}}{% post_url 2025-09-03-iseq-clarity}) - How to analyze an `ISeq`
- [C4: Functional anatomy]({{site.baseurl}}{% post_url 2025-09-04-functional-anatomy}) - How functions are implemented in Clojure
- __C4: The fn*: talkin' 'bout my generation__ - Code-gen for functions
- __C4: How type-ical__ - Type analysis by the compiler
- __C4: I have something to emit__ - More on code generation
- __C4: A time for reflection__ -- Reflection and dynamic callsites
- __C4: Is there a protocol for that?__ -- Protocols

## The terms of discussion

Languages are often classified as interpreted or compiled.  Languages with interpreters tend to process one form or statement at time.  The interpreter reads a statement, analyzes it to produce a fairly high-level representation of the statemnt, typically in the form of an abstract syntax tree (AST).  It then evaluates the statement by traversing the AST. Evaluation often makes incremental changes to the running environment, such as defining functions or creating bindings for variables.

Compiled languages typically process an entire compilation unit--a file or perhaps multiple files making up a project.  Analysis of the code produces an AST on which various semantic checks and embellishments are made.  The compiler then generates code for an abstract machine, which is then optimized and translated into into some lower-level language, such as machine code or Java bytecodes or MSIL instructions.  That output can be directly executed (byt the CPU or some excution engine such as the JVM or CLR).  

In either case, the compiler or interpreter share certain steps:

- Lexical analysis: take the stream of characters of the source code and output a stream of 'tokens'.
- Syntax analysis: Take the stream of tokens and determine its hierarchical structure, outputing an abstract syntax tree (AST).
- Semantic analysis: check the semantic consistency of the AST, including making sure that referenced elements exist, types are correct, etc.

They differ in the what follows this analysis.  An interpreter typically performs a traversal of the AST to evaluate it directly. A compiler uses the AST to generate code, usually in several stages:

- Intermediate code generation: take the typed AST and generate code for an abstract machine
- Code optimization: optimize the intermediate code
- Code generation: generate code for the target machine

## The Clojure difference

Clojure fits a bit uncomfortably into this framework.  

- Clojure is a Lisp and exhibits common Lisp-y features.  

   - A runtime environment that is incrementally modified by the evaluation of forms, even during compilation.
   - Homoiconicity.  To quote [Wikipedia/Homoiconicity](https://en.wikipedia.org/wiki/Homoiconicity): "In a homoiconic language, the primary representation of programs is also a data structure in a primitive type of the language itself."  The Lisp reader translates from source text to Clojure data structures.  The evaluation and compiler components do not deal with source text directly.

- Clojure runs on an execution engine, such as the JVM or the CLR.  Code generation goes only to the platform level, with the hard work of code optimization and machine code generation delegated to the JITter of the platform.  (I'm restricting attention to Clojure(JVM) and ClojureCLR.  I cannot speak to some of the other implementations.)

- Clojure does not support naive evaluation for all constructs.  The are primitive operations that the Clojure evaluator will wrap into a function, compile the function, and then call the function to execute the construct.

- Clojure compilation involves evaluation.  Discard the notion of compilation as a separate application that takes in source code and churns out a binary, the sole computational effect being translation.  During compilation, one has to evaluate each form in the current Clojure environment, likely causing side-effects in the environment.

Given these differences, I will try to be careful in my terminology.  I will use:

- _source code_: There is no need to refer to the source text.  By _source code_ we mean the Clojure data structure that the Lisp reader produces and that we will pass to the interpreter/compiler.
- _analyze_/_analysis_: The process of taking the code and producing an AST.  This is a syntactic analysis of the code, but also includes semantic analysis.  The result is an AST that is annotated with type information and other semantic information.
- _evaluate_/_evaluation_: The process of traversing the AST and directly performing computation from it.
- _code-gen_: Creation of platform classes and methods and the generation of bytecode/MSIL instructions for the methods.  This could be either dynamic or _persisted_ (generated into files, either Java .class files or .NET assemblies, for later loading).  This can occur during the _analysis_ phase or during _AOT-compilation_.
- _AOT-compilation_: Doing persisted code-gen from Clojure code.


## What a load of ...

The primary interaction with Clojure is to read a form, analyze it, and evaluate it. This could happen:

- At the REPL: A Clojure instance running a Read-Eval-Print Loop does this repetitively.
- From a file: calling `load`, `compile`, or `require`, or start a Clojure `main` with a command to load a file.

All of these methods at root come down to calls to `clojure.core/eval` or `clojure.core/load`, which themselves ultimately call methods in the `clojure.lang.Compiler` class, written in the base language (Java, C#, whatever).
Even `compile` just ends up calling `clojure.core/eval` with a flag set to indicate that we want code-gen to an external file.

`clojure.lang.Compiler.load` really is just repeated to calls to `Compiler.eval`.  Leaving out a few details, it is just

```C#
while ((form = LispReader.read(lntr, false, eofVal, false, readerOpts)) != eofVal)
{
      ret = eval(form);
}

return ret;
```

(`lntr` is a line-numbering text reader; the other parameters to the reader need not concern us.)

## Let's evaluate where we stand

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

We first macroexpand to find the code that really needs to be evaluated. 
We analyze the resulting form and construct an AST representing it.  (The `pconEval` is a parameter that indicates the context in which form is being analyzed.  Here, it is set to indicate we are calling in an evaluation context.  More on contexts later.)  Once we have the AST root node, we can call its `eval` method to perform the computation.

The careful reader will notice that I said `Compiler.eval` _should_ be like this.
There are two small complications: One has to do with the results of macroexpansion. When a macro needs to expand into multiple forms, the standard practice is to wrap the forms in a `do` form.

```Clojure
(m ...args...) ; (macroexpand)=>  (do form1 form2 ...)
```

With the incremental update model of Clojure loading, we can run into a problem treating the `do` expression monolithically;  it might be necessary to evaluate  `form1` before even beginning the analysis of `form2`.  An example of this is the "Gilardi scenario":

> The Gilardi Scenario refers to a case where you want to evaluate some code that both loads in a new var and refers to that var in the same piece of code. [ [source](https://technomancy.us/143)  ]

After macroexpanding, we check the resulting form.  If it is of the form `(do form1 form2 ...)`, we iterate across the forms and recursively call `eval` on each.

The second special case is hard to even state, much less justify.  There are two conditions that trigger it:

- the form is an `IType`.  `IType` is a marker interface that is only used for types defined by `deftype` (directly, or indirectly -- `defrecord`, for example, uses `deftype` to do its business).  I have no idea why this type is tested for.
- the form looks like `(f1 f2 ...)` and `f1` is not a symbol with a name that starts with `"def"`.

In this case, we do the following:

```C#
ObjExpr objx = (ObjExpr)Analyze(pconExpr, RT.list(FnSym, PersistentVector.EMPTY, form), "eval" + RT.nextID());
IFn fn = (IFn)objx.Eval();
return fn.invoke();
```

The key element here is `RT.list(FnSym, PersistentVector.EMPTY, form)`: we take the `form` and make it the body of a function with no arguments.  Analyzing that will produce an AST with the root node being an `FnExpr`. (The eagle-eyed will note that the analysis context is no longer `pconEval` -- that's important and will be explained later.)  The analysis has a side-effect: it creates a new class that implements `IFn` and has a method `invoke()` into which we _code-gen_ the form.  `ObjExpr.eval()` returns an instance of that class, i.e., an object on which we can call `invoke()`.  And that's what we do.  The `invoke()` performs the calculation defined by `form`.

In this case, we see code-gen happening during analysis.  By wrapping the form in an `fn*` form, we end up compiling `form` all the way down to bytecode/MSIL instructions. 

Why?  Some AST nodes do not support evaluation.  Perhaps because doing so just wasn't worth the effort.  For example, evaluating a `let` form would require managing local variables scopes, the nesting of scopes, shadowing of names, and then closing over non-local variable bindings in nested functions.  So, just wrap the form in a function to provide the local binding scope machinery and compile it.  At least, that's my guess.

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
[definitely maybe] ; => [14 13], but no function class generated.  We just create a vector.
 ``` 