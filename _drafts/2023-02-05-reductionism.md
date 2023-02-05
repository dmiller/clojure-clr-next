---
layout: post
title: Reductionism
date: 2023-02-05 12:00:00 -0500
categories: general
---

You can be a boss at reducers if you know this one weird trick!

At least I had hopes for that at one point.  It turns out that getting it right every time requires thinking it through _every single time_.

## Background

If you search for 'clojure reduce' in your search engine of choice, you might run across [Reducers](https://clojure.org/reference/reducers).
Reducers are a very useful suite of functions that should definitely be in the arsenal of any Clojure programmer, but we have a simpler aim: [clojure.core/reduce](https://clojuredocs.org/clojure.core/reduce).   We will get to source code of that shortly.  

There are two forms of reduce, two-arg and three-arg.  The two arg take a reducing function (of two arguments) that is applied first to the first two items of the collection, then to the result of that invocation and the third item, that result and the fourth item, etc., until the end of the collection is reached and the accumulated result is returned.

```Clojure
(reduce '+ [1 2 3 4 5] ; =>15
```
Effectivly it computes `(((1+2)+3)+4+5)`.

The three-argument version supplies a starting value that is supplied to the reducing function along with the first item, and so on.

```Clojure
(reduce '+ 10 [1 2 3 4 5] ; =>25
```
Effectivly it computes `((((10+1)+2)+3)+4+5)`. 

If you look at the code in `core.clj`, you will note that the defintion of `reduce` comes almost 7000 lines down.  There is actually a need for the functionality of reduce many places prior to that.  Those uses are all uses on small lists that help to do things like expand macros and deal with `& more` arguments.  As such, that early code can use a simple version of `reduce` without ill effect.  That one is called `reduce1` and it shows how one could define a simple general reduction function.  I've simplified it a bit (left out the chunked variation):

```Clojure
(defn reduce1
  ([f coll]
    (let [s (seq coll)]
      (if s
         (reduce1 f (first s) (next s))
         (f))))
  ([f val coll]
    (let [s (seq coll)]
      (if s
          (recur f (f val (first s)) (next s)))
          val))))
```

It cleverly defines the two-arg version by using the three-argument version.
In the two-arg version, if the collection has no elements, `f` is called with no arguments, and that value is the result.
Otherwise, it calls the three-arg version with the first element of the collection as the start value and passing the rest of the sequence to iterate over.

In the three argument version, we have a `recur` loop that iterates through the sequence applying the reduction function to the value computed so far and the next item in the sequence.  Note that if we were to enter the three-argument version directly and the sequence was empty, we would just return the starting value with no application of the reduction function.  We will be seeing similar code when we write versions of this in F#.

What we have just described is the contract for `reduce`:

> f should be a function of 2 arguments. If val is not supplied,
returns the result of applying f to the first 2 items in coll, then
applying f to that result and the 3rd item, etc. If coll contains no
items, f must accept no arguments as well, and reduce returns the
result of calling f with no arguments.  If coll has only 1 item, it
is returned and f is not called.  If val is supplied, returns the
result of applying f to val and the first item in coll, then
applying f to that result and the 2nd item, etc. If coll contains no
items, returns val and f is not called.

The code for `reduce` itself is a bit less obvious:

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

If our collection supports the `IReduce` or `IReduceInit` interface, than the collection supplies its own specialized reduction methods.
Otherwise, the magic of protocols is used to extend reduction to types that do not have these interfaces defined.  That code is in `clojure.core.protocols`.

For the collections we are writing, our concern is implementing `IReduce` or `IReduceInit`.

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

These are one-arg and two-arg -- the missing arg is the collection itself, `this`.


##  Feeling reduced, but not diminished

Before I start showing code, we need to have a little chat about `Reduced`.  It figures significantly in the code we are about to write.
It is hard to find information about it.  I checked five books on Clojure and found nary a mention.  I went into the repo and did 'Blame' on a bunch of files to track down when it was introduced. 

 `Reduced` is used to stop reductions early. It was introduced back in 2012 when Rich was redesigning the reduce mechanism.  (Here is the [commit](https://github.com/clojure/clojure/commit/96e8596cfdd29a2bb245d958683ee5fc1353b87a).)  The most prominent mention of `Reduce` is in the reference on [Transducers](https://clojure.org/reference/transducers#_early_termination):

> Clojure has a mechanism for specifying early termination of a reduce:
>
> - reduced - takes a value and returns a reduced value indicating reduction should stop
>
> - reduced? - returns true if the value was created with reduced
>
> - deref or @ can be used to retrieve the value inside a reduced
>
>A process that uses transducers must check for and stop when the step function returns a reduced value (more on that in Creating Transducible Processes). Additionally, a transducer step function that uses a nested reduce must check for and convey reduced values when they are encountered. (See the implementation of cat for an example.)

Let's look at some Clojure source.  Here are the `reduced` and a few others in that neighborhood in `core.clj` (edited lightly); 

```Clojure
(defn reduced
  "Wraps x in a way such that a reduce will terminate with the value x"
  [x]
  (clojure.lang.Reduced. x))

(defn reduced?
  "Returns true if x is the result of a call to reduced"
  ([x] (clojure.lang.RT/isReduced x)))

...
```

 I'll save you the trouble on `clojure.lang.RT/isReduced`; it just checks to see if its argument is an instance of `Reduce`.
And here is `Reduced`:

```F#
[<Sealed>]
type Reduced(value) =
    interface IDeref with
        member _.deref() = value
```

Yep, has one field, accessed through the `IDeref` interface:

```F#
type IDeref =
    abstract deref: unit -> obj
```

You can read the _Transducers_ article for reasons for using this.  For one thing, it is the only way to run a reduction over an infinite collection -- you have to send a signal that you've had enough.  In writing tests for `reduce` on a collection class such as `Cycle` (see below), I bake up a reduction function that looks for one its inputs to get above a certain value and then return a `Reduced` object with the value accumulated to that point.

__One essential rule when writing a `reduce` method:__  after each invocation of the reduction function, check the result to see if it is an instance of `Reduced`; if so, stop immediately and return the `deref` value.

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

Based on the `reduce` comment, we have a set of requirements to check our `reduce` code against:

1. Without a start value:

    a. if the collection has no items, return the result of calling `f` with no arguments

    b. if the collection has only one item, return that item (`f` is not called)

    c. the first application of f should be to the first and second items in the collection

    d. if a call to `f` results in a `Reduced` instance, dereference it and return that value.

2. With a start value:

    a. if the collection has no items, return the start value (`f` is not called)

    b. the first call to `f` should be on the start value and the first item

    c. if a call to `f` results in a `Reduced` instance, dereference it and return that value.

It might appear that requirements (1a) and (2a) are violated.  And then you remember that `PersistentList` always has at least one member so that no check for emptiness is required. 
We'll have to be more careful with other collection classes.  You can check that the other requirements are satisfied.

There is no simple way to translate these methods into F#.  They rely on early returns out of loops.  We do not have that capability in F#.  Usually we translate to a recurive function that does the looping.  It's a bit of an art.   A recent video that address exactly this topic can be found [here](https://www.youtube.com/watch?v=ljletzD8oGs).  In essence, you need to figure out what values are initialized and then modified each time around the loop.  Those become parameters to the function.  There will be one or more conditional statements that check for exit conditions, either regular exits or emergency exists; for those you have to figure out what needs to be returned at that point.  and then one or more clauses that set up for the next iteration, which is done as a recurive call to the function.

It sometimes help to write this all done linearly and then translate it.  For example, take our first `reduce` above:

```C#
        object ret = first();
        for (ISeq s = next(); s != null; s = s.next()) { 
            ret = f.invoke(ret, s.first());
            if (RT.isReduced(ret))
                return ((IDeref)ret).deref();
        }
        return ret;
```
Two things change from iteration to iteration: the values of `ret` and `s`.  Those become our parameters.  Regular exit is when `s = null` -- we negate the condition of loop continuation to get the condition for loop termination.  Early exit is done by checking for `Reduced`.   Thus our loop can be  encoded by

```F#
let rec step (ret:obj) (s:ISeq) =
    if isNull s then
        ret
    else
        match f.invoke(ret,s.first()) with
        | :? Reduced as red -> (red:>IDeref).deref()
        | nextRet -> step nextRet (s.next())
```

The iteration is started by calling with arguments that set up the correct initial values for `ret` and `s`:

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

If you look closely, there is distinct difference between the two, both in the original and in the translation.  For the first one, in the loop, we call `f` and check its value.  For the second one, we check the value from the previous iteration, then call `f` to generate a value to pass for the next iteration.  If one coul have written the second version in C# differently:


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

Now the loop body of the second is the same as the first.  Translating that into F#, the two versions now have identical `step` functions, so you could conceivably move that into a method (in this case, it could even be static as we don't need any access to the `PersistentList` itself) and we now have:

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

If you check against our requirements (leaving off the 'no items' checks see we definitely have an item):

1. Without a start value:

    b. if the collection has only one item, return that item (`f` is not called)

    c. the first application of f should be to the first and second items in the collection

    d. if a call to `f` results in a `Reduced` instance, dereference it and return that value.

2. With a start value:

    b. the first call to `f` should be on the start value and the first item

    c. if a call to `f` results in a `Reduced` instance, dereference it and return that value.

they are all met.

## Cycling

Let's do one more.  There is a `cycle` function in Clojure that "[r]eturns a lazy (infinite!) sequence of repetitions of the items in coll."  It just calls a factory method on a the `Create` class.

`Cycle` plays an interesting game to stay as lazy as possible.  A simple implementation of `Cycle` would 



## End note

As we dig our way through all this code, it is useful to remember that we are looking at the current state of code that has been under development for a decade and a half.  And that's just this source.  The inspiration goes back into the late 1950s, for Lisp itself, or the 1930s for the lambda calculus.  We're sitting at the still-growing tip of a long branch.

If you want to get a sense of the history of reduce, reducers, and transducers, check out the [Clojure change log](https://github.com/clojure/clojure/blob/master/changes.md).  These things take time to develop.  Changes sometimes work through the code slowly.  `clojure.lang.Reduce` was introduced in 2012 and incorporated into _some_ of the `reduce` methods at that time.  But other edits came later.  For example, it was two years later that `IReduceInit` was split off from `IReduce` ([this commit](https://github.com/clojure/clojure/commit/4c963fb0f9ef9e4fc68b8b167729d857ced4b530)) and checking for a `Reduce`'d value was added to `PersistentList.reduce()` ([this commit](https://github.com/clojure/clojure/commit/3e4bf71f455a5fae665420d10c9ebdebd52b823b)).

If you've made it this far, you're likely someone who would check these things out.  

