---
layout: post
title: PersistentHashMap, part 4 -- Other matters
date: 2024-07-02 00:00:00 -0500
categories: general
---

We continue our discussion of `PersistentHashMap` with a discussion of transiency and alternative F# coding techniques.


The previous posts in this series are:

- [Part 1: Making a hash of things (this post)]({{site.baseurl}}{% post_url 2024-07-02-persistent-hash-map-part-1 %})
- [Part 2: The root]({{site.baseurl}}{% post_url 2024-07-02-persistent-hash-map-part-2 %})
- [Part 3: The guts]({{site.baseurl}}{% post_url 2024-07-02-persistent-hash-map-part-3 %})

# Transiency

I refer you to the [discussion]({{site.baseurl}}{% post_url 2023-02-18-PersistentVector-part-3 %}) of transiency in the `PersistentVector` series.

As for `PersistentVector`, `PersistentHashMap` supports interfaces such as `IEditableCollection`, `ITransientCollection`, `ITransientAssociative`.  There is one additional, map-specific transiency inteface:

```F#
[<AllowNullLiteral>]
type ITransientMap =
    inherit ITransientAssociative
    inherit Counted
    abstract assoc: key: obj * value: obj -> ITransientMap
    abstract without: key: obj -> ITransientMap
    abstract persistent: unit -> IPersistentMap
```

. The same mechanisms are used to implement transiency in `PersistentHashMap` as in `PersistentVector`.  I leave the implementation to you as an exercise.


# Alternative coding techniques

The code described in these posts is a direct translation of the C# code, itself directly translated from the Java code in Clojure(JVM).

I decided to experiment with idiomatic F# techiques, with an eye toward code clarity and seeing the impact on performance. 

## Options

We have special handling of of `null` as a key because `null` is used as a signal in `BitmapIndexedNode` to discriminate between an key-value entry and a node entry.  Also, empty slots in an `ArrayNode` are signaled by `null`.  We could use `Option` types to handle these cases.  This means also that `INode` and its implementating classes no longer need to be `AllowNullLiteral`.  We do manage to get rid of the special handling of `null` at the top, in `PersistentHashMap` itself.  Some `if` expressions can be replace by `match`, etc.  But there is not much gain.


## Discriminated unions

We could use discriminated unions to represent the different types of nodes. I did this before I worked on transiency.  It was ... okay.  Not a big help.  The big problem comes with transiency:  as implemented it requires some mutable fields (counts, bitmaps) and discriminated unions cannot have mutable fields.  I went so far as to put `ref` fields in the DUs -- you can do that -- but the resulting code was _really_ not pretty.  

One place I did try discriminated unions was in the `BitmapIndexedNode`.  The existing implementation uses an array with even entries being `null` or a (non-`null`) key and the odd entries being correspondingly the node the next level down or the key's value.  I replaced this with a DU with two cases, one for a key-value pair and one for a node.    The array is now half the size, with entries of type

```F#
BNodeEntry =
    | BKey of Key:obj
    | BSubNode of Node:INode
```
In one version I had a separate array to contain the key values.
I also tried

```F#
    | BKey of Key:obj * Value:obj
    | BSubNode of Node:INode
```

This works fine until you try to implement transiency.  Here the problem is that transiency ends up creating a few more entries in the array than currently required, to some of the entries are empty.  You either end up with a `BNodeEntry option array` -- the pattern matching gets just nasty with this one -- or you end up with 

```F#
BNodeEntry =
    | KeyValue of Key: obj * Value: obj
    | Node of Node: INode2
    | EmptyEntry
```

I ran out of ideas.  Sometimes the code was a little easier to look at, but generally the overall complexity of the algorithms in things like `BitmapIndexedNode.assoc` overwhelmed any gains.  Nothing seemed to improve performance.

Given that each variation was causing me to rework hundreds of lines of code, I also ran out of energy.  I decided to stick with the original implementation.


# Onward

We conclude this series of posts with a [look at performance]({{site.baseurl}}{% post_url 2024-07-02-persistent-hash-map-part-5 %}) and a comparison of the F# and Clojure versions of `PersistentHashMap`.