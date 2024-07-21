---
layout: post
title: What's the point?  BigDecimal in review
date: 2024-07-21 00:00:00 -0500
categories: general
---

More to the point: Where's the point?  Recently I had to dig into the `BigDecimal` implementation to fix a reported bug. Every time I have to look at the `BigDecimal` code, it is a journey of rediscovery.  I'm going to write down a few things to save me some time in the future.

# General Decimal Arithmetic Specification

All the implementations of BigDecimal that I looked at when I started working on this implemented some variation of the specfication given in [The General Decimal Arithmetic Specification](http://speleotrove.com/decimal/decarith.pdf).  The specification is a bit ... long.  (74 pages.)  And dense.  But between the spec and the implementations, I got enough insight to proceed.

The GDAS has lots of added complexity.  I was only trying to match the capabilities of `java.math.BigDecimal`.   That implementation targeted a subset of the spec, more or less the X3.274 subset that is specified in an appendix of the GDAS.  This subset shield us from some complexitiies:  NaNs, infinite values, subnormal values, and negative zero. But I figured if it was good enough for Java, ... .

# What is a number?

The GDAS provides an abstract model for finite numbers.  (I'm going to ignore the designated _special cases_.) A finite number is defined by three integer parameters:

- `sign`: 0 for positive, 1 for negative
- `coefficient`: an integer which is zero of or positive.
- `exponent`: a signed integer

(There is a lot of discussion about allowed values for these parameters and interactions of limits and ranges.  You're welcome to have a go at it.)

The numerical _value_ of a finite number is given by the formula:

```math
 value = (-1)^sign * coefficient * 10^exponent
```
In what follows, I'll use the abstract representation for clarity.  It will be notated as `[sign,coeff,exp]`. For convenience, I'll often reduce this to `[coeff,exp]` and assume the coefficient is a signed integer value.  For example, I might notate the 
number `123.45` as `[0, 12345, -2]` or `[12345, -2]`, depending on the context.  There should be no confusion.

The first thing to understand is this:

> This abstract definition deliberately allows for multiple representations of values which are numerically equal but are visually distinct (such as 1 and 1.00).  [GDAS, p. 10]

What is `1`? `1.00`?  Simple. The former is `[1, 0]` and the latter is `[100, -2]`.  The save the same value.  They differ in _precision_: the former has a precision of one digit; the latter has precision 3.

# Conversions

The GDAS defines specific algorithms from converting an abstract representation to a string and string to an abstract representation.  The algorithms are not complicated, but a little longer than we need to get into.  Here are some examples from the GDAS:

```math
[0, 123,   0]   <=>    "123"
[1, 123,   0]   <=>    "-123"
[0, 123,   1]   <=>    "1.23E+3"
[0, 123,   3]   <=>    "1.23E+5"
[0, 123,  -1]   <=>    "12.3"
[0, 123,  -5]   <=>    "0.00123"
[0, 123, -10]   <=>    "1.23E-8"
[1, 123, -12]   <=>    "-1.23E-10"
[0,   0,   0]   <=>    "0"
[0,   0,  -2]   <=>    "0.00"
[0,   0,   2]   <=>    "0E+2"
[1,   0,   0]   <=>    "-0"         // except we won't have negative zero in our implementation
[0,   5,  -6]   <=>    "0.000005"
[0,  50,  -7]   <=>    "0.0000050"
[0,   5,  -7]   <=>    "5E-7"
```
You understand now why I will stick with the abstract representation. Implementing the reading printing algorithms is a bit of fun.

# Contexts

The GDAS provides "arbitrary precision"; that is rarely what one wants. One can do the following:

```clojure
(+ 100000000M 1E-20M) ;; => 100000000.00000000000000000001M
```

but one finds it hard to image a situation where 29 digits of precision are really necessary.  But, you do you.

In case you would like to limit the precision, the GDAS provides a `context` object which can be used to control the precision and other parameters affecting arithmetic operations.  We need only these:

- `precision`: "An integer which must be positive (greater than 0). This sets the maximum number of 
significant digits that can result from an arithmetic operation." [GDAS, p 13]
- `rounding`: "A named value which indicates the algorithm to be used when rounding is necessary. 

Rounding is applied when a result _coefficient_ has more significant digits than the value 
of _precision_; in this case the result coefficient is shortened to _precision_ digits and may 
then be incremented by one (which may require a further shortening), depending on 
the rounding algorithm selected and the remaining digits of the original _coefficient_. The 
exponent is adjusted to compensate for any shortening. " [GDAS, p 13]"

There are five rounding 'algorithms'  -- usually called _rounding mode_ that must be implemented:
Again quoting from GDAS:

| Mode      | Description |
|---------- | ----------- |
| _round-down_ |  (Round toward 0; truncate.) The discarded digits are ignored; the result is unchanged. |
| _round-half-up_ | If the discarded digits represent greater than or equal to half (0.5) of the value of a one in the next left position then the result coefficient  should be incremented by 1 (rounded up).  Otherwise the discarded digits are ignored. |
| _round-half-even_ | If the discarded digits represent greater than half (0.5) the value of a one in the next left position then the result coefficient should be incremented by 1 (rounded up). If they represent less than half, then the result coefficient is not adjusted (that is, the discarded digits are ignored). <br/> Otherwise (they represent exactly half) the result coefficient is unaltered if its rightmost digit is even, or incremented by 1 (rounded up) if its rightmost digit is odd (to make an even digit). |
| _round-ceiling_ | (Round toward +∞.) If all of the discarded digits are zero or if the sign is 1 the result is unchanged. Otherwise, the result coefficient should be ncremented by 1 (rounded up). |
| _round-floor_ | (Round toward -∞.) If all of the discarded digits are zero or if the sign is 0 the result is unchanged. Otherwise, the sign is 1 and the result coefficient should be incremented by 1. |

In Clojure, the dynamic `Var` named `*math-context*` is used to hold the current context.  The default value of `nil` indicates unbounded precision and no rounding mode.  The `Numbers` suite of arithmetic operations will use this context to determine the precision and rounding mode for the operation.  The context can be set using the `with-precision` macro.  For example:

```clojure
 (with-precision 5 :rounding HalfUp 
    (+ 1000000000M 1E-20M))        ;; => 1.0000E+9M = [10000, 5]
```

Note: if `:rounding` is not specified, the default is `HalfUp`.

Some operations require an explicit context, typically when the result of an operation with no rounding does not have exactly representable result.  Division is the poster child.

```clojure
 (/ 10M 2M)  ;; => 5M
 (/ 10M 3M)  ;; => throws ArithmeticException!  
             ;;    "Non-terminating decimal expansion; no exact representable decimal result."

 (with-precision 4 :rounding HalfUp
           (/ 10M 3M))                 ;; => 3.333M   
```

# Basic arithmetic

The GDAS provides algorithms for the basic arithemetic operations.  Some of them are rather involved.  In fact, in my original implementation in C#, I have comments specifically noting places where I felt compelled to "port while looking", i.e, I pretty much just straight translated the code from the OpenJDK implementation.  

We can look at one operation, addition, to get a feel for how arithmetic computations are done, especially with regard to how the context comes into play for limiting precision and rounding.

Paraphrasing the GDAS (which combines the description of additoin and subtraction -- I'm subtracting the subtraction part):

- The _coefficient_ of the result is computed by adding the _aligned coefficients_ of the two operands.  
- The _aligned coefficients_ are computed by comparing the `exponent`s of the operands:
    - If the exponents are equal, the aligned coefficients are the same as the original coefficients.
    - Otherwise, the aligned coefficent of the number with the larger exponent is multiplied by `10^n`, where `n` is the absolute value difference between the exponents; the aligned coefficient of the other operand is the same as the original coefficient.
- The result _exponent_ is the minimum of the exponents of the two operands.
    
 In other words, basically you do the equivalent of shifting in order to align the decimal points.  Without talking about decimal points, just exponents.  

 An example, using our notation for numbers:

 ```math
 [2751, -1] + [4356, 1] = [2751, -1] + [435600, -1] 
                        = [438351, -1]
 ```
Or in the way we usually do arithmetic:

```math   
     275.1
+  43560.0
----------
   43835.1
```

- The result is then rounded to _precision_ digits if necessary, counting from the most significant digit of the result.

Now, this is where you are going to get into trouble.  Precision is how many digits you want to keep.  Rounding with a context does not make it easy to say -- give me just an integer result.

Given these definitions for our friend in the previous example:

```clojure
(def d5 275.1M)
(def d6 4356E1M)
```

fill in the following table for evalauting:

```clojure
(with-precision N (+ d5 d6)
```

| N  | Result |
|----:|--------|
| 10 |  |
|  6 |  |
|  5 |  |
|  4 |  |
|  3 |  |
|  2 |  |
|  1 |  |
|  0 |  |

I just went ahead and ran the code.

```clojure
(map #(with-precision % (+ d5 d6)) '(10 6 5 4 3 2 1))
;; => (43835.1M 
;;     43835.1M 
;;     43835M 
;;     4.384E+4M 
;;     4.38E+4M 
;;     4.4E+4M 
;;     4E+4M
;;     43835.1M)
```

Or

| N  | Result |
|----:|--------:|
| 10 |  43835.1 |
|  6 |  43835.1 |
|  5 |  43835   |
|  4 |  43840   |
|  3 |  43800   |
|  2 |  44000   |
|  1 |  40000   |
|  0 |  43835.1 |

(Remember that a precision of 0 means no precision limit.)

What if you really want to round a result to get an integer?
See below.  But first, let's write some code to at least get us through addition.  It is easiest to discuss the rounding operation concretely.

# It's coding time

Let's get a context type going first.  We need an enum to cover the rounding modes.  

```F#
type RoundingMode =
    | Up
    | Down
    | Ceiling
    | Floor
    | HalfUp
    | HalfDown
    | HalfEven
    | Unnecessary
```
The context is a record type.

```F#

[<Struct>]
type Context =
    { precision: uint32
      roundingMode: RoundingMode }

    // There are some standard contexts that can be used

    /// Standard precision for 32-bit decimal
    static member val Decimal32 =
        { precision = 7u
          roundingMode = HalfEven }
    /// Standard precision for 64-bit decimal
    static member val Decimal64 =
        { precision = 16u
          roundingMode = HalfEven }

    static member val Unlimited =
        { precision = 0u
          roundingMode = HalfUp }
    /// Default mode
    static member val Default =
        { precision = 9ul
          roundingMode = HalfUp }

    // And some factory methods

    /// Create a Context with specified precision and roundingMode = HalfEven
    static member ExtendedDefault precision =
        { precision = precision
          roundingMode = HalfEven }

    /// Create a Context from the given precision and rounding mode
    static member Create(precision, roundingMode) =
        { precision = precision
          roundingMode = roundingMode }
```

Now we can start implementing `BigDecimal`.  For exposition purposes, I'll present the code out of order.  You'll need to rearrange for an F# compilation to work.

We need three fields. The coefficient is a `BigInteger` and the exponent is an `int`. In addition, we lazily compute the precision of the number itself.  We provide the precision in our private constructor.  We supply 0, indicating _not-yet-computed_, much of the time, but if we know it at construction time we can supply it.

```F#
[<Sealed>]
type BigDecimal private (coeff, exp, precision) =

    // Precision

    // Constructor precision is shadowed with a mutable.
    // Value of 0 indicates precision not computed
    let mutable precision: uint = precision

    // Compute actual precision and cache it.
    member private _.GetPrecision() =
        match precision with
        | 0u -> precision <- Math.Max(ArithmeticHelpers.getBIPrecision (coeff), 1u)
        | _ -> ()

        precision

    // Public properties related to precision

    member this.Precision = this.GetPrecision()
    member _.RawPrecision = precision
    member this.IsPrecisionKnown = this.RawPrecision <> 0u
```

I'm going to skip that little helper method `getBIPrecision` for now.  It deserves its onw (short) post.

Now let's take a look at addition.  We'll provide two versions, one talking a context and one not.  If we don't take a context, then there is no rounding involved.  Just align and add the coefficients  and use the exponent of the smaller one.

```F#
    member this.Add(y: BigDecimal) =
        let xc, yc, exp = BigDecimal.align this y in BigDecimal(xc + yc, exp, 0u)
```

We will define `align` to give us back the aligned coefficents and the smaller exponent from the two `BigDecimal`s.

```F#
    /// Return the aligned coefficients and the smaller exponent
    static member private align (x: BigDecimal) (y: BigDecimal) =
        if y.Exponent > x.Exponent then 
            (x.Coefficient, BigDecimal.computeAlign y x, x.Exponent)
        elif x.Exponent > y.Exponent then 
            (BigDecimal.computeAlign x y, y.Coefficient, y.Exponent )
        else 
            (x.Coefficient, y.Coefficient, y.Exponent)
```


The `computeAlign` function is simple.  It just multiplies the coefficient of the larger exponent by 10 raised to the difference in exponents.  The larger value is the first argument.

```F#
    static member private computeAlign (big: BigDecimal) (small: BigDecimal) =
        let deltaExp = (big.Exponent - small.Exponent) |> uint
        big.Coefficient * ArithmeticHelpers.biPowerOfTen (deltaExp)
```

The `biPowerOfTen` function is a simple helper function to compute `BigInteger` powers of ten. 

When contexts are involved, we have to deal with rounding:

```F#
    member this.Add(y: BigDecimal, c: Context) =
        let result = this.Add(y)

        if c.precision = 0u
           || c.roundingMode = RoundingMode.Unnecessary then
           // rounding not required
            result
        else
            BigDecimal.round result c
```

Rounding is required if the precision of the result is greater than the precision of the context.  
The precision of the result is just the number of digits in its `BigInteger` coefficient.  Suppose we have the 
`BigDecimal` value  `[123456789, -2]` (= 1234567.89) and the context precision is 4.  We need to reduce the coefficient to four digits, leaving us with either `1234` or `1235`, depending on the rounding mode.  We get the 1234 by dividing by a power of ten, the power being the difference in precision.  Here the the difference is `9 - 4 = 5`, so we divide the coefficent by 100000 = 10^5.  This yields 1234.  Rounding up means adding 1, yielding 1235.  Finally, to construct the result, we need the correct exponent.  We divided by 10000, so we should multiply by the same amount; equivalently, increase the exponent by 5.  The result is `[1235, 3]` or 1235000.  In other words:

```F#
    static member private round (v: BigDecimal) c =
        let vp = v.GetPrecision()

        if (vp <= c.precision) then
            // No rounding required: precision is less than or equal to context precision
            v
        else
            // Rounding required
            let drop = vp - c.precision
            let divisor = ArithmeticHelpers.biPowerOfTen (drop)

            let rounded =
                BigDecimal.roundingDivide2 v.Coefficient divisor c.roundingMode

            // read below
            let exp =
                BigDecimal.checkExponentE ((int64 v.Exponent) + (int64 drop)) rounded.IsZero

            // check for the case where we had a 9999... that rounded up to 10000...

            if (ArithmeticHelpers.getBIPrecision(rounded) > c.precision && c.precision > 0u) then
                let newCoeff =  rounded / ArithmeticHelpers.biTen
                BigDecimal(newCoeff, exp+1, c.precision)
            else
                BigDecimal(rounded, exp, c.precision)
```

The call to `checkExponentE` is to ensure that the exponent is not too large.  Our exponents are limited to the range of `int32`.  The increment of the exponent might cause an overflow.  We do the arithmetic in `int64`.  `checkExponentE` will make sure it is in bounds (and also checks if we have a zero result, for which we can just set the exponent to 1).   If the exponent is too large, it throws an exception.  

The final conditional covers this warning in the GDAS:

> When a result is rounded, the coefficient may become longer than the current precision. 
In this case the least significant digit of the coefficient (it will be a zero) is removed 
(reducing the precision by one), and the exponent is incremented by one. 

An example: Context = (4, HalfUp).  Number is [999996789, -2].  As in our previous example, we divide by 100000, yielding 9999.  Rounding up gives 10000.  The exponent is increased by 1, giving [100000, 3].  However, our precision is now 5, not four.  The code detects that our coefficient does not have the correct precision; in that case, we divide by 10 and increment the exponent.  The result is [1000, 4].

# Where's my integer?

Dig around in GDAS and you will note mention of functions `round-to-integral-exact` and `round-to-integral-value`.  These are both defined as calls the `quantize` function. The `quantize` text mentions that it used to be called `rescale`, with slightly different parameters.  I decided to write `Quantize` and `Rescale`.  (Some other implementations have the a version of `rescale` -- `java.lang.BigDecimal` has a `setScale` method.)

```F#
    static member Quantize(lhs: BigDecimal, rhs: BigDecimal, mode: RoundingMode) =
        BigDecimal.Rescale(lhs, rhs.Exponent, mode)
```

The `lhs` is the value to be quantized or rescaled.  The second argument provides the exponent.  (`Quantize` takes the exponent from a `BigDecimal` value; `Rescale` gets the exponent directly.

 The GDAS has this to say about `quantize`:

 > [...] , quantize returns the number which is equal in value (except for any rounding) and sign to the first (left-hand) operand and which has an exponent set to be equal to the exponent of the second (right-hand) operand.
>
> The coefficient of the result is derived from that of the left-hand operand.  It may be rounded using the current rounding setting (if the exponent is being increased), multiplied by a positive power of ten (if the exponent is being decreased), or is unchanged (if the exponent is already equal to that of the right-hand operand).
>
> Unlike other operations, if the length of the coefficient after the quantize operation would be greater than precision then an Invalid operation condition is raised. This guarantees that, unless there is an error condition, the exponent of the result of a quantize is always equal to that of the right-hand operand. 

Let's work through two cases, increasing vs. decreasing the exponent.  

Suppose we call `Rescale` with `[123456789, -4]` (= 12345.6789) and desired exponent -1. Use mode `HalfUp`.  We can guess what the result should be: 12345.7 = `[123457, -1]`. 
Now, we could figure out the code to do that directly but as it turns out, we can press `round` into service for us.  We can view this as decreasing the precision.  We just need to figure out what the new precision is.  We can see it is 6 = 9 - 3 = the precision of the left-hand size minus the change in the exponent.

For the other direction, suppose we have  `[123456,-1]` and we want to rescale to exponent -4.  In other words, we go from 12345.6 to 12345.6000 = `[123456000,-4]`.  No rounding will be needed.  Just multiply by the appropriate power of 10.

And that is essentially the code below.  A few minor tweaks to compute the precision of the result (since we know it) and a recursive call to `Rescale` to handle the 999... => 1000... problem

```F#
    static member Rescale(lhs: BigDecimal, newExponent, mode): BigDecimal =

        let increaseExponent delta =
            // delta negative => increasing the exponent => we might have to round to a new precision
            let decrease = -delta |> uint
            let p = lhs.Precision

            let newPrecision = if p < decrease then 0u else p - decrease

            let r =
                lhs.Round
                    ({ precision = newPrecision
                       roundingMode = mode })

            if (r.Exponent = newExponent) then r else BigDecimal.Rescale(r, newExponent, mode)

        let decreaseExponent delta =
            // delta positive => decrease the exponent => multiply by 10^some power and don't underflow
            let newCoeff =
                lhs.Coefficient
                * ArithmeticHelpers.biPowerOfTen (delta)

            let oldPrec = lhs.Precision

            let newPrec =
                oldPrec + (if oldPrec = 0u then 0u else delta)

            BigDecimal(newCoeff, newExponent, newPrec)

        let delta =
            BigDecimal.checkExponentE ((int64 lhs.Exponent) - (int64 newExponent)) false

        if delta = 0 then lhs
        elif lhs.Coefficient.IsZero then BigDecimal(BigInteger.Zero, newExponent, 0u)
        elif delta < 0 then increaseExponent delta
        else decreaseExponent (uint delta)
```

And I think that's it for the exposition.

You can stick around for a little background, if you like.

# How we got here

I was inspired to write this post because of a post on the Clojurian Slack channel that
there was bug in converting a `BigDecimal` to an integer using rounding mode `Ceiling`.  That got me to digging into the `BigDecimal` code, re-reading the GDAS, etc.  It was a simple fix, but it took me a long time to get my head into the game;  I hadn't done any substantive work on this code in 14 years.

I thought writing some things down might help future me (or a future maintainer) should I venture this way again.

While writing this post, I found a few things hard to explain/justify and ended up making some code tweaks that got rid of an allocation and simplified a recursive call.  A nice side-effect of posting.


But this begs the deeper question of how we got here:  Why did I implement `BigDecimal`?


When Clojure first appeared, it supported all the primitive numeric types of Java and also the `java.math.BigInteger` and `java.math.BigDecimal` classes: The lisp reader supports literals of those types (`123N` or '`123.45M` respectively); the `Numbers` suite of arithemetical operations supports them via casts, arithemetic contagion, etc.

This presented a bit of a problem when porting to the CLR. At that time, there were no standard packages for these types in the Base Class Libary (BCL).  If ClojureCLR was going to provide support for arbitrary precision integers and decimals, the choices seemed to be either find some libraries to include or write my own.  I chose the latter approach, in part because there seemed to be a definite distaste for including third-party libraries in the Clojure eco-system.  Okay, also in part because I thought it would be fun.

So I looked around at some implementations of `BigInteger` packages, decided on what I wanted to provide -- I needed to support at least the basic methods available in the Java version -- and started coding.  That was relatively straightforward.  I could look at `Microsoft.Scripting.Math.BigInteger` (part of the IronPython project) and the `java.math.BigInteger` source code from OpenJDK. But mostly I relied on Donald Knuth's _The Art of Computer Programming, Volume 2_.  It was worth doing just for the excuse to read that book.

The coding was not terribly hard: We have a pretty intuitive feeling for integer arithmetic. If you can add two integers represented as sequences of digits in the range '0' to '9', how hard can it be to add two integers represented as sequences of _'digits'_ in the range '0' to `UInt32.MaxValue`?   (We use arrays of `uint32` to represent values.)  See?  You're almost there..


`BigDecimal` was a another game entirely.  This is not floating-point, so no inspiration there.   Even the `System.Decimal` class in the CLR is of no help in this world. 
Fortunately, I found references to the GDAS early in the process. 

I also look at various implementations of `BigDecimal` in other languages. I think my primary sources were:

- [the OpenJDK implementation of `java.math.BigDecimal](https://github.com/openjdk/jdk/blob/master/src/java.base/share/classes/java/math/BigDecimal.java -- written in Java
- [the IronPython implementation](https://github.com/IronLanguages/ironpython3/blob/main/Src/StdLib/Lib/decimal.py) - written in Python
- [the IronRuby implementation](https://github.com/IronLanguages/ironruby/tree/master/Src/Libraries/BigDecimal) - written in C#

Each has its own approach.  The OpenJDK implementation is similar to ours, except instead of the `exponent`, they have a `scale` field, which is the negative of the exponent. (Translating that is fraught.  All the comparisons and adjustments are backwards.) They also have some efficiency hacks, such as a compact representation for when the coefficient is small enough to fit into a regular integer; I decided not to bother with the added complexity.

I'm not sure of the origin of the IronPython version -- I think it comes from some older Python implementation.  This package provides a much more complete implementation of GDAS.  It has more of the signals (Inexact, Subnormal, etc.). The representation if `[exponent, coefficient, sign, isSpecial]`, implements NaNs, infinities, and other special values.
The following comment in the code indicated that I wasn't going to get much help here:

        # Note that the coefficient, self._int, is actually stored as
        # a string rather than as a tuple of digits.  This speeds up
        # the "digits to integer" and "integer to digits" conversions
        # that are used in almost every arithmetic operation on
        # Decimals.  This is an internal detail: the as_tuple function
        # and the Decimal constructor still deal with tuples of
        # digits.

The IronRuby implementation also has more of the spec:  NaNs and infinity, the overflow exception modes.  The representation for a finite value is `[sign, fraction, exponent]`.  
The `fraction` is of type `Fraction`, implemented in the package.  The representation here is unusual.  An array of `uint` is used for the value, but rather than using the whole range in each 'digit', each `uint` will be in the range of 0 to 999999999.  This makes translating to and from strings much simpler.  It might be fun to play with a `BigInteger` implemenation usng this representation.

For ClojureCLR.Next, I decided to ditch my own `clojure.lang.BigInteger` and use `System.Numerics.BigInteger` instead. Less to maintain.  So the F# version of `BigDecimal` uses `System.Numerics.BigInteger` for the coefficients.

And that's the story.
