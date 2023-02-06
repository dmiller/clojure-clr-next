---
layout: post
title: Reductionism
date: 2023-02-05 12:00:00 -0500
categories: general
---

You can be a boss at reducers if you know this one weird trick!

At least at one point I had hoped that was true.   It turns out that getting reducers right requires thinking it through _every single time_ you are confronted with new one.  But I think we can come up with enough guidance so that after a few examples, we won't really need to look at the reducers in collections to come;  you'll be able to understand them and verify them yourself.



## Background

If you look for 'clojure reduce' in your search engine of choice, you might run across [Reducers](https://clojure.org/reference/reducers).
Reducers are a very useful suite of functions that should definitely be in the arsenal of any Clojure programmer, but we have a simpler aim: [clojure.core/reduce](https://clojuredocs.org/clojure.core/reduce).

There are two forms of `reduce`, with two and three parameters.  The two-parameter form takes a reducing function (of two arguments) that is applied first to the first two items of the collection, then to the result of that invocation and the third item, that result and the fourth item, etc., until the end of the collection is reached and the accumulated result is returned.

```Clojure
(reduce '+ [1 2 3 4 5] ; =>15
```
Effectively it computes `(((1+2)+3)+4+5)`.

The three-paremeter version supplies a starting value that is supplied to the reducing function along with the first item, and so on.

```Clojure
(reduce '+ 10 [1 2 3 4 5] ; =>25
```
Effectivly it computes `((((10+1)+2)+3)+4+5)`. 


Here is the contract for `reduce`:

> f should be a function of 2 arguments. If val is not supplied,
returns the result of applying f to the first 2 items in coll, then
applying f to that result and the 3rd item, etc. If coll contains no
items, f must accept no arguments as well, and reduce returns the
result of calling f with no arguments.  If coll has only 1 item, it
is returned and f is not called.  If val is supplied, returns the
result of applying f to val and the first item in coll, then
applying f to that result and the 2nd item, etc. If coll contains no
items, returns val and f is not called.

The code for `reduce` in `core.clj` is indirect:

```Clojure
(defn reduce
  ([f coll]
     (if (instance? clojure.lang.IReduce coll)
       (.reduce ^clojure.lang.IReduce coll f)
       (clojure.core.protocols/coll-reduce coll f)))
  ([f val coll]
     (if (instance? clojure.lang.IReduceInit coll)
       (.reduce ^clojure.lang.IReduceInit coll f val)
       (clojure.core.protocols/coll-reduce coll f val))))
```

A collection implements the `IReduce` and/or `IReduceInit` interfaces to provide a specialized, presumably more efficient, reduction algorithm.
Otherwise, the magic of protocols is used to extend reduction to types that do not have these interfaces defined.  That code is in `clojure.core.protocols`.  The protocols aspect is not our focus here;  we are interested in how to implement the interfaces in our collections.

## The interfaces

And here they are:

```F#
[<AllowNullLiteral>]
type IReduceInit =
    abstract reduce: IFn * obj -> obj

[<AllowNullLiteral>]
type IReduce =
    inherit IReduceInit
    abstract reduce: IFn -> obj
```

 `IReduceInit.reduce` take a function and start argument.  `IReduce.reduce` just takes the reducing function.  The missing argument is the collection itself, which is `this`.


##  Feeling reduced, but not diminished

Before we get to code, we need to have a little chat about `Reduced`.  It figures significantly in the code we are about to write.

It is hard to find information about `Reduced`.  I checked five books on Clojure and found nary a mention.  The most prominent mention of `Reduced` is in the reference on [Transducers](https://clojure.org/reference/transducers#_early_termination).

 `Reduced` is used to stop reductions early. From the _Transducers_ article:

> Clojure has a mechanism for specifying early termination of a reduce:
>
> ...
>
>A process that uses transducers must check for and stop when the step function returns a reduced value (more on that in Creating Transducible Processes). Additionally, a transducer step function that uses a nested reduce must check for and convey reduced values when they are encountered.

A _reduced value_ is literally an object of type `Reduced`.  It just wraps a value, making it available through the `deref` method of the `IDeref` interface:

```F#
type IDeref =
    abstract deref: unit -> obj

[<Sealed>]
type Reduced(value) =
    interface IDeref with
        member _.deref() = value
```

You can read the _Transducers_ article for reasons for using this.  For one thing, it is the only way to run a reduction over an infinite collection -- you have to send a signal that you've had enough. 

__One essential rule when writing a `reduce` method (for `IReduce` and `IReduceInit`):__  after each invocation of the reduction function, check the result to see if it is an instance of `Reduced`; if so, stop immediately and return the `deref` value.

Note: if you are actually writing transducers, you might need to be passing back the `Reduced` object itself.  This is not our concern.  Our rule is only for `IReduceInit.reduce` and `IReduce.reduce`.


## Some code

I'm going to start with some of the original C# code (essentially identical to the Java code) to see the problems we have with making a translation to F#.  One of the simplest comes from `PersistentList`:

```C#
public object reduce(IFn f)
    {
        object ret = first();
        for (ISeq s = next(); s != null; s = s.next()) { 
            ret = f.invoke(ret, s.first());
            if (RT.isReduced(ret))
                return ((IDeref)ret).deref();
        }
        return ret;
    }

public object reduce(IFn f, object start)
{
    object ret = f.invoke(start, first());
    for (ISeq s = next(); s != null; s = s.next()) {
        if (RT.isReduced(ret))
            return ((IDeref)ret).deref(); 
        ret = f.invoke(ret, s.first());
    }
    if (RT.isReduced(ret))
        return ((IDeref)ret).deref();
    return ret;
}
```

The contract for `reduce` makes these demands:

1. Without a start value:

    a. if the collection has no items, return the result of calling `f` with no arguments

    b. if the collection has only one item, return that item (`f` is not called)

    c. the first application of f should be to the first and second items in the collection

    d. if a call to `f` results in a `Reduced` instance, dereference it and return that value.

2. With a start value:

    a. if the collection has no items, return the start value (`f` is not called)

    b. the first call to `f` should be on the start value and the first item

    c. if a call to `f` results in a `Reduced` instance, dereference it and return that value.

It might appear that requirements (1a) and (2a) are violated in the `PersistentList` code.  And then you remember that `PersistentList` always has at least one member so that no check for emptiness is required.  You should verify that the other conditions are met.

You can't take that C# code and just copy it into F#.    It relies on early returns out of loops, which we don't have in F#. And we'd probably prefer to avoid mutating bindings.  The technique often used is to translate  to a recurive function that does the looping, which is essentially the same in our examples as using a `recur` loop in Clojure.  

For example, take our first `reduce` above:

```C#
        object ret = first();
        for (ISeq s = next(); s != null; s = s.next()) { 
            ret = f.invoke(ret, s.first());
            if (RT.isReduced(ret))
                return ((IDeref)ret).deref();
        }
        return ret;
```
Two things change from iteration to iteration: the values of `ret` and `s`; just look for the assignments to those variables.  Those become our parameters.  Regular exit is when `s = null` -- we negate the condition of loop continuation to get the condition for method termination.  Early exit is done by checking for `Reduced`.   Thus our loop can be  encoded by

```F#
let rec step (ret:obj) (s:ISeq) =
    if isNull s then
        ret
    else
        match f.invoke(ret,s.first()) with
        | :? Reduced as red -> (red:>IDeref).deref()
        | nextRet -> step nextRet (s.next())
```

The iteration is started by calling `step` on arguments that set up the correct initial values for `ret` and `s`:

```F#
step (this:>ISeq).first() ((this:>ISeq).next())
```

The other `reduce` is similar

```F#
interface ReduceInit with
    member this.reduce(f,init) =
        let rec step (ret:obj) (s:ISeq) =
            if isNull s then
                ret
            else
                match ret with
                | :? Reduced as red -> (red:>IDeref).deref()
                | _ -> step (f.invoke(ret,s.first())) (s.next())
    let ret = step (f.invoke(start,(this:>ISeq).first())) (this:>ISeq>.next())
    match ret with
    | :? Reduced as red -> (red:>IDeref).deref()
    |_ -> ret
```

If you look closely, there is distinct difference between the two, both in the original and in the translation.  For the first one, in the loop, we call `f` and check its value.  For the second one, we check the value from the previous iteration, then call `f` to generate a value to pass for the next iteration.  If one writes the start-value version in C# this way:

```C#
public object reduce(IFn f, object start)
{
    object ret = f.invoke(start, first());
    if (RT.isReduced(ret)) 
        return ((IDeref)ret).deref();

    for (ISeq s = next(); s != null; s = s.next()) {
        ret = f.invoke(ret, s.first());
        if (RT.isReduced(ret))
            return ((IDeref)ret).deref(); 
    }
    return ret;
}
``` 
the loop body is now the same here as in the first one. Translating ths into F#, the two versions now have identical `step` functions.  You can move that into a method, leading to this code:

```F#
member this.recurser(acc:obj, s:ISeq) =
    if isNull s then
        ret
    else
        match f.invoke(ret,s.first()) with
        | :? Reduced as red -> (red:>IDeref).deref()
        | nextRet -> this.recurser(nextRet, (s.next()))

interface IReduce with
    member this.reduce(f) = 
        let asSeq = this:>ISeq
        this.recurser(asSeq.first(),asSeq.next())

interface IReduceInit with
    member this.reduce(f,start) =
        let asSeq = this:> ISeq
        match f.invoke(start,asSeq.first()) with
        | :? Reduced as red -> (red:>IDeref).deref()
        | acc -> this.recurser(acc,asSeq.next())   
```

Because the start-value version does a call of `f(start,first())` before we get into the loop, we must make sure to check it for `Reduced` before looping.

If you check carefully against our requirements, you will find that they are all met.  Do not neglect to do this exercise for every reduce you write.  Trust me.


## Cycling

Let's do one more.  There is a `cycle` function in Clojure that "[r]eturns a lazy (infinite!) sequence of repetitions of the items in coll."  It just calls a factory method on a the `Create` class.

```Clojure
(cycle [1 2 3] ) ;=> (1 2 3 1 2 3 1 2 3 ...)
```

A simple implementation of `Cycle` would hold the original sequence on the side so we could start over at the beginning if we have run through all the elements.  It then just needs to know the 'current' sequence.  Calling `first()` on the `Cycle` would just call `first()` on the 'current' sequence.  Calling `next()` on the `Cycle`, we'd call `next()` on the underlying sequence and make that result the 'current' sequence in a new `Cycle` object.

The actual implementation of `Cycle` works a little harder in order to more efficient, by being lazy about calling `next` on the underlying sequence.   One does not really need to know the `next()` on the underlying sequence until you either call `first()` or `next()` on the cycle object.  At that point you can compute `next()`.    We will need a mutable field in our `Cycle` to save the 'current' sequence when we finally get around to computing it.  This will not be visible from the outside, so `Cycle` is immutable to outward appearance.

It's probably easier just to look at the code.

```F#
type Cycle private (meta:IPersistentMap, all:ISeq, prev:ISeq, c:ISeq, n:ISeq) = 
    inherit ASeq(meta)
    
    [<VolatileField>]
    let mutable current : ISeq = c   // lazily realized

    [<VolatileField>]
    let mutable next : ISeq = n  // cached
    
    private new(all,prev,current) = Cycle(null,all,prev,current,null)

    static member create(vals:ISeq) : ISeq =
        if isNull vals then
            PersistentList.Empty
        else
            Cycle(vals,null,vals)

    member this.Current() =
        if isNull current then
            let c = prev.next()
            current <- if isNull c then all else c

        current

    interface ISeq with
        override this.first() = this.Current().first()
        override this.next() =
            if isNull next then
                next <- Cycle(all,this.Current(),null)

            next
```

A couple of small details.  If `Cycle.create(s)` is called with an empty sequence, we return an empty list, not a `Cycle`.  If we have `Cycle` object in our hand, we are guaranteed that its base sequence is not empty.  Note that both `first()` and `next()` access the 'current' sequence through a call to `Current`; that method takes care of noticing if the underlying field `current` is occupied -- `null` indicates we haven't done the work of calling `next` on the underlying sequence yet.  When you access `Current`, it will do that computation and save the result.  This code also handles cycling back to the beginning if we have reached the end.  It's pretty slick.  (Note: the cleverness is in the Java code.  I didn't come up with it.  Authorship note in that file credits Alex Miller.   Little tricks to promote laziness pop up all over the place.)

On to `reduce`. We will need to advance through the underlying sequence to access successive items.  We do _not_ need to use `Cycle.next()` to do this -- that would create a bunch of unnecessary `Cycle` items.  We just need to compute on the underlying sequence directly, performing the action that is done in `Cycle.Current()`.  The following method does this.

```F#
member this.advance(s: ISeq) =
    match s.next () with
    | null -> all         // we've hit the end, cycle back to the beginning
    | x -> x
```

Consider the no-start-value version of `reduce`.  We always have items, no need to check.  The sequence is infinite, so there is no end condition from the sequence. The only way out is to get a `Reduce` back from `f`.    I wrote down the sequence of steps and looked for a loop point.

```
    acc <- first
    s <- advance from current (because we have just eaten the first element)
    Loop:
    newAcc = f.invoke(acc, s.first())
    check newAcc for Reduced -> leave
    loop with newAcc, advance(s)
```    
How does the start-value version compare?

```
    acc <- start-value
    s <- current
    Loop:
    newAcc = f.invoke(acc, s.first())
    check newAcc for Reduced -> leave
    loop with newAcc, advance(s)
```  

The loop is the same, other than how get started.  Verify that the conditions (1c), (1d), (2b), and (2c) are met.  (The others don't matter.) And this goes straight to code.

```F#
member this.reducer(f: IFn, startVal: obj, startSeq: ISeq) =
    let rec step (acc: obj) (s: ISeq) =
        match f.invoke (acc, s.first ()) with
        | :? Reduced as red -> (red :> IDeref).deref ()
        | nextAcc -> step nextAcc (this.advance s)

    step startVal startSeq

interface IReduce with
    member this.reduce(f) =
        let s = this.Current()
        this.reducer (f, s.first (), this.advance (s))

interface IReduceInit with
    member this.reduce(f, v) = this.reducer (f, v, this.Current())
```

## Side note

The only way to test `Cycle.reduce` is to have an `IFn` that at some point returns a `Reduced` object.  The magic of F#'s object expressions comes in handy here.  We can create an object directly that implements `IFn`.  However, don't try to do this with an object expression based on `IFn` directly -- you'd have to have an entry for each of the almost-20 `invoke` methods.  Instead, you can base your object expression on `AFn`, an abstract class that has default implementations (raising `NotImplementedException') for all of them.  Here is an extract of my test code (using Expecto for writing the tests):

```F#
let adderStopsShort n =
        { new AFn() with
            member this.ToString() = ""
        interface IFn with
            member this.invoke(x, y) =
                if Numbers.gte(y ,n:>obj) then Reduced(x) else Numbers.add(x,y) }

let iter = Cycle.create(LongRange.create(100)) :?> IReduce
Expect.equal (iter.reduce (adderStopsShort 10)) 45L "Add them up for a while"
Expect.equal (iter.reduce ((adderStopsShort 10),100L)) 145L "Add them up for a while, with a kicker"
```

Our cycle is based on a 100-element `LongRange`.  `addStopsShort` called on a  stop value yields an `IFn` with this behavior:  When the second argument reaches the stop value, it returns the current value of the accumulator wrapped in a `Reduced`; otherwise, it is just `+`.

(The override of `ToString` in the object expression is necesary.  It seems you can't just override an inteface only.)

And with that, let's quit.

## Behind the scenes

What I've not talked about is all the machinery behind the `CollReduce` protocol.   That all lies out in the Clojure source code and is not our present concern.  Mostly.  I did have to dig into it to solve one problem.  There is a `reduce` method in `ArrayChunk`.   That actually is the reduce method for the `IChunk` interface.  (See previous post [Laziness and Chunking]({{site.baseurl}}{% post_url 2023-02-03-laziness-and-chunking %}.)  The `reduce` method in `ArrayChunk` does stop early when it gets a `Reduced` object back from the reducer function, but it returns the `Reduced` object, not the wrapped value.  I struggled with this for a while until finally getting set on the correct track by Alex Miller over in the #clr-dev channel in the Clojurian slack.  First is to note that this `reduce` is for `IChunk`.  Then you have to figure out where it gets called from.  And that's where the protocol comes in. `reduce` will through the `CollReduce` protocol, which in this case will end up going through the `InternalReduce` protocol, wherein we find a handler for `IChunkedSeq`:

```Clojure
  clojure.lang.IChunkedSeq
  (internal-reduce
   [s f val]
   (if-let [s (seq s)]
    (if (chunked-seq? s)
       (let [ret (.reduce (chunk-first s) f val)]
         (if (reduced? ret)
           @ret
           (recur (chunk-next s)
                  f
                  ret)))
       (interface-or-naive-reduce s f val))
	 val))
```

It is this handler that calls `Chunk.reduce`. It notes the returned `Reduced` value, stops the iteration, and does the `deref`.  If `ArrayChunk` did the `deref`, this handler woudn't know to stop.

My head hurts.

## End note

If you want to get a sense of the history of reduce, reducers, and transducers, check out the [Clojure change log](https://github.com/clojure/clojure/blob/master/changes.md).  These things take time to develop.  Changes sometimes work through the code slowly.  `clojure.lang.Reduced` was introduced in 2012 and incorporated into _some_ of the `reduce` methods at that time. (Here is the [commit](https://github.com/clojure/clojure/commit/96e8596cfdd29a2bb245d958683ee5fc1353b87a).)  But other edits came later.  For example, it was two years later that `IReduceInit` was split off from `IReduce` ([this commit](https://github.com/clojure/clojure/commit/4c963fb0f9ef9e4fc68b8b167729d857ced4b530)) and checking for a `Reduce`'d value was added to `PersistentList.reduce()` ([this commit](https://github.com/clojure/clojure/commit/3e4bf71f455a5fae665420d10c9ebdebd52b823b)).

If you've made it this far, you're likely someone who would check these things out. 
