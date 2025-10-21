---
layout: post
title: C4 - Inline skating
date: 2025-10-18 00:00:00 -0500
categories: general
---

Inlining allows the compiler to replace some function calls with direct code insertion, improving performance.  There are two mechanisms of inlining in Clojure: user-defined inlining and intrinsic operations.

After the heavy lifting of the last few posts, this post is just a little palate cleanser.

In [C4 - Primitive urges][TBD], we looked at how unnecessary boxing of primitive types can be avoided.  That is accomplished by signficant compiler magic spread over a large number of classes.

In contrast, inlining and intrinsic operations are very localized and easy to understand.

## Inlining for function definitions

Clojure allows to specify inlining instructions on function definitions.  This is done by adding metadata to the function definition.  This is done extensively in the Clojure core and math libraries.  It can also be used by regular folks in application code.

We will take our examples from the Clojure core library.  We start with a simple example: `compare`.

```Clojure
(defn compare
  "Comparator. Returns a negative number, zero, or a positive number
  when x is logically 'less than', 'equal to', or 'greater than'
  y. Same as Java x.compareTo(y) except it also works for nil, and
  compares numbers and collections in a type-independent manner. x
  must implement Comparable"
  {
   :inline (fn [x y] `(. clojure.lang.Util compare ~x ~y))
   :added "1.0"}
  [x y] (. clojure.lang.Util (compare x y)))
```

Ignoring the metadata, we have a simple function definitions that does host interop, calling a static method in the `clojure.lang.Util` class.

You can ignore the `:static` metadata; it is no longer used.  Of relevance is the `:inline` metadata.  Its value is a function that takes a single argument and returns a list.  It is reminiscent of a macro definition, and in fact its use is similar.

Suppose we are compiling and reach the expression:

```Clojure
(compare x "abc")
```

This code will make its way down to `Compiler.AnalyzeSeq`.  That method will first try to macroexpand it; `compare` is not a macro, so there is no change.  The next step is to check `compare` for inlining metadata.

```C#
// in this example, op = `compare`
// the count is the number of arguments, here 2
IFn inline = IsInline(op, RT.count(RT.next(form)));

// If there is inline data, it will be a function.
// apply the function to get the a replacement form to be analyzed.
// Transfer metadata from the source to the replacement code and analyze that instead.
if (inline != null)
    return Analyze(pcon, MaybeTransferSourceInfo(PreserveTag(form, inline.applyTo(RT.next(form))), form));
```

For our example, `(compare x "abc")` will be tranformed to:

```Clojure
(. clojure.lang.Util compare x "abc")
```

Rather than compiling to a call to the compare function, which would itself call `clojure.lang.Util.compare`, we directly compile the interop call, bypassing the function call overhead.

One could achieve the same effect by defining `compare` as a macro.  However, that would preclude using `compare` as a first-class function; for example, you would not able to passing it as an argument to higher-order functions.  Inlining allows the use as a first-class function while still allowing the compiler to optimize away the function call overhead.


## Inline arity

You will note that the method `Compiler.IsInline` is passed the operator (`compare`) and the argument count (2).  We can specify which arities should be inlined.  For example, 

```Clojure
(defn =
  "Equality. Returns true if x equals y, false if not. Same as
  Java x.equals(y) except it also works for nil, and compares
  numbers and collections in a type-independent manner.  Clojure's immutable data
  structures define equals() (and thus =) as a value, not an identity,
  comparison."
  {:inline (fn [x y] `(. clojure.lang.Util equiv ~x ~y))  
   :inline-arities #{2}
   :added "1.0"}
  ([x] true)
  ([x y] (clojure.lang.Util/equiv x y))
  ([x y & more]
   (if (clojure.lang.Util/equiv x y)
     (if (next more)
       (recur y (first more) (next more))
       (clojure.lang.Util/equiv y (first more)))
     false)))
```

If `:inline-arities` is specified, its value should be a function that takes an integer and returns truthy if a call of that arity should be inlined.  In this case, the function is a set; `IPersistentSet` implements `IFn` on one argument and checks for membership.  The set `#{2}` will return true only for the argument 2.  One can find examples like `#{2 3}`, allowing several designated arities to be inline.  Also, you can apply an arbitrary function as a predicate.   For exmaple, there are multiple places in the core library where one finds `:inline-arities >1?`, which is a function that returns true for any argument greater than 1.

```Clojure
(defn ^:private >1? [n] (clojure.lang.Numbers/gt n 1))
```

Note that this is private, so you'll have to define your own version if you need this test.


__TMI warning!__: One last note on the core code.  You will see one mysterious function, `nary-inline`, used in several places.  For example:

```Clojure
(defn +
  "Returns the sum of nums. (+) returns 0. Does not auto-promote
  longs, will throw on overflow. See also: +'"
  {:inline (nary-inline 'add 'unchecked_add)
   :inline-arities >1?
   :added "1.2"}
  ([] 0)
  ([x] (. clojure.lang.RT (NumberCast x)))        ;;;  (cast Number x))
  ([x y] (. clojure.lang.Numbers (add x y)))
  ([x y & more]
     (reduce1 + (+ x y) more))
```

`nary-inline` is also a private function.  It picks one of the two method names passed to it based on whether `*unchecked-math*` is true or not, and constructs a call to the corresponding static method in `clojure.lang.Numbers`.  It is used in several places in the core library to handle math operations.


## Intrinsic operations

Now that we have user-defined inlining, we can look at intrinsic operations.  These are used to replace calls to certain static methods with direct MSIL/bytecode instructions.  This is not user-definable; it is hard-coded in the compiler and applies to basic arithmetic, logical, and bitwise operations on primitive types.

Consider:

```clojure
(defn f [x y] (+ x y))
```

Inlining will help us.  `f` will compile directly to a call to `clojure.lang.Numbers.add`, avoiding the overhead calling `f.invoke`:

```C#
	public static object invokeStatic(object P_0, object P_1)
	{
		return Numbers.add(P_0, P_1);
	}
```

There are a lot of overloads of `Numbers.add` to handle different types of arguments.  In this case, because we do not have more specific type information about `x` and `y`, we will end up calling the version that takes two `object` arguments.  This will result in boxing if `x` and `y` are primitive type values, and a bunch of type checking and dispatching inside `Numbers.add`.

We learned in  [C4 - Primitive urges][TBD] how to avoid boxing.  If we restrict the types of `x` and `y` to, say, `double`, we can avoid boxing:


```clojure
(defn f ^double [x y] ^double (+ x y))
```

One would expect this to compile to:

```C#
  public static double invokeStatic(double P_0, double P_1)
  {
    return Numbers.add(P_0, P_1);
  }
```

where now we will be calling the `double Numbers.add(double, double)` overload of `Numbers.add`.
And that would be case if not for intrinsic operation inlining.  In the method that does code generation for host interop calls, where we know the actual method to invoke (i.e., not a reflection situation) we find:

```C#
// ... Code to put the arguments on the stack ...
if (IsStaticCall)
{
    if (Intrinsics.HasOp(_method))
        // We have an intrinsic operation definition for this method.
        // Generate equivalent IL instructions directly.
        Intrinsics.EmitOp(_method, ilg);
    else
        // No intrinsic operation; defined.
        // Generate regular static method call
        ilg.Emit(OpCodes.Call, _method);
}
else 
{
  // ...
}
```

There are internal tables that map certain static methods to sequences of MSIL/bytecode instructions.  In our example, the call is to `clojure.lang.Numbers.add(double, double)`.  There is an entry in the table matching that method a single instruction,  `Opcodes.Add`.  No method call is generated.  The `Add` opcode is inserted in the instruction stream.

```C#
		// return P_0 + P_1;
		IL_0000: ldarg.0
		IL_0001: ldarg.1
		IL_0002: add     // <<<<==== intrinsic operation inlining
		IL_0003: ret
```

There are intrinsic definitions for many of the static methods in `clojure.lang.Numbers`, typically similar to the one given here, where the arguments are one or two primitive types.  There are also intrinsic definitions for other operations, such as array access and numeric conversions.

A separate category is for predicates:  Tests to be performed that return a boolean result and branch.
These are called with a label for the false branch.  This substitution is done only in ode generation for the test expression in an `IfExpr`.  For example,  consider

```clojure

```

`clojure.core/<` is defined as:>

```Clojure
(defn <
  "Returns non-nil if nums are in monotonically increasing order,
  otherwise false."
  {:inline (fn [x y] `(. clojure.lang.Numbers (lt ~x ~y)))
   :inline-arities #{2}
   :added "1.0"}
  ([x] true)
  ([x y] (. clojure.lang.Numbers (lt x y)))
  ([x y & more]
   (if (< x y)
     (if (next more)
       (recur y (first more) (next more))
       (< y (first more)))
     false)))
```

Regular inlining reduces `(< x y)` to `(. clojure.lang.Numbers (lt x y))`.  There is an intrinsic definition for `Numbers.lt(double,double)`.  We end up generating the code:

```C#
		// return (P_0 >= P_1) ? P_1 : P_0;
		IL_0000: ldarg.0
		IL_0001: ldarg.1
		IL_0002: bge IL_000d

		IL_0007: ldarg.0
		IL_0008: br IL_000e

		IL_000d: ldarg.1

		IL_000e: ret
``` 

Intrinsic operations are a powerful optimization technique.  However, it is a closed set of operations defined in the compiler, not available for user extension.  Perhaps that's a good thing?