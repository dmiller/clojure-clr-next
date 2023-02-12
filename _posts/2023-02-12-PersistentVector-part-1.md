---
layout: post
title: Persistent vectors, Part 1 -- The landscape
date: 2023-02-12 12:00:00 -0500
categories: general
---

This is the first of a series of posts on the design and implementation of `PersistentVector`, an immutable, persistent vector class supporting a transient mode for efficient batch operations. 

To warm up, let's look at what we're aiming for.

## The goal

The set of interfaces to implement is a good place to start.  A little picture might help:

<img src="{{site.baseurl | prepend: site.url}}/assets/images/seq1.png" alt="IPersistentVector dependencies" />


`IPersistentVector` is our goal.
That means we will be implementing all of


- `Seqable`
- `IPersistentCollection`
- `ILookup`
-  `Associative`
-  `Sequential`
-  `IPersistentStack`  (*)
-  `Reversible` (*)
-  `Counted`
-  `Indexed`  (*)
-  `IPersistentVector`  (*)
- `IEditableCollection` (*)
- `IKVReduce` (*)
- `IMappedEnumerable` (*)
- `IMappedEnumerableTyped` (*)
- `IChunkedSeq`
- `IObj` (*)
- `IMeta` (*)
- `IFn`

Some of these we've seen before.  The starred ones we've not talked about.
The interfaces of greatest importance are mentioned in `IPersistentVector`:

```F#
[<AllowNullLiteral>]
type IPersistentVector =
    inherit Associative
    inherit Sequential
    inherit IPersistentStack
    inherit Reversible
    inherit Indexed
    abstract length: unit -> int
    abstract assocN: i: int * value: obj -> IPersistentVector
    abstract cons: o: obj -> IPersistentVector
    abstract count: unit -> int
```

The new ones here are

```F#
[<AllowNullLiteral>]
type Indexed =
    inherit Counted
    abstract nth: i: int -> obj
    abstract nth: i: int * notFound: obj -> obj
```

This interface provides indexing, i.e., access to vector elements by integer index.  `Counted` you've seen already.

`Reversible` just provides for sequencing in reverse order:

```F#
[<AllowNullLiteral>]
type Reversible =
    abstract rseq: unit -> ISeq
```

`IPersistentStack` allows the vector be treated like a stack, with the highest-indexed element being the top of the stack.  We can `peek` at the top element, or we can `pop` the stack.  `pop` does not return the element on the top of the stack.  Rather, it returns a new `PersistentVector` that is missing the top element of the original.   This is the nature of having an immutable structure -- we can't modify it to exclude the top element but must create a new object.

```F#
[<AllowNullLiteral>]
type IPersistentStack =
    inherit IPersistentCollection
    abstract peek: unit -> obj
    abstract pop: unit -> IPersistentStack
```

Wnat about pushing a new item onto the vector-as-stack?  That's the `cons` operation, part of `IPersistentVector`.

What we consider standard operations for a vector are accomplished thus:

- `v.nth(i)` -- access the i-th element
- `v.assocN(i,newValue)` -- assign a new value to the i-th element. (A new vector is returned.)
- `v.cons(o)`  -- create a vector with a new element added at the top end
- `v.pop()` -- create a new vector with the top element removed.



`IMeta` and `IObj` allow for metadata (an `IPersistentMap`) to be associated with an object and for a new object with new metadata to be created.  In face, `PersistentList` and some others also support it. I've just not wanted to take the time to talk about it.

```F#
[<AllowNullLiteral>]
type IMeta =
    abstract meta: unit -> IPersistentMap

[<AllowNullLiteral>]
type IObj =
    inherit IMeta
    abstract withMeta: meta: IPersistentMap -> IObj
```

I still don't want to take time to talk about.
 The implementations are pretty much all the same.
 I'll cover it at some point.

`IKVReduce` is a form of `IReduceinit` where the function take the previous value and a key and value (key here is the index).  Given our previous coverage of `IReduce(Init)`, there's nothing to learn here.

`IEditableCollection` relates to transient collections.  That will get its own post.

Enough of the general picture.  Time to dig in.