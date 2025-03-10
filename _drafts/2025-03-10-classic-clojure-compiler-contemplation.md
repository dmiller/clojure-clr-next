---
layout: post
title: C4 - Classic Clojure Compiler Contemplation
date: 2025-03-10 00:00:00 -0500
categories: general
---

Introducing a series of posts analyzing the Clojure (JVM/CLR) compiler.


One of the goals of the ClojureCLR.Next project is a complete rewrite of the compiler.  As stated in the [introductory post]({{site.baseurl}}{% post_url 2023-01-06-clojureclr-reconsidered %}):

> 3. Rewrite the compiler completely.  Goals include: 
>    (a) improving the structure of the code to be more maintainable; 
>    (b) getting rid of code smells such as the global state mechanism; 
>    (c) moving to a different mechanism (think Mono.Cecil, Lokad.IL, Roslyn) for IL code generation,  thereby restoring AOT-compilation and improving start-up time; 
>    (d) improving type analysis and code generation for value types in particular; 
>    (e) examining the treatment of other non-JVM functionality such as (real) generics; 
>    (f) rethinking the mechanisms around the use of dynamic call sites.

This work necessitates a thorough analysis of the existing compiler.  
There are things about the current Clojure (both JVM and CLR).
The CLR version is definitely the result of port-and-patch: copy the Java code, transliterate to C#, patch it to make it work.
There was a bit more than that, I suppose.  Significant changes had to be made to adapt from the Java classfile/classloader/classpath
model to the asssmbly-cnetric model of the CLR.  And I added the Dynamic Language Runtime (DLR) mechanisms 
to manage occurrences of reflection with the equivalent of `dynamic` in C#.
But there were many things I could leave unexamined.

No longer.

- [C4: Compiler structure]() - A quick examination of what we get from the Lisp reader
- [C4: AST me anything]() - A tour of the AST nodes produced by the compiler
- [C4: Symbolic of what?]() - A little digression on what symbols represent
- [C4: Type-ical]()- Type analysis by the compiler
- [C4: I have something to emit]() - Code generation

