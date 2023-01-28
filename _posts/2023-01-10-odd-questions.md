---
layout: post
title: Odd questions
date: 2023-01-10 00:00:00 -0500
categories: general
---

Now for some homework.  Let's see how well you absorbed the material in the [previous post]({{site.baseurl}}{% post_url 2023-01-09-a-minimal-implementation-of-cons %}).

- __Exercise 1__: Implement a simple integer range type that implements the golden trio of interfaces.  You need only deal with increasing ranges. You should provide a `create` method that takes a start value and end value.  The resulting sequence should be `start  (start+1) (start+2) ...  (end-1)`.  Note that if `end <= start` you should return some representation of an empty sequence.

- __Exercise 3__:  Implement a type representing a sequence over a string.  Provide a `create` method that takes a string.   If the string is null or empty, you should return some representation of an empty sequence.


Good luck.  (But I do grade on the curve.)

Don't read ahead.

...


And now for my solutions to the exercises.  Like some textbooks you may have encountered, we will only provide solutions to the odd-numbered exercises.  Fortunatately, all the questions I pose are odd.

## An integer range

You should be able to use much of the code in `SimpleCons`.  The novel aspect of this type is the generative nature of `next`.  With `SimpleCons`, `next` returns something that exists already, namely, the value in the `tail` field.  Here, the `next` of the integer range `[7,20)`  is `[8,20` -- it is a new `SimpleIntRange` object.

Here is my implementation, leaving out the parts that are identical to `SimpleCons`.

```F#
[<AllowNullLiteral>]
type SimpleIntRange private (startVal: int, endVal: int) =

    interface ISeq with
        member _.first() = upcast startVal

        member this.more() =
            if startVal = endVal then
                upcast SimpleEmptySeq()
            else
                upcast SimpleIntRange(startVal + 1, endVal)

        member this.cons(o) = upcast SimpleCons(o, (this :> ISeq))

    interface IPersistentCollection with
        member _.count() = endVal - startVal + 1
```

Our type will hold two fields, `startVal` and `endVal`.  I have made the constructor private, to be called only from our code. When we do so, we will make sure that `startVal < endVal`, because otherwise we have an empty sequence and can return something else.

The definition of `first` should be obvious.   For `more`, we detect when we have hit the end, hence the special case return of a `SimpleEmptySeq`; otherwise, it returns an object representing the range one step further along.

For `cons`, note that we can cons anything on to the front, so the result will not be a `SimpleIntRange`.  We need a sequence that has the item at the front and our `SimpleIntRange` following; a `SimpleCons` will provide exactly that.

As for `count`, you have to know how to count.

How about the `create` method.  Here is one possible implementation.

```F#    
    static member create(startVal,endVal) : ISeq = 
        if endVal <= startVal then  
            SimpleEmptySeq()
        else
            SimpleIntRange(startVal,endVal)
```


## A string sequence

This is similar in that `next` requires a new object to be created.  The `next` of the sequence of characters based on `"abcd"` is a sequence for `"bcd"`.  One could create a new `SimpleStringSeq` with the truncated string.  However, that creates a new string on each iteration step -- that seems wasteful.  Instead, we can include in our object an index indicating the position of the `first` character in the string.

Again, I leave out the duplicate code.

```F#
type SimpleStringSeq private (index : int, source : string) =

    interface IPersistentCollection with
        member _.count() = 
            if index < source.Length then source.Length-index else 0

        member this.equiv(o) =
            match o with
            | :? Seqable as s -> Util.seqEquiv (this :> ISeq) (s.seq ())
            | _ -> false

    interface ISeq with
        member _.first() = upcast source[index]
        member this.next() =
            if index + 1 < source.Length then
                SimpleStringSeq(index+1,source)
             else
                null
            
        member this.more() =
            let s = (this :> ISeq).next()
            if isNull s then SimpleEmptySeq()
            else s
```

`count` and `first` should be obvious.

I did play a little trick with `more` versus `next`.  In `SimpleCons`, we showed defining `next` in terms of `more`.  That is one trick.  Another trick seen in the Clojure source is the opposite: Define `more` in terms of `next`.  This can be done when deciding what is the next item is very straightforward.   Here, `next` directly decides whether there is a sequence with elements to follow.  If not, `null` can be returned.  `more` can call `next`.  If `null` comes back, `more` _cannot_ just return `null` -- it must return "a logical sequence for which seq returns nil," i.e., a `SimpleEmptySeq`.

Finally, our `create` function:

```F#
    static member create (source : string) : ISeq =
        match source.Length with
        | 0 -> null
        | _ -> upcast SimpleStringSeq(0,source)
```

Source code for these examples are available at [ClojureCLR-Next repo](https://github.com/dmiller/clojure-clr-next).