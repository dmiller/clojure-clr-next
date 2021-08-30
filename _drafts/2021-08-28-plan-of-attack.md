---
layout: post
title: The plan of attack
date: 2021-08-28 19:36:00 -0500
categories: general
---

# The plan of attack

One does not simply start writing a Clojure implementation.  One needs a plan.

## The existing code

Just for fun, I ran Visual Studio's code metrics on the C# projects in the current ClojureCLR (ignoring the .clj code that defines the upper level of the runtime environment).  Breaking this into the application proper _versus_ tests:

| Category    		|   SLOC 	|   ELOC 	| Types 	| Members 	| Types (2)		| Members (2) 	|
|-------------		|-------:	|-------:	|------:	|--------:	|----------:	|------------:	|
| Application(1) 	| 76,223 	| 15,304 	|   859 	|   7,293 	|       501 	|       6,935 	|
| Tests       		| 17,761 	|  7,773 	|   119 	|   1,138 	|         - 	|           - 	|

Notes:

1.  _Application_ here is Clojure.dll only;  there is very little code in Clojure.Main and other pieces.
2.  These additional columns with type and members counts subtract the types/members of `clojure.lang.primifs`, 358 interfaces with one method each.  They could be auto-generated and inflate the counts unnecessarily. I hope to get rid of them entirely in the rewrite.

I'm not sure how the tool computes ELOC (Effective Lines of Code), but I think we can conclude there are a lot of blanks lines, header lines, brace-only lines, and comments.  And copyright notices. Still, 500 classes and 7000 memebers is nothing to sneeze at.

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

The Standalones are a good place to start.  Not because they are needed immediately, but because they are simple and can be written and tested independently.  I will start with `BigDecimal` and `Murmur3` so I can practive writing and structuring F# code--I have almost no experience--writing unit tests, and benchmarking.  I plan to ditch my own `BigInteger` for `System.Numerics.BigInteger` and rewrite `BigDecimal` using the latter.  `Murmur3` is needed fairly early for certain hash code computations. 

The Collections, IFn, and Runtime categories are intertwined.   Much of Runtime needs a certain minimum of collections to exist.  The collections in turn use code out of the Runtime.  The support code for implementing `IFn`s needs some collections.  The collections need `IFn` for things like `reduce` and `LazySeq`.   Deconstructing and linearising this code is going to take serious analysis of dependencies.  

Once one has the right set of collections, support for `IFn`, and some of `RT`, one can implement most of the Reader code. This will also involve much of what I called the Library code:  The readers know symbols, keywords, namespaces, vars, ... . There are a few parts of  `LispReader` that invoke the compiler; we'll play some tricks to defer those references until the compiler is ready. (And to prevent circularities.)

And then the compiler and remaining pieces of runtime support.

## an approach

I'm going to start off implementing the Collections/IFn/parts-of-Runtime with a fairly direct mapping of C# to F#.  That means interfaces, abstract classes, concrete classes, inheritance, a liberal sprinkling of `[<AllowNullLiteral>]`, etc.  There are some significant reasons for this approach -- non-trivial amounts of shared code as defaults for operations, for example.  Some of the new capabilities of F# such as default implementations for interface methods don't appear to be applicable -- differing implementations across categories of collections are a stumbling block.

I am not starting with protocols.  I don't know how to do protocols yet.  I could spend a long time figuring this out and never get to the more critical part of this rewrite, the compiler improvements.  So I am deferring on protocols for now.   I will do significant rewriting of my early code if that is required.