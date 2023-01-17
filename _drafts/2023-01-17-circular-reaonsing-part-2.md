---
layout: post
title: Circular reasoning (part 2)
date: 2023-01-16 00:00:00 -0500
categories: general
---

The code with the greatest entanglement across the Clojure codebase comes in the static classes `clojure.lang.RT` and `clojure.lang.Util`.  How can these classes be restructured to reduce cyclic dependence and improve clarity?  

## Digging in

Line counts may not be a good indicator of complexity, but let's be clear: `RT` and `Util` are big.  In ClojureCLR, RT is over 3500 lines; Util is another 900 lines.  We have a lot of code wade through.

These classes provide services to several discrete parts of CLojure*. The collections, the reader, the compiler, and the Clojure source code in core.clj and related.  Given that I'm trying to decouple the collections from the rest, I restricted my usage analysis to collections.

Let's start with Util.  Breaking the code into categories:

- hashing
- equiv/equals
- numeric support
    - conversions
    - defining
    - little utilities
- miscellaneous

The problem of hashing, equality, the difference between `equiv` and `equals` is going to take its own post down the line.  For the moment, what's most concerning is that these things involve numerics:  what is a number; how do you compare numbers, including for equality; how do you has numbers; and so forth.  A lot of these problems are handled in Util by calls to methods in `clojure.lang.Numbers` -- and suddenly we have 4700 lines of code to analyze.

We could extract some things from Numbers and just use them; `Numbers.hasheq`, for example.  But also references or `Numbers.equal` and `Numbers.compare` that use the whole machinery that Numbers set up.  It might be possible to pull out just those pieces for now.  If we have to implement all of Numbers, we're going to be here for a while.  Not to mention that I'd like to think about how to rewrite Numbers entirely.

Other than this, I think Util is not so deeply entwined with the other parts of Clojure that we can't implement it and make it availble early in the file sequence.

## RT 

I will spare you a full-blown analysis.  There is plenty of code that is not needed for implementing collections and can be deferred until later.  There are a few trouble spots in collections.

The first is a set of methods that focus on maps: `RT.map`, `RT.assoc`, `RT.dissoc` primarily.  These use some map classes.  But it looks like the maps that use them are not ones they refer to.  Just splitting them out and introducing them into the right place in the compile sequence should suffice.

### RT.seq

The second area concerns `RT.seq`.  This method converts its argument into an `ISeq`.  It is like `Seqable.seq` -- in fact, it uses that, but also special cases things like `String`.  It also puts some special cases up front for efficiency.    Here is the C# code:

```C#
public static ISeq seq(object coll)
        {
            if (coll is ASeq aseq)
                return aseq;

            return coll is LazySeq lseq ? lseq.seq() : seqFrom(coll);
        }

        // N.B. canSeq must be kept in sync with this!

        private static ISeq seqFrom(object coll)
        {
            if (coll == null)
                return null;

            if (coll is Seqable seq)
                return seq.seq();

            if (coll.GetType().IsArray)
                return ArraySeq.createFromObject(coll);

            if (coll is string str)
                return StringSeq.create(str);

            if (coll is IEnumerable ie)  // java: Iterable  -- reordered clauses so others take precedence.
                return chunkEnumeratorSeq(ie.GetEnumerator());            // chunkIteratorSeq

            // The equivalent for Java:Map is IDictionary.  IDictionary is IEnumerable, so is handled above.
            //else if(coll isntanceof Map)  
            //     return seq(((Map) coll).entrySet());
            // Used to be in the java version:
            //else if (coll is IEnumerator)  // java: Iterator
            //    return EnumeratorSeq.create((IEnumerator)coll);

            throw new ArgumentException("Don't know how to create ISeq from: " + coll.GetType().FullName);
        }
```

You see our good friend `ASeq` at the front, followed by `LazySeq`.  (I believe the structure here of splitting off what presumably are the less common cases is designed to make `seq` itself inlined by the compiler/jitter.)  The problem is that `ASeq` really needs `RT.seq` for  itself.  As do `Cons`, `EmptyList`, and `PersistentList`, either directly or through `RT.first`, `RT.count` and other `RT` methods that use `RT.seq`.

If this was all there was to it, I can think of some easy solutions, such as having two versions of `RT.seq`, one for `Cons`, `EmptyList` and `PersistentList`, and another for everyone defined after (perhaps with a cyclic dependency on `ASeq`.)    Or we could indirect through a static mutable binding to be updated at a later point.  

But took a close look. `RT.seq` uses `ArraySeq`, `StringSeq` and `RT.chunkEnumeratorSeq`.  And guess what?  Those all are themselves or create things that are based on `ASeq`.

Shall we do an inventory of our cylic dependencies?

- `Cons`
- `EmptyList`
- `PersistentList`
- `ASeq`
- `ArraySeq` derivates  (There are a bunch, include one for each primitive numeric type.)
- `StringSeq`
- `ChunkedCons` -- used by `RT.chunkEnumeratorSeq`
- `RT.seq()`

Even if we split off the first three as discussed in the last post, we still have a mess on our hands.

The multiple versions of `RT.seq` solution seems unachievable.  Indirecting through a static mutable binding is possible but a little distasteful.

The best solution likely is protocols.  If you are familiar with protocols in Clojure, basically some variation on that theme. Here, types would be registered as supporting the protocol corresponding to `RT.seq`.  

You will recall my mentioning in  [ClojureCLR reconsidered]({% post_url 2023-01-06-clojureclr-reconsidered %}) that Rich Hickey would use protocols from the bottom up if rewriting Clojure from scratch.  Here's example number one.

 Based on what is in ClojureCLR right now,  our version would need enchancements to handle generic classes (open or closed), maybe for types that are `.IsArray`, and some other variations.  It would need to be performant.

 I don't know how to do that yet.  And I don't want to wait until I figure it out.  So the way forward is just to hack in a solution for `RT.seq` that will get us out of this cycle of de ... spair.  The hack will be a version of `RT.seq` that has a lookup mechanism in which participating types can register in.  

## RT.printString

And we're going to come the opposite conclusion on this one.

`RT.printString` is called by pretty much all the collection data structures in their `ToString` methods.  The difficultyof writing this method ahead of the collections is that it relies on machinery that will be created later, `MultiFn` and `Var` in particular.  It is definitely not possible to implement those dependencies ahead of collections.

`RT.printString` is extensible via `MultiFn`.  The code for defining in the extensions is in the Clojure source, specifically `clojure/core_print.clj`.  And `RT.printString` has a fallback in case it is used prior to that file being loaded, in `RT.print` which does the real work.  In the C# source,

```C#
static public void print(Object x, TextWriter w)
{
    //call multimethod
    if (PrintInitializedVar.isBound && RT.booleanCast(PrintInitializedVar.deref()))
    {
        PrOnVar.invoke(x, w);
        return;
    }

    bool readably = booleanCast(PrintReadablyVar.deref());

    // A bunch of code to print things in a default way
```

There is a dependence on two Vars, `*print-initialized*` and `*print-readably*` in this code.
We'll have to finesse those calls until `Var` has been defined.  I'll have to come up with some workaround, probably involving a mutable binding that later initialization can modify.
There is just no way to bring the use of `Var` and `MultiFn` into the code at this point without creating a cyclic dependency involving more types than I can count.


## Summary

The `Util` class seems relatively straightforward other than its reliance on `clojure.lang.Numbers`.  We will analyze that in an upcoming post, along with a look at hashing and equality in Clojure.

As for `RT`, of the things we need right now `RT.seq` and `RT.printString` are the problems areas.  `RT.seq` we will look at solving by having an extensibilty mechanism.  `RT.printString` will require a way to work around the lack of `Var` at the beginning.  

Both solutions to our `RT` problem are going to require initializations further along in the source code when the needed pieces become available.   THis suggests ultimately some kind of static initializer that must be triggered before using the collections.  That is not a problem when the collections are being used in the context of ClojureCLR as a whole -- we already have `RT.init` being called.  It could be a problem in a testing context when we are isolating the collections.  I'll work it out.

What is clear is the monoliths of `Util` and `RT` will not exist as single modules in this rewrite.  These are not advertised interfaces and outside of the base (C#/F#/Java) source code should be referenced only in the Clojure source code of `core.clj` and the like.  But we already do plenty of platform-specific modifications to that code.  A little more won't be a bother.  But we should exercise some care in how we package this functionality into discrete modules.







