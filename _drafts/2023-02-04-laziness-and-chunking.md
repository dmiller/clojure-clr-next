---
layout: post
title: Laziness and chunking
date: 2023-02-04 00:00:00 -0500
categories: general
---

Laziness is a central concept in the handling of sequences in Clojure.  Chunking comes along as an efficiency measure.  Surprisingly, at the level of implementation we are looking at it, very little needs to be done; laziness is defined most in the Clojure code that builds `clojure.core`. We'll take a look at what is needed at the bottom to support laziness and chunking.

##  Introduction

Laziness and chunking permeate the sequence machinery in Clojure.  There are numerous resources explaining the general concept.  One will also discover it is a source of confusion for beginners.  For our purposes, [Laziness in Clojure](https://clojure-doc.org/articles/language/laziness/) will suffice.

There are several useful exercises you can undertake to get a sense of how laziness and chunking are defined.

- Search `core.clj` in the Clojure source code for 'lazy' and 'chunk'.
- Use the [Cheatsheet](https://clojure.org/api/cheatsheet).  The secion 'Creating a Lazy Seq' seems promising.  Click on any function there to get the doc; the doc page has a link to the source.  Note that no function with 'chunk' in its name is listed; they are not commonly used.

## In the Clojure source

Look for `lazy-seq` in the Clojure source cod3. The macro `lazy-seq` just wraps its body as `(fn* [] body)` and passes it to the constructor for `LazySeq`.  `lazy-seq` occurs in the definitions of `concat`, `map`, `filter`, `take`, `take-while`, `drop`, `drop-while`, `line-seq`, `partition`, `take-nth`, `interleave`, `lazy-cat` (aren't they all?), `for`, `re-seq`, `tree-seq`, `distinct`, `repeatedly`, `pmap`, `partition-by`, `reductions`, `partition-all`, `partitionv`, `partitionv-all`, `map-indexed`, `keep`, `keep-indexed`, and `iteration`. (I make no claim that this list is exhaustive; mistakes may have been made.)

One of the simplest uses of `lazy-seq` is in `repeatedly`:

```Clojure
(defn repeatedly
  "Takes a function of no args, presumably with side effects, and
  returns an infinite (or length n if supplied) lazy sequence of calls
  to it" 
  {:added "1.0"
   :static true}
  ([f] (lazy-seq (cons (f) (repeatedly f))))
  ([n f] (take n (repeatedly f))))
```
I can guarantee you do not want to try to realize an infinite sequence.  Without the `lazy-seq`, this would result immediately in an infinite recursion. 

 You will note in the Clojure code that `lazy-seq`  and chunking appear frequently together.  Here is a piece of the code for the function `map`:

```Clojure
 ([f coll]
   (lazy-seq
    (when-let [s (seq coll)]
      (if (chunked-seq? s)
        (let [c (chunk-first s)
              size (int (count c))
              b (chunk-buffer size)]
          (dotimes [i size]
              (chunk-append b (f (.nth c i))))
          (chunk-cons (chunk b) (map f (chunk-rest s))))
        (cons (f (first s)) (map f (rest s)))))))
```

A lot of `chunky`-inesss in there.  `chunked-seq`, `chunk-first` and the rest are defined  right after `lazy-seq`:

```Clojure
(defn ^:static ^clojure.lang.ChunkBuffer chunk-buffer ^clojure.lang.ChunkBuffer [capacity]
  (clojure.lang.ChunkBuffer. capacity))

(defn ^:static chunk-append [^clojure.lang.ChunkBuffer b x]
  (.add b x))

(defn ^:static ^clojure.lang.IChunk chunk [^clojure.lang.ChunkBuffer b]
  (.chunk b))

(defn ^:static ^clojure.lang.IChunk chunk-first ^clojure.lang.IChunk [^clojure.lang.IChunkedSeq s]
  (.chunkedFirst s))

(defn ^:static ^clojure.lang.ISeq chunk-rest ^clojure.lang.ISeq [^clojure.lang.IChunkedSeq s]
  (.chunkedMore s))

(defn ^:static ^clojure.lang.ISeq chunk-next ^clojure.lang.ISeq [^clojure.lang.IChunkedSeq s]
  (.chunkedNext s))

(defn ^:static chunk-cons [chunk rest]
  (if (clojure.lang.Numbers/isZero (clojure.lang.RT/count chunk))
    rest
    (clojure.lang.ChunkedCons. chunk rest)))
  
(defn ^:static chunked-seq? [s]
  (instance? clojure.lang.IChunkedSeq s))
```

We can see that `ChunkBuffer`, `IChunkedSeq`, and `ChunkedCons` are in our immediate future.  

We can glean a few clues about them by looking at the `map` code.  We have two cases depending on whether the sequence we are going to map over is _chunky_ or _smooth_ (had to be said).  The smooth case is what you think `map` should do:

```Clojure
(cons (f (first s)) (map f (rest s)))
```

Note that laziness is in here already.  `f` will be applied to `(first s)` when this node is _realized_, but the recursive call to `map` results in a lazy sequence again, so the `f` will not applied until the next value is required.

For the chunking piece, we see a parallel:

```Clojure
(chunk-cons (chunk b) (map f (chunk-rest s)))
```

That `b` is filled first with result of calling `f` on every item in the first chunk of the chunked sequence:

```Clojure
(dotimes [i size]
  (chunk-append b (f (.nth c i))))
```

This is the heart of chunking.  Rather than just apply `f` once at a time, do a number of them all at once.  `f` may get called more than it would on a non-chunked basis, but presumably a price you are willing to pay for avoiding the overhead of creating sequence elements for all the items in the chunk.

## In the basement

Let's dig in.  `LazySeq` is quite easy, ignoring a few distractions.  (`LazySeq` does _not_ derive from `ASeq`, so it has supply all the goodies it would otherwise inherit.  I'll leave off the implementation code for `System.Collections.IList` and `System.Collections.ICollection`.  Boring, really.)

Easy does not equate to obvious.

```F#


[<Sealed; AllowNullLiteral>]
type LazySeq private (m1, fn1, s1) =
    inherit Obj(m1)
    let mutable fn: IFn = fn1
    let mutable s: ISeq = s1
    let mutable sv: obj = null
    new(fn: IFn) = LazySeq(null, fn, null)
    private new(m1: IPersistentMap, s1: ISeq) = LazySeq(m1, null, s1)
```

The only public constructor takes an `IFn`.  One you get around to needing a value from this sequence, `fn1.invoke()` will be called to generate ... something. At that time, `fn1` will be set to `null`  -- we are done with it.  And that flags this `LazySeq` as being _realized_ (Clojure function `realized?` called on it will return `true`)

```F#
    interface IPending with
        member _.isRealized() = isNull fn
```

The value that `fn1.invoke()` returns is cached temporarily in `sv`.  Note this is an `Object`, not necessarily an `ISeq`.  We are only part of the way there.  This invocation and field mutation is done in member `sval`:

```F#
    member _.sval() : obj =
        if not (isNull fn) then
            sv <- fn.invoke ()
            fn <- null

        match sv with
        | null -> upcast s
        | _ -> sv
```

Where does `sval` get called?  From `seq()`:

```F#

    interface Seqable with

        [<MethodImpl(MethodImplOptions.Synchronized)>]
        override this.seq() =
            let rec getNext (x: obj) =
                match x with
                | :? LazySeq as ls -> getNext (x.sval ())
                | _ -> x

            this.sval () |> ignore

            if not (isNull sv) then
                let ls = sv
                sv <- null
                s <- RT0.seq (getNext (ls))

            s
```

Why does this important action (calling `sval`) occur here, and what does it imply?
If any of the Clojure sequence functions need something from us, either to process an element or even just to check if we are empty, they will call `seq` on us.
And within `LazySeq` itself, all the `ISeq` methods call `seq()`:

```F#
    interface ISeq with
        member this.first() =
            (this :> ISeq).seq () |> ignore
            if isNull s then null else s.first ()

        member this.next() =
            (this :> ISeq).seq () |> ignore
            if isNull s then null else s.next ()

        member this.more() =
            (this :> ISeq).seq () |> ignore

            if isNull s then upcast PersistentList.Empty else s.more ()
```

These are straightforward.  But what is `seq()` doing?


