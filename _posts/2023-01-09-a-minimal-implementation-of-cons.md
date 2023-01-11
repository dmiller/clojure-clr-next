---
layout: post
title: A minimal implementation of Cons
date: 2023-01-09 00:00:00 -0500
categories: general
---

Seeing the complexity of the Clojure interface/data-structure ecosystem as we did in [the last post]({% post_url 2023-01-08-consideration %}) can be a bit daunting.  But if we start gently we can tease out some of the basic interactions and techniques that underlie how the real Clojure versions of these data structures are implemented.

Despite perhaps putting the scare into you with the diagram of `Cons` in the last post, a simple cons cell is the place to start.   A cons cell is just a node for singly-linked list -- a field containing an item and a field pointing to the rest of the list, perhaps the simplest notion of _sequence_ we can come up with.  However, as we will see, we need more than just this node type to meet the needs of the Clojure interfaces. 

## The interfaces

To simplify our task, we will implement only the trio of interfaces that sits at the heart of Clojure's sequence paradigm -- `ISeq`, `IPersistentCollection`, and `Seqable`.
 
If you need a refresher on how Clojure works with sequences, you can take a gander at [Sequences](https://clojure.org/reference/sequences). We are about to expose what lies underneath, what a data structure must implement in order to participate in actions such as interating through a sequence. 

Let's start with `Seqable`:

```F#
type [<AllowNullLiteral>] Seqable =
    abstract seq : unit -> ISeq
```
A `Seqable` is anything that can produce an `ISeq` to iterate over its contents.  Calling `seq` on a `Sequable` will hand you an `ISeq`, an object that represents a sequence, suitable for iteration and other sequence-y things.  For some ojbects, `seq` might return the object itself: its class implements `ISeq`.  For other objects, calling its `seq()`  will produce a different object to handle the iteration.  Thus, something could be `Seqable` without itself being an `ISeq` or an `IPersistentCollection`.

> Side note: If you think `Sequable.seq` sounds like the `seq` function in Clojure, well, yes.  But the Clojure `seq` can be called on many types of objects, first and foremost those that implement `Sequable`.  However, `String` does not implement `Seqable` but nevertheless evaluate `(seq "abcd")` and you will get back a sequence comprising the characters in the string.  Clojure's `seq` is implemented as a call to a runtime library function (`clojure.lang.RT.seq` if you must know).  `RT.seq` first checks if its argument implements `Seqable`, in which case it calls its `Seqable.seq` method.  Otherwise, it runs through a bunch of special cases -- strings, arrays, `IEnumerable` (or the Java equivalent) -- and creates appropriate `ISeq` objects for them.  (Real inside baseball:  this is where protocols could come into play.  Shhhh.)

The `IPersistentCollection` interface is straightforward.  An object could be an `IPersistentCollection` without being an `ISeq`, hence these interfaces cannot be merged.  But an `IPersistentCollection` must be `Seqable`; its essence as a collection is that you can iterate over it.  It's just that the iterator (its `seq`) may be different kind of object.


```F#
and [<AllowNullLiteral>] IPersistentCollection = 
    inherit Seqable
    abstract count : unit -> int
    abstract cons: obj -> IPersistentCollection
    abstract empty: unit -> IPersistentCollection
    abstract equiv: obj -> bool
```

A quick look at the methods in `IPersistentCollection`:

- `count()` returns a count of items in the collection -- for an `ISeq`, that would be the number of items in the sequence from that point on.  
- `empty()` yields an empty collection _of the appropriate type_.  (You'd have to read comments in the Clojure code to get this.) A hash map would return an empty hash map, for example.  In what follows, because a `Cons` cannot be empty --it has an element -- some other type will have to be returned. 
- `x.cons(o)` returns a new `ISeq` which has the item `o` first, followed by the items in `x`.  It is up to each type to figure out how to implement this.  In actual Clojure , `PersistentList` returns a new `PersistentList`.  `EmptyList` does also.  Other types might use a `Cons`.
- `equiv(o)` is used for equality checking on collections.  Each collection defines its own.   (Discussing `equiv` and equality in Clojure will require a separate post.)

The `ISeq` interface captures the essence of iteration across a sequence.  

```F#
and [<AllowNullLiteral>] ISeq =
    inherit IPersistentCollection
    abstract first : unit -> obj
    abstract next : unit -> ISeq
    abstract more : unit -> ISeq
    abstract cons : obj -> ISeq
```

 The difference between `next` and `more` is subtle.  The best place to learn a little more (no pun) is [here](https://clojure.org/reference/lazy) but you'll have to read carefully.  Originlly, Clojure had just `first` and `rest`. When laziness was added into the picture, a redesign was needed.  
 Essentially, `more` gives you an object that represents the rest of sequence, without actually triggering a computation to compute the next element in the sequence.  `next` goes to the trouble of figuring out if there is more to the sequence or not (so to speak) -- it will determine if there is a next element or if we have reached the end of the sequence.   `more` is provided to avoid a potentially significant computation that might not be necessary.  If you are doing something like iterating through a list, for example, you would use `next` -- you know you are going to use the next element.

  From [Sequences](https://clojure.org/reference/sequences) we have:

> The Seq interface
> 
> ( _first_ coll)
>
>  Returns the first item in the collection. Calls seq on its argument.  If coll is nil, returns nil.
>
>  ( _rest_ coll)
>
>  Returns a sequence of the items after the first. Calls seq on its argument. If there are no more items, returns a logical sequence for which seq returns nil.

The doc string for the `next` function in Clojure states:

> Returns a seq of the items after the first. Calls seq on its argument.  If there are no more items, returns nil.

The `rest` function in Clojure is essentially `ISeq.more`.  Note that it does not return `nil`.  Thus, we need something to represent an empty list that is not `nil`.  A cons cell is never empty.  Thus, our minimum is a cons cell and 'a logical sequence for which seq returns nil' -- an `EmptyList`.


> You should note that the null value in Clojure -- `nil` -- is not an empty list.  And the empty list -- `()` -- is not `nil`.

```Clojure 
(nil? ())    ; -> false
(nil? nil)   ; -> true
(seq? ())    ; -> true
(seq? nil)   ; -> false
(list? ())   ; -> true
(list? nil)  ; -> false
```

## Getting started

Rather than just jump directly into implementing  `SimpleCons` and `SimpleEmptyList`, I'll complicate things a bit and introduce some helper functions.  We could do without these for our simple example, but this mimics how it is actually done in the Clojure code and also will set us up to implement more complicated data structures down the line.  I put these helpers into a module named, of course, `Util`.

Equality first.  

```F#
   let checkEquals o1 o2 = obj.ReferenceEquals(o1,o2) || not (isNull o1) && o1.Equals(o2)
	
    let rec seqEquals (s1:ISeq) (s2:ISeq) =
        match s1, s2 with   
        | null, null -> true
        | null, _ -> false
        | _, null -> false
        | _ ->  checkEquals (s1.first()) (s2.first()) && seqEquals (s1.next()) (s2.next())
```

`checkEquals` just checks if two objects are equal.  As written, it short-circuits through `Object.ReferenceEquals` to both handle null values properly and avoid unnecessary calls to `Equals`.  

`seqEquals` is prototypical example of iteration through a sequence using `first` and `next`.  In this case, it happens to iterate through two sequences simultaneously.  The recursion stops when then end of one of the sequences is reached.  (And if they are not both at the end, of course they cannot be equal.)

There is a notion of `equiv` (equivalent) that is separate from `Equals`, mostly in how numeric values are handled.  We ignore that here, but reserve a place now for elaboration in a later post.

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
        let rec step (s:ISeq) cnt = if isNull s then cnt else step (s.next()) (cnt+1)
        step s 0
```

Finally, let's provide a little help for printing sequences.  (Feel free to skip this on the first pass.)

```F#
    let rec seqToString (s: ISeq) =
        let sb = new StringBuilder()

        let rec appendItem (o: obj) =
            match o with
            | :? Seqable as s -> appendSeq (s.seq ())
            | _ -> sb.Append(o.ToString()) |> ignore

        and appendSeq (s: ISeq) =
            match s with
            | null -> ()
            | _ ->
                appendItem (s.first ())
                sb.Append(" ") |> ignore
                appendSeq (s.next ())

        sb.Append("(") |> ignore
        appendSeq s
        sb.Append(")") |> ignore
        sb.ToString()
```

Again, a sequence iteration implemented by recursion.  Please note this _emphatically_ is not how printing is handled for in the Clojure code;  this will serve as a placeholder for now.

## The empty sequence

And now we can write our two collection classes.  These are mutually recursive and so are joined by `and` in the actual code.  Let's start with the simpler 'empty sequence'.

```F#
type SimpleEmptySeq() =

    override _.GetHashCode() = 1
       
    override _.ToString() = "()"
   
    override _.Equals(o) = 
        match o with
        | :? Seqable as s ->  s.seq() |> isNull
        | _ -> false
```

The basic overrides are simple:  Fixed hashcode value; `ToString` as required by Clojure.  (That `()` should look familiar.) For equality, we can only be equal to an empty sequence.  As Clojure is set up, an empty sequence is something  that returns `nil` (= `null` in .NET) when `seq` is called on it.  That means we need something that `seq` can be called on: a `Seqable`.

> Yes, in Clojure, when you call `seq` on an empty sequence, you get back `nil`:

```Clojure
(seq ())  ; -> nil
```

You can read all kinds of dicussions on Clojure groups about why one tests  `(seq s)` for various conditions -- keep in mind that `nil` counts as `false` in boolean conditions.

On to the sequence goodies.  The defining characteristic of an empty sequence object is that calling `seq` on it returns null, as we just saw.

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

For  `cons`, the definition is a bit odd.  We have two different `cons`es floating around: `IPersistentCollection.cons` and `ISeq.cons`.  They have distinct return types.  Because an `ISeq` is an `IPersistentCollection`, you will often see what have done here: implement the `IPersistentCollection` version in terms of the `ISeq` version, with an `upcast`.

Finally,

```F#
    interface ISeq with 
        member _.first() = null
        member _.next() = null
        member this.more() = upcast this
        member this.cons(o) = upcast SimpleCons(o,this)
```
The values for `first` and `next` are by definition.  We need to return an emtpy sequence--not `null`-- for `more`; this object itself is such a thing.  Finally, with `cons` we see our entanglement with `SimpleCons`.


## A simple cons cell

And so on to `SimpleCons`:

```F#
and type SimpleCons(head: obj, tail: ISeq ) =

 ```
 
 The main constructor is all we need, along with two (immutable) fields to hold the values.   For the `Object` overrides, we can defer mostly to our utility functions:
 
 ```F#
    override this.GetHashCode() = Util.getHashCode this

    override this.ToString() = Util.seqToString this

     override this.Equals(o) = 
        match o with
        | :? Seqable as s -> Util.seqEquals (this:>ISeq) (s.seq())
        | _ -> false
```

We will compare affirmatively only to other sequences, and they must have equal elements in order.

```F#
    interface Seqable with
        member this.seq() = upcast this
```

Because `SimpleCons` implements `ISeq`, the object can be its own `seq`.

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

As a collection, `SimpleCons` has its own element plus the elements in its `tail`; thus `count`.
For `cons` we defer to `ISeq.cons` as mentioned above.
And `empty` gets us the reverse entanglement with `SimpleEmptySeq`.  

Finally,

```F# 
    interface ISeq with
        member _.first() = head
        member this.next() = (this:>ISeq).more().seq()
        member _.more() =  if isNull tail then upcast SimpleEmptySeq() else tail 
        member this.cons(o) = upcast SimpleCons(o,this)
```

I hope it is not a surprise that to _cons_ something onto the front of ourselves, we use a `SimpleCons`.

The dance here between `more` and `next` is common.
`more` cannot return `null`, so it cannot just return the `tail`.  If the `tail` is null, we must return 'a logical sequence for which seq returns nil': our `SimpleEmptySeq`.  That test for emptiness here is very specific to `SimpleCons`: a null `tail`.  

`next` is often defined in terms of `more` in the manner shown here.  If `more` returns an empty sequence, the `seq` on it will return `null`, which is what `next`'s contract says. And our `tail` is an `ISeq`, which is an `IPersistentCollection`, which is an `ISeq`, so the call to `seq()` on the `more` will always be safe.

And we are done.


Source code for these examples are available at [ClojureCLR-Next repo](https://github.com/dmiller/clojure-clr-next).