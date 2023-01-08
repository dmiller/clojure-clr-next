---
layout: post
title: The plan of attack
date: 2023-01-07 00:00:00 -0500
categories: general
---

One does not simply start writing a Clojure implementation.  One needs a plan.

## The existing code

Just for fun, I ran (back in 2021) Visual Studio's code metrics on the C# projects in the current ClojureCLR.  This is for the C# code in the Clojure project itself, ignoring things like Clojure.Main (minimal code) and all the .clj code that defines the Clojure runtime environment, tests, etc.

Breaking this into the application proper _versus_ tests:

| Category    		|   SLOC 	|   ELOC 	| Types 	| Members 	| Types (2)		| Members (2) 	|
|-------------		|-------:	|-------:	|------:	|--------:	|----------:	|------------:	|
| Application(1) 	| 76,223 	| 15,304 	|   859 	|   7,293 	|       501 	|       6,935 	|
| Tests       		| 17,761 	|  7,773 	|   119 	|   1,138 	|         - 	|           - 	|

Notes:

1.  _Application_ here is Clojure.dll only;  there is very little code in Clojure.Main and other pieces.
2.  These additional columns with type and members counts subtract the types/members of `clojure.lang.primifs`, 358 interfaces with one method each.  They could be auto-generated and inflate the counts unnecessarily. I hope to get rid of them entirely in the rewrite.

However the metric computes ELOC (Effective Lines of Code), I think we can conclude there are a lot of blanks lines, header lines, brace-only lines, and comments.  And copyright notices. But no matter how you count the lines, 500 classes and 7000 members is daunting.


Here is a rough breakdown of the application code (again, not including `primifs`):

| Area 			| #Types	| Description										|
|------			| -------:	| ---------											|
| Collections	| 152		| Persistent, immutable sequences, maps, etc.		|	
| Compiler		| 107		| The compiler itself								|
| IFn			| 54		| Support for IFn and related						|
| Library		| 50		| Clojure entities that are not collections, etc.	|
| Reader		| 53		| LispReader, EdnReader								|
| Runtime		| 62		| Runtime support functions							|
| Standalone	| 23		| BigDecimal, Printf, etc.							|

The line I drew between Library and Runtime is fuzzy.  I included in the Library category objects such as symbols, keywords, vars, refs, atoms, and agents.  The Runtime category includes the `RT` and `Util` classes that provide code needed, well, at runtime.  `RT` in particular provides a lot of methods that implement Clojure functions such as `first` and `reduce`. This area also will also the locus of analysis for and application of _protocols-at-the-bottom_.

## Where to start?

There are a lot of dependencies in this code.  Those dependencies determine roughly the order in which things can be written.

There are so many circular dependencies one is inclined to think there is no place to start.  But let's give it a shot.

The Standalones are a good place to start.  Not because they are needed immediately, but because they are simple and can be written and tested independently.  I plan to start with `BigDecimal` and `Murmur3` so I can practice writing and structuring F# code, writing unit tests, and benchmarking.  I plan to ditch my own `BigInteger` for `System.Numerics.BigInteger` and rewrite `BigDecimal` using the latter.  `Murmur3` is needed fairly early for certain hash code computations.  (Confession: I shouldn't be using the future tense.  I got this done back in 2021. )

The Collections, IFn, and Runtime categories are intertwined.   Much of Runtime needs a certain minimum of collections to exist.  The collections in turn use code out of the Runtime.  The support code for implementing `IFn`s needs some collections.  The collections need `IFn` for things like `reduce` and `LazySeq`.   Deconstructing and linearising this code is going to take serious analysis of dependencies.  

Once one has the right set of collections, support for `IFn`, and some of `RT`, one can implement most of the Reader code. This will also involve much of what I called the Library code:  The readers know symbols, keywords, namespaces, vars, ... . There are a few parts of  `LispReader` that invoke the evaluator/compiler; we'll play some tricks to defer those references until the those other pieces are ready. (And to prevent circularities.)

And then the compiler and remaining pieces of runtime support.

##