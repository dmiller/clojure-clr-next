---
layout: post
title: ClojureCLR -- Reconsidered
date: 2023-01-06 00:00:00 -0500
categories: general
---

Introducing a project to rewrite ClojureCLR -- ClojureCLR.Next.  With this blog, I hope to record some of the thinking I go through in the process, for myself mostly but perhaps for a future maintainer of the project.


## The origin

I originally started ClojureCLR, with Rich Hickey's blessing, in the fall of 2008.  (I wrote about that [here](https://rationalx.blogspot.com/2011/11/clojureclr-genesis.html)).  Jump ahead 14+ years and ClojureCLR is still keeping up with ClojureJVM itself, at least in terms of basic language features.

That's only one sense of _keeping up_.  The community using ClojureCLR is very small.  As a result, there has not been much development of documentation, libraries, tooling, and other contributions to the ecosystem that make a language successful.

I did not have the time and energy to do much more than keep ClojureCLR up to date.  But circumstances change and I should be able to devote more time to ClojureCLR going forward.  I plan to use this time to: (1) work on building the community and ecosystem around ClojureCLR; and (2) to improve ClojureCLR itelf.

Building the community and eco-system obviously will be the work of many.  I hope to concentrate discussions on ClojureCLR in two places, the `#clr` channel in the [Clojurians slack workspace](https://clojurians.slack.com/) and in [ask.clojure.org](https://ask.clojure.org). (In order to avoid spreading effort too far, I would like to deprecate the [ClojureCLR Google group](https://groups.google.com/g/clojure-clr) and the [ClojureCLR Gitter](https://gitter.im/clojure-clr/community).)  There seems to be some good energy developing.  There are some new library ports avaiable.  There is work on an implementation of NRepl that will allow things like CIDER and Calva support so that the editing situation will be improved.  And there are some efforts underway to move to get support for deps.edn and CLI.  Things appear to be moving on this front.

This website and blog are dedicated to the other task: improving ClojureCLR.  The posts here serve the dual purposes of talking through things as I work on redesign and providing guidance to different parts of the code base for the benefit of future maintainers of ClojureCLR.


## The inspiration

I have a number of reasons for starting this project.

1. I made some suboptimal decisions ranging back more than a decade, when trying to adapt the ClojureJVM implementation to the CLR. For example, I did not pay enough attention to ways in which the JVM and CLR differ in areas such as structs and generics.

2.  The move to .Net Core 3.x and .Net 5/6/... resulted in the loss of ahead-of-time (AOT) compilation. .Net no longer supports `AssemblyBuilder.Save()` and with that ClojureCLR suffers slower startup times and loss of functionality for `gen-class` and relatives. Available workarounds have not proved sufficient to restore this capability.

3. The work of Ramsey Nasser and Tims Gardner has shown that better type analysis and IL generation yields significant performance improvements for some applications.  (Head over to [Ramsey's Github page](https://github.com/nasser) and look at Arcadia, Magic, Mage, and related projects.)

4. .Net has been moving to platforms where code generation as done by the DLR (Dynamic Language Runtime) is prohibited.  Moving to those platforms will require some sort of hand-built polymorphic-inline-caching to run and be performant.  An area where Ramsey has already done some investigation, so there are ideas to build on.

Addressing these points is going to require non-trivial modification of the compiler including a complete rewriting of the code generation parts.  The prospect is  daunting projects the complexity of the compiler code.

Not only my decisions bear inspection. In his recent paper ["A History of Clojure"](https://download.clojure.org/papers/clojure-hopl-iv-final.pdf) Rich writes "I wish I had though of protocols sooner, so that more of Clojure's abstractions could have been built atop them rather than Java interfaces."  I remember documents -- I can't find them now, but I believe they go back as far back as 2010-11--where Rich talked about problems in the compiler with the use of global state and other sins against good taste.  I'd like to address these points also.


## The project

Thus this project: A complete rewrite of ClojureCLR.  May it see the light of day.  My goals:

1.  Examine every single line of C# code in ClojureCLR.  (I'd like to stick with my requirement that the Clojure source code used to provide the runtime environment be modified minimally.)

2. Document all differences from ClojureJVM.

3. Rewrite the compiler completely.  Goals include: (a) improving the structure of the code to be more maintainable; (b) getting rid of code smells such as the global state mechanism; (c) moving to a different mechanism (think Mono.Cecil, Lokad.IL, Roslyn) for IL code generation,  thereby restoring AOT-compilation and improving start-up time; (d) improving type analysis and code generation for value types in particular; (e) examining the treatment of other non-JVM functionality such as (real) generics; (f) rethinking the mechanisms around the use of dynamic call sites.

4.  Investigate the use of protocols at the lowest level, a la ClojureScript, ClojureDart, and other derivatives of Clojure.   (See the note above _re_ the _History_ paper.)

(Side note:  In the _History_ paper, Rich also writes "I think transducers are a fundamental primitive that decouples critical logic from list/sequence processing and construction, and if I had Clojure to do all over I would put them at the bottom."  I think this is one step beyond what I can take on at this time -- mostly because I have no idea what he is talking about.)

I've been thinking about this project for quite some time.  As evidence, I talked about it on [a podcast](https://www.patreon.com/posts/48-david-miller-26139408) back in 2019.  And I have some emails to someone on the Clojure team on this idea dating to 2016.  And I started writing this blog post in mid-2021.  I think it's about time to get started.


## A small detail

I plan to rewrite all the C# code in F#.  I have several reasons for this.

1. I don't know F#.  I like what I see.  Learning a new language keeps me motivated.  And the newer parts of C# that interest me the most are mostly here already.

2. Rewriting in another language will actually force me to look at every single line of code.  When writing the current code, I often could do rote translation of the Java code to C#. This time, I will have to think.

3. Writing proper F# will force analysis of the code that was not necessary before.  For example, good F# style is opposed to circular references or mutually recursive code.  I can't just connect 75 type definitions with `and` and have any self-respect.   The discipline required will be healthy.

Given the relative sizes of the F# and C# developer communities, one might object that moving to F# would reduce the number of people who might contribute to the ClojureCLR implementation.  To which I counterpose: check the commit log for the last 14 years. There is no down from where we are.  And perhaps I can find more kindred spirits in the F# community. 

## And ...


away we go.



