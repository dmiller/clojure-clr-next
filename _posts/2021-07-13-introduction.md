---
layout: post
title: Introduction
date: 2021-07-13 19:36:00 -0500
categories: general
---

# ClojureCLR: Reconsidered

I originally started ClojureCLR, with RichHickey's blessing, in Fall 2008, primarily to scratch a few itches.  (I wrote about that [here](https://rationalx.blogspot.com/2011/11/clojureclr-genesis.html)).  Jump ahead almost 13 years and ClojureCLR is still keeping up with ClojureJVM itself, at least in terms of basic language features.

There are many ways in which it does not match up to ClojureJVM:

1. There are very few users, and subsequently
2. there are very few libraries and
3. there is almost no tooling.

I cannot do much about the first two points; I plan to do something (eventaually) about point (3), specifically to match the recent work on [Deps and CLI](https://clojure.org/reference/deps_and_cli).

But before that I want to turn attention to the ClojureCLR engine itself.  On my mind:

1. I made some ill-considered decisions ranging back more than a decade, when trying to adapt ClojureJVM techniques to the CLR. 

2.  The move to .Net Core 3.x and .Net 5.0 caused the loss of ahead-of-time (AOT) compilation. .Net no longer supports `AssemblyBuilder.Save()` and with that ClojureCLR suffers slower startup times and loss of functionality for`gen-class` and similar. Available workarounds have not proved sufficient to the task.

3. The work of Ramsey Nasser and Tims Gardner has shown that better type analysis and IL generation yields significant performance improvements for some applications.  (Head over to [Ramsey's Github page](https://github.com/nasser) and look at Arcadia, Magic, Mage, and related projects.)

Item (2) requires at minimum a rewriting of the compiler's code generation code in the compiler. Item (3) requires even more significant changes.  These are daunting projects given the complexity and fragility of that code.

Item (1) will requires even greater effort.  Not only my decisions bear inspection. In his recent paper ["A History of Clojure"](https://download.clojure.org/papers/clojure-hopl-iv-final.pdf) Rich writes "I wish I had though of protocols sooner, so that more of Clojure's abstractions could have been built atop them rather than Java interfaces."  I can no longer find some of the documents, dating back as far as 2011 if memory serves, where Rich talks about problems in the compiler with the use of global state and other sins against good taste.

# The project

Thus this project: A complete rewrite of ClojureCLR.

1.  Examine (and likely rewrite) every single line of C# code in ClojureCLR.  (The actual Clojure code used to provide the runtime environment should not be touched more than a minimum.)

2. Document all differences from ClojureJVM.

3. Rewrite the compiler completely.  Goals include (a) improving the structure so the code is more maintainable; (b) getting rid of code smells such as the global state mechanism; (c) moving to Mono.Cecil or similar for code generation, restoring AOT-compilation; (d) improving type analysis and code generation for value types in particular; (e) examinint the treatment of other non-JVM functionality such as generics (I mean, real generics, not that type-erasing stuff in the JVM); (f) rethinking the mechanisms around the use of dynamic call sites.

4.  Investigate the use of protocols, a la ClojureScript (and, I believe, the ClojureDart that is under development.)



# A little detail

I plan to rewrite all the C# code in F#.  I have several reasons for this.

1. Learning a new language keeps my interest up.  This is the only new language I would consider for this project, given the target platform.

2. Rewriting in another language will actually force me to look at every single line of code.  When writing the current code, I often could do rote translation of the Java code to C#. This time, I will have to think.

3. Writing proper F# will force analysis of the code that was not necessary before.  For example, F# is opposed to circular references or mutually recursive code.  I can't just connect 75 type definitions with `and` and have any self-respect.   The discipline required will be healthy.

4. Good F# code and the analysis it will require might be useful for anyone thinking eventually of doing a real Clojure-in-Clojure.  (Why do I choose not to go this direction?  An allergy to bootstrapping, perhaps.  Definitely I'm not that good of a Clojure programmer.  And I think doing a Clojure-in-Clojure is the domain of Rich -- not my territory.)

# These posts

I plan to write up some of my adventure here.  It will help clarify my own thinking.  I hope it will be useful to some future maintainer of this code, or to anyone trying to figure out how the pieces fit together.

But mostly for me.




