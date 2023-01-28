---
layout: post
title: A road to maps
date: 2023-01-30 00:00:00 -0500
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
| PersistentVector    | 1259   | APersistentVector |    *     |      *      |             |          |   *   |        |
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

I included line counts here as a rough indicator of relative effort.  All these files have about the same bloat from blank lines, comments, and the like.  At first glance, you can discern minnows, panfish, and whales.

We have already written `LazySeq` but not discussed it.  It might be interesting to look at as an introduction to laziness.

`PersistentQueue` is just not very interesting.  It does not depend on `ASeq`, so it has to implement all the machinery that `ASeq` provides to the other sequential collections.  We'll not learn much doing that one.

`ArraySeq` has a lot of lines, but there is an incredible amount of repetition due to created specialized types for the primitive numeric types.  I can think of some tricks for reducing that. But here too we won't learn anything terribly useful.

Below these we see the sequential collections based on ASeq. I'll mostly just crank these out.  Not a lot to learn here.

Then we hit the monsters:  vectors, maps, and sets.  First, you should note the _A_'s: `APersistentVector`, `APersistentMap`, and `APersistentSet`.  Similar to `ASeq`, these are abstract base classes that provide common functionality for, well, vectors, maps, and sets.  Note that each is based on `AFn`, meaning they implement `IFn`, i.e., objects of types deriving from these are functions in Clojure.  You've seen maps, e.g., being used functions.

```Clojure
(my-map :a)
```

gets the value for the key `:a` in `my-map`.  The ability to do that comes from the `AFn` root.

Sets may look pleasantly small and thus perhaps a good place to start.  But those lines counts are misleading.  The sets rely on a map to provide their storage: `PersistentTreeSet` uses `PersistentTreeMap` and `PersistentHashSet` uses `PersistentHashMap`.  So they will come last.  By the time we get there, they will be a yawn-fest.

We cannot implement the maps without having `MapEntry`, and `MapEntry` is based on `AMapEntry` which is based on `APersistentVector`.    Why?  A `MapEntry` supports `IPersistentVector`; it is a two-element vector.  Thus our simple little key-value pair sits on top of 1400+ lines of infrastructure.

All this means there is no place to start other than `APersistentVector`.  It's not as complex as the line count might suggest.  It is similar to `ASeq` in providing basic functionality; think `count`.  It is inflated because it also provides utility classes for doing `ISeq` and reverse-order `ISeq` over a vector, as well as a specialized _subvector_ implementation that just provides a slice of another vector.  Inflates the line count, not much to learn.

`PersistentVector` _is_ as complex as its size suggests.  Along with `PersistentHashMap`, the implementation is based on Phil Bagwell's Hash Array Mapped Trie (HAMT) data structure, to which Rich Hickey added mechanisms for persistence (path copying instead of modifying structures in place) and, somewhat later, transience (a special mode for efficient batched add/remove/update operations).  If you get `PersistentVector`, `PersistentHashMap` does not add much in terms of conceptual difficulty, just more code.  This will take some heavy lifting.

## The roadmap

- Pick off the `ASeq`-derived sequential collections as time permits.  Refreshing breaks from the heavy toil of the big data structures.

- Hit `APersistentVector` and `PersistentVector`.  

- Go after `APersistentMap` and a few of the simper map structures.

- Port `PersistentHashMap`. We'll have learned most of what we need to know from `PersistentVector` so hopefully this will be less effort.

- Finish off the rest.

On the blogging front, I anticipate doing some write-ups on `IReduce`, `IChunkedSeq` and maybe a few other minor interfaces.  Seeing `IReduce` implemented once will hold you for all time.  It's just the same code over and over in the other data structures.

`PersistentVector` will take some explaining. We need to understand first how HAMTs are implemented.  Then we need to look at how we this structure immutabile and persistent. To which we then must add the additional complication of providing efficient operations via transience.  Lots to talk about.
