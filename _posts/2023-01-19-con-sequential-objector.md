---
layout: post
title: con-Sequential objector
date: 2023-01-19 06:00:00 -0500
categories: general
---

In which I contemplate the meaning of `Sequential`.   This is an easy one compared to what you just went through.

There is an interface named `Sequential` in the pantheon of Clojure collection-related interfaces.  It is defined thus:

```F#
type Sequential =
    interface
    end
```

In other words, a _marker interface_, so-called.  A class implements it to make a statement about itself; in this case, to declare itself _sequential_.

We should actually have done this for our `SimpleCons` example;


```F#
type SimpleCons(head:object, tail:object) =
...
    interface Sequential

...
```

Which Clojure collections are `Sequential`?  Things which have an inherent ordering, such as conses and persistent vectors.  Which are not `Sequential`?  Think of maps and sets; their ordering is essentially random.

There are some odd cases.  One might think that a sorted set has an inherent ordering, but apparently its 'set-ness' overrides its 'ordered-ness'.

```Clojure
user=> (sequential? (sorted-set 3 2 1))
false
```

(The Clojure function `sequential?` just checks to see if its argument's type implements `Sequential`.)

Is an `ISeq` sequential?  One would think so. They can be. 

```Clojure
user=> (sequential? (seq (cons 2 ())))
true
user=> (sequential? (seq "abc"))
true
user=> (sequential? (seq {:a 1 :b 2}))
true
```

It seems reasonble.  But there is no guarantee.  AFAIK, someone could hand you an `ISeq` that is not `Sequential`.  I'm not sure what that would mean.  As you can see above, even the `seq` for a map is marked sequential. 

Why am I spending time on this simple little interface?  Because at first glance, I didn't understand why it was there.  Looking at how `Sequential` is used in the Clojure(JVM/CLR) code, every place where `Sequential` is checked, when the answer is 'yes' there is a call to `RT.seq` to get a sequence.  Not unreasonable for something that is 'sequential'.  However, if you look at `RT.seq` -- and as you will see in an upcoming post, I'm spending a _lot_ of time looking at `RT.seq` -- the call will fail unless the object in question satisfies one of the following:

- It implements `Seqable`.
- It is one of small class of special cases: strings, iterables/enumerables, JVM/CLR arrays.  (There are special classes providing `ISeq` functionality for these types.)

Why do we need `Sequential`?

In the fog at the end of a long day of analysis, I was not finding clarity, so I put this question to the `#clojure-dev` channel in the [Clojurian slack](https://clojurians.slack.com/messages). And I received a proper education on the matter.  (Quite kindly. I botched my initial the statement -- it had been a _long_ day -- but eventually got something coherent out.  And it might have been stated a bit provocatively.  And it took me way to long to get the points that others made.  Don't look.)  

 A good example was from code generating JSON.  Maps get encoded differently than 'sequential' data structures. The notion of `Seqable` things that are not `Sequential` is meaningful.   Yep, I get that.
 
My thanks to the patience of the other folks on that thread.

But I still had questions:  What is an example of a data structure that is `Sequential` but not `Seqable`?  And how would it fit into Clojure sequence handling?

 It turns out there is an example sitting right there in the `core.clj` source code: Tne `deftype`'d  `Eduction` implements `Sequential` but not `Seqable`.  It can survive going through `RT.seq` because it implements `Iterable`/`IEnumerable`, thus meeting one of the `RT.seq`'s special cases.

 I self-administered one dope-slap for forgetting that there was a second point of extensionality for `RT.seq` beyond `Seqable`.

 I'm happy now. Almost completely. Except for feeling foolish. Except for one last question: 

Is there an example of an `ISeq` that is not `Sequential`?  Should that ever happen? (The issue could be forced:  Have `ISeq` inherit from `Sequential`.)  Think about a `Cons`.  It is `Sequential`.  Its tail defines the rest of its sequence of items.  Its tail is an `ISeq`.  What would it mean to have `Cons` with a `next` that is not `Sequential`?
