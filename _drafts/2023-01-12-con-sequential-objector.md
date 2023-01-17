---
layout: post
title: con-Sequential objector
date: 2023-01-15 00:00:00 -0500
categories: general
---

In which I contemplate the meaning of `Sequential`.

There is an interface named `Sequential` in the pantheon of Clojure collection-related interfaces.  It is defined thus:

```F#
type Sequential =
    interface
    end
```

In other words, it a _marker interface_, so-ca..ed.  A class implements it to make a statement about itself; in this case, to declare itself _sequential_.

(In F#, a type with all abstract methods will be an interface.   With an interface such as `Sequential` that has no methods,  one has be deliberate in indicating that it is an interface, as shown.)


```F#
type Cons(head:object, tail:object) =
...
    interface Sequential

...
```

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

And it probably should be.  I don't know of any cases of an `ISeq` not being `Sequential`.  But there is no guarantee.  This would be an implementer's choice, 

Why am I spending time on this simple little interface?  As I analyze the Clojure and ClojureCLR implementations for the ClojureCLR.Next project, I try to figure out why things are the way they are.  And looking at how `Sequential` is used in the Clojure(JVM|CLR) code, the operational consequences are a bit odd.  Every place where `Sequential` is checked, when the answer is 'yes' there is a call to `RT.seq` to get a sequence.  Not unreasonable for something that is 'sequential'.  However, if you look at `RT.seq`, the call will fail unless the object in question satisfies one of the following:

- It implements `Seqable`.
- It is one of small class of special cases: strings, iterables/enumerables, JVM/CLR arrays.  (There are special classes providing `ISeq` functionality for these types.)

Why do we need `Sequential`?

Well, the answer is clear now, but it was not coming to me, so I put this question to the `#clojure-dev` channel in the [Clojurian slack](https://clojurians.slack.com/messages). And I received a proper education on the matter.  (Quite kindly. I kind of botched the statement -- it had been a long day at the keyboard -- but eventually got something coherent out.  And it might have been stated a bit provocatively.  And it took me way to long to get the point.  Don't look.)  

 A good example was from code generating JSON.  Maps get encoded differently than 'sequential' data structures, for example. The notion of `Seqable` things that are `Sequential` is meaningful. (At one point in thinking about this (a year ago) I had remembered maps, but forgot about them at the critical moment.)  
 
 I walked away sheepishly, accepting the lesson.  But I still had a question:  What is an example of a data structure that is `Sequential` but not `Seqable`?  

 It turns out there is one sitting right there in the `core.clj` source code.

 There is a `deftype Eduction` that implements `Sequential` but not `Seqable`.  It survives going through `RT.seq` because it implements `Iterable`/`IEnumerable`, thus meeting one of the `RT.seq`'s special cases.

 I'm happy now. Almost completely. Except for feeling foolish. Except for one last question: 

Is there an example of an `ISeq` that is not `Sequential`?  Should that ever happen? (The issue could be forced:  Have `ISeq` inherit from `Sequential`.)