---
layout: post
title: A road to maps
date: 2023-02-03 00:00:00 -0500
categories: general
---

I'd like to draw a roadmap for how to approach implementing the remaining collections.

Let's start with an analysis of features and dependencies.  Big picture first, then some explanation.


| __Class__           | #lines | Base              | IReduce* | ITransient* | IChunkedSeq | IPending | IDrop | Sorted |
|:--------------------|-------:|:------------------|:--------:|:-----------:|:-----------:|:--------:|:-----:|:------:|
| LazySeq             |  357   |                   |          |             |             |    *     |       |        |
| PersistentQueue     |  461   |                   |          |             |             |          |       |        |
| ArraySeq            |  680   | ASeq              |    *     |             |             |          |       |        |
| ChunkedCons         |  112   | ASeq              |          |             |     *       |          |       |        |
| Cycle               |  142   | ASeq              |    *     |             |             |    *     |       |        |
| EnumeratorSeq       |  153   | ASeq              |          |             |             |          |       |        |
| Iterate             |  135   | ASeq              |    *     |             |             |    *     |       |        |
| LongRange           |  360   | ASeq              |    *     |             |     *       |          |   *   |        |
| Range               |  336   | ASeq              |    *     |             |     *       |          |       |        |
| Repeat              |  174   | ASeq              |    *     |             |             |          |   *   |        |
| StringSeq           |  178   | ASeq              |    *     |             |             |          |   *   |        |
| APersistentVector   | 1164   | AFn               |          |             |             |          |       |        |
| PersistentVector    | 1259   | APersistentVector |    *     |      *      |     *       |          |   *   |        |
| AMapEntry           |  284   | APersistentVector |          |             |             |          |       |        |
| MapEntry            |   87   | AMapEntry         |          |             |             |          |       |        |
| APersistentMap      |  718   | AFn               |          |             |             |          |       |        |
| PersistentStructMap |  571   | APersistentMap    |          |             |             |          |       |        |
| ATransientMap       |  152   | AFn               |          |      *      |             |          |       |        |
| PersistentArrayMap  |  870   | APersistentMap    |    *     |             |             |          |   *   |        |
| PersistentHashMap   | 1868   | APersistentMap    |    *     |      *      |             |          |       |        |
| PersistentTreeMap   | 1276   | APersistentMap    |    *     |             |             |          |       |   *    |
| APersistentSet      |  317   | AFn               |          |             |             |          |       |        |
| PersistentTreeSet   |  243   | APersistentSet    |          |      *      |             |          |       |   *    |
| ATransientSet       |   96   | AFn               |          |             |             |          |       |        |
| PersistentHashSet   |  234   | APersistentSet    |          |      *      |             |          |       |        |
|                     | 12,227

I included line counts here as a rough indicator of relative effort.  All these files have about the same bloat from blank lines, comments, and the like.  

We have already written `LazySeq` but not discussed it.

`PersistentQueue` is just not very interesting.  It does not depend on `ASeq`, so it has to implement all the machinery that `ASeq` provides to the other sequential collections.  We'll not learn much doing that one.

`ArraySeq` has a lot of lines, but there is an incredible amount of repetition due to created specialized types for the primitive numeric types.  I can think of some tricks for reducing that. But here too we won't learn anything terribly useful.

Below these we see the sequential collections based on `ASeq`. I'll feature a few of these in disucssions of laziness, chunking and reduction.

Then we hit the monsters:  vectors, maps, and sets.  First, you should note the _A_'s: `APersistentVector`, `APersistentMap`, and `APersistentSet`.  Similar to `ASeq`, these are abstract base classes that provide common functionality for, well, vectors, maps, and sets.  Note that each is based on `AFn`, meaning they implement `IFn`, i.e., objects of types deriving from these are functions in Clojure.  You've seen maps, e.g., being used functions.

```Clojure
(my-map :a)
```

gets the value for the key `:a` in `my-map`.  The ability to do that comes from the `AFn` root.

Sets may look pleasantly small and thus perhaps a good place to start, but those lines counts are misleading.  Each type sets relies on a map type to provide its storage: `PersistentTreeSet` uses `PersistentTreeMap` and `PersistentHashSet` uses `PersistentHashMap`.  So they will come last.  By the time we get there, they will be a yawn-fest.

We cannot implement the maps without having `MapEntry`, and `MapEntry` is based on `AMapEntry` which is based on `APersistentVector`.    Why?  A `MapEntry` supports `IPersistentVector`; it is a two-element vector.  Thus our simple little key-value pair sits on top of 1400+ lines of infrastructure.  (Without the bloat, it's about 560 lines of infrastrcture in my F# implementation.)

All this means there is no place to start other than `APersistentVector`.  It's not as complex as the line count might suggest.  It is similar to `ASeq` in providing basic functionality; think `count`.  It is inflated because it also provides utility classes for doing `ISeq` and reverse-order `ISeq` over a vector, as well as a specialized _subvector_ implementation that just provides a slice of another vector.  Inflates the line count, not much to learn.

`PersistentVector` and `PersistentHashMap` are complex.   The implementation of `PersistentHashMap` is based on Phil Bagwell's Hash Array Mapped Trie (HAMT) data structure, to which Rich Hickey added mechanisms for persistence (path copying instead of modifying structures in place) and, somewhat later, transience (a special mode for efficient batched add/remove/update operations).  `PersistentVector` is a simplification of those ideas to support vectors.  So we have two reasons for starting with `PersistentVector`: relatively simplicity and the dependency of `MapEntry` on `PersistentVector`.   Our work understanding `PersistentVector` will help us make the leap to `PersistentHashMap`,

## The roadmap

I decided to just jump into the deep end of the pool and start work on `APersistentVector`, `AMapEntry`, `MapEntry`, and `PersistentVector`.  In part I wanted to see if I had missed any connections to other parts that would complicate the implementation.  And it was looking good.  As I turned my attention to writing tests, I saw a need for long sequences to initialize vectors.   _Long_ here means of sufficient size to get the trees used in the vectors to be of depth at least 2, meaning 1000+ elements.  There are lots of ways I could have written generators for such things, but it occurred to me that the `LongRange` class would be great for that.  A relatively small amount of code and I have to do it eventually.  Well, as is turns out `LongRange` needs `Range` (when the iteration count is larger than an `int`) and `Repeat` (when the specified step is zero and the start and end values are different, you just repeat the start value forever).  Both ranges need some support for chunking, so that brings in `ArrayChunk` and some other friends.  And thus I found myself back in the land of `ASeq`.

Having worked backwards from the desired to the necessary, we can write it up in the other direction.



- Take a look at laziness and chunking.  I had thought to delay that for a while--my own laziness--but in all the collections, it happens that `Range`, `LongRange`, and `PersistentVector` are the only places it comes up, so we might as well get it donw now.  (There's also `ChunkedCons`, which we'll cover, but I consider that more of the support machinery for chunking rather than a standalone collection of importance.)

- Take a look at how to write code for `IReduce`.  We're going to be doing this over and over.  There is a pattern that works for most implementations.  With that pattern in hand, implementing reduction just takes a little work to adapt to the collection under consideration.

- Do a deep dive on `PersistentVector`.  This will come in several parts.
    - A discussion of persistence and immutability and how they can be implemented efficiently, together with how tree navigation is done via bit-level operations (masking, shifting).
    - A discussion of transience, its impact on performance and its implementation.
    - As a bonus, depending on how things go, I may talk about an alternative coding for PersistentVector that is more in the style of F#. (I haven't started working on that code yet, but I have some ideas.)

- After that, I'll probably go after `PersistentHashMap`. 