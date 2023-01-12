---
layout: post
title: This map is the territory
date: 2023-01-11 00:00:00 -0500
categories: general
---

Maps are hugely important in Clojure programming.   Internally, they are supported by a specific group of interfaces.  Here we will examine these interfaces and provide an incredibly naive implementation.  The intention is to make clear the mechanics of these interfaces in a simple setting.  Later, when we implement realistic maps, we can wave at this stuff in passing and focus on the intricacies of the data structures themselves.


## The interfaces

The most fundamental operation on a map is the ability to look up a value from a key.  It is sometimes helpful to be able to provide a value to use instead if the key is not present (rather than throwing an exception).

```F#
[<AllowNullLiteral>]
type ILookup =
    abstract valAt: key: obj -> obj
    abstract valAt: key: obj * notFound: obj -> obj
```

There are data structures floating around in Clojure that allow lookups in this way without the other map operations that follow, hence proving this interface as a standalone.

The `Associative` interface takes us further:  An `IPersistentCollection` that not only has key lookup but allows the addition of new assocations.  In the world of immutability that Clojure inhabits, calling the `assoc` method does not modify the object; rather, we create a new object that has the new key/value pairing in it.  (Doing that efficiently is the complication in many of Clojure's data structures.)


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

I am not sure why `containsKey` and `entryAt` could not be in `ILookup`, but I will assume there was a good reason for keeping `ILookup` slim.  Note that we introduce `IMapEntry` here to provide a simple interface for a key/value pair.

Finally, our fully-featured map will allow the removal of keys from the map, the `without` method.  Immutability again here, so `without` returns a new object with the specified key removed.

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

You will note duplication of some methods: `cons`, and  `assoc`.  The reason is that in the context of an `IPersistentMap` these can now have more explicit return types.  You start with an `IPersistentMap`, you will keep getting back `IPersistentMap`s.  This makes chaining of operations much nicer, with no need to downcast an `Associative` return from `Associative.assoc` to an `IPersistentMap` so we can follow with a `without` operation:

```F#
m.assoc("a",12).without("b")
```

versus

```F#
(m.assoc("a",12) :> IPersistentMap).without("b")
```

The `cons` operation is interesting.  It takes any old object, but will object if that object does not look in some way like a key/value pair.  We'll deal with what looks like a key/value pair later.

## A very naive implementation

Really very naive.  Embarassingly so.  But I don't want to focus on implementing a decent map.  I want to focus on how the pieces fit together, how the interface implementations are structured.

We'll start with the simplest piece of the puzzle. We need an implementation of `IMapEntry`.

```F#
type SimpleMapEntry =
    {Key: obj 
     Value: obj} 
    
    interface IMapEntry with
        member this.key() = this.Key
        member this.value() = this.Value
```

I decided to use an F# record for this.  This suffices. 

Now, for the embarrassingly simple decision: Our map will just contain an F# list of SimpleMapEntry items.  We do linear searches.  We construct new maps by adding to the list, creating a new list with an entry removed, etc.   Away we go.

```F#
[<AllowNullLiteral>]
type SimpleMap(kvs : IMapEntry list) =
```

That defines our constructor.  All you need is a list of key/value pairs in the form of an `IMapEntry`.

We will need an empty map here or there.  Because we are immutable, we can just create one and use it everywhere.

```F#     
    static member EmptyMap = SimpleMap(List.Empty)
```

It will be handy to have a utility method to compare a map to any other object for equality:

```F#
    static member mapCompare(m1: IPersistentMap, o: obj) : bool =
        if obj.ReferenceEquals(m1, o) then
            true
        else
            match o with
            | :? IPersistentMap as m2 ->
                if m1.count () <> m2.count () then
                    false
                else
                    let rec step (s: ISeq) =
                        match s with
                        | null -> true
                        | _ ->
                            let me: IMapEntry = downcast s.first ()

                            if m2.containsKey (me.key ()) && m2.valAt(me.key ()).Equals(me.value ()) then
                                step (s.next ())
                            else
                                false

                    step (m1.seq ())
            | _ -> false

```

We will only compare affirmatively with other `IPersistentMap` objects.
If the other map has the same count and all of our keys appear there with the same value associated, we are good.  We can iterate through our key/value pairs by `seq`ing and getting a sequence of `IMapEntry` objects to check for in the other map.
 
Let's move on to the Clojure interfaces, starting first with the sequence triumvirate.  Well, only a duo here, as a map is not a sequence itself; it does not implement `ISeq`.  This is in contrast to other examples to date.  

However, we do implement `Seqable`, so we will need another class to serve as a sequence-on-a-map.  We call it `SimpleMapSeq`. We'll cover its details later.

```F#
    interface Seqable with
        member _.seq() = upcast SimpleMapSeq(kvs)
```

`IPersistentCollection` is straightforward.

```F#
    interface IPersistentCollection with
        member this.count() = (this :> IPersistentMap).count ()

        member this.cons(o) =
            (this :> IPersistentMap).cons (o)

        member _.empty() = SimpleMap.EmptyMap
        member this.equiv(o) = SimpleMap.mapCompare (this, o)
```

We have multiple `count` methods, from `IPersistentCollection`, `Counted`, and `IPersistentMap`.  We will write directly for `IPersistentMap` and let the others
delegate to it.  Similarly for `cons`.

And now to the map-centric interfaces.  We are going to have multiple occasions where we need to determine if an entry for some key is present in our `kvs` list.  So let's create a little static method for that purpose.

```F#
    static member isEntryForKey key (me : IMapEntry) = me.key() = key
```

The flavors of 'find value from key' are straightforward.

```F#
    interface ILookup with
        member _.valAt(key) =
            match List.tryFind (SimpleMap.isEntryforKey key) kvs with
            | Some me -> me.value()
            | None -> null

        member _.valAt(key, notFound) =
            match List.tryFind (SimpleMap.isEntryforKey key) kvs with
            | Some me -> me.value()
            | None -> notFound

    interface Associative with
        member this.containsKey(key) = 
            (List.tryFind (SimpleMap.isEntryforKey key) kvs).IsSome 

        member this.entryAt(key) =
            match List.tryFind (SimpleMap.isEntryforKey key) kvs with
            | Some me -> me
            | None -> null

        member this.assoc(k, v) =
            upcast (this :> IPersistentMap).assoc (k, v)

```

All the lookups are variations on the theme.  Try to find something, return the appropriate thing (`IMapEntry`, the value, `null`, a default value), depending.

The heart is `IPersistentMap`, where we must deal with `assoc` and `without`.

```F#
    interface IPersistentMap with
        member this.assoc(k, v) =
            if (this :> Associative).containsKey k then
                (this :> IPersistentMap).without(k).assoc (k, v) 
            else
                SimpleMap({Key = k; Value = v} :: kvs) :> IPersistentMap
```

The idea is simple.  If you want to add a new key/value pair, just create a new map with that key/value pair at the front of the list.   You could leave the old pair in there and not mess up things like `valAt` because they do linear search from the front.  But `count` would be complicated, iteration through the sequence would be complicated.
So we check first if the key is already present and remove it if it is, then add our new pair on the front.

```F#

        member this.assocEx(k, v) =
            if (this :> Associative).containsKey (k) then
                raise <| InvalidOperationException("Key already present.")
            else
                (this :> IPersistentMap).assoc (k, v)
```

`assocEx` is simiar except it throws an exception if the key is already present.
Note this is a little inefficent because after we check `containsKey` we will call `assoc` which will check it again.   In real life, one might try to code around this duplication.

```F#
        member this.without(key) =
            match List.tryFindIndex (fun (me:IMapEntry) -> me.key() = key) kvs with
            | Some idx ->
                let kvsHead, kvsTail = List.splitAt idx kvs
                SimpleMap(kvsHead @ kvsTail.Tail)
            | None -> this 
```

For `without`, if the key is absent, then this map is already a map with the key removed.  Otherwise, we create a new map by cutting the list where the key/value pair is, and resplicing it without that entry.  Just a little list surgery.

```F#
        member this.cons(o) =
            match o with
            | :? IMapEntry as me -> (this :> IPersistentMap).assoc (me.key (), me.value ())
            | _ -> raise <| InvalidOperationException("Can only cons an IMapEntry to this map")
```

We only allow `cons`ing an `IMapEntry`.  In real-life Clojure, you can do this plus other things that look like a pair, such as a two-element vector.

Finally,

```F#
        member _.count() = kvs.Length
```

which hopefully needs no explanation. Same for:

```F#
    interface Counted with
        member _.count() = kvs.Length
```

One of our promises for `IPersistentMap` is that we support `IEnumerable` and `IEnumerable<IMapEntry>`  This is true in Clojure for many of its collections.

```F#
    interface IEnumerable<IMapEntry> with
        member _.GetEnumerator() : IEnumerator<IMapEntry> =
            (seq {
                for i = 0 to kvs.Length - 1 do
                    yield kvs.Item(i)
            })
                .GetEnumerator()

    interface IEnumerable with
        member this.GetEnumerator() : IEnumerator =
            upcast (this :> IEnumerable<IMapEntry>).GetEnumerator()
```

This is mostly F# insider baseball -- using `seq` with `yield` and `.GetEnumerator` to implement `IEnumerable`.  The iteration itself is a simple iteration through `kvs`.  Not efficiently, it should be noted.  This is O(n^2).  You get what you paid for.  Exercise: improve this.

Finally, our class that implements `ISeq`.  It takes the key/value list and iterates through it.
It needs to create a new version of itself on `next`, which will be based on `kvs.Tail`.

```F#
and SimpleMapSeq(kvs : IMapEntry list) =

    interface Seqable with
        member this.seq() = upcast this

    interface IPersistentCollection with
        member _.count() = List.length kvs
        member this.cons(o) = upcast SimpleCons(o, this)
        member _.empty() = upcast SimpleEmptySeq()

        member this.equiv(o) =
            match o with
            | :? Seqable as s -> Util.seqEquiv this (s.seq ())
            | _ -> false

    interface ISeq with
        member _.first() = kvs.Head

        member _.next() =
            if kvs.Length <= 1 then
                null
            else
                upcast SimpleMapSeq(kvs.Tail)

        member this.more() =
            match (this :> ISeq).next () with
            | null -> upcast SimpleEmptySeq()
            | s -> s

        member this.cons(o) = upcast SimpleCons(o, this)
```

At this point, given your prior experience, this code should be straightforward.

And we are done.

Code in [this repo](https://github.com/dmiller/clojure-clr-next/tree/main/src/Pages/SimpleCollections).






