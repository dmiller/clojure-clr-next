---
layout: post
title: Circular reasoning (part 1)
date: 2023-01-16 00:00:00 -0500
categories: general
---

I have to analyze the nature of circular references in the current Clojure implementations in order to avoid making an inelegant F# monolith -- massive quantities of code in one file with all the types mutually recursive.

In [For your Cons-ideration]({{site.baseurl}}{% post_url 2023-01-08-consideration %}), I pointed out two places where mutual reference occurs:  the `Seqable`/`IPersitentCollection`/`ISeq` triple of interfaces and the `Cons`/`ASeq` pairing.

## Triple play

Let's start with our faves:

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

`ISeq` inherits from `IPersistentCollection`, which inherits from `Seqable`, which refers to `ISeq`.   Can this be avoided?

Yes.  You can find articles dealing with exactly this issue.  Some of the first I read were a seqeuence you can find [here](https://fsharpforfunandprofit.com/posts/removing-cyclic-dependencies/#series-toc).  For our situation, adding type parameters will work.

```F#
type SeqableT<'T> =
    abstract seq: unit -> 'T

type IPersistentCollectionT<'T> = 
    inherit SeqableT<'T>
    abstract cons: obj -> IPersistentCollectionT<'T>


type ISeqT<'T> =
    inherit IPersistentCollectionT<'T>
    abstract next: unit -> 'T

```

(Showing just enough of the methods to get the circularities.)  But how do we instantiate this?  What goes in for `'T`?

The magic starts at the bottom.  Define

```F#
type ISeq =
    inherit ISeqT<ISeq>
```

Yes, the recursion in that type definition works.
For the other two interfaces, you can go one of two ways:  (1) define aliases; (2) define derived types.

We can define aliases to get the names we want:

```F#
type IPersistentCollection = IPersistentCollectionT<ISeq>
type Seqable = SeqableT<ISeq>
```

Then we could write an implementation along these lines:

```F#
type MyClass(x:int) = 
    
    interface ISeq

    interface ISeqT<ISeq> with
        member this.next() = upcast this

    interface IPersistentCollection with
        member this.cons(o) = upcast this

    interface Seqable with
        member this.seq() = upcast this
```

The problem is that aliases are not sticky.  Any program from the outside would not see that `MyClass` implements `IPersisentCollection`.  It would see `IPersistentCollectionT<ISeq>`.  I'm not sure I'm willing to violate expectations to that extent.

An alternative would be to create real types:

```F#
type ISeq =
    inherit ISeqT<ISeq>

type IPersistentCollection =
    inherit IPersistentCollectionT<ISeq>

type Seqable =
    inherit SeqableT<ISeq>
```

With this, `MyClass` becomes

```F#
type MyClass(x:int) = 
    
    interface ISeq
    interface IPersistentCollection
    interface Seqable

    interface ISeqT<ISeq> with
        member this.next() = upcast this

    interface IPersistentCollectionT<ISeq> with
        member this.cons(o) = upcast this

    interface SeqableT<ISeq> with
        member this.seq() = upcast this
```
 
Is it worth all this extra work and complexity for every class that implements these interfaces? For the sake of two `and`s among three very small interfaces, I'm saying not.  But I'm open to persuasion, I suppose.

## The duple is a quadruple

We mentioned the close tie between `Cons` and `ASeq`, but there are two more players:  `PersistentList` and `PersistentList+EmptyList`. Let's call them `C`, `A`, `P`, and `E`, respectively.  We have these relationships:

- `A` uses `C` in `cons`
- `A` uses `E` in `empty`
- `A` uses `E` in `more`
- `C` inherits from `A`
- `C` uses `E` in `empty`
- `E` uses `P` in `cons`
- `P` inherits from `A` 
- `P` uses `E` in `empty`

Can we reduce the dependencies?

Could `E` by represented by a `P`?  Only with significant contortion.  Having no element and having an element are quite distinct operationally.  One could perhaps use a discriminated union, but only at the cost of no longer using `ASeq` (more on that in a moment) and perhaps not interacting well externally. And we add a bunch of `match` expressions in the code. Or, given that, as implemented, `P` carries a count, we could complicate every piece of implementation code with a `count=0` test.  Is this worth it?  I think not.  I prefer the clarity that comes at the cost of cylic dependency. 

Could we reduce complexity by severing the connection of `C`, `P`, and `E` on `A`?  This would mean `P` and `C` not inheriting from  `A`, thus requiring a duplication of code. How bad would this be?

I did an analysis of the overrides of `A` methods in `P` and `C`.  Looking at interfaces `ISeq`, `IPersistentCollection`, and `Seqable`, almost all of the implementations are overriden.  What is not overriden so much are `A`'s implement of `Object` overrride (`ToString`, `Equals`, `GetHashCode`) and implementations of `System.Collections.IList`, `System.Collections.ICollection`, and `System.IEnumerable`, which are in the contract for these types.  One could split this apart by defining a base class that provides this latter group of interface implementations, define `E`, `P`, and `C` as a recursively joined group, and defining `ASeq` for everyone else to use.

Are there operational consequences?  The only places `ASeq` is mentioned directly in code is in the aforementioned `RT.seq` and in the definitions of `CollReduce` extensions in `protocols.clj`.  For `CollReduce`, we'll just have to make a note.

Unfortunately, `RT.seq` holds some nasty surprises, as we will discover in [Circular Reasoning, part 2]({{site.baseurl}}{% post_url 2023-01-17-circular-reasoning-part-2 %}).