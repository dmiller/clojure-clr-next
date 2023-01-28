---
layout: post
title: First code
date: 2023-01-27 05:00:00 -0500
categories: general
---

We have working code.

I have tagged a recent commit to give [a snapshot of the code at this point](https://github.com/dmiller/clojure-clr-next/tree/snapshot-1/src/Clojure.Next).  Given the possibility that some decisions will be revisited and some of this code revised, this snapshot preserves this first cut.

## The content

The code here is all F#.  Mostly it is ported from the C# ClojureCLR implementation, but with an eye to writing idiomatic F#.  Given how it will be used, I had do some things one might avoid in a closed F# implementation.  For example, given how `nil` (= `null`) is used in Clojure, I could not think of how to avoid a liberal sprinkling of  the `[<AllowNullLiteral>]` attribute on interfaces and classes.  Tuple arguments are used often instead of curried functions.    Because of overloading, some groups of functions that could have been gathered in a module are in a static (sealed abstract) class.  When I understand the code better, I'll revisit some of this.  But I wanted to make some progress now.

For those with some familiarity with the existing Clojure implementations,  know that I have restructured the code substantially.  There are multiple projects.  I have tried to use namespaces appropriately  This will have consequences when it comes to doing the Clojure interop that you see in `core.clj` and company, but given that these internals are to be accessed by Clojure users through the `clojure.core` environment,  this should remain

This first snapshot has the following functionality.

- `Clojure.BigArith` -- this project (and namespace) provides 

    - `BigDecimal` - implements an immutable, arbitrary precision, signed decimal.  This implementation was inspired by the [General Decimal Arithmetic Specification](http://speleotrove.com/decimal/decarith.html).  I did not implement the entire specification.  For one thing, it is big and hairy.  For another, I really only needed to miimic the API of `java.math.BigDecimal`, as that is all Clojure uses.  My implementation is close to the X3.274 subset of the GDAS, meaning I do not implement infinite values, NaNs, subnormal values, and negative zero, along with few other things. I'd have to go read a book to find out what a subnormal value is.  Maybe someday.

        My C# implementation of `BigDecimal`  was based on my own implementation of a `BigInteger` class.  Back in the day, I didn't find a usable `BigInteger` implemenetation, so I just built my own.  I based this new implementation of `BigDecimal` on `System.Numerics.BigInteger`.  I didn't not bother porting my `BigInteger`.

    - `Rational` -- Not used in Clojoure.  Clojure has its own, rather limited, `clojure.lang.Ratio`.  Mine is cooler, but no matter; I just wrote this for fun.
 
- `Clojure.Numerics` contains:

    - `Murmur3` - An implementation of the Murmuer3 hashing API.  Clojure on the JVM imported the Guava Murmur3 implementation and made some changes.  I copied the API and based the algorithms on the description on [Wikipedia](http://en.wikipedia.org/wiki/MurmurHash).  See also [SMHasher](https://github.com/aappleby/smhasher).  Murmur3 provides the mechanics for our `hasheq` implementations.

    - `Ratio` - see above.  This is minor.  It doesn't even implement its own arithmetic, relying on `Numbers` for that.

    - `Converters` - I pulled the code used to convert `Object`s to a numeric type (when possible) from `clojure.lang.Util` to this project.  Makes more sense here, and I can use it here.  This is part of the deconstruction of the massive `Util` and `RT` classes in order to decouple types.

    - `Numbers` -- the big enchilada. Described in the previous posts.  Big.  I may have a few more unit tests to write.  But there are over 400 tests now.  I'm feeling pretty good about it.

    - `Hashing` -- I moved the utility code supporting hashing from `Util` to here.

- `Clojure.Collections` -- The start of the implementation of all the Clojure collections.  In this snapshot:

    - The interfaces -- you've been introduced to `Seqable`, `IPersistentCollection`, and `ISeq`, but they are joined by essentially all the other interfaces that are used (I counted 32 more.)

    - `AFn` -- an abstract base class to help with defining `IFn` derivatives.  I'll post about this some other time.   I needed it early on to help test `IReduce` for `IPersistentCollection`. Those tests are not in this snapshot.  A post for another day.

    - `SeqEnumerator` -- provides enumerators (= Java iterator) for `ISeq`'s.  `ASeq` needs this and it can be implemented without any dependencies, other than on the interface `ISeq` itself.

    - The core types to enable us to properly define the many sequential data structures to come:

        - `ASeq`
        - `Cons`
        - `EmptyList`
        - `PersistentList`

        As described in a previous post, these are mutually dependent.

    - Enough bits and pieces of `RT` and `Util` to get us going.  Only a vestigial ipmlementation of `RT.seq`, as discussed.  Enough to get started, but I don't have the extension mechanism defined yet.  That will have to be soon, as we will be adding to its repertoire soon.

    - `LazySeq` --I had a version of it from 18 months ago when I first started playing with this stuff, so I just tossed it in.)  Soon we'll need to have a little chat about laziness, chunking, and reducing.

There are test suites written in Expecto for each of the projects.  They are executables. You can just run them.


## Some random notes on the code text

Naming of functions and methods is a complete shambles, as it is for  the C# code for ClojureCLR today.  From the beginning of that project I was faced with reconciling the different capitalization standards of Java and C#.  I ended up with a mixture.  I kept some names with the initial lowercase just so I would have to do less rewriting the Clojure source; other places, I went with intial uppercase.   It's a mess.  I regret it.

And this code is a mess, too.  And I don't care.  For now. I don't wan I nt to think about it at this point.  I'll have a big renaming party eventually.

Coming a little to my own defense, the situation in F# is a little confused, too.  There are coding guidelines for F# that are pretty clear, at least when the project is F# only.  Other rules come into play if you are creating a library for outside consumption, from C# say. Obviously, there are no rules for writinng F# for Clojure interop. 

When I've written enough code, I'll have a better sense of the rules I will abide by.  In the meantime, just know that I know.  Don't judge.


## What's next

At this point, it should be pretty easy to march through many the sequential data structures that rely on `ASeq`.  That includes `ArraySeq`, `ChunkedCons`, `Cycle`, `EnumeratorSeq`, `Iterate`, `LongRange`,  `Range`, `Repeat`, and `StringSeq`.  I may do some of these just take the occasional break from our next big adventure: maps.
