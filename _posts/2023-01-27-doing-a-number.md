---
layout: post
title: Doing a number on Numbers
date: 2023-01-27 00:00:00 -0500
categories: general
---

Actually, more like `Numbers` did a number on me.  But we're good friends now.  `Numbers` is ready to go.  This is a long post; `Numbers` is big.

In the previous post, [_A numbers game_]({{site.baseurl}}{% post_url 2023-01-19-a-numbers-game %}), I talked about how I might approach implementing `clojure.lang.Numbers`, the heart of the implementation of arithmetic in Clojure. At the end of that post was a section called "What we'll do":

> - Implement enough of `Ops` and its subclasses to get `equiv`, `compare`, and `hasheq`
> - Maybe contemplate whether there is a better way to accomplish the two-parameter arg dispatch  in F#.
> - Definitely contemplate how to get the remaining operations, mostly arithmetic operations, working with this when we have the other pieces to make that possible.  (But mostly kicking this can down the road.)

I ended up implementing all of it except for one tiny piece that won't be needed until much later. I did end up changing the type-dispatch based on some benchmarking And the third point is now moot.

Here's how it went down, including a quick look at the internals of `Numbers`.

## The whole enchilada

I had contemplated porting just enough of `Numbers` to enable continued work on the collections implementation.  Two reasons for this: (1) some concerns about how entangled `Numbers` was with other parts of Clojure; and (2) `Numbers` is big.  For (1), further analysis showed that the entanglement was not too bad.  For (2), to give you an idea, my port of just the code in `Numbers` itself has around 650 member methods. That does not include some of the supporting classes, such as `Ratio` and `BigInt.`  But I finally decided to do it all just to make sure I was not missing something that would be a bigger problem down the road.

## Dispatching dispatch

In the previous post, we discussed the need to take objects and map them to a numerical category, and take pairs of objects and map them to a common category.  

 Our categories are  _L_ = `Long`, _D_ = `Double`, _R_ = `Ratio`, _BI_ = `BigInteger` and _BD_ = `BigDouble`, _UL_ = `Unsigned long` and _CD_ = `decimal (CLR built-in)`.  Our mapping table is this:


 |  *  |  L |  D |  R | BI | BD | UL | CD |
 |---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
 | __L  >__ |  L |  D |  R | BI | BD | BI | CD |
 | __D  >__ |  D |  D |  D |  D |  D |  D |  D |
 | __R  >__ |  R |  D |  R |  R | BD |  R | BD |
 | __BI >__ | BI |  D |  R | BI | BD | BI | BD |
 | __BD >__ | BD |  D | BD | BD | BD | BD | BD |
 | __UL >__ | BI |  D |  R | BI | BD | UL | CD |
 | __CD >__ | CD |  D | BD | BD | BD | CD | CD |


I couldn't think of anything simpler than just using an enum to represent the categories, and 2D array to represent the table.

Mapping an object to its category is a simple `match` on type.  The code, leaving out some of the table initialization--that gets boring quickly-- is here:

```F#
type OpsType =
    | Long = 0 | Double = 1 | Ratio = 2 | BigInteger = 3
    | BigDecimal = 4 | ULong = 5  | ClrDecimal = 6

module OpsSelector =

    let selectorTable =
        array2D
            [|  // first row of table
                [| OpsType.Long; OpsType.Double; OpsType.Ratio; OpsType.BigInteger;
                  OpsType.BigDecimal; OpsType.BigInteger OpsType.ClrDecimal |]

                // etc

             |]

    let ops (x: obj) : OpsType =
        match Type.GetTypeCode(x.GetType()) with
        | TypeCode.SByte
        | TypeCode.Int16
        | TypeCode.Int32
        | TypeCode.Int64 -> OpsType.Long


        | TypeCode.Byte
        | TypeCode.UInt16
        | TypeCode.UInt32
        | TypeCode.UInt64 -> OpsType.ULong

        | TypeCode.Single
        | TypeCode.Double -> OpsType.Double

        | TypeCode.Decimal -> OpsType.ClrDecimal

        | _ ->
            match x with
            | :? BigInt
            | :? BigInteger -> OpsType.BigInteger
            | :? Ratio -> OpsType.Ratio
            | :? BigDecimal -> OpsType.BigDecimal
            | _ -> OpsType.Long

    let combine (t1: OpsType, t2: OpsType) = selectorTable[int (t1), int (t2)]
```

(I'll back to that placement of `Byte` later on.)

Yes, you have to set your enum values and arrange the table properly.  I have a unit test that explicitly lists and checks for all 49 entries.  But we now can see the interactions all in one place; that beats chasing around a 4000-line file hunting down the connections.

Unsurprisingly, array lookup is faster than working through virtual methods as the current implementation does. More than twice as fast. 

The benchmark results are:

|          Method |     Mean |   Error |   StdDev | Ratio | RatioSD |
|---------------- |---------:|--------:|---------:|------:|--------:|
|     TypeCombine | 349.9 us | 6.98 us | 10.23 us |  1.00 |    0.00 |
|   LookupCombine | 178.9 us | 2.81 us |  2.63 us |  0.51 |    0.02 |
| LookupCombine2D | 168.9 us | 2.04 us |  1.91 us |  0.48 |    0.02 |

`TypeCombine` is the virtual method approach.  `LookupCombine2D` is the 2D table lookup.  The `LookupCombine` was a version of table lookup that used an array of arrays rather than a 2D array.  That requires an extra array lookup.  Just thought I'd check.  Not least because in the virtual method approach, the `combine` operation hands back the implementation object for the category.  In my 2D approach, `combine` hands back an `enum` which I then use to index into an array of objects.  So I end up with two array lookups.  I'm pretty confident from this test that that is still a win.

The benchmark code can be found [here](https://github.com/dmiller/clojure-clr-next/tree/main/src/Experiments/DispatchBenchmark).

## Clojure arithmetic

Check out the [Clojure cheatsheet](https://clojure.org/api/cheatsheet) for a handy list of Clojure functions.   For our purposes, look under the heading _Primitives/Numbers_; the functions listed as _Arithmetic_, _Compare_, _Bitwise_, and _Unchecked_, as well as a few others such as `zero?` all rely on the class `clojure.lang.Numbers`.

Much of the complexity of `Numbers` comes from the implementation of the arithmetic operators. The functions `+`, `-`, `*`, `inc`,  and `dec` are all quite similar.  And they come in quoted versions: `+'`, `='`, etc.  The functions `unchecked-add` and the other `unchecked-` functions will come into play, too.  

Here again is the Clojure source for `+`:

```Clojure
(defn +
  "Returns the sum of nums. (+) returns 0. Does not auto-promote
  longs, will throw on overflow. See also: +'"
  {:inline (nary-inline 'add 'unchecked_add)
   :inline-arities >1?
   :added "1.2"}
  ([] 0)
  ([x] (cast Number x))
  ([x y] (. clojure.lang.Numbers (add x y)))
  ([x y & more]
     (reduce1 + (+ x y) more)))
```

You need to look at the `:inline`. The `nary-inline` call makes `+` expand into a call to `Numbers.add` or `Numbers.unchecked_add` depending on the value of `*unchecked-math*`.  By comparison, here is the source for `+'`:

```Clojure
(defn +'
  "Returns the sum of nums. (+') returns 0. Supports arbitrary precision.
  See also: +"
  {:inline (nary-inline 'addP)
   :inline-arities >1?
   :added "1.0"}
  ([] 0)
  ([x] (cast Number x))
  ([x y] (. clojure.lang.Numbers (addP x y)))
  ([x y & more]
   (reduce1 +' (+' x y) more)))
   ```

   Unchecked math no longer comes into play.  We just call `Numbers.addP`

   Finally, `unchecked-add` is:

```Clojure
(defn unchecked-add
  "Returns the sum of x and y, both long.
  Note - uses a primitive operator subject to overflow."
  {:inline (fn [x y] `(. clojure.lang.Numbers (unchecked_add ~x ~y)))
   :added "1.0"}
  [x y] (. clojure.lang.Numbers (unchecked_add x y)))
```

It just uses `Numbers.unchecked_add`, so it is the same as `+` in an unchecked context.
Note the slight variations in the comments:

- `+`: "Does not auto-promote longs, will throw on overflow."
- `+'`: "Supports arbitrary precision."
-  `unchecked-add`: "uses a primitive operator subject to overflow."

(The comment on `+` is  incorrect -- it will throw if `*unchecked-math*` is false, else it will "[use] a primitive operator subject to overflow.")

Note that "auto-promote" and "supports arbitrary precision". are the same: if the result of an operation cannot be represented in the type in question, promote to a wider type and do the computation there.  As an example, add 1 to the maximum long value with each flavor of addition:

```Clojure
user=> (+ Int64/MaxValue 1)
...
integer overflow                          <---- BOOOOMMM!
user=>
user=> (+' Int64/MaxValue 1)
9223372036854775808N                      <-  BigInteger, our of Int64 range
user=> (unchecked-add Int64/MaxValue 1)
-9223372036854775808                      <- overflow, wrap around to negative
user=> (class *1)
System.Int64
user=> (== *2 Int64/MinValue)
true
user=>
```

We will dig into `Numbers.add`, `Numbers.addP`, and `Numbers.unchecked_add`.  The other arithmetic operators are similar.  `Numbers.add` has a _lot_ of overloads:  

```F#
add(x: obj, y: obj)
add(x: double, y: double)
add(x: int64, y: int64)
add(x: uint64, y: uint64)
add(x: decimal, y: decimal)
add(x: double, y: obj)
add(x: obj, y: double)
add(x: double, y: int64)
add(x: int64, y: double)
add(x: double, y: uint64)
add(x: uint64, y: double)
add(x: int64, y: obj)
add(x: obj, y: int64)
add(x: uint64, y: obj)
add(x: obj, y: uint64)
add(x: int64, y: uint64)
add(x: uint64, y: int64)
```

And the same for `addP` and `unchecked_add`.  (Starting to get an idea why there are 650 methods in `Numbers`?)

Why so many overloads?  Why these in particular?

Leaving out `decimal` for the moment, the types mentioned are the three primitive numeric types you are likely to run into--`double`, `int64` and `uint64`--plus `obj`, which covers everthing else.  The sixteen entries cover all pairs. The ones with specific numeric primitive type will be called if there is an inferred type at the point of invocation.  Depending on the particulars, we might be able to avoid boxing, both at the call site and inside the method itself.

Take the `double` overloads in all their glory:

```F#
    static member add(x: double, y: double) = x + y
    static member add(x: double, y: obj) = x + y
    static member add(x: obj, y: double) = x + y
    static member add(x: double, y: int64) = x + double (y)
    static member add(x: int64, y: double) = double (x) + y
    static member add(x: double, y: uint64) = x + double (y)
    static member add(x: uint64, y: double) = double (x) + y
```
 
 Because `double` is contagious -- if there is a double in your computation, the other argument will be converted to double.-- in the overloads of `double` with the primitive numeric types, we just convert and do the addition -- not boxing. 

 (For some reason the double/obj and obj/double methods on the JVM are written the equivalent of `static member add(x: double, y: obj) = Numbers.add (x, convertToDouble (y))`  I have no idea why.  If you do, let me know.)

 As a bonus, the return type for each is `double`, providing typing for the result that might avoid boxing at the call site.
 
 The situation is more complicated when overflow is involved.  (`double` arithmetic does not overflow --  `+/-Inf` take care of that.)  Let's look at `int64`.  We cannot just define

 ```F#
    static member add(x: int64, y: int64) = x + y
 ```
because the `+` operator is unchecked.  In C#, one could wrap this in `checked` context.  In F#, there are checked versions of the arithmetic operators, but I've not found a good way to use them locally; see [this StackOverflow entry] https://stackoverflow.com/questions/2271198/f-checked-arithmetics-scope) for an example.  (The JVM version uses `java.util.math.addExact(long,long))` here.)  So we'll do it ourselves.

How can we tell if an overflow has occurred?    

Overflow occurs when the result of the addition cannot be represented in the type in question.  With unchecked addition, you still get a result, its just not the arithemetically correct answer: `9223372036854775807L + 1L = -9223372036854775808L`.  Overflow in addition cannot happen when the operands are of differing sign (+/-). The symptom of overflow is that operands have the same sign and the result has the opposite sign.  One could write a complicated condition to test for this.  Or one could use some bit-twiddling magic to do it more efficiently.

In 2's-complement arithmetic, the sign is carried in the most-signifiant bit (MSB).  If you XOR two integers, the result will have 0 in the MSB if their MSBs agree (i.e., they have the same sign) and 1 if they disagree.  Thus a negative XOR result indicates differing sign.  Our overflow test is that the sum differs in sign from both operands.  Thus:

```F#
static member add(x: int64, y: int64) =
    let ret = x + y

    if (ret ^^^ x) < 0 && (ret ^^^ y) < 0 then
        raise <| OverflowException("integer overflow")
    else
        ret
```

You are going to have do this kind of analysis for each of `int64 `, `uint64`, and `decimal` for each arithmetic operation.  If your're thinking those unchecked operators are looking better, well, they won't help in the long run.  `addP` is going to have to check for overflow, so you're still going to have to put in the work.  (Or you could try checked and catch overflow exceptions, but that just seems wrong.)

The other overloads of `add` for `int64` pretty much have to throw in the towel.  For example,

```F#
static member add(x: int64, y: obj) = Numbers.add (x :> obj, y)
```

In other words, just defer to `add(obj,obj)` and let it do its magic.  This the default approach when you can't figure out something more bespoke to do.

And thus we arrive at the most general case:

```F#
static member add(x: obj, y: obj) = Numbers.getOps(x, y).add (x, y)
```

We discussed this operation in detail in the previous post.  We dispatch to an appropriate handler depending the types of the arguments, per the table given above.

This is not circular.  The method we are defining above is `Numbers.add`.  The `add` we calling there is on a class specialized for operations of a particular category, the category selected by the `getOps`. 

Let's continue with addition and look at `addP`.  This is the _promoting_ version of the operation.  It has the same overloads as `add`.  Promoting happens when the result of the operation is not representable in the type in question.  That can't happen with `double`, so the `addP` overloads involving `double` are identical to their `add` counterparts.  For `int64`, the result not being representable is just the condition of overflow.  Promotion here means to move to a type that _can_ represent the result.  Thus

```F#
static member addP(x: int64, y: int64) =
    let ret = x + y

    if (ret ^^^ x) < 0 && (ret ^^^ y) < 0 then
        Numbers.addP (x :> obj, y :> obj)
    else
        ret :> obj
```

When overflow is detected, rather than throwing an `OverflowException` as we did in `add`,  we default to the double-`obj` case, which is just as you might expect:

```F#
static member addP(x: obj, y: obj) = Numbers.getOps(x, y).addP (x, y)
```

And I've never understood why this is coded this way. Because here's what's going to happen. The `getOps` is going to hand us a `LongOps` object because both arguments are `int64` and our combination table says `L x L -> L`.  In `LongOps.addP` we are going to do the exact same thing as here:  do the addition and the check for overflow.  The difference is what we do on this overflow.  We call `Numbers.BIGINT_OPS.add (x, y)`.  WHy didn't we just do that directly?  Are we afraid that somewhere down the line `int64` overflows are going to be handled by something other than `BigInt`? We incur a cost here of an extra method call, to conversions of the boxed integers, and the extra addition/overflow check.  (I'll probably be changing to a direct call and just leave note in the code.)

After all that, `unchecked_add` is marvelously uncomplicated.  Just unchecked addition using the built-in operation.

```F#
static member unchecked_add(x: int64, y: int64) = x + y
```

And the general case is handled in the usual way:

```F#
static member unchecked_add(x: obj, y: obj) =
    Numbers.getOps(x, y).unchecked_add (x, y)
```

## The general case

The static class `Numbers` itself provides the entry points that Clojure code calls into.  Where possible, as you've seen, it will try to do the operation.  But the general case always comes down to:  take the operands, determine what category of number we should be working in--`L`, `D`, `R`, `BI`, `BD`, `UL`, or `CD`, as above-- and hand it off to a specialized object.  These objects implement the `Ops` inteface:

```F#
type Ops =
    abstract isZero: x: obj -> bool
    abstract isPos: x: obj -> bool
    abstract isNeg: x: obj -> bool

    abstract add: x: obj * y: obj -> obj
    abstract addP: x: obj * y: obj -> obj
    abstract unchecked_add: x: obj * y: obj -> 

    ...
```

There is class for each numeric category: `LongOps`, `DoubleOps`, etc.
When a method on one of these is invoked, say `BigDecimalOps`, you know that you are working in a context where the arguments can be converted to the type in question, namely `BigDecimal`.  Do that, compute your result accordingly. 

A few examples will give the general idea.  For `DoubleOps`,

```F#
member this.add(x, y) : obj =
    convertToDouble (x) + convertToDouble (y) :> obj
```

while for `LongOps`:

```F#
member this.add(x, y) : obj =
    Numbers.add (convertToULong (x), convertToULong (y))
```

Here is why it hard to decouple `Numbers` from the `Ops` classes.  They call back and forth.

There are some other tricks used in the code.  For example, for types that do not involve promotion (`D`, `R`, `BI`, and `BD`), the `P` versions of the operations can just call the regular versions:

```F#
member this.addP(x, y) = (this :> Ops).add (x, y)
```

Given that these things are true for four classes, we capture those commonalities in an abstract base class for those specific classes.

With this background, you should be able to work through the rest of the code.  It all comes down to the peculiarities of the individual categories.

## The problem with Byte

In Java, the primitive type `byte` is signed.  The CLR has both `Byte` and `SByte`, unsigned and signed, respectively.  `Byte` is used when you are doing byte-level work for I/O, etc.  I don' think I've every used `SBtye`.

When I created the signed and unsigned hierarchies, I assigned `Byte` to the unsigned side.

```F#
      sbyte <  int16 <  int32 <  int64 
       byte < uint16 < uint32 < uint64
```

Though that is technically pure, the result is that `(+ b 1)`, where `b` is byte, ends up involving `BigInt` because we are mixing signed and unsigned integers, which takes us into `int64` and `uint64` by promotion, and those two `combine` to `BigIntOps`.   I'm not sure that's what people would expect.

When I first thought about adding proper handling of unsigned, I debated just promoting all the unsigned type other than `uint64` into `int64`--their values are all representable in `int64`. Only `uint64` has values not representable in `int64` (and _vice versa_).  The fact is that is almost impossible to stay working with any primitive numeric type other than `int64` and `double` in Clojure.

I have a dream of someday adding a compiler mode that allows all the primitive types to be handled efficiently.  It certainly would be non-portable and hence need to be a switched feature.  But I'm thinking here of someone like a person using Clojure in Unity where efficiency in handling those numeric types is important. 


## Status

As I write this, I have completed porting the `Numbers` code.  I'm writing tests like crazy. In an upcoming post I'll provide a link to a snapshot of the code at this point.

In the process of writing tests, I found bugs that I traced back to the original code. (My code, not the JVM code.)  These are all edge cases in the handling of `decimal` and `uint64`.  Code to handle those types are a very recent addition.  I will be doing bug fixes in the current code.

