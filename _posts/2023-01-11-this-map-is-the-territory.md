---
layout: post
title: This map is the territory
date: 2023-01-11 00:00:00 -0500
categories: general
---

Maps are a significant category of data structures in Clojure.  Internally, they are supported by a slew of interfaces.  Here we will examine these interfaces and provide an incredibly naive implementation.


## The interfaces

The most fundamental operation on a map is the ability to look up a value from a key.  It is sometimes helpful to be able to provide a value to use instead if the key is not present (rather than throwing an exception).

```F#
[<AllowNullLiteral>]
type ILookup =
    abstract valAt: key: obj -> obj
    abstract valAt: key: obj * notFound: obj -> obj
```

There are data structures floating around in Clojure that allow lookups in this way without the other map operations that follow, hence the split.

The `Associative` interface takes us further:  An `IPersistentCollection` that not only has key lookup but allows new assocations.  In the world of immutability that Clojure inhabits, calling the `assoc` method does not modify the object; rather, we create a new object that has the new key/value pairing in it.  (Doing that efficiently is the complication in many of Clojure's data structures.)


```F#
[<AllowNullLiteral>]
type IMapEntry =
    abstract key: unit -> obj
    abstract value: unit -> obj

[<AllowNullLiteral>]
type Associative =
    inherit IPersistentCollection
    inherit ILookup
    abstract containsKey: key: obj -> bool
    abstract entryAt: key: obj -> IMapEntry
    abstract assoc: key: obj * value: obj -> Associative
```

I am not sure why `containsKey` and `entryAt` could not be in `ILookup`, but I will assume there was a good reason for keeping `ILookup` slim.

Finally, our fully-featured map will allow the removal of keys from the map, the `without` method.  Immutability again here, so `without` returns a new object with the key removed.

```F#
[<AllowNullLiteral>]
type Counted =
    abstract count: unit -> int

[<AllowNullLiteral>]
type IPersistentMap =
    inherit Associative
    inherit IEnumerable<IMapEntry>
    inherit Counted
    abstract assoc: key: obj * value: obj -> IPersistentMap
    abstract assocEx: key: obj * value: obj -> IPersistentMap
    abstract without: key: obj -> IPersistentMap
    abstract cons: o: obj -> IPersistentMap
    abstract count: unit -> int
```

The `Counted` interface is added here.  We have seen a `count` method before, in `IPersistentCollection`.  Why another one here?  The problem for `count` generally is that some collections may take a _long_ time to compute the count.  A list constructed from a sequence of `SimpleCons` cells will have to traverse the entire list to know what the count is.  (And potentially infinite collections will never return from `count`.)   A data structure that implements `Counted` is signalling that it can compute `count` efficiently, where 'efficiently' is usually taken to mean constant time, not in time dependent on the size of the collection.

You will note duplication of some methods: `cons`, `without`, `assoc`.  The reason is that in the context of an `IPersistentMap` these can now have more explicit return types.  You start with an `IPersistentMap`, you will keep getting back `IPersistentMap`s.  This makes chaining of operations much nicer, with no need to downcast an `Associative` return from `Associative.assoc` to an `IPersistentMap` so we can follow with a `without` operation:

```F#
m.assoc("a",12).without("b")
```

versus

```F#
(m.assoc("a",12) :> IPersistentMap).without("b")
```

The `cons` operation is interesting.  It take anything that looks like a _pair_ and treats them as a pair of (key,value) to `assoc` in.  We'll deal with what looks like 'pair' later.

## A very naive implementation

Really very naive.  Embarassingly so.  But I don't want to focus on implementing a decent map.  I want to focus on how the pieces fit together, how the interface implentations are structured.





