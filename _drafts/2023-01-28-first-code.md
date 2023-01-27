---
layout: post
title: First code
date: 2023-01-24 00:00:00 -0500
categories: general
---

We have working code.

I have tagged a recent commit to give a snapshot of the code at this point.  Given the possibility that some decisions will be revisited and some of this code revised, this snapshot preserves the first cut.

## Some random notes on the code

I have structured this code in a way that makes some logical sense, to me at least.  There are multiple projects.  I have tried to use namespaces appropriately, meaning no single `clojure.lang` namespace.   This will have consequences when it comes to doing the Clojure interop down to these classes that you see in `core.clj` and company, but I'll deal with that later.

Naming of functions and methods is a complete shambles, as it is the C# code for ClojureCLR today.  From the beginning of that project I was faced with reconciling the different capitalization standards of Java and C#.  I ended up with mix.  I kept some names with the initial lowercase just so I would have to do less rewriting the Clojure source; other places, I went with intial uppercase.   It's a mess.

And this code is a mess, too.  And I don't care.  I don't want to think about it at this point.  I'll have a big renaming party somewhere down the line when I come to a decision.

Coming a little to my own defense, the situation in F# is a little confused.  There are structural guidelines for F# that are pretty clear, at least when the project is F# only.  Other rules are suggested if you are creating a library for outside consumption, from C# say.  And I am definitely in that situation.  Not just capitalization.  Do you use curried arguments or use tuples for your functions?  Do you use a module or a static (= sealed abstact in F#) class?  Do you let things like `option` escape?  

When I've written enough code, I'll have better answers to these questions.  In the meantime, just know that I know the code has some messiness.  Don't judge.

## The content

Here are the pieces available in this first snapshot.  These are mostly ported from teh C# code.

- `Clojure.BigArith` -- this project (and namespace) provides 
    - `BigDecimal` - implements an immutable, arbitrary precision, signed decimal.  This implementation was inspired by the [General Decimal Arithmetic Specification](http://speleotrove.com/decimal/decarith.html).  I did not implement the entire specification.  For one thing, it is big and has a lot of functionality.  For another, I really only needed to miimic the AIP of `java.math.BigDecimal`, as that is all Clojure uses.  Therefore, my implementation is close to the X3.274 subset of the GDAS, meaning I do not implement infinite values, NaNs, subnormal values, and negative zero, along with few other things.  Maybe someday.

        My C# implementation was based on my own implementation of a `BigInteger` class.  Back in the day, I didn't have one to pull one.  I based this new implementation on `System.Numerics.BigInter`.  That also means I didn't have to port my `BigInteger`.

    - `BigRational` -- Not used in Clojoure.  Clojure has its own, rather limited, `clojure.lang.Ratio`.  Mine is cooler, but who cares.  I wrote this for fun.
 
- `Clojure.Numerics` contains:
    - `Murmur3` - An implementation of the Murmuer3 hashing API.  Clojure on the JVM imported the Guava Murmur3 implementation and made some changes.  I copied the API sand based the algorithm on the description on [Wikipedia](http://en.wikipedia.org/wiki/MurmurHash).  See also [SMHasher](https://github.com/aappleby/smhasher).  Murmur3 provides the mechanics for our `hasheq` implementations.

    - `Ratio` - see above.  This is minor.  It doesn't even implement its own arithmetic, relying on `Numbers` for that.

    - `Converters` - I pulled the code used to convert `Object`s to a numeric type (when possible) from `clojure.lang.Util` to here so that it could be used downstream.  Part of the deconstruction of the massive `Util` and `RT` classes in order to decouple types.

    - `Numbers` -- the big enchilada. Described in the previous posts.  Big.  Still writing unit tests, but I have the nasty arithmetic parts covered pretty well.

- `Clojure.Collections` -- The start of the implementation of all the Clojure collections.  In this snapshot:

    - The interfaces -- you've been introduced to `Seqable`, `IPersistentCollection`, and `ISeq`, but they are joined by essentially all the other interfaces that are used (I counted 32 more.)

    - `AFn` -- an abstract base class to help with defining `IFn` derivatives.  I'll post about this some other time.   I needed it early on to help test `IReduce` for `IPersistentCollection`. (ditto)

    - `SeqEnumerator` -- provides an enumerator over an `ISeq`.  `ASeq` needs this and it can be implemented without any dependencies, other than on the interface `ISeq` itself

    - The core implementation to enable us to properly define the many sequential data structures to come.

        - `ASeq`
        - `Cons`
        - `EmptyList`
        - `PersistentList`

    - Enough bits and pieces of `RT` and `Util` to get us going.  Only a vestigial ipmlementation of `RT.seq`, as discussed.  Enough to get started, but I don't have the extension mechanism defined yet.  That will have to be soon, as we will be adding to its repertoire soon.

    - `LazySeq` -- I just tossed this in.  (I had a version of it from 18 months ago when I first started playing with this stuff.)  We'll need to have a little talk about laziness, chunking, and reducing soon.

There are test suites written in Expecto for each of the projects.  They are executables. You can just run them.

## What's next

At this point, it should be pretty easy to march through many the sequential data structures that rely on `ASeq`.  That includes `ArraySeq`, `ChunkedCons`, `Cycle`, `EnumeratorSeq`, `Iterate`, `LongRange`,  `Range`, `Repeat`, and `StringSeq`.  I may do some of these just take a break from while off on our next big adventure: maps.
