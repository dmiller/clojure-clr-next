---
layout: post
title: Introduction
date: 2021-08-27 19:36:00 -0500
categories: general
---

# ClojureCLR: Reconsidered

I originally started ClojureCLR, with Rich Hickey's blessing, in Fall 2008.  (I wrote about that [here](https://rationalx.blogspot.com/2011/11/clojureclr-genesis.html)).  Jump ahead almost 13 years and ClojureCLR is still keeping up with ClojureJVM itself, at least in terms of basic language features.

Beyond just keeping up, there are few other things I'd like to tackle.

Tooling is high on the list. Now that Clojure core group is working in this area (rather than letting it all be third-party), ClojureCLR should be following their lead.  I'm thinking specifically of the recent work on  [Deps and CLI](https://clojure.org/reference/deps_and_cli) and [tools.build](https://clojure.org/guides/tools_build)  It appears that more is coming -- see [Source libs and builds] https://clojure.org/news/2021/07/09/source-libs-builds, for example.  Personally, I've relied too long on lein-clr for my library work and would like a better solution.  (One definition of 'better' is that I would not be required to download Java and ClojureJVM just to work on a library for ClojureCLR.)

The highest priority is improving ClojureCLR itself.  In particular,

1.  I made some sub-optimal choices when trying to adapt ClojureJVM techniques to the CLR. FOr example, I did not pay enough attention to ways in which the JVM and CLR differ in areas such as structs and generics.

2.  The move to .Net Core 3.x and .Net 5.0 resulted in the loss of ahead-of-time (AOT) compilation. .Net no longer supports `AssemblyBuilder.Save()` and with that ClojureCLR suffers slower startup times and loss of functionality for`gen-class` and relatives. Available workarounds have not proved sufficient to restore this capability.

3. The work of Ramsey Nasser and Tims Gardner has shown that better type analysis and IL generation yields significant performance improvements for some applications.  (Head over to [Ramsey's Github page](https://github.com/nasser) and look at Arcadia, Magic, Mage, and related projects.)

Item (2) requires at minimum a rewriting of the compiler's IL generation code. Item (3) requires even more significant changes to the compiler.  These are daunting projects given the complexity of the compiler.

Item (1) will requires even greater effort.  Not only my decisions bear inspection. In his recent paper ["A History of Clojure"](https://download.clojure.org/papers/clojure-hopl-iv-final.pdf) Rich writes "I wish I had though of protocols sooner, so that more of Clojure's abstractions could have been built atop them rather than Java interfaces."  I remember documents--I cant' find them now, but I believe as far back as 2010 or 2011--where Rich talked about problems in the compiler with the use of global state and other crimes against good taste.

# The project

Thus this project: A complete rewrite of ClojureCLR.  May it see the light of day.  My goals:

1.  Examine (and likely rewrite) every single line of C# code in ClojureCLR.  (The code written in Clojure itselfused to provide the runtime environment should not be touched minimally.)

2. Document all differences from ClojureJVM.

3. Rewrite the compiler completely.  Goals include: (a) improving the structure of the code to be more maintainable; (b) getting rid of code smells such as the global state mechanism; (c) moving to Mono.Cecil or similar for code generation,  therebe restoring AOT-compilation and improving start-up time; (d) improving type analysis and code generation for value types in particular; (e) examining the treatment of other non-JVM functionality such as (real) generics; (f) rethinking the mechanisms around the use of dynamic call sites.

4.  Investigate the use of protocols at the lowest level, a la ClojureScript (and, I believe, the ClojureDart that is under development.)  (See the note above _re_ the _History_ paper.  In that paper, Rich also writes "I think transducers are a fundamental primitive that decouples critical logic from list/sequence processing and construction, and if I had Clojure to do all over I would put them at the bottom."  I think this is one step beyond what I can take on at this time -- mostly because I don't understand either transducers or what Rich is thinking.)


# A small detail

I plan to rewrite all the C# code in F#.  I have several reasons for this.

1. Learning a new language keeps my interest up.  This is the only new language I would consider for this project, given the target platform.

2. Rewriting in another language will actually force me to look at every single line of code.  When writing the current code, I often could do rote translation of the Java code to C#. This time, I will have to think.

3. Writing proper F# will force analysis of the code that was not necessary before.  For example, F# is opposed to circular references or mutually recursive code.  I can't just connect 75 type definitions with `and` and have any self-respect.   The discipline required will be healthy.

Did I mention that I don't really know F#?  I find it's good to have a nice project when learning a new language.  Immplementing a language is a great way to learn a language.  (The extreme of this is to implement the language you are learning.  I'm not going to go to Clojure-in-Clojure, though.)

# These posts

The posts on this site serve at least these two purposes: To help clarify my thinking as I work on redesign; To provide introductions to different parts of the code base for the benefit of future maintainers of ClojureCLR.


