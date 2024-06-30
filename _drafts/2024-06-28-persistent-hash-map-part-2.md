---
layout: post
title: `PersistentHashMap`, part 2 -- The root
date: 2024-06-28 00:00:00 -0500
categories: general
---

I'll sketch the code for the root of the `PersistentHashMap` structure in this post.  The focus is on the data structures used and the primary operations:  adding a key/value pair, find the value associated with a key, and removing a key.  We'll start with the Clojure interfaces that must be implemented. 

## Refresher

You can refer to the [first post]({{site.baseurl}}{% post_url 2024-06-28-persisent-hash-map-part-1 %}) in this series for the background on HAMTs.

## Interfaces

 To fit into the Clojure eco-system, we will need to implement the following interfaces.   We discussed these in detail in [This map is the territory]({{site.baseurl}}{% post_url 2023-01-11-this-map-is-the-territory %}).


 ```F#
[<AllowNullLiteral>]
type Seqable =
    abstract seq: unit -> ISeq

and [<AllowNullLiteral>] IPersistentCollection =
    inherit Seqable
    abstract count: unit -> int
    abstract cons: obj -> IPersistentCollection
    abstract empty: unit -> IPersistentCollection
    abstract equiv: obj -> bool

and [<AllowNullLiteral>] ISeq =
    inherit IPersistentCollection
    abstract first: unit -> obj
    abstract next: unit -> ISeq
    abstract more: unit -> ISeq
    abstract cons: obj -> ISeq

[<AllowNullLiteral>]
type ILookup =
    abstract valAt: key: obj -> obj
    abstract valAt: key: obj * notFound: obj -> obj

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

[<AllowNullLiteral>]
type Sequential =
    interface
    end

[<AllowNullLiteral>]
type Counted =
    abstract count: unit -> int

[<AllowNullLiteral>]
type IPersistentMap =
    inherit Associative
    inherit IEnumerable<IMapEntry> // do we really need this?
    inherit Counted
    abstract assoc: key: obj * value: obj -> IPersistentMap
    abstract assocEx: key: obj * value: obj -> IPersistentMap
    abstract without: key: obj -> IPersistentMap
    abstract cons: o: obj -> IPersistentMap
 ```

## At the root

The code below is fairly direct translation of the original C# code, a direct translation of the original Java code from Clojure(JVM).  There are ways in which the code might not be considered idiomatic F#. For example, the code uses `null` values as flags instead of using `option` types.  Deal with it.

Actually, after getting this code running, I re-implemented the code at least three times to explore other choices.  It was ... interesting.  Mostly, small variations in efficiency and readability.  I thought I'd just stick with the code that's been heavily tested in the wild.

There is one immediate consequence of using `null` as a flag:  the internal nodes that contain keys cannot handle `null` itself as a key.  So the root data node holds a flag and the value for the `null` key, if required.


```F#
 PersistentHashMap private (meta: IPersistentMap, 
                            count: int, 
                            root: INode, 
                            hasNull: bool, 
                            nullValue: obj) =
    inherit APersistentMap()
```

The `PersistentHashMap` class represents the map itelf.  It holds any associated metadata (needed for the `IObj` and `IMeta` interface implementations).  It holds the count of entries so we can implement `Counted` (for an efficient `count` method).
The `hasNull` flag indicates if `null` is present as a key in the map; the `nullValue` field holds the associated value.

The `root` field contains a pointer to the index node at the top of the tree.  There are three types of nodes in the tree:  `BitmapIndexedNode`, `ArrayNode`, and `HashCollisionNode`; each implements the `INode` interface.  In general, operations on the map are performed on the root and generally defer to the root node to do the heavy lifting -- those are the operations defined in `INode`.  

The base class, `APersistentHashMap` provides some standardized implementations for things that are common to most maps and that we will not concern ourselves with here.  For example, implementations of map equality, of `IDictionary`, etc.

We provide some convenient accessors for these fields:

```F#
    member internal _.Meta = meta
    member internal _.Count = count
    member internal _.Root = root
    member internal _.HasNull = hasNull
    member internal _.NullValue = nullValue
```

Note that `root` can be `null`; this will be the case if the map is empty or has only the `null` key as an entry.
Thus we can define our default 'empty' map via:

```F#
    static member Empty = PersistentHashMap(null, 0, null, false, null)
```

At this point, many interface operations are simple.  Either they can be done directly or we delegate them to the `root`.

```F#
    interface IMeta with
        override _.meta() = meta

    interface IObj with
        override this.withMeta(m) =
            if LanguagePrimitives.PhysicalEquality m meta then
                this
            else
                PersistentHashMap(m, count, root, hasNull, nullValue)
    
    interface Counted with
        override _.count() = count

    interface IPersistentCollection with
        override _.count() = count
        override _.empty() =
            (PersistentHashMap.Empty :> IObj).withMeta (meta) :?> IPersistentCollection     
            // empty passes the metadata along   
```

We start getting a bit more in the weeds as we get into detailed operations:

```F#
    interface ILookup with
        override this.valAt(k) = (this :> ILookup).valAt (k, null)

        override _.valAt(k, nf) =
            if isNull k then
                if hasNull then nullValue else nf
            elif isNull root then
                null
            else
                root.find (0, NodeOps.hash (k), k, nf)
```

You see in the second `valAt` our special handling of a possible `null` key and the `null` key value.  
If the `root` is null, there are no non-`null` entries are so nothing to find.  else we defer the lookup to the `root` node.
The `find` method takes the level in the tree (`0` at the start), the hash value for the key, the key, and the 'not found' value to return if the key is not in the map.

The `Associative` inteface is the more map-specific version of `ILookup`.  It deals with key presence and returning `IMapEntry` objects (key/value pairs) instead of just the values.  But the form is similar to the what we just saw.

```F#
    interface Associative with
        override _.containsKey(k) =
            if isNull k then
                hasNull
            else
                (not (isNull root))
                && root.find (0, NodeOps.hash (k), k, PersistentHashMap.notFoundValue)
                   <> PersistentHashMap.notFoundValue

        override _.entryAt(k) =
            if isNull k then
                if hasNull then
                    upcast MapEntry.create (null, nullValue)
                else
                    null
            elif isNull root then
                null
            else
                root.find (0, NodeOps.hash (k), k)
```

You will note a fairly standard trick:  we use a special value, `notFoundValue`, to indicate that a key was not found.  Thisis passed down to the nodes below.  If it comes back, no entry for the key was found.

Other than the lookup functionality defined above, the map operations of interest as `assoc`, adding a new key/value pair, and `without`, removing a key.  There are in the `IPersistentMap` interface.  Starting with `assoc`:


```F#
    interface IPersistentMap with
        override this.assoc(k, v) =
            if isNull k then
                if hasNull && LanguagePrimitives.PhysicalEquality v nullValue then
                    upcast this
                else
                    upcast PersistentHashMap(meta, (if hasNull then count else count + 1), root, true, v)

```
We being with special handling for a `null` key.  If present, and the value matches, there is no change: just return `this`.  Otherwise, we create new map with the `null` key/value pair.  The count will increase or not depending on whether the `null` key was already present. Continuing:

```F#
            else
                let addedLeaf = BoolBox()

                let rootToUse: INode = if isNull root then upcast BitmapIndexedNode.Empty else root

                let newRoot = rootToUse.assoc (0, NodeOps.hash (k), k, v, addedLeaf)

                if LanguagePrimitives.PhysicalEquality newRoot root then
                    upcast this
                else
                    upcast
                        PersistentHashMap(
                            meta,
                            (if addedLeaf.isSet then count + 1 else count),
                            newRoot,
                            hasNull,
                            nullValue
                        )
```

This is already a bit in the weeds.  We have to have a `root` to defer the operation to.  If there is a value for `root`, we use. Otherwise we create an empty node of the appropriate type (`BitmapIndexedNode` -- much more on this coming below).  We defer the `assoc` operation to the root node.  If the root node comes back unchanged, then the key/value pair was already present and the `assoc` operation was a no-op -- we can return `this`.  If the root node is different, then we have a new root node and we create a new map with that root node.  The count of entries in the map will increase if the key was not already present.  The special object `addedLeaf` is a sentinel that will be set if a new entry is added to the map.  The `BoolBox` type is a simple mutable boolean type.

```F#

type BoolBox(init) =

    let mutable value: bool = init

    new() = BoolBox(false)

    member _.set() = value <- true
    member _.reset() = value <- false
    member _.isSet = value
    member _.isNotSet = not value
```

(There are other ways to handle this.  I just used what the C#/Java versions used.)

The `assocEx` operation is an `assoc` that throws an exception if the key is present.

```F#
        override this.assocEx(k, v) =
            if (this :> Associative).containsKey (k) then
                raise <| InvalidOperationException("Key already present")

            (this :> IPersistentMap).assoc (k, v)

```

Finally, the `without` operation:

```F#
        override this.without(k) =
            if isNull k then
                if hasNull then
                    upcast PersistentHashMap(meta, count - 1, root, false, null)
                else
                    upcast this
            elif isNull root then
                upcast this
            else
                let newRoot = root.without (0, NodeOps.hash (k), k)

                if LanguagePrimitives.PhysicalEquality newRoot root then
                    upcast this
                else
                    upcast PersistentHashMap(meta, count - 1, newRoot, hasNull, nullValue)
```

There is the usual special case handling for the `null` key.  When we do the operation on the root, getting back the same root indicates the key was not present, so removing it was a no-op.  Otherwise, we have a new root and reduced count.

In the [next post]({{site.baseurl}}{% post_url 2024-06-28-persisent-hash-map-part-3 %}), we look at the structure of the `INode` interface and the three node types that implement it.
