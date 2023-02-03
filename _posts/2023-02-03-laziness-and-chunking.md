---
layout: post
title: Laziness and chunking
date: 2023-02-03 12:00:00 -0500
categories: general
---

Laziness is a central concept in the handling of sequences in Clojure.  Chunking comes along as an efficiency measure.  Surprisingly, at the level of implementation we are looking at it, very little needs to be done; laziness is defined most in the Clojure code that builds `clojure.core`. We'll take a look at what is needed at the bottom to support laziness and chunking.

##  Introduction

Laziness and chunking permeate the sequence machinery in Clojure.  There are numerous resources explaining the general concept. (Searching around for those resources, one will discover these topics are a source of confusion for beginners.)  For our purposes, [Laziness in Clojure](https://clojure-doc.org/articles/language/laziness/) will suffice.

There are several useful exercises to prepare for what follows:

- Search `core.clj` in the Clojure source code for 'lazy' and 'chunk'.
- Use the [Cheatsheet](https://clojure.org/api/cheatsheet).  The secion 'Creating a Lazy Seq' seems promising.  Click on any function there to get the doc; the doc page has a link to the source.  Note that no function with 'chunk' in its name is listed; they are not commonly used.

## In the Clojure source

Look for `lazy-seq` in the Clojure source code. The macro `lazy-seq` turns its argument into `(fn* [] body)` and passes that to the constructor for `LazySeq`.  `lazy-seq` occurs in the definitions of `concat`, `map`, `filter`, `take`, `take-while`, `drop`, `drop-while`, ... . You get the idea.  (I'd like to point out `lazy-cat`; notice this is a statement, not a question.) 

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

We'll discuss the underlying interfaces and classes below.

We can glean a few clues about chunking by looking at the `map` code.  There are two cases depending on whether the sequence we are going to map over is _chunky_ or _smooth_ (had to be said).  The smooth case is what you think `map` should do: create a sequence with `f` applied to each element.  Defined recursively as:

```Clojure
(cons (f (first s)) (map f (rest s)))
```

Note that laziness is crucial here.  `f` will be applied to `(first s)` when this node is _realized_, but the recursive call to `map` results in a lazy sequence again, so the `f` will not applied until the next value is required.

For the chunking piece, we see a parallel:

```Clojure
(chunk-cons (chunk b) (map f (chunk-rest s)))
```

That `b` is filled first with result of calling `f` on every item in the first chunk of the chunked sequence:

```Clojure
(dotimes [i size]
  (chunk-append b (f (.nth c i))))
```

Thus `b` plays the role of `(f (first s))`.
This is the essence of chunking.  Rather than just apply `f` once at a time, do a number of them all at once.  `f` may get called more than it would on a non-chunked basis, but presumably thia a price you are willing to pay for avoiding the overhead of creating sequence elements for all the items in the chunk.

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

    private new(m1: IPersistentMap, s1: ISeq) = LazySeq(m1, null, s1)

    new(fn: IFn) = LazySeq(null, fn, null)
```

The only public constructor takes an `IFn`.  One you get around to needing a value from this sequence, `fn1.invoke()` will be called to generate ... something. At that time, `fn1` will be set to `null`  -- we are done with it.  Doing so is a flags that this `LazySeq` has been _realized_ (Clojure function `realized?` called on it will return `true`.)

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

The `if` expression does the invocation if it hasn't been done already.
The `match` returns either `sv` or `s`. 
You have to see the rest of the code (below) to piece this together, but
in essence if `sv` is not null, then we have not gone all the way to get a sequence.
If `sv` is null, then `s` holds the sequence. (Which could be `null` if the sequence is empty.)

Where does `sval` get called?  From `seq()`:

```F#
    interface Seqable with

        [<MethodImpl(MethodImplOptions.Synchronized)>]
        override this.seq() =

            this.sval () |> ignore

            if not (isNull sv) then

                let rec getNext (x: obj) =
                    match x with
                    | :? LazySeq as ls -> getNext (ls.sval ())
                    | _ -> x

                let ls = sv
                sv <- null
                s <- RT0.seq (getNext ls)

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

These are straightforward.  But what is `seq()` doing?  It calls `sval` for the potential side-effect of calling `fn1` to realize the sequence.  At that point, if `sv` is null, we have our sequence in `s`.  However, if `sv` is not null, we need to do a little more work.
We grab `sv`'s value, set `sv` to `null` to indicate we will have computed the final sequence, then call our little internal function `getNext`, a recursive loop to work though a potential chain of `LazySeq`s until we get a 'real' sequence, or at least something we can call `RT.seq()` on.  (Remember `RT.seq()`?)  Now we are realized (`fn1` has been invoked), and we have tracked through to a sequence.  We are good to go.

You might ask if that little loop is necessary.  First, `LazySeq`'s being nested are quite common.  (Trust me.) By separating `sval` from `seq`, we can avoid unnecessary calls to `seq` on the intervening `LazySeq`s.  Definitely worth it.

And that's pretty much it for `LazySeq`.  There are some cute consequences of some parts of the encoding.  For example, if you want to add metadata via `IObj.withMeta()':

```F#
    interface IObj with
        override this.withMeta(meta: IPersistentMap) =
            if obj.ReferenceEquals((this :> IMeta).meta (), meta) then
                this :> IObj
            else
                LazySeq(meta, (this :> ISeq).seq ()) :> IObj
```

You can't do that without realizing the `LazySeq`; see that call to `seq()`.  This explains the one private constructor that takes a `PersistentMap` and an `ISeq`.  The `LazySeq` it constructs has `fn1` set to `null` (we're realized), `sv` set to `null` (we've tracked through to our 'real' sequence), and `s` set to the 'real' sequence.


## Chunking

All that work and we haven't gotten to chunking yet.
The basics are straightforward.  A collection indicates support for chunking by implementing the `IChunkedSeq` interface.

```F#
[<AllowNullLiteral>]
type IChunkedSeq =
    inherit ISeq
    inherit Sequential
    abstract chunkedFirst: unit -> IChunk
    abstract chunkedNext: unit -> ISeq
    abstract chunkedMore: unit -> ISeq
  ```

  which looks a lot like `ISeq`.  Think of a chunked sequence as, well, a sequence of chunks, where a chunk is one of these:

```F#
[<AllowNullLiteral>]
type IChunk =
    inherit Indexed
    abstract dropFirst: unit -> IChunk
    abstract reduce: f: IFn * start: obj -> obj
```

By inheriting `Indexed`, it picks up `count()` and two flavors of `nth`, giving us direct access to the `count()` number of elements in the buffer.  We usually build a chunk by first creating a `ChunkBuffer`:

```F#
[<Sealed>]
type ChunkBuffer(capacity:int) =

    let mutable buffer : obj array = Array.zeroCreate capacity
    let mutable cnt : int = 0

    interface Counted with
        member _.count() = cnt

    member _.add(o:obj) = 
        buffer[cnt] <- 0
        cnt <- cnt+1

    member _.chunk() : IChunk =
        let ret = ArrayChunk(buffer,0,cnt)
        buffer <- null
        ret
```

which allocates an array and allows adding elements to it.  And then you call`chunk()` on it to create an `ArrayChunk` that implements `IChunk`.

```F#
[<Sealed>]
type ArrayChunk(arr:obj array,offset:int ,iend:int) =
    
    new(arr,offset) = ArrayChunk(arr,offset,arr.Length)


    interface Counted with
        member _.count() = iend-offset


    interface Indexed with
        member _.nth(i) = arr[offset+i]
        member this.nth(i,nf) =
            if 0 <= i && i < (this:>Counted).count() then  
                (this:>Indexed).nth(i)
            else
                nf

    interface IChunk with
        member _.dropFirst() =
            if offset = iend then
                raise <| InvalidOperationException("dropFirst of empty chunk")
            else
                ArrayChunk(arr,offset+1,iend) 

        member _.reduce(f,start) =
            let ret = f.invoke(start,arr[offset])
            let rec step (ret:obj) idx =
                match ret with  
                | :? Reduced -> ret
                | _ when idx >= iend -> ret
                | _ -> step (f.invoke(ret,arr[idx])) (idx+1)
            step ret (offset+1)
```

Note than an `ArrayChunk` has `count()` and `nth(*)` for getting its elements.
`dropFirst()` gives a new `ArrayChunk` on the same array with a new starting point in the array.  Reduction will talk about in a later post.

The last piece of the puzzle is `ChunkedCons`:

```F#
type ChunkedCons(meta:IPersistentMap, chunk:IChunk, more:ISeq) =
    inherit ASeq(meta)

    new(chunk,more) = ChunkedCons(null,chunk,more)

    interface IObj with 
        override this.withMeta(m) =
            if obj.ReferenceEquals(m,meta) then
                this
            else
                ChunkedCons(m,chunk,more)

    interface ISeq with
        override _.first() = chunk.nth(0)
        override this.next() =
            if chunk.count() > 1 then
                ChunkedCons(chunk.dropFirst(),more)
            else 
                (this:>IChunkedSeq).chunkedNext()
        override this.more() =
            if chunk.count() > 1 then
                ChunkedCons(chunk.dropFirst(),more)
            elif isNull more then
                PersistentList.Empty
            else
                more

    interface IChunkedSeq with
        member _.chunkedFirst() = chunk
        member this.chunkedNext() = (this:>IChunkedSeq).chunkedMore().seq()
        member _.chunkedMore() =
            if isNull more then
                PersistentList.Empty
            else 
                more
```

It gets most of its goodness from `ASeq` and otherwise looks somewhat like `Cons` except that its first 'element' is actually a chunk.  `first` grabs the `nth(0)` element of that chunk, while `next()` does a `dropFirst` to  move on, unless we've reached the end of the leading chunk, in which case we move to what follows.

## Our chunky collections

Only three collections down in the basement (other than `ChunkedCons`) implement `IChunkedSeq`: `Range`, `LongRange`, and `PersistentVector`.

I have `LongRange` and `Range` completed, but this part of the code is too messy to be very edifying.  Chunking is actually used in an essential manner in these classes, however.  Here is one snippet to give you a flavor:

```F#
  let arr: obj array = Array.zeroCreate Range.CHUNK_SIZE
  let lastV, n = fillArray startV arr 0
  chunk <- ArrayChunk(arr, 0, n)
```
`fillArray` fills values into the array up to size `Range.CHUNk_SIZE`, and returns the next starting value and how many elements were put into the array. (That might be less than `Range.CHUNK_SIZE` if we are at the end of the range.)  And then we create a chunk.

We'll cover the `PersistentVector` implementation of this when we get to that class.  That's a lot more fun, actually, because a `PersistentVector` essentially is implemented directly in a chunky manner, so that mapping to `IChunkedSeq` is very natural.

Enough.