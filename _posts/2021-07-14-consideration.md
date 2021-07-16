---
layout: post
title: For your Cons-ideration
date: 2021-07-14 19:36:00 -0500
categories: general
---

# For your Cons-ideration

In starting to build a Lisp, you would perhaps start with the simplest data structure the cons cell: two fields that hold pointers.  In Clojure, we don't even need setters for the fields. 

```F#
type Cons = {car:obj; cdr:obj}
```

(In old Lisps, `car` and `cdr` were the traditional names for the fields. )  Okay, good, let's move on. Well, not really.  If you look at a diagram of the inheritance relationships of the various interfaces and concrete collection classes in Clojure, you get a picture something like this:

![Full dependency graph](/assets/images/all-dependencies.png)

And this only shows the direct inheritance relationships.  It does not show, for example that `Seqable` has a method with a return type of `ISeq`, thus creating a circularity.

F# does not like circularities.

What is the minimum needed implement `Cons`.  Let's shake the C# code and see what comes out.  The following graph is encoded with interfaces in boxes, abstract classes in 'eggs', and concrete classes in ellipses.  Solid lines are inheritance.  Dashed lines indicate usage of the head class in the API of the tail class.

![Cons dependency graph](/assets/images/cons-dependencies.png)

Circularities are the first concern; if implemented naively as indicated, they indicate places where F# will require mutually recursive definitions.  The first such grouping is the triple of interfaces `Seqable`, `IPersistentCollection`, and `ISeq`.  This is easily managed.  These interface definitions must precede essentially everything else.

```F#

```
In fact, once these are defined, all the other interfaces can be defined directly, with no mutual recursion, requiring only to pay attention to the order forced by the inheritance relationship.  (Do a topological sort based on the directed acyclic graph of inheritance relations.)

The second area of possibily mutual recursion is defined by the pair `Cons` and `ASeq`.  `ASeq` is an abstract class that provides default implementations for a number of interface methods.  `Cons` inherits from `ASeq` to pick up those defaults.  One could break the cycle by just duplicating the code.  (And avoid some duplication by creating functions that could be called by each of them.)  However, it would still call for a fair amount of code.  Perhaps simpler just to make `Cons` and `ASeq` mutually recursive.

Finally, we have a circularity between `EmptyList` and `PersistentList`.  `EmptyList` is actully `PersistentList.EmptyList`. These two really are joined.  `PersistentList` uses `EmptyList` as the return value for `IPersistentCollection.empty()`.  `EmptyList` returns a `PersistentList` for `IPersistentCollection.cons(o)` and  `ISeq.cons(o)`.  A `PersistentList` must have at least one element; an `EmptyList` obvious has none.  Why not return a `Cons` for `cons()`?  There is a bias in a few places in the runtime code, notably `RT.conj` and `RT.cons`) to return a `PersistentList` of one element rather than a `Cons` of that element with `null`.    I suppose this is so that continuing to conj/cons/etc. onto the former leads to more efficiency(?).

The embrace between `PersistentList` and `EmptyList` could be handled by mutually recursive definitions.  We could embrace F# as we encode this embrace by using a discriminated union for the pairing.  However, we would lose inheriting from `ASeq` -- not sure that's worth it.  

Two other areas of the map which bear inspection are the `IMeta`/`IObj`/`Obj` triple and the `IFn`/`IReduceInit`/`IReduce` triple.  The former comes in because most collections support attachment of metadata, in the form of an `IPersistentMap` instance.  However, the metadata-related code does not involve creating any persistent maps, just passing them around, so we can build the code without introducing any relationships to concrete map classes.  We only need to defer testing the `IMeta`/`IObj`/`Obj` until we have developed at least one map class.

The reduce functionality brings `IFn` into the mix.  This opens up a lot of complexity.  Similary to the metadata, we can implement the reduce code in `PersistentList` but not test it.  Be eventually we will have to implement the abstract classes that support `IFn` functionality.   Fortunately, that does not appear to create any circularities.

And we're off to the races.  The minimum:  

1. Define the interfaces shown above, with the only mutual recursion being among `Seqable` + `IPersistentCollection` + `ISeq`.  
2. Implement `Obj`.  
3. Implement `EmptyList`.  If we make `cons` and raise errors to get started, we can isolate this to test.
4. Implement `Cons`, `ASeq` together.
5. Implement `PersistentList`.  Mutually recursive with `EmptyList`.  Finish the definition of `EmptyList.cons`.
6. Did I mention testing?


