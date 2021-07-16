---
layout: post
title: The plan of attack
date: 2021-07-14 19:36:00 -0500
categories: general
---

# The plan of attack

One does not simply start writing a Clojure implementation.  One needs a plan.

Just for fun, I ran Visual Studio's code metrics on the C# projects in the current ClojureCLR (ignoring the .clj code that populates the runtime environment).  Breaking this into the application proper _versus_ tests:

| Category    		|   SLOC 	|   ELOC 	| Types 	| Members 	| Types (2)		| Members (2) 	|
|-------------		|-------:	|-------:	|------:	|--------:	|----------:	|------------:	|
| Application(1) 	| 76,223 	| 15,304 	|   859 	|   7,293 	|       501 	|       6,935 	|
| Tests       		| 17,761 	|  7,773 	|   119 	|   1,138 	|         - 	|           - 	|

Notes:

1.  Application here is Clojure.dll only, not including the very minor amount of code in Clojure.Main and similar.
2.  These additional columns with type and members counts subtract the types/members of clojure.lang.primifs, 358 interfaces with one method each.  They could be auto-generated and inflate the counts unnecessarily.  And I hope to get rid of them entirely.

Hmmm, a lot of blanks lines, brace-only lines, comments, etc.  And copyright notices. 

We can roughly group the code into the following areas:

| Area 			| #Types	| Description										|
|------			| -------:	| ---------											|
| Collections	| 152		| Persistent, immutable collections, sequences		|	
| Compiler		| 107		| The compiler itself								|
| IFn			| 54		| Support for IFn and related						|
| Library		| 50		| Clojure entities that are not collections, etc.	|
| Reader		| 53		| LispReader, EdnReader								|
| Runtime		| 62		| Runtime support functions							|
| Standalone	| 23		| BigDecimal, Printf, etc.							|

The distinctions between Library and Runtime are a bit blurry.  The Library category includes things such as symbols, keywords, vars, refs, atoms, and agents.  The Runtime category includes the `RT` and `Util` classes that provide code needed, well, at runtime.  RT in particular provides a lot of methods that implement clojure functions such as `first` and `reduce`.

Given all the dependencies, one needs to determine roughly the order in which things can be written.

The Standalones are a good place to start.  Not because they are needed immediately, but because they are simple and can be written and tested independently.  Given that at the onset I will have not have written an F# program more than a few lines long, I need to practice writing and structuring F# code and to develop friendly relations with testing libraries, BenchmarkDotNet, and other niceties/necessities.  I plan to ditch `BigInteger` for `System.Numerics.BigInteger` and rewrite `BigDecimal` using the latter.  The only other libraries hear are Murmur3, which will be needed fairly early in the process (hash codes for collections), and Printf, which won't be needed for a long time.  

Next is the IFn-related classes.  It is almost impossible to make progress on the collections without having this in place.  The class count is not indicative of the complexity -- 43 of the 54 classes in this category are trivial stubs.  The whole IFn approach is something I need to examine carefully -- is this something that protocols should be handling? -- but I plan to do a straight implementation to get things going and come back later to investigate other approaches.

The Collections must follow.  One cannot write the Lisp reader without being able to build the data structures.  One cannot write the compiler without being handed code to compile -- and in Lisp-land, code indeed is data.  These data structures bring complexity in several ways.  

1. Some of the data structures are just plain difficult to implement efficiently.  For example, 
One is that some of the data structures, for example, 'PersisentHashMap' is an implementation of Phil Bagwell's Hash Array Mapped Trie, which Rich Hickey adjusted to make persistent and immutable -- it is not easy code.

2. All the collections participate in the Clojure's _seq_ paradigm that provides uniform access to collections considered as sequences.  One must understand how that universe is structured.  For some of the data structures, seq-ifying is trivial.  For others, non-trivial support is needed.

3.  Other efficiency hacks add complexity.  This includes things like laziness, chunking, and editable collections.

Conceivably one could skip the third category of complexity at the beginning.  In fact, I may well deal with editable collections (an efficiency hack that safely suspends immutability while building a collection) at some later point, when I get to that point in the Clojure environment code.  

This area contains a zoo of interfaces and abstract classes.  (The interface count is in the high 20s.)  This is an area that needs to be looked at with respect to how interfaces might come into play.  However, to get things up quickly, I plan to just do a straightforward interface-based implementation.


The readers (Lisp and Edn) come next.  Once the data structures are in place, they can be done almost completely, except for LispReader callouts for evaluation, that can be added later.

Then the compiler and runtime.  It is very difficult to break this into pieces that can be written, tested, and debugged in a serial manner.  Parsing will be done completely ahead of time -- that's the LispReader.   One can stub up the classes representing the abstract syntax tree (AST) that is the first stage of analysis.  Then implement `eval` on that to begin testing before moving to code gen. When I brought up the existing compiler for the first time, I literally took `core.clj`, the primary file containing Clojure functions definitions that define the Clojure environment, commented it all out, and then uncommented it one line at time. I'm looking forward to more fun times.

And then it will be done.
 




