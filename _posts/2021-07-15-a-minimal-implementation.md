---
layout: post
title: For your Cons-ideration
date: 2021-07-14 19:36:00 -0500
categories: general
---

I had been working on the mistaken assumption that I was going to need a concrete implementation of IPersistentMap andthus of IFn early in the implementation process.  In fact, by deferring testing of the metadata and reduce functionality until later, we can follow the plan laid out.

However, I did some experiments to see how things were going to go.  Keep in mind that I'm still learning F# so it is helpful to have some simpler problems to work on.  Also, I haven't had to look at the details of collections code for a decade -- I don't remember how all the pieces fit together.

So I ended up working on `AFn`, the abstract class that provides base for many `IFn` implementations.  And I needed to test it.  And that requires having at least one concrete implementation of `ISeq`.  What is the minimum?  I went with a simple cons cell and a simple empty list (necessary because a cons cell is definitely not empty), implementing only the `ISeq` + `IPersistentCollection` + `Seqable` interfaces.  I also implemented a third collection type (a simple range sequence) just to see what the common code might be that could eventually be moved to an abstract base class or a utility module.  (Which is exactly the case in the current code -- such things reside in places such as `RT`.) 

It would be very helpful to have an idea of how sequences work in Clojure.  We are about to expose what lies underneath.

Let's start with the interfaces.

```F#
type [<AllowNullLiteral>] Seqable =
    abstract seq : unit -> ISeq

and [<AllowNullLiteral>] IPersistentCollection = 
    inherit Seqable
    abstract count : unit -> int
    abstract cons: obj -> IPersistentCollection
    abstract empty: unit -> IPersistentCollection
    abstract equiv: obj -> bool

and [<AllowNullLiteral>] ISeq =
    inherit IPersistentCollection
    abstract first : unit -> obj
    abstract next : unit -> ISeq
    abstract more : unit -> ISeq
    abstract cons : obj -> ISeq
```

A `Seqable` is anything that can produce an `ISeq` to iterate over its contents.  For some collections, the object itself might implement `ISeq`.  For some objects, calling `seq()` on it might produce a different object to handle the iteration.

The `IPersistentCollection` interface is straightforward.  

- `count()` returns a count of items in the collection -- for an `ISeq`, that would be the number of items in the sequence from that point on.  
- `empty()` yields an empty collection _of the appropriate type_.  (You'd have to read comments in the Clojure code to get this.) A hash map would return an empty hash map, for example.  For a `Cons`, say, which cannot itself be empty, `EmptyList` is used.
- `x.cons(o)` returns a new `ISeq` which has the item `o` first, followed by the items in 'x'.  It is up to each type to figure out how to implement this.  For example, `PersistentList` returns a new `PersistentList`.  `EmptyList` does also.  Other types might use a `Cons'.
- `equiv(o)` is used for equality checking on collections.  Each collection defines its own.  This deserves its own post.

The `ISeq` interface captures the essence of iteration across a sequence.  If you have `null` in your hand, that is an empty sequence. The Clojure code for `first`  `next`, `more`, others special case this.  The difference between `next` and `more` is subtle.  The best place to learn a little more is [here](https://clojure.org/reference/lazy) but you'll have to read carefully.  




