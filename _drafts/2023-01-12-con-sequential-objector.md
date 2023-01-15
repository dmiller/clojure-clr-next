---
layout: post
title: ClojureCLR -- Reconsidered
date: 2023-01-06 00:00:00 -0500
categories: general
---

In which I contemplate the meaning of `Sequential`.

There is an interface named `Sequential` in the pantheon of Clojure collection-related interfaces.  It is defined thus:

```F#
type Sequential =
    interface
    end
```

(In F#, a type with all abstract methods will be an interface.   With an interface such as `Sequential` that has no methods,  one has be deliberate in indicating that it is an interface.)

An interface with no methods is often called a _marker interface_.  
Implementing the interface is rather easy.  If we had done so in `SimpleCons` -- you will soon learn that we should have -- it would look like

```F#
type Cons(head:object, tail:object) =
...
    interface Sequential

...
```

Marker interfaces are used to, well, mark a type as having a particular property,  in this case of being _sequential_.  

Which Clojure collections are `Sequential`?  Conses and persistent vectors are examples.  Things that have an inherent ordering, one supposes.  Which are not `Sequential`?  Examples are maps and sets; their ordering is essentially random.

There are some odd cases.  One might think that a sorted set has an inherent ordering, but apparently its 'set-ness' overrides its 'ordered-ness'.

```Clojure
user=> (sequential? (sorted-set 3 2 1))
false
```

(The Clojure function `sequential?` just checks to see if its argument's type implements `Sequential`.)

Is an `ISeq` sequential?  It can be.

```Clojure
user=> (sequential? (seq (cons 2 ())))
true
user=> (sequential? (seq "abc"))
true
user=> (sequential? (seq {:a 1 :b 2}))
true
```

(I don't know of any cases of an `ISeq` not being `Sequential`.  But there is no guarantee.  This would be an implementer's choice, given that the `ISeq` interface does not inherit from `Sequential`.   I'm not sure if that's important.)

Why am I spending time on this simple little interface?  As I analyze the Clojure and ClojureCLR implementations for the ClojureCLR.Next project, I try to figure out why things are the way they are.  And looking at how `Sequential` is used in the Clojure(JVM|CLR) code, the operational consequences are a bit odd.  Every place where `Sequential` is checked, when the answer is 'yes' there is a call to `RT.seq` to get a sequence.  Not unreasonable for something that is 'sequential'.  However, if you look at `RT.seq`, the call will fail unless the object in question satisfies one of the following:

- It implements `Seqable`.
- It is one of small class of special cases: strings, iterables/enumerables, JVM/CLR arrays.  (There are special classes providing `ISeq` functionality for these types.)

If you implement `Sequential` and you don't implement `ISeqable`, you will blow up in `RT.seq`.  At least in internal Clojure-implementation code. And in any Clojure sequence functions that calls `seq` on its collection argument.

Do we need `Sequential`?

I put this question to the `#clojure-dev` channel in the [Clojurian slack](https://clojurians.slack.com/messages).  (I kind of botched the statement -- it had been a long day at the keyboard -- but eventually got something coherent out. Don't look.)  And I received a proper education on the matter.

 The argument was made that the semantic distinction is important.  A good example was from code generating JSON.  Maps get encoded diffently than 'sequential' data structures.  

Things should be coded clearly. In the example given, there was a `cond` that tested for `map?` and for `sequential?`.   You could put those two tests in either order and they would work.  If you only checked `seqable?` the `map?` test have to come first.  

However, in the `sequential?` branch `map` is invoked.  `map` calls `seq` on its collection argument and we are back in `RT.seq`.  In other words, if your `Sequential` doesn't also implement `Seqable`, you are in trouble.

I agree there is an important semantic distinction to be made.  But I'm left with questions.

1.  Is there a good example of a data structure that is `Sequential` but not `Seqable`?  The argument in the channel was that this was reasonable, that someone would do other processing in this case.  Accepting that,  I'd still like to see an example.  And when I get one, I will post it.


2. Is there an example of an `ISeq` that is not `Sequential`?  Should there be? (The issue could be forced:  Have `ISeq` inherit from `Sequential`.)

Dont' worry.  I will be putting `Sequential` in ClojureCLR.Next.  It's in the contract, so to speak.