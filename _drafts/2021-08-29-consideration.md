---
layout: post
title: For your Cons-ideration
date: 2021-08-29 19:36:00 -0500
categories: general
---

# For your Cons-ideration

To build a Lisp, you would perhaps start with the simplest data structure, the cons cell: two fields that hold pointers.  In Clojure, we don't even need setters for the fields. 

```F#
type Cons = {Car:obj; Cdr:obj}
```

(In old Lisps, `car` and `cdr` were the traditional names for the fields. And they would be mutable.)  Okay, good, we're done. 

Well, not really.  If you look at a diagram of the inheritance relationships of the various interfaces and concrete collection classes in Clojure, you get a picture something like this:

![Full dependency graph](/assets/images/all-dependencies.png)

And this only shows the direct inheritance relationships.  It does not show, for example that `Seqable` has a method with a return type of `ISeq`, thus creating a circularity.

F# does not like circularities.

What is the minimum needed implement `Cons`?  Let's grab the tree by the `Cons`,  pull it up, and see what comes up with it.  In the following graph boxes indicate interfaces, 'eggs' indicate abstract classes, and ellipses indicate concrete classes. Solid lines indicate inheritance; dashed lines indicate a reference in the API of the 'tail' to the 'head' type.

![Cons dependency graph](/assets/images/cons-dependencies.png)

Circularities are the first concern; if implemented naively as indicated, they indicate places where F# will require mutually recursive definitions.  The first such grouping is the trio of `Seqable`, `IPersistentCollection`, and `ISeq`; mutually recursive definitions are required.

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

Once these are defined, the other interfaces can be defined directly, with no mutual recursion, just the appropriate ordering to respect inheritance.

The second area of possible mutual recursion involves `Cons` and `ASeq`.  `ASeq` is an abstract class that provides default implementations for a number of interface methods.  One such default is for the `cons` method, which uses a `Cons`. (Duh.)  `Cons` inherits from `ASeq` to pick up those defaults.  One could break the cycle by just duplicating the code.  Mutual recursion is cleaner.

Finally, we see a circularity between `EmptyList` and `PersistentList`.    `PersistentList` uses `EmptyList` as the return value for `IPersistentCollection.empty()`.  `EmptyList` returns a `PersistentList` for `IPersistentCollection.cons(o)` and  `ISeq.cons(o)`.  A `PersistentList` must have at least one element; an `EmptyList` obviously has none.  Why not return a `Cons` for `cons()`?  There is a bias in a few places in the runtime code, notably `RT.conj` and `RT.cons`) to return a `PersistentList` of one element rather than a `Cons` of that element with `null`.  (Continuing to conj/cons/etc. onto the former leads to more efficiency.)

The embrace between `PersistentList` and `EmptyList` can be handled by mutually recursive definitions.  The alternative of using a discriminated union for the pairing loses the ability to inherit from `ASeq` -- that would cause a lot of code duplication.  

Two other areas of the map to examine are the `IMeta`/`IObj`/`Obj` triple and the `IFn`/`IReduceInit`/`IReduce` triple.  The former comes into play because most collections support attachment of metadata in the form of an `IPersistentMap` instance.  However, the metadata-related code does not involve creating any persistent maps, just passing them around, so we can build the code without introducing any relationships to concrete map classes.  

The reduce functionality brings `IFn` into the mix.  This opens up a lot of complexity. Eventually we will have to implement the abstract classes that support `IFn` functionality.   Fortunately, that does not appear to create any circularities.  Some of the collections, such as `LazySeq` also require `IFn`  

And we're off to the races.  The minimum:  

1. Define the interfaces shown above, with the only mutual recursion being among `Seqable` + `IPersistentCollection` + `ISeq`.  
2. Implement `Obj`.  
3. Implement `EmptyList`.  If we make `cons` and raise errors to get started, we can isolate this to test.
4. Implement `Cons`, `ASeq` together.
5. Implement `PersistentList`.  Mutually recursive with `EmptyList`.  Finish the definition of `EmptyList.cons`.
6. Test it all.

[At the time of this posting, this has all been done.]