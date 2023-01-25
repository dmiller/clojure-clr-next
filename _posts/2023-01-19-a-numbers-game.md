---
layout: post
title: A numbers game
date: 2023-01-19 00:00:00 -0500
categories: general
---

Getting started implementing Clojure collections requires methods in `clojure.lang.Util` for operations such as equality testing and hashing.  These methods must operate properly on numeric types.   The machinery for this is the class `clojure.lang.Numbers`.

## Some observations

To write performant numeric code in Clojure, you will need to use type hints and type conversions and other tactics to avoid the inefficiencies of reflection and boxing.  

However, there are places in Clojure where unknowns collide.  If you are comparing two Clojure persistent vectors for equality, say, you have to compare corresponding elements.  If they are both numbers, you have to know the rules for promotion/contagion (convert a byte to a long in order to compare it to a long and long to double to compare it to a double).

There are some hints about the rules in the [Equality guide](https://clojure.org/guides/equality). For `=`, the guide says a true is answer comes if

> Both arguments are numbers in the same 'category', and numerically the same, where category is one of:
>- integer or ratio
>- floating point (float or double)
>- BigDecimal.

For `==` it states

> Clojure’s `==` is intended specifically for numerical values:
>
>- `==` can be used with numbers across different number categories (such as integer `0` and floating point `0.0`).
>- If any value being compared is not a number, an exception is thrown.

You should definitely read the section titled [Numbers](https://clojure.org/guides/equality#numbers) in that guide.


## clojure.lang.Numbers

The rules are encoded in `clojure.lang.Numbers`.   In writing ClojureCLR, I couldn't just copy the code in `Numbers` and hope for the best.  Just for starters, there is a class [`java.util.Number`](https://docs.oracle.com/javase/8/docs/api/java/lang/Number.html) the has `Float`, `Integer`, `Long`, etc. as subclasses.  This class is used extensively -- it is the type of many method parameters.   The CLR has no equivalent.

In fact, classes such `Integer` in Java do not have a direct match in the CLR.  `Integer` is a class, a reference type.  It boxes an `int`:

> The `Integer` class wraps a value of the primitive type `int` in an object. An object of type `Integer` contains a single field whose type is `int`.

In the CLR, we have type `Int32`.  It directly is an _int_.  It is a value type, hence not a reference type, not a box.  An `Int32` can be boxed, certainly, but there is no separate type for that.

Actually, I managed to get `Numbers` mostly working with fairly obvious substitions.  Where I had to really understand the structure of `Numbers` was when I extended it to cover the numeric types that Java not have: the unsigned integers and the built-in decimal type.  With a decent understanding of the principles underlying `Numbers`, I could make coherent decisions on the design of the extension.

Take a deep breath and hold it.  Time for a deep dive.

## The magic

Let's work bottom up.  Suppose you are the compiler looking at the form

```Clojure
(+ x y)
```

If you are lucky, there are some type hints on x and y, either explicily stated or inherited from the expressions that created their values; you can write direct IL code to convert them to a compatible type (if they are of different types) and perform a typed sum operation.

If you are not lucky and one or both have no type hints, you will fall back on the definition of `+`.  That is in `core.clj`:

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

The two-parameter case is what interests us.  We end up call `clojure.lang.Numbers.add`.

(If you are aware of inlining, it ends up in the same place.)

And here is `Numbers.add` (using the C# version):

```C#
public static object add(object x, object y) 
{ 
    return ops(x).combine(ops(y)).add(x, y); 
}
```

The `ops` operation determines the type of its argument and maps it to a category.  The category is encoded in an object of a subclass of `Ops` object.  The set of categories are fixed; there is a specific set of classes defined that extend `Ops`.  They are:

- `LongOps`  -- primitive integers, such as `short` and `int`
- `ULongOps`  -- unsigned primtive integers (for the CLR at least)
- `DoubleOps` -- for floating-point
- `RatioOps` -- for, well, ratios
- `BigIntOps` -- obvious
- `BigDecimalOps` -- obvious, also
- `ClrDecimaOps` -- for the built-in `Decimal` type on the CLR

Promotion and contagion are handled by methods in `Ops`:

```C#
Ops combine(Ops y);
Ops opsWith(LongOps x);
Ops opsWith(ULongOps x); 
Ops opsWith(DoubleOps x);
Ops opsWith(RatioOps x);
Ops opsWith(ClrDecimalOps x); 
Ops opsWith(BigIntOps x);
Ops opsWith(BigDecimalOps x);
```

In the expression

```C#
ops(x).combine(ops(y)).add(x, y)
```

the `ops(x)` is call to `Number.ops()`.  It maps its argument to value for one of `LongOps`, `DoubleOps`, etc.

```C#
       static Ops ops(Object x)
        {
            Type xc = x.GetType();

            switch (Type.GetTypeCode(xc))
            {
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                    return LONG_OPS;

                case TypeCode.Byte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return ULONG_OPS;

                case TypeCode.Single:
                case TypeCode.Double:
                    return DOUBLE_OPS;

                case TypeCode.Decimal:
                    return CLRDECIMAL_OPS;

                default:
                    if (xc == typeof(BigInt))
                        return BIGINT_OPS;
                    else if (xc == typeof(BigInteger))
                        return BIGINT_OPS;
                    else if (xc == typeof(Ratio))
                        return RATIO_OPS;
                    else if (xc == typeof(BigDecimal))
                        return BIGDECIMAL_OPS;
                    else
                        return LONG_OPS;
            }
        }
```

where, for example,

```C#
static readonly LongOps LONG_OPS = new LongOps();
```


Let's say the value of `x` is an `Int32` and the value of `y` is a `Double`.
Then we will end up evaluating

```C#
LONG_OPS.combine(DOUBLE_OPS)
```

For the `LongOps` class, `combine` is defined as:

```C#
public Ops combine(Ops y)
{
    return y.opsWith(this);
}
```

Even after having been through this code multiple times, I still do a double-take seeing this line of code.
We called `combine` on the first argument's ops value, and that thing turns around and passes it off to the second argument's ops value!

It's very clever.  Look above and note that the `opsWith` method is overloaded on all the different `Ops`-derived classes.  We can't do something like `x.opsWith(y)` because `y` does not have a known type.  However, `x` does:  we are in `LongOps`. In other words, at the time this C# code is compiled, the expression `y.opsWith(this)` can be resolved because `this` is a `LongOps`.  It is a call to the `opsWith(LongOps)` overload.  It's a virtual method, so we will pick up `DoubleOps.opsWith(LongOps)`. Which is:

```C#
public override Ops opsWith(LongOps x)
{
    return this;
}
```

This returns the `DoubleOps` object itself.  What does this mean?  Look at where we started:

```C#
ops(x).combine(ops(y)).add(x, y)
```

The `add` method that will be called is the one belonging to `DoubleOps`.

(Take a moment and marvel at the magic of overloads and virtual methods.  What you have just witnessed is a very efficient form of double-dispatch on type for fixed-ahead-of-time set of types.)

```C#
public override object add(object x, object y)
{
    return Util.ConvertToDouble(x) + Util.ConvertToDouble(y);
}
```

Why do we have to convert the first argument to `Double`? Don't we know it is a `Double`?  Nope.  If we were adding an `Int32` to a `Single`, we'd end up here.  If we were adding a `Single` to an `Int16`, we'd end up here.   The code of `NumberOps` above shows that built-in integer types end up as `Long`'s (or `ULong` for unsigned integer types) and floating-point values end up as `Double`s.  That's promotion.   If you look at the code for all the overloads of `DoubleOps.opsWith`, it turns out the all return `this` -- if there's a primitive floating point in the mix, you will be doing `Double` arithmetic -- that's contagion.

There needs to be some consistency in the coding.  For example, the order of arguments should not matter for something like addition: `(+ x y)` should be the same as `(+ y x)`.  That means that
`ops(x).combine(ops(y))` should be the same as `ops(y).combine(ops(x))`.  In other words, the `combine` operation is symmetric.

If you look across all the code for `Number.ops`, `Ops.combine`, and `Ops.combineWith` you come up with the rules for promotion and contagion.  Promotion we've described, and is as defined in the `Number.ops` code. For contagion, sticking with the types the JVM and the CLR have in common, we get this (symmetric) matrix:


|     |   L  |   D  |   R  |  BI  |  BD  |
|-----:|:----:|:----:|:----:|:----:|:----:|
| __L >__  |   L  |   D  |   R  |  BI  |  BD  |
| __D >__  |   D  |   D  |   D  |   D  |   D  |
| __R >__  |   R  |   D  |   R  |   R  |  BD  |
| __BI >__ |  BI  |   D  |   R  |  BI  |  BD  |
| __BD >__ |  BD  |   D  |  BD  |  BD  |  BD  |

where _L_ = `Long`, _D_ = `Double`, _R_ = `Ratio`, _BI_ = `BigInteger` and _BD_ = `BigDouble`.

As I mentioned before, `Double` contaminates everything.
The other conversions are _widening_: the wider type can represent faithfully all the values of the narrower type.

```
L => BI => R => BD
```

when I added the unsigned types the CLR `Decimal` type, I tried to follow the patterns shown above.
I introduced _UL_ = `ULong` and _CD_ = `Decimal`.  Unsigned integer types get promoted up to _UL_.
There are some interesting cases.  What is the combination of _L_ with _UL_?  The only integral type that can represent both values faithfully is _BI_.    _L_ and _UL_ can widen to _CD_, but other combinations with _CD_ need to go up to _BD_.  And we end up with:

 |  *  |  L |  D |  R | BI | BD | UL | CD |
 |---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
 | __L  >__ |  L |  D |  R | BI | BD | BI | CD |
 | __D  >__ |  D |  D |  D |  D |  D |  D |  D |
 | __R  >__ |  R |  D |  R |  R | BD |  R | BD |
 | __BI >__ | BI |  D |  R | BI | BD | BI | BD |
 | __BD >__ | BD |  D | BD | BD | BD | BD | BD |
 | __UL >__ | BI |  D |  R | BI | BD | UL | CD |
 | __CD >__ | CD |  D | BD | BD | BD | CD | CD |


## What we need

Our goal is to get collections implemented, not provide the entire runtime apparatus for numbers.  We don't need arithemetic and shift operators, for example.  Does that mean we should split `Numbers`.   Actually, we do need to.  There are a few places in `Numbers` where collections or other types such as `Var` are needed.

We need just enough to support comparing numbers and computing hashes.  For comparisons, `Numbers` provides two static methods:

```C#
public static bool equal(object x, object y)
{
    return category(x) == category(y)
        && ops(x).combine(ops(y)).equiv(x, y);
}


public static int compare(object x, object y)
{
    Ops xyops = ops(x).combine(ops(y));
    if (xyops.lt(x, y))
        return -1;
    else if (xyops.lt(y, x))
        return 1;
    else
        return 0;
}
```

So we need the mapping-to-`Ops`-subclass code, and the `Ops.combine`, `Ops.equiv`, and `Ops.lt` methods.  Fortunately, these methods can be written very directly.   And we can toss `Ops.lte` and a few others for very little extra cost.

For hashing, we also get lucky:

```C#
public static int hasheq(object x)
{
    Type xc = x.GetType();

    if (xc == typeof(long))
    {
        long lpart = Util.ConvertToLong(x);
        //return (int)(lpart ^ (lpart >> 32));
        return Murmur3.HashLong(lpart);
    }
    if (xc == typeof(double))
    {
        if (x.Equals(-0.0))
            return 0;  // match 0.0
        return x.GetHashCode();
    }

    return hasheqFrom(x, xc);
}
```

I'll save you the details of `hasheqFrom`, but it is not problematic.

## What we'll do.

- Implement enough of `Ops` and its subclasses to get `equiv`, `compare`, and `hasheq`
- Maybe contemplate whether there is a better way to accomplish the two-parameter arg dispatch  in F#.
- Definitely contemplate how to get the remaining operations, mostly arithmetic operations, working with this when we have the other pieces to make that possible.  (But mostly kicking this can down the road.)


