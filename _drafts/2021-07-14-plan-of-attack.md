---
layout: post
title: The plan of attack
date: 2021-07-14 19:36:00 -0500
categories: general
---

# The plan of attack

One does not simply start writing a Clojure implementation.  One needs a plan.

## The existing code

Just for fun, I ran Visual Studio's code metrics on the C# projects in the current ClojureCLR (ignoring the .clj code that populates the runtime environment).  Breaking this into the application proper _versus_ tests:

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

The distinctions between Library and Runtime are a bit blurry.  I included in the Library category objects such as symbols, keywords, vars, refs, atoms, and agents.  The Runtime category includes the `RT` and `Util` classes that provide code needed, well, at runtime.  `RT` in particular provides a lot of methods that implement clojure functions such as `first` and `reduce`. This area also will also the locus of analysis for and application of _protocols-at-the-bottom_.

## Where to start?

Given all the dependencies, one needs to determine roughly the order in which things can be written.

The Standalones are a good place to start.  Not because they are needed immediately, but because they are simple and can be written and tested independently.  Given that at the onset I will have not have written an F# program more than a few lines long, I need to practice writing and structuring F# code and to develop friendly relations with testing libraries, BenchmarkDotNet, and other niceties/necessities.  I plan to ditch `BigInteger` for `System.Numerics.BigInteger` and rewrite `BigDecimal` using the latter.  The only other libraries here are Murmur3, which will be needed fairly early in the process (hash codes for collections), and Printf, which won't be needed for a long time.  (For the record, I've already completed `BigDecimal`.)

The Collections, IFn, and Runtime categories are incredibly intertwined.   Much of Runtime needs a certain minimum of collections to exist.  The collections in turn use code out of the Runtime.  The support code for implementing `IFn`s needs some collections.  The collections need `IFn` support for things like `reduce` and `LazySeq`.   Decomposing and linearising this code is going to take serious analysis of dependencies.  

Once one has the right set of collections, support for `IFn`, and some of `RT`, one can implement most of the Reader code. This will also involve much of what I called the Library code:  The readers know symbols, keywords, namespaces, vars, ... . There are a few pieces that involve the compiler, such as evaluation of certain constructs.  We'll play some tricks to defer those references.

And then the compiler and remaining pieces of runtime support.

## A bad start?

I'm going to start off implementing the Collections/IFN/parts-of-Runtime with a fairly direct mapping of C# to F#.  That means interfaces, abstract classes, concrete classes, inheritance, a liberal sprinkling of `[<AllowNullLiteral>]`, etc. 

That means I am not starting with protocols.  I don't know how to do protocols yet.  I could spend a long time figuring this out and never get to the more critical part of this rewrite, the compiler improvements.  So I defer on protocols for now.   I am more than willing to do significant reworking of my early code if that is required.  But I have a suspicion that the final choice of implementation will not require significant rip-and-replace of the early code.  In the meantime, I will not where protocols are likely to come into play. 


## A summary


- Implement `BigDecimal` and `Murmur3` to warm up.
- Do a lot of back-and-forth between Collections, `IFn`-related and Runtime, in particular, teasing out the the minimum of the Runtime pieces to support the other two.
- Readers  and Library
- The compiler and the remaining runtime support.
- Throughout, contemplate protocols.  Eventually do something about it.









