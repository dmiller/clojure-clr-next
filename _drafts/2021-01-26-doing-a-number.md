---
layout: post
title: Doing a number on Numbers
date: 2023-01-24 00:00:00 -0500
categories: general
---

Actually, more like Numbers did a number on me.  But we're good friends now.

In the previous post, [_A numbers game_]({% site_url 2023-01-29-a-numbers-game %}), I talked about how I might approach implementing `clojure.lang.Numbers`, the heart of the implementation of arithmetic in Clojure.  Here's what I actually ended up doing.

At the end of that post was a section called "What we'll do":

> - Implement enough of `Ops` and its subclasses to get `equiv`, `compare`, and `hasheq`
> - Maybe contemplate whether there is a better way to accomplish the two-parameter arg dispatch  in F#.
> - Definitely contemplate how to get the remaining operations, mostly arithmetic operations, working with this when we have the other pieces to make that possible.  (But mostly kicking this can down the road.)


I ended up implementing all of it except for one tiny piece that can be deferred a long time.
I did end up changing the type-dispatch based on some benchmarking, that results of which I'll share below.  And the third point is now moot.

Here's how it went down.

## The whole enchilada

My original thought was to port just enough of `Numbers` to keep working on the collections implementation.  I had several reasons, the majof one being how daunting the task felt.  'Daunting as in my F# port at the moment has 650 member methods. That does not include some of the supporting classes, such as `Ratio` and `BigInt.` Also, a further analysis showed that entanglement to later pieces was not as significant as I thought.  So decided to go for it.

It was not possible to reduce circular dependencies as much as I had hoped.  I _was_ able to separate out the type dispatch I wrote about in the last post into a mostly separate component.  And I replaced the clever use of overloads and virtual methods with a simple table lookup that benchmarking shows is more efficient.

I'll dig into that mechanism first, then talk more generally about how arithmetic works in Clojure and how the `Numbers` class and its acolytes mirror that.

## Dispatching dispatch

From the previous post, we know we need to take objects and map them to a category, and take pairs of objects and map them to a common category.  
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

Mapping an object to its category is a simple `match` on type.  The code, leaving out some of the table initialation--that gets boring quickly-- is here:

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

Yes, you have to set your enum values and arrange the table properly.  I have a big unit test that explicitly lists and checks for all 49 entries.  But it's all in one place.  That beats chasing around a 4000-line file chasing the code down.

And it's faster than the overload/virtual method. That's not too surprising.  It's hard to beat an array lookup.  I ran a benchmark.  The 2D array is twice as fast.  I also ran a version that used an array of arrays instead of a 2D array.  Why?  One difference between the old code and the new code is that the old code's combine hands back an implementation object, not an enum.  In the new code, I have to look up the implementation code from the category.  That requires an extra array lookup.  The result is still considerably faster.  (Yes, I could have populated this array with the implementation objects, but that would have coupled this module with that other code.  I decided to decouple and take a very minor hit.  Still faster. Good enough.)

The benchmark results are:

|          Method |     Mean |   Error |   StdDev | Ratio | RatioSD |
|---------------- |---------:|--------:|---------:|------:|--------:|
|     TypeCombine | 349.9 us | 6.98 us | 10.23 us |  1.00 |    0.00 |
|   LookupCombine | 178.9 us | 2.81 us |  2.63 us |  0.51 |    0.02 |
| LookupCombine2D | 168.9 us | 2.04 us |  1.91 us |  0.48 |    0.02 |

The benchmark code can be found [here]().

## Clojure arithmetic

Check out the [Clojure cheatsheet](https://clojure.org/api/cheatsheet) for a handy list of Clojure functions.   For our focus here, check under Primitives/Numbers.  The functions listed as Arithmetic, Compare, Bitwise, and Unchecked, as well as a few others such as `zero?` all rely on the class `clojure.lang.Numbers`.

Much of the complexity of `Numbers` comes from the implementation of the arithmetic operators. The functions `+`, `-`, `*`, `inc`,  and `dec` are all quite similar.  And they come in quoted versions: `+'`, `='`, etc.  The functions `unchecked-add` and the other `unchecked-` functions will come into play, too.  

We looked in the last post at the Clojure source for `+`:

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

You need to look at the `:inline` because that is what is used. the `nary-inline` call makes `+` expand into a call to `Numbers.add` or `Numbers.unchecked_add` depending on the value of `*unchecked-math*`.  By comparison, here is the source for `+'`:

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

- `+`: Does not auto-promote longs, will throw on overflow.
- `+'`: Supports arbitrary precision.
-  `unchecked-add`: uses a primitive operator subject to overflow.

(The comment on `+` is  incorrect -- it will throw if `*unchecked-math*` is false, else it will "[use] a primitive operator subject to overrflow.")

Note that "auto-promote" and "supports arbitrary precision". are the same.  Just to check, we can add 1 to the maximum long value with each:

```Clojure
user=> (+ Int64/MaxValue 1)
...
integer overflow
user=>
user=> (+' Int64/MaxValue 1)
9223372036854775808N
user=> (unchecked-add Int64/MaxValue 1)
-9223372036854775808
user=> (class *1)
System.Int64
user=> (== *2 Int64/MinValue)
true
user=>
```

We will dig into `Numbers.add`, `Numbers.addP`, and `Numbers.unchecked_add` and see how they are implemented.  The other arithmetic operators will be similar.  `Numbers.add` has a _lot_ of overloads:  

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
    static member add(x: double, y: obj) = Numbers.add (x, convertToDouble (y))
    static member add(x: obj, y: double) = Numbers.add (convertToDouble (x), y)
    static member add(x: double, y: int64) = x + double (y)
    static member add(x: int64, y: double) = double (x) + y
    static member add(x: double, y: uint64) = x + double (y)
    static member add(x: uint64, y: double) = double (x) + y
```
 
 Because `double` is fatally contagious -- if there is a double in your computation, the other argument will be converted to double.   In the overloads of `double` with the four primitive numeric types, we just convert and do the addition -- not boxing.  In the overloads of `double` with `obj`, we convert the `obj` to a `double` and call the `add(double,double)` overload
 (I copied this from the JVM version.  I'm not sure why we just don't do the addition directly as the other overloads do.  With inlining, it should come to the same thing.)

 As a bonus, the return type for each is `double`, providing typing for the result that might avoid boxing when we return at the call site.
 
 The situation is more complicated when overflow is involved.  (`double` arithmetic does not overflow -- `NaN` and `+/-Inf` have us covered.)  Let's look at `int64`.  We cannot just define

 ```F#
    static member add(x: int64, y: int64) = x + y
 ```
because the `+` operator is unchecked.  In C#, one could wrap this in `checked` context.  In F#, there are checked versions of the arithmetic operators, but I've not found a good way to use them locally; see [this StackOverflow entry] https://stackoverflow.com/questions/2271198/f-checked-arithmetics-scope) for an example.  (The JVM version uses `java.util.math.addExact(long,long))` here.)

How can we tell if an overflow has occured.  Grab a copy of _Hacker's Delight_ and look it up. (You should just have a copy sitting around anwyay.) Or I can save you some time and just derive it here.  

Overflow occurs when the result of the addition cannot be represented in the type in question.  With unchecked addition, you still get a result, its just not the arithemetically correct answer: `9223372036854775807 + 1 = -9223372036854775808`.  Overflow in addition cannot happen when the operands are of differing sign (+/-). Overflow has occurred when the operands have the same sign and the result has the opposite sign.  One could write a complicated condition to test for this.  Or one could use some bit-twiddling magic to do it more efficiently.

In 2's-complement arithmetic, the sign is carried in the most-signifiant bit (MSB).  If you XOR two integers, the result will have 0 in the MSB if their MSBs agree (same sign) and 1 if they disagree.  Thus a negative XOR result indicates differing sign.  Our overflow test is that the sum differs in sign from both operands.  Thus:

```F#
static member add(x: int64, y: int64) =
    let ret = x + y

    if (ret ^^^ x) < 0 && (ret ^^^ y) < 0 then
        raise <| OverflowException("integer overflow")
    else
        ret
```

You are going to have do this kind of analysis for each of `int64 `, `uint64`, and `decimal` for each arithmetic operation.  Thinking those unchecked operators are looking better.  It won't help in the long run.  `addP` is going to have to check for overflow, so you're still going to have to put in the work.

The other overloads of `add` for `int64` pretty much have to throw in the towel.  For example,

```F#
static member add(x: int64, y: obj) = Numbers.add (x :> obj, y)
```

In other words, just defer to `add(obj,obj)` and let it do its magic.  This the default approach when you can't figure out something more bespoke to do.

And thus we arrive at

```F#
static member add(x: obj, y: obj) = Numbers.getOps(x, y).add (x, y)
```

We discussed this operation in detail in the previous post.  We dispatch to an appropriate handler depending the types of the arguments, per the table discussed above.

Let's continue with addition and look at `addP`.  This is the _promoting_ version of the operation.  It has the same overloads as `add`.  Promoting happens when the result of the operation is not representable in the type in question.  That can't happen with `double`, so the `addP` overloads involving `double` are identical to their `add` counterparts.  For `int64`, the result not being representable is just the condition of overflow.  Promotion here means to move to a type that _can_ represent the result.  Thus

```F#
static member addP(x: int64, y: int64) =
    let ret = x + y

    if (ret ^^^ x) < 0 && (ret ^^^ y) < 0 then
        Numbers.addP (x :> obj, y :> obj)
    else
        ret :> obj
```

When overflow is detected, we default to the double-`obj` case, which is just as you might expect:

```F#
static member addP(x: obj, y: obj) = Numbers.getOps(x, y).addP (x, y)
```

And I've never understood why this is coded this way. Because here's what's going to happen. The `getOps` is going to hand us a `LongOps` object because both arguments are `int64` and our combination table says `L x L -> L`.  In `LongOps.addP` we are going to do the exact same thing as here:  do the addition and the check for overflow.  The difference is what we do on this overflow.  We call `Numbers.BIGINT_OPS.add (x, y)`.  WHy didn't we just do that directly?  Are we afraid that somewhere down the line `int64` overflows are going to be handled by something other than `BigInt`? We encur a cost here of an extra method call, to conversions of the boxed integers, and the extra addition/overflow check.  (I'll probably be changing to a direct call and just leave note in the code.)

After all that, `unchecked_add` is marvelously uncomplicated.  Just unchecked addition using the built-in operation.

```F#
static member unchecked_add(x: int64, y: int64) = x + y
```

Though why the JVM does the equivalent of this for `double`

```F#
static member unchecked_add(x: double, y: double) = Numbers.add (x, y)
```
rather than just defining it as `x+y`, I cannot say.  Inlining shoudl take care of it, but why not be consistent?

And the general case is handled in theusual way:

```F#
static member unchecked_add(x: obj, y: obj) =
    Numbers.getOps(x, y).unchecked_add (x, y)
```

## The general case

`Numbers` provides the entry points that Clojure code calls into.  Where possible, as you've seen, it will try to do the operation.  But the general case always comes down to:  take the operands, determine what category of number we should be working in--`L`, `D`, `R`, `BI`, `BD`, `UL`, or `CD`, as above-- and hand it off to a specialized object.  These objects implement the `Ops` inteface:

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

There is class for each: `LongOps`, `DoubleOps`, etc.
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

We capture those commonalities in an abstract base class for those specific classes.

With this background, you should be able to work through the rest of the code.  It all comes down to the peculiarities of the individual categories.

## The problem with Byte

In Java, the primitive type `byte` is signed.  The CLR has both `Byte` and `SByte`, unsigned and signed, respectively.  `Byte` is used when you are doing byte-level work for I/O, etc.  I doubt that anyone is using `SBtye` for much of anything.

When I created the signed and unsigned hierarchies, I assigned `Byte` to the unsigned side.

```F#
      sbyte < int16  < int32  < int64 
       byte < uint16 < uint32 < uint64
```

Though that is technically pure, the result is that `(+ b 1)`, where `b` is byte, ends up involving `BigInt` because we are mixing signed and unsigned ints, which takes us into `int64` and `uint64` by promotion, and those two `combine` to `BigIntOps`.   I'm not sure that's what people would expect.

Of course, I wonder if they are thinking that `(+ b 1)` is doing `long` arithmetic in Java.

When I first thought about adding proper handling of unsigned, I debated just promoting all the unsigned type other than `uint64` into `int64`--their values are all representable in `int64`. Only `uint64` has values not representable in `int64` (and _vice versa_).  The fact is that is almost impossible to stay working with any primitive numeric type other than `int64` and `double` in Clojure.

I have a dream of someday adding a compiler mode that allows all the primitive types to be handled efficiently.  It certainly would be non-portable and hence need to be a switched feature.  But I'm thinking here of someone like a person using Clojure in Unity where efficiency in handling those numeric types is important. 


## Status

As I write this, I have completed all the code.  I'm writing tests like crazy. 

Code snapshot -

In the process of writing tests, I found bugs that I traced back to the original code.  These are all edge cases in the handling of `decimal` and `uint64`.  Code to handle those types are a very recent addition and I doubt that anyone is using them.  I will be doing bug fixes in the current code.







