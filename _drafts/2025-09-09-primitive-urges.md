---
layout: post
title: C4 - Primitive urges
date: 2025-09-09 00:00:00 -0500
categories: general
---

How primitive types are handled in the Clojure compiler.

## Boxing day is every day

We would like to avoid unnecessary boxing and unboxing of primitive values. 

By default, the Clojure compiler will generate reference values everywhere.  To avoid this in the case of primitive values, we need to things:

- an expression that can generate a primitive value
- a context that can accept a primitive value

## MaybePrimitiveExpr

An AST node type that can possibly generate a privitive value indicates this by deriving from `MaybePrimitiveExpr`. 

```C#    
public interface MaybePrimitiveExpr : Expr
    {
        bool CanEmitPrimitive { get; }
        void EmitUnboxed(RHC rhc, ObjExpr objx, CljILGen ilg);
    }
```

A given instance of a `MaybePrimitiveExpr`-implementating class may or may not be able to emit a primitive value, depending on its internal state.  This is indicated by the `CanEmitPrimitive` property.  If this is true, then the `EmitUnboxed` method can be called to generate IL that leaves a primitive value on the stack.  However, whether or not to call `EmitUnboxed` instead of the usual `Emit` method is decided by the context in which the expression appears.

 The following expression types implement `MaybePrimitiveExpr`:

`BodyExpr`
`CaseExpr`
`HostExpr`  (and thus derived classes also)
`IfExpr`
`InstanceOfExpr`
`LetExpr`
`LocalBindingExpr`
`MethodParamExpr`
`NumberExpr`
`RecurExpr`
`StaticInvokeExpr`


### CanEmitPrimitive

For most of these, the computation of `CanEmitPrimitive` is straightforward.  For example, `NumberExpr` can always emit a primitive value; it only emits `long` and `double` values.   Whether a `BodyExpr` can emit a primitive value depends on whether its last expression can emit a primitive value.  `HostExpr`-derived classes can emit a primitive value if they are not reflections: we have the explicit `MethodInfo`/`PropertyInfo`/`FieldInfo` to work with and the return type of that is primitive.  Many of the others have an explicit return type calculated; `CanEmitPrimitive` is true if that type is primitive.

The most complicated formula for `CanEmitPrimitive` is in `IfExpr`.  We need both the 'then' and the 'else' expressions to be `MaybePrimitiveExpr`.  They both must have `CanEmitPrimitive` be true, and they must be of the same primitive type -- or one of them can have a return type of type `Recur.RecurType`.  You might recall from [C4: Tag! You're int!][TBD] that `Recur.RecurType` is a special type used as the return type of a `RecurExpr`.  The only reason `RecurExpr` implements `MaybePrimitiveExpr` is to make this code work:

```c#
_thenExpr is MaybePrimitiveExpr tExpr
&& _elseExpr is MaybePrimitiveExpr eExpr
&& (_thenExpr.ClrType == _elseExpr.ClrType
    || _thenExpr.ClrType == Recur.RecurType
    || _elseExpr.ClrType == Recur.RecurType)
&& tExpr.CanEmitPrimitive
&& eExpr.CanEmitPrimitive;
```

`RecurExpr.CanEmitPrimitive()` always returns true.

Are there any surprising exclusions here?  A few omissions strike me.

- `BooleanExpr` -- Perhaps this has something to do with the way boolean values are handled in ClojureJVM; I really don't know.  I tried implementing it. The code is trivial.  However, it messes up parsing or code generation (I forget which) in `recur` expressions.  I never got around to figuring out why or how to solve it.
- `LetFnExpr` -- this should parallel `LetExpr`.  The difference is in the intializations, not in the body.

One that is not surprising with a bit of analysis: `FnExpr`.  Recall that the `FnExpr` itself returns a function object; its value definitely is not primitive.  The question is actually whether the the given application of a function to arguments can return a primitive value.
That analysis is done by `InvokeExpr` and will be discussed below.

### EmitUnboxed

For most of these expressions, the `EmitUnboxed` method is straightforward. For example,
for `BodyExpr`, the code for `Emit` and `EmitUnboxed` are identical except for the call to the last expression.  We are going to call `LastExpr.Emit(...)` or `(LastExpr as MaybePrimitiveExpr).EmitUnboxed(...)`, respectively.

Some are trickier.  For example, `IfExpr`.  `IfExpr` emits three sections of code:  the test, the 'then' code, and the 'else' code.  Independent of whether we are emitting the `IfExp` as boxed or unboxed -- that depends on who is calling it and whether `CanEmitPrimitive` is true -- we still might be able to emit the 'test' code as unboxed.   Then we emit the 'then' and 'else' code as boxed or unboxed, depending on whether we are emitting the `IfExpr` as boxed or unboxed.  


## Callsites

Where do we call `MaybePrimitiveExpr.EmitUnboxed`?

Some of the calls are pass-throughs.  For example, if we have a `BodyExpr`, if we call its `Emit`, it calls `LastExpr.Emit(...)`; if we call its `EmitUnboxed`, it calls `LastExpr.EmitUnboxed(...)`.   This is true for many of our `MaybePrimitiveExpr`-implementing classes that have subordinate AST nodes.

Where are the 'instigators', the places that decide on whether to call `Emit` or `EmitUnboxed` on subordinate expressions independently of whether the instigator itself is being emitted as boxed or unboxed?  Instigators are the root causes of primitive urges.

We've seen one already: `IfExpr`. Its decision to call `EmitUnboxed` on its 'test' expression is independent of whether the `IfExpr` itself is being emitted as boxed or unboxed.

Local binding scopes with initializations are another.  Think `LetExpr`.
For a local binding with an initialization, if the initialization expression is a `MaybePrimitiveExpr` and `CanEmitPrimitive` is true, then we will declare the local variable in the code gen to have that type and call `EmitUnboxed` on the initialization expression.  Note that the local variable itself does not have to have an explicit type hint.

Also, in `deftype`s and related, we can have mutable fields.  The code to assign to a mutable field can call `EmitUnboxed` on the value expression under the same conditions as for `LetExpr`.

A major locus is method calls for host platform interop.  One should look at the abstract class `MethodExpr` and its derived classes `InstanceMethodExpr` and `StaticInvokeExpr`.   We might call either `Emit` or `EmitUnboxed` on the `MethodExpr` itself, depending on what the calling context is interested in.  The only difference between `Emit` and `EmitUnboxed` is whether we box the return value or not.   `MethodExpr` instigates primitive generation when dealing with the arguments to the method call and this is independent of whether the return value is boxed or not.


These are used for method calls, property accesses, and field accesses.  The decision to call `EmitUnboxed` on the target expression (for instance methods) and on the argument expressions is independent of whether the method call itself is being emitted as boxed or unboxed.  The code for emitting the argument to for a parameter is:

```C#
public static void EmitTypedArg(ObjExpr objx, CljILGen ilg, Type paramType, Expr arg)
{
    // paramType is the type of the parameter in the method
    // It will have a non-null value.

    // primt will be non-null if the arg is MaybePrimitiveExpr, it can emit primitive,
    // it has ClrType, and the ClrType is primitive.
    Type primt = Compiler.MaybePrimitiveType(arg);

    // mpe will be non-null if the arg is MaybePrimitiveExpr
    MaybePrimitiveExpr mpe = arg as MaybePrimitiveExpr;

    if (primt == paramType)
    {
        // paramType is primitive and matches the type of the arg.
        // Just do it.
        mpe.EmitUnboxed(RHC.Expression, objx, ilg);
    }
    else if (primt == typeof(int) && paramType == typeof(long))
    {
        // arg is int, param is long.  Widen.
        mpe.EmitUnboxed(RHC.Expression, objx, ilg);
        ilg.Emit(OpCodes.Conv_I8);
        }
    else if (primt == typeof(long) && paramType == typeof(int))
    {
        // arg is long, param is int.  Narrow, possibly with overflow check.
        mpe.EmitUnboxed(RHC.Expression, objx, ilg);
        if (RT.booleanCast(RT.UncheckedMathVar.deref()))
            ilg.Emit(OpCodes.Call,Compiler.Method_RT_uncheckedIntCast_long);
        else
            ilg.Emit(OpCodes.Call,Compiler.Method_RT_intCast_long);
    }
    else if (primt == typeof(float) && paramType == typeof(double))
    {
        // arg is float, param is double. Widen.
        mpe.EmitUnboxed(RHC.Expression, objx, ilg);
        ilg.Emit(OpCodes.Conv_R8);
    }
    else if (primt == typeof(double) && paramType == typeof(float))
    {
        // arg is double, param is float. Narrow.
        mpe.EmitUnboxed(RHC.Expression, objx, ilg);
        ilg.Emit(OpCodes.Conv_R4);
    }
    else
    {
        // arg is not primitive, or it is primitive but not directly compatible with paramType.
        // Emit the arg unboxed.
        arg.Emit(RHC.Expression, objx, ilg);

        // Try to cast it to the paramType.
        HostExpr.EmitUnboxArg(objx, ilg, paramType);
    }
}
```

At the very end you see the call to `HostExpr.EmitUnboxArg`.  It looks at the paramType and generates a call to a conversion method for numeric types, or to `Opcodes.CastClass`.  It inadequately deals with CLR value types.  (See below.)  Some things are best not discussed in polite company.
 

There is similar code over in `NewExpr` for constructor calls.

And there is very similar code on `ObjMethod.EmitBody`.  This method is used to emit code for `invokeStatic` and `primInvoke` method definitions in a function class.  These are the (possibly) typed versions of `invoke` used in direct linking.  We do `EmitUnboxed` on the body if possible, and then possibly do some conversion as above to align numeric types.

## Recur-ing nightmares

`RecurExpr` is another instigator of primitive urges.  Its `Emit` and `EmitUnboxed` methods are identical -- not surprising as `RecurExpr` doesn't leave anything on the argument stack;  it is a goto.  The arguments in a `recur` expression must have types matching the types (implicit or explicit) of the loop parameters.  If the loop parameter is primitive, we must call `EmitUnboxed` on the corresponding argument expression.  Appropriate `int`/`long` and `float`/`double` conversions are done as seen above.  If there is a mismatch that cannot be resolved, an a reflection warning will be issued.


## Invocations

During parsing, we need to analyze function invocations.  We've been given something like

```Clojure
(fexpr ...args...)
```
and we've ruled out any number of special cases: We've macroexpanded;  `fexpr` is not `fn*` so this is not a function definition; `fexpr` is not a special form such as `let*'; and so on.
In other words, we have a function call expression.  The form gets handed off to `InvokeExpr.Parse`.  This method does not necessarily just do some parsing and return an `InvokeExpr`.  There is some case analysis, mostly based on `fexpr`, the leads to some special cases.

The first special case has these conditions:

- `fexpr` parses to a `VarExpr`
- direct linking is turned on
- we are not in an evaluation context (let's not talk about that here)
- the `Var` in the `VarExpr` is not dynamic and does not have `:redef` or `:declared` set true in its metadata
- the `Var` is bound to a value
- the type of the value has a static `invokeStatic` method with the correct arity (and an `ISeq`-typed last parameter for variadic calls)

If all these conditions are met, we create a `StaticInvokeExpr` to represent the call.  This is of relevance here because (a) `StaticInvokeExpr` implements `MaybePrimitiveExpr` and (b) whether or not its `Emit` or `EmitUnboxed` method is called, we will be calling `EmitTypedArg` to avoid unnecessary boxing genering argument values and passing them to the `invokeStatic` method.

The second special case is when

- `fexpr` parses to a `VarExpr`
- we are not in an evalution context
- the `Var` in the `VarExpr` has `:arglists` metadata
- there is an entry in the `:arglists` metadata that has the correct arity
- the type hints on the parameters and the return type have us in ODL land (i.e., `long`, `double`, or `object` for each parameter and the return type).

Suppose these conditions are met.  And suppose the indicated primitive interface is, say `DLO` (take a `double` and a `long`, return an `object`).  Then we generate the following form:

```Clojure
(.invokeStatic (^IFn$DLO form ...args...))
```

(and transfer metadata from the original form to this one).  Analyzing this will generate a host platform interop call -- specifically, a static method call.  The `StaticMethodExpr` will take take care to avoid unnecessary boxing of the argument and return values. 

## A pause that refeshes

Let us contemplate

```Clojure
(ns test.boxing)

(defn fb [x y] (+ x y))

(defn fp  ^double [^double x ^double y] (+ x y))

(defn circle-area 
  ^double  [^double radius] (* radius #?(:clj Math/PI :cljr Double/Pi)))

(defn calling1 ^double [] (circle-area (fb 1.0 2.0 3.0)))

(defn calling2 ^double [] (circle-area (fp 1.0 2.0 3.0)))
```

The only host-interop is getting the value of π.  Compiling (with direct linking turned on) and decompiling to C#, we get:


```C#
public class boxing$fb : AFunction
{
	public static object invokeStatic(object P_0, object P_1)
	{
		return Numbers.add(P_0, P_1);
	}

	public override object invoke(object P_0, object P_1)
	{
		return invokeStatic(P_0, P_1);
	}
    //...
}
```

`fb` ("f with boxing") has no type information, so argument and return types are `object`.  Boxing and unboxing will occur as needed.

```C#
public class boxing$fp : AFunction, DDD
{
	public static double invokeStatic(double P_0, double P_1)
	{
		return P_0 + P_1;
	}

	public override object invoke(object P_0, object P_1)
	{
		return invokeStatic(RT.doubleCast(P_0), RT.doubleCast(P_1));
	}

	public override double invokePrim(double P_0, double P_1)
	{
		return invokeStatic(P_0, P_1);
	}
    //...
}
```
 `fp` ("f with primitives") has `DDD` as a primitive interface.  `invokeStatic` and `invokePrim` require and unboxed values and return unboxed values.  If we come in via   `invoke`, it does casting on the arguments.  There is an implement boxing on the return value.  Also, note that we can compile directly to the `+` operator for `double` values here.
 In `fb`, we have to go through a helper method that has to do dispatch based on the types of the arguments.
 
 `circle-area` is similar.

 Let us compare the calls in `calling1` and `calling2`: `(circle-area (fb 1.0 2.0))`  vs `(circle-area (fb 1.0 2.0))`

```C#
// const__2 and const__3 are boxed 1.0 and 2.0
// We need to cast the result of the $fb$ call to pass to circle_area
// And don't forget the call to Numbers.add inside fb.
boxing$circle_area.invokeStatic(RT.doubleCast(boxing$fb.invokeStatic(const__2, const__3)));
```
vs

```C#
// No boxing anywhere.
boxing$circle_area.invokeStatic(boxing$fp.invokeStatic(1.0, 2.0));
```

Just doing a rough timing in a loop, `calling2` is about 5x as fast as `calling1`.  

## Intrinsics

Why `fp` has a direct IL `add` instruction, while `fb` has to go through `Numbers.add` is a story worthy of its post.  See [C4: Of intrinsic merit][TBD].

## CLR considerations

The efforts outlined above for dealing with primitive types are completely inadequate for ClojureCLR.

Primitive types on the JVM are just the primitive numeric types plus `boolean` and `char`.  That is all that is dealt with in the code above, because these are the values subject to boxing.  And even there, not all numeric types are treated equally.  Types `long` and `double` get special treatment.

The CLR has an unlimited number of types subject to boxing:  any type derived from `System.ValueType`.  This includes all the primitive numeric types (to which you must add the unsigned variants and `decimal`).  But many other types are value types: normally allocated on the call stack, boxed when required to be treated as an object.  Examples include `System.DateTime`, `System.Guid`, and user-defined `struct` types.

There are some hacks in ClojureCLR to make sure the compiler doesn't choke on these types.  But there is no systematic treatment of them, no way to avoid boxing in most situations.  It was the performance hit of boxing and unboxing on value types that led to the efforts of Ramsey Nasser and Tims Gardner to extend the ClojureCLR compiler.  They were working on using ClojureCLR to drive Unity 3D game engine for game development.  That platform uses value types extensively.  Look [here](https://github.com/nasser/magic) for more background on the project.
Their work was a significant inspiration for my work on [ClojureClr.Next](https://github.com/dmiller/clojure-clr-next); this blog post is part of that effort -- I need to understand every single detail of the current compiler in order to undertake a redesign of the required magnitude.


