---
layout: post
title: A minimimal implement of sequences
date: 2021-08-30 19:36:00 -0500
categories: general
---

I wanted to begin testing certain functionality before I got to work on `ASeq`, `Cons`, and the others in the minimal implementation.  E.g., those classes might use `RT.count()`.  How do I test `RT.count()` without those classes?

<<<<<<< HEAD:_drafts/2021-07-15-a-minimal-implementation.md
I decided to write a minimum implementation of an `ISeq` for testing purposes.  Doing so forced me to sift through the `ASeq`/`Cons`/etc. to really see how this code fits together.  (Seriously, it's been a decade since I had to look at this code.  I don't remember any details.)
=======
So I wrote a minimum implementation of an `ISeq` for testing purposes.  Doing so forced me to sift through `ASeq`/`Cons`/etc. to really see how this code fits together.  (Seriously, it's been a decade since I had to look at this code.  I don't remember any details.)
>>>>>>> 747e5b1c1bc960de3024da5082b93ca96fd99d88:_drafts/2021-08-30-a-minimal-implementation.md

What is the minimum?  I went with a simple cons cell and a simple empty list (necessary because a cons cell is definitely not empty), implementing only the `ISeq`, `IPersistentCollection`, `Seqable` interfaces.  I also implemented a third collection type (a simple range sequence) just to see what the common code might be that could eventually be moved to an abstract base class or a utility module.  

To continue you need to have an idea of how sequences work in Clojure.  Start with [Sequences](https://clojure.org/reference/sequences). We are about to expose what lies underneath.

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

<<<<<<< HEAD:_drafts/2021-07-15-a-minimal-implementation.md
A `Seqable` is anything that can produce an `ISeq` to iterate over its contents.  For some collections, `seq()` might return the object itself: its class implements `ISeq`.  For some objects, calling `seq()` on it will produce a different object to handle the iteration.  Thus, something could be `Seqable` without itself being an `ISeq` or an `IPersistentCollection`.

The `IPersistentCollection` interface is straightforward.  Again, an object could be an `IPersistentCollection` without being an `ISeq`, so we cannot merge.

- `count()` returns a count of items in the collection -- for an `ISeq`, that would be the number of items in the sequence from that point on.  
- `empty()` yields an empty collection _of the appropriate type_.  (You'd have to read comments in the Clojure code to get this.) A hash map would return an empty hash map, for example.  For a `Cons`, say, which cannot itself be empty, `EmptyList` is used.
- `x.cons(o)` returns a new `ISeq` which has the item `o` first, followed by the items in 'x'.  It is up to each type to figure out how to implement this.  For example, `PersistentList` returns a new `PersistentList`.  `EmptyList` does also.  Other types might use a `Cons`.
- `equiv(o)` is used for equality checking on collections.  Each collection defines its own.   (Discussing `equiv` and equality in Clojure will require a separate post.)

The `ISeq` interface captures the essence of iteration across a sequence.  If you have `null` in your hand, that is an empty sequence. The Clojure code for `first`  `next`, `more`, and others special case this.  The difference between `next` and `more` is subtle.  The best place to learn a little more (no pun) is [here](https://clojure.org/reference/lazy) but you'll have to read carefully.  Originlly, Clojure had just `first` and `rest`, the equivalents to `car` and `cdr` in old Lisps. When laziness became central, a redesign was needed.  To decide what the next element is in a lazy sequence might require significant computation, computation that you might want to defer until absolutely necessary.  Thus, `rest` got split into two methods in `ISeq`, `next` and `more`.   `more` can defer determining if there is a next element.  From [Sequences](https://clojure.org/reference/sequences) we have:
=======
A `Seqable` is anything that can produce an `ISeq` to iterate over its elements.  For some collections, `seq()` might return the object itself: its type implements `ISeq`.  For some objects, calling `seq()` on it might produce a different object to handle the iteration.  Thus, something could be `Seqable` without itself being an `ISeq` or even an `IPersistentCollection` -- hence, we cannot merge these interfaces.

The `IPersistentCollection` interface is straightforward.  Again, an object could be an `IPersistentCollection` without being an `ISeq`, so we cannot merge.  However, it makes no sense for it to be a collection if we cannot access all of its elements in some way.  Hence an `IPersistentCollection` must be a `Seqable`.

- `count()` returns a count of items in the collection -- for an `ISeq`, that would be the number of items in the sequence from that point on.  
- `empty()` yields an empty collection _of the appropriate type_.  (You'd have to read comments in the Clojure code to get this.) A hash map would return an empty hash map, for example.  For a `Cons`, which cannot itself be empty, `EmptyList` is used.
- `x.cons(o)` returns a new `ISeq` which has the item `o` first, followed by the items in 'x'.  It is up to each type to figure out how to implement this.  For example, `PersistentList` returns a new `PersistentList`.  `EmptyList` does also.  Other types might use a `Cons`.
- `equiv(o)` is used for equality checking on collections.  Each collection defines its own.   `equiv` and equality in Clojure will require a separate post.

The `ISeq` interface captures the essence of iteration across a sequence.  If you have `null` in your hand, that is an empty sequence. The Clojure code for `first`  `next`, `more`, and others special case `null`.  The difference between `next` and `more` is subtle.  The best place to learn a little more (no pun) is [here](https://clojure.org/reference/lazy) but you'll have to read carefully.  Originlly, Clojure had just `first` and `rest`, the equivalents to `car` and `cdr` in old Lisps. When laziness became central, a redesign was needed.  To decide what the next element is in a lazy sequence might require significant computation, computation that you might want to defer until absolutely necessary.  Thus, `rest` got split into two methods in `ISeq`, `next` and `more`.   `more` can defer determining if there is a next element.  From [Sequences](https://clojure.org/reference/sequences) we have:
>>>>>>> 747e5b1c1bc960de3024da5082b93ca96fd99d88:_drafts/2021-08-30-a-minimal-implementation.md

> The Seq interface
> 
> ( _first_ coll)
>
<<<<<<< HEAD:_drafts/2021-07-15-a-minimal-implementation.md
>  Returns the first item in  he collections. Calls seq on its argument.  If coll is nil, returns nil.
=======
>  Returns the first item in the collections. Calls seq on its argument.  If coll is nil, returns nil.
>>>>>>> 747e5b1c1bc960de3024da5082b93ca96fd99d88:_drafts/2021-08-30-a-minimal-implementation.md
>
>  ( _rest_ coll)
>
>  Returns a sequence of the items after the first. Calls seq on its argument. If there are no more items, returns a logical sequence for which seq returns nil.

The doc string for the `next` function in Clojure states:

> Returns a seq of the items after the first. Calls seq on its argument.  If there are no more items, returns nil.

The `rest` function in Clojure is essentially `ISeq.more`.  Note that it does not return `nil`.  Thus, we need something to represent an empty list that is not `nil`.  A cons cell is never empty.  Thus, our minimum is a cons cell and 'a logical sequence for which seq returns nil' -- an `EmptyList`.

I knew I would create a few more dummy collections for testing in the future, so I decided to complicate matters just a bit from the start.  In particular, looking at the existing code, I could anticipate some base functionality that could be shared, functionality that could be defined first and not introduce circularity.  So I started with a `Util` module to contain these functions.  The contents are close approximations to what will end up in the `RT` and `Util` code.

Equality first.  Equality for collections is based on the sequence of values, so iteration is required.

```F#
   let checkEquals o1 o2 = obj.ReferenceEquals(o1,o2) || not (isNull o1) && o1.Equals(o2)
	
    let rec seqEquals (s1:ISeq) (s2:ISeq) =
        match s1, s2 with   
        | null, null -> true
        | null, _ -> false
        | _, null -> false
        | _ ->  checkEquals (s1.first()) (s2.first()) && seqEquals (s1.next()) (s2.next())
```

There is a notion of `equiv` that is separate from `Equals`, mostly in how numeric values are handled.  We ignore that here, but give a place for later elaboration.

```F#
    let seqEquiv s1 s2 = seqEquals s1 s2
```

While we are on basics, we need to make hash codes consistent with equality for sequences.

```F#
    let getHashCode (s:ISeq) = 
        let combine hc x = 31*hc + if isNull x then 0 else x.GetHashCode()
        let rec step (s:ISeq) hc = 
            if isNull s then hc 
            else step (s.next()) (combine hc (s.first()))
        step s 1
```

This walks the sequence and combines hash codes for each element.  Finally, we need a standard way to compute the count of a sequence.  (This is a slight simplification of the code in `RT`.)

```F#
    let seqCount (s:ISeq) = 
<<<<<<< HEAD:_drafts/2021-07-15-a-minimal-implementation.md
        let rec step (s:ISeq) cnt = if isNull s then cnt else step (s.next()) (cnt+1)
        step s 0
=======
        let rec step (s:ISeq) cnt = 
            if isNull s then cnt 
            else step (s.next()) (cnt+1)
>>>>>>> 747e5b1c1bc960de3024da5082b93ca96fd99d88:_drafts/2021-08-30-a-minimal-implementation.md
```

And now we can write our two collection classes.  These are mutually recursive and so are joined by `and` in the actual code.  Let's start with the simpler 'empty sequence'.

```F#
type SimpleEmptySeq() =

    override _.Equals(o) = 
        match o with
        | :? Seqable as s ->  s.seq() |> isNull
        | _ -> false

    override _.GetHashCode() = 1
       
    override _.ToString() = "()"
```

The basic overrides are simple.  Fixed hashcode value; `ToString` as required by Clojure.   For equality, we can only be equal to an empty sequence, which is something that returns `null` (= `nil` in Clojure) when `seq` is called on it.  That means we need something that `seq` can be called on: a `Seqable`.

You will note that a `SimpleEmptySeq` is not equal to `null`.   In Clojure:

```
user=> (class ())
clojure.lang.PersistentList+EmptyList
user=> (class nil)
nil
user=> (= () nil)
false
```

On to the sequence goodies.  The defining characteristic of an empty sequence object is that calling `seq` on it returns `null`.

```F#
    interface Seqable with
        member _.seq() = null
```

Next, an empty list is a very simple sort of collection.

```F#
    interface IPersistentCollection with
        member _.count() = 0
        member this.cons(o) = upcast (this:>ISeq).cons(o)
        member this.empty() = upcast this
        member this.equiv(o) = this.Equals(o)
```

The object is its own `empty`.  Obviously the `count` is zero.  `equiv` equates to `Equals` here.

For `cons`, the definition is a bit odd.  We have two different `cons`es floating around: `IPersistentCollection.cons` and `ISeq.cons`.  They have distinct return types. In C#, we have to deal with this using explicit interface declarations.  In F#, all interface definitions are explicit.   Because an `ISeq` is an `IPersistentCollection`, you will often see what have done here: implement the `IPersistentCollection` version in terms of the `ISeq` version, with an `upcast`.

Finally,

```F#
    interface ISeq with 
        member _.first() = null
        member _.next() = null
        member this.more() = upcast this
        member this.cons(o) = upcast SimpleCons(o,this)
```
The values for `first` and `next` are by definition.  We need to return an emtpy sequence--not `null`-- for `more`; the object itself is such a thing.  Finally, tith`cons` we see our entanglement with `SimpleCons`

And so on to `SimpleCons`:

```F#
<<<<<<< HEAD:_drafts/2021-07-15-a-minimal-implementation.md
type SimpleCons(head: obj, tail: ISeq ) =
    // I had to restrain myself from calling these car & cdr
=======
type SimpleCons(head: obj ,tail: ISeq) =
>>>>>>> 747e5b1c1bc960de3024da5082b93ca96fd99d88:_drafts/2021-08-30-a-minimal-implementation.md
 ```
 
 The main constructor is all we need, along with two fields to hold the values.  Like all the Clojure collections, these are immutable.  Doing the `Object` overrides, we can defer mostly to our utility functions:
 
 ```F#
     override this.Equals(o) = 
        match o with
        | :? Seqable as s -> Util.seqEquals (this:>ISeq) (s.seq())
        | _ -> false

    override this.GetHashCode() = Util.getHashCode this
```

We will compare affirmatively only to other sequences, and they must have equal elements in order.

```F#
    interface Seqable with
        member this.seq() = upcast this
```

As will be the case with many collections, the object can be its own `seq`.  (One demarcation in the code is between collections that inherit from `ASeq` -- they do implement `seq()` like this-- and other collections such as maps and vectors that use auxiliary types to represent their sequences.

```F#
    interface IPersistentCollection with
        member _.count() = 1 + Util.seqCount tail
        member this.cons(o) = upcast (this:>ISeq).cons(o)
        member _.empty() = upcast SimpleEmptySeq() 
        member this.equiv(o) =
            match o with
            | :? Seqable as s -> Util.seqEquiv (this:>ISeq) (s.seq())
            | _ -> false
```

And here we get the reverse entanglement with `SimpleEmptySeq`.  Finally,

```F#
 
    interface ISeq with
        member _.first() = head
        member this.next() = (this:>ISeq).more().seq()
        member _.more() =  if isNull tail then upcast SimpleEmptySeq() else tail 
        member this.cons(o) = upcast SimpleCons(o,this)
```

The reason I went with a cons cell to begin with is that it is just what we need to implement `cons`.  The dance here between `more` and `next` is common.
`more` cannot return `null`, so it cannot just return the `tail`.  If the `tail` is `null`, we must return 'a logical sequence for which seq returns nil': our `SimpleEmptySeq`.
<<<<<<< HEAD:_drafts/2021-07-15-a-minimal-implementation.md
That test for emptiness here is very specific to `SimpleCons`: a `null` `tail`.  `next` is often defined in terms of `more`.  If `more` returns an empty sequence, the `seq` on it will return `null`, which is what `next`'s contract says.
=======
The test for emptiness here is very specific to `SimpleCons`: a `null` `tail`.  `next` is often defined in terms of `more`.  If `more` returns an empty sequence  (guaranteed this is not `null`), the `seq` on it will return `null`, which is what `next`'s contract says.
>>>>>>> 747e5b1c1bc960de3024da5082b93ca96fd99d88:_drafts/2021-08-30-a-minimal-implementation.md

And we are done.