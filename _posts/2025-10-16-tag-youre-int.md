---
layout: post
title: C4 - Tag! You're int!
date: 2025-10-16 00:00:00 -0500
categories: general
---

Chasing type hints around the Clojure compiler.

## Introduction

Clojurists by nature are type-loose; they'd prefer not to think about types.  But a Clojurist must contemplate types occasionally, to avoid reflection in platform interop and to avoid boxing numeric values.  

The Clojure compiler computes and propagates type information throughout the abstract syntax tree (AST) as it is constructed. These calculations combine user-supplied type hints,  characteristics of individual functions (return types, signatures of host platform methods), and inherent characteristics of the each basic expression class.

This post explores how typing information is propagated through the AST.  In post [Primitive urges][TBD], we will see how the compiler uses this information to avoid boxing of numeric values.  How reflection is avoided will be covered in [C4: A time for reflection][TBD].

## Just a hint

Clojure coders supply type hints in various ways.  The most common is a `^SomeType` metadata tag, which translates to `{:tag SomeType}` in the metadata map.  One should keep in mind that the `:tag` is a hint, not a mandate.  If one writes:

```clojure
(defn f  [^String s] 
  (if (>= (count s) 18)
       Int32/MaxValue
       (Int32/Parse s)))
```

the `^String` type hint will prevent reflection on the call to `Int32/Parse`.
But it is still perfectly okay to pass a non-string value to `f`.  It will be noticed unless its count is less than 18, in which case an exception will be thrown -- Can't cast whatever to String.

## The root

Where to begin?  We know tags are important, and in fact a field named `Tag` or something similar appears in many AST node classes.  The string "tag" appears in the the JVM's "Compiler.java" file 284 times.  Happy code chasing!

A slightly more systematic approach starts with the hierarchy of AST node types.  The root is `Expr`; it exposes type information for each class.

```C#
public interface Expr
{
    bool HasClrType { get; }    // hasJavaClass in ClojureJVM
    Type ClrType { get; }       // getJavaClass in ClojureJVM
    // ...
} 
```  

Note that if `HasClrType` is false, then `ClrType` will throw an exception if accessed.  Always check `HasClrType` first.

I went through the 55 classes starting at `Expr` and descending the hierarchy and examined the code for each `HasClrType` and `ClrType` for each.   (ClojureCLR has a few more `Expr`-derived classes than ClojureJVM due to some differences in how the former deals with subclasses of `HostExpr`.) You can see my results in [a PDF of my Excel spreadsheet]({{site.baseurl | prepend: site.url}}/assets/expr-analysis.pdf"). 

A quick examination reveals some general categories of AST nodes that we can dispense with quickly.

- Node types derived from `UntypedExpr`:  `MonitorEnterExpr`, `MonitorExitExpr`, and `ThrowExpr`. Not a surprise to learn that `HasClrType` is always false for these.
- Other node types that do not carry type information: `ImportExpr`, `UnresolvedVarExpr`.
- Node types that hold constant values or similar data that carry the type.

| Node type   | `ClrType`   |   Comment |
|:-----------:|:-----------:|:---------|
| `BooleanExpr` | `typeof(bool)` | A boolean literal always yields a bool |
| `ConstantExpr`| The type of the value it holds, mostly | Values that inherit from `APersistentMap`, `APersistentVector`, `APersistentSet`, and `Type` use those types, otherwise `_v.GetType()` |
| `DefExpr`   | `typeof(Var)` | A `DefExpr` always yields a `Var` |
| `EmptyExpr` | the type of the empty collection to be produced | `IPersistentList`, `typeof(IPersistentVector)`, `IPersistentSet`, `IPersistentMap` |
| `InstanceOfExpr` | `typeof(bool)` | An `instance?` expression always yields a bool |
| `KeywordExpr` | `typeof(Keyword)` | A keyword literal always yields a `Keyword` |
| `MapExpr` | `typeof(IPersistentMap)` | A map literal always yields a map |
| `NilExpr` | `null` | The `nil` literal always yields null |
| `NumberExpr` | The specific numeric type it holds | `int`, `long`, or `double`.  Other numeric values are held in a `ConstantExpr`. |
| `SetExpr` | `typeof(IPersistentSet)` | A set literal always yields a set |
| `StringExpr` | `typeof(string)` | A string literal always yields a string |
| `TheVarExpr` | `typeof(Var)` | From `#'Name`,  always yields a `Var` |
| `VectorExpr` | `typeof(IPersistentVector)` | A vector literal always yields a vector |

- Node types that return their tag if present: `VarExpr`, `KeywordInvokeExpr`.

- Pass-throughs: these node types simply yield the type of their contained expression.

| Node type   |    Comment |
|:-----------:|:-----------|
| `AssignExpr` | The type of the value being assigned to the target. |
| `BodyExpr` | The type of the last expression in the body. |
| `LetExpr` | The type of its body (see above).| 
| `LetFnExpr` | Ditto. |
| `MetaExpr` | The type of the expression we are attaching the metadata to. |

There are several 'pass-throughs' that deserve special mention.

- `CaseExpr`:  A case expression has a sequence of tests and corresponding 'then' expression, as well as default expression.  When a `CaseExpr` is being parsed, a list is formed of the 'then' expressions and the default expression.
This list is passed to `Compiler.MaybeClrType`:

```C#
internal static Type MaybeClrType(ICollection<Expr> exprs)
{
    Type match = null;
    try
    {
        foreach (Expr e in exprs)
        {
            if (e is ThrowExpr)
                continue;
            if (!e.HasClrType)
                return null;
            Type t = e.ClrType;
            if (t == null)
                return null;
            if (match == null)
                match = t;
            else if (match != t)
                return null;
        }
    }
    catch (Exception)
    {
        return null;
    }
    return match;
}
```
 In other words, throws are ignored.  If any expression has no type, the `CaseExpr` has no type.  If all expressions have the same type, that is the type of the `CaseExpr`.  Otherwise, the `CaseExpr` has no type.

- `IfExpr`: The most obvious statement would be:  if the types of the 'then' clause and the 'else' clause match, use that.    However, there is a complication: `recur`.  Typically, a `recur` expression will occur inside either the 'then' or the 'else' clause of an `if` expression.  Moreover, it has to have nothing following it, so it  will surface as the "value" of the containing clause.

Of course, a `recur` actually does not have a value; it is a go-to: strictly flow control.  However, we code `RecurExpr` so that it does return a type, a special type used only here, a type that when detected by `IfExpr` indicates that a `recur` is present. 

The type that is used is `typeof(Recur)`.  [Note: not `RecurExpr`.]  Defined as

```C#
public static class Recur
{
    public static readonly Type RecurType = typeof(Recur);
}
```

In `RecurExpr`, `HasClrType` is always true and `ClrType` always returns `Recur.RecurType`.

For an `IfExpr` to have a type, then 'then' clause and the 'else' clause each much have a type and they must agree or one of them must be `Recur.RecurType`.  We also allow a `null` value to match and reference type (which is say, any non-value type).  

```C#
return _thenExpr.HasClrType
&& _elseExpr.HasClrType
&& (_thenExpr.ClrType == _elseExpr.ClrType
    || _thenExpr.ClrType == Recur.RecurType
    || _elseExpr.ClrType == Recur.RecurType
    || (_thenExpr.ClrType is null && !_elseExpr.ClrType.IsValueType)
    || (_elseExpr.ClrType is null && !_thenExpr.ClrType.IsValueType));
```

and for `IfExpr.ClrType`:

```C#
Type thenType = _thenExpr.ClrType;
if (thenType is not null && thenType != Recur.RecurType)
    return thenType;
return _elseExpr.ClrType;
```

- `TryExpr`:  A `try` expression has a body, zero or more `catch` clauses, and an optional `finally` clause.  The type of the `try` expression is determined by the body only.  The `catch` clauses are not considered, because they are only executed if an exception is thrown, and the `finally` clause is not considered because it does not yield a value.  

One could argue (I would) that the values of exceptional paths are still part of the contract of the expression.  We have a try in order to provide regularity of output.  We catch exceptions that we have a reasonable expectation might occur and that we know how to deal with in a reasonable manner. I would consider this code:

```clojure
(try
   (Int32/Parse x)
   (catch FormatException e "Doofus!"))
```

to be unhappy coding; better to return a default value.  Or have the whole thing return some kind of `Option` or `Result` type that captures the duality of response.    In Clojure (JVM or CLR), the type of the expression will be `int`, even though it can yield a string.  I did make an inquiry about this.  The answer: "With a try, the body is the “normal” (non-exceptional) path[.]  getJavaClass() is not a type but a type hint, and seems like the hint should be the expected type of the body[.]"

I'm probably just type-perverted from spending so much time coding in C#/F#.  Can't be helped.

## Interop results

Even though we plan to cover the details of reflection for interop elsewhere, we can here consider the result types of interop calls.  `HostExpr` is the base class for most interop expressions.  There is also `NewExpr` for constructor calls.  The hierarchy is:

<img src="{{site.baseurl | prepend: site.url}}/assets/images/hostexpr-type-dependencies.png" alt="Interop class dependency graph" />


ClojureCLR has a few more classes than ClojureJVM here because we have to deal with properties in addition to fields and methods.  Fields and properties share enough characteristics that they can share a base class, `InstanceFieldOrPropertyExpr` or `StaticFieldOrPropertyExpr`.  

The classes under `HostExpr` handle `HasClrType` and `ClrType`  almost identically.  There are two ways type information is available.  If the method or property or field is known, i.e., we have identified a `Type` and corresponding `MethodInfo`, `PropertyInfo`, or `FieldInfo`, then the type is available from that.  Or the user can supply a type hint on the interop call expression.  If we are in a reflection situation and do not have the `MethodInfo`, `PropertyInfo`, or `FieldInfo`, then the type hint will be all that we have.  If both are availble, there are two circumstances:  For field/property, the user type hint takes precedence.
For method calls, it's a bit more complicated. See below.

`InstanceFieldExpr` and `InstancePropertyExpr` are almost identical.  Let's use the former:


```C#
public override bool HasClrType
{
    get { return _tinfo != null || _tag != null; }
}

public override Type ClrType
{
    get
    {
        if (_cachedType == null)
            _cachedType = _tag != null ? HostExpr.TagToType(_tag) : _tinfo.FieldType;
        return _cachedType;
    }
}
```

Here `_tinfo` is the `FieldInfo` and `_tag` is the user-supplied type hint.  We cache the computed type in `_cachedType` for efficiency.  The only difference for `InstancePropertyExpr` is that `_tinfo` is a `PropertyInfo` and we use its `PropertyType` property.
We discuss `HostExpr.TagToType` below.

`InstanceMethodExpr` is similar:

```c#
public override bool HasClrType
{
    get { return _method != null || _tag != null; }
}

public override Type ClrType
{
    get
    {
        if (_cachedType == null)
            _cachedType = Compiler.RetType((_tag != null ? HostExpr.TagToType(_tag) : null), _method?.ReturnType);
        return _cachedType;
    }
}
```

`Compiler.RetType` requires some explanation.  It is defined as:

```c#
// tc is the user-supplied type hint, ret is the return type of the method
// either can be null.
public static Type RetType(Type tc, Type ret)
{
    // if one is null, use the other
    if (tc == null)
        return ret;
    if (ret == null)
        return tc;

    // both are non-null
    // if both are primitive types, they must match or be compatible integral types
    if (ret.IsPrimitive && tc.IsPrimitive)
    {
        if ((Inty(ret) && Inty(tc)) || (ret == tc))
            return tc;
        throw new InvalidOperationException(String.Format("Cannot coerce {0} to {1}, use a cast instead", ret, tc));
    }

    // both are non-null and at least one is not primitive.
    // Prefer the user-supplied type hint
    return tc;
}
```

Static method/property/field expressions are almost identical.
The only significant difference is that `HasClrType` always returns true: there is always a type known for static properties and fields.

The only remaining `HostExpr` derivative is `InstanceZeroArityCallExpr`.  This is created when we have an interop call that we know is an instance call (as opposed to a static call) but we can't resolve the name of the method/property/field.  We are in a reflection situation.  The only way we have a `ClrType` is if the user has supplied a type hint.  

That leaves us with `NewExpr`.  For `NewExpr`, we know the type we are creating; that gives us our `ClrType`.  

I'll toss one more in here.  Though not derived from `HostExpr`, `StaticInvokeExpr` is just a special case of `StaticMethodExpr` where the method is an `invokeStatic` on an `IFn`-derived class.  This node type comes up in direct linking of function calls.  For more information, refer to [C4: Functional anatomy]({{site.baseurl}}{% post_url 2025-09-04-functional-anatomy %}) and [C4: fn*: talkin' 'bout my generation][TBD].


## Symbols and bindings

We talked about the analysis of symbols in [C4: Symbolic of what?]({{site.baseurl}}{% post_url 2025-09-02-symbolic-of-what %}).  We did not dwell on the role of type hints in that analysis.
But it is there at the very start:

```c#
private static Expr AnalyzeSymbol(Symbol symbol)
{
    // The tag on the symbol will be used to pass along user-specified type hints to various constructs that will use them.
    Symbol tag = TagOf(symbol);

    // ...
}
```

Where does this tag go?  How is it used?

The possible expressions returned by `AnalyzeSymbol` are:

| Expression type | Comment |
|:----------------:|:--------|
| `new LocalBindingExpr(b, tag)` | A local binding, e.g. a `let` or `fn` parameter |
| `new StaticFieldExpr(... , tag, t, symbol.Name, finfo);` | A static field access |
| `new StaticPropertyExpr(..., tag, t, symbol.Name, pinfo);` | A static property access |
| `new QualifiedMethodExpr(t, symbol)` | A qualified method reference (static, instance, or constructor on a known type) |
| `new VarExpr(oAsVar, tag);` | A variable reference |
| `new ConstantExpr(o);` | A constant value |
| `new UnresolvedVarExpr(oAsSymbol);` | An unresolved variable reference |
| `new VarExpr(sym, tag);` | A Var definition |

We have already discussed `StaticFieldExpr`, `StaticPropertyExpr`, `ConstantExpr`,`UnresolvedVarExpr`, and `VarExpr`.  

We'll have more to say about `QualifiedMethodExpr` when we talk about invocations below.
THough it may look like it ignores the tag, the first thing it does in its constructor is ... grab the tag from the symbol.  (Why we don't just pass it, I can't say.  I copied the JVM code.)

Leaving us with `LocalBindingExpr`.  Local bindings are created when parsing function bodies and `let*`. `letfn*` and the `catch` blocks in `try`  expressions.   Local bindings have a name (a `Symbol`), may have a binding expression to provide an initial value, and may be tagged.  There is a `LocalBinding` class that holds this information.  Even though `LocalBinding`is not `Expr`-derived, it does implement our two favorite properties.  It caches both the `HasClrType` and `ClrType` values when first computed.  For the former, it confusingly has a flag to indicate if the other flag has been computed.

```C#
// Have we computed HasClrType?
bool _hasTypeCached = false;

// If we have computed HasClrType, that value is cached here.
bool _cachedHasType = false;

// The cached type for ClrType, if we have computed it.
Type _cachedType;

public bool HasClrType
{
    get
    {
        if (!_hasTypeCached)
        {
            // first time, not yet computed and cached
            // See below -- this is tricky.
            if (Init != null
            && Init.HasClrType
            && Util.IsPrimitive(Init.ClrType)
            && !(Init is MaybePrimitiveExpr))
                _cachedHasType = false;
            else
                // the easy case.
                _cachedHasType = Tag != null || (Init != null && Init.HasClrType);

            // Mark that we have set the value of _cachedHasType
            _hasTypeCached = true;
        }
        return _cachedHasType;
    }
}

public Type ClrType
{
    get
    {
        if (_cachedType == null)
            _cachedType = Tag != null ? HostExpr.TagToType(Tag) : Init.ClrType;
        return _cachedType;
    }
}
```

If you are confused looking at `HasClrType`, join my club.  In a simpler world, we would just have

```C#
_cachedHasType = Tag != null || (Init != null && Init.HasClrType);
```

We have a type if either we have a tag or we have an initialization form and _it_ has a type.
There is a condition above this which negates having a type. In English:  if there is an initialization form, and it has a type and its type is a primitive type (this would have to be a tag on the initialization expression), but the initialization expression is not a `MaybePrimitiveExpr` -- something capable of emitting a primitive value -- then we are in trouble.  We want to hold a primitive value without boxing, but our initialization is at best going to yield a boxed value.  So we say we don't have a type.   We discuss primitive types in great detail in [C4: Primitive urges][TBD].

If we do have a type, a user-supplied tag takes precedence over the type of the initialization expression, per usual.

 `LocalBindingExpr` wraps a `LocalBinding`.  The occurrence of the reference (the `Symbol` name of the binding) can also be tagged, yielding a third source  of type information.  This is straightforward:  the tag on the symbol at the reference site takes precedence over the type coming from the `LocalBinding` definition.  The code is starting to look kind of familiar:

```C#
public bool HasClrType
{
    get { return _tag != null || _b.HasClrType; }
}

public Type ClrType
{
    get
    {
        if (_cachedType == null)
            _cachedType = _tag != null ? HostExpr.TagToType(_tag) : _b.ClrType;
        return _cachedType;
    }
}
```


## Function definitions

Final category.  The universe of dicussion:

<img src="{{site.baseurl | prepend: site.url}}/assets/images/objexpr.png" alt="Graph of all types related to ObjExpr" />

The most common way for `fn*` forms to be created is via `defn`

```clojure
(defn ^String ^{:my-data 2} f "Comment" {:other-data 3} 
     ^double [^double x]  (- Double/MaxValue x))
```

If you macroexpand this, you get 

```
(def f (clojure.core/fn ([x] (str (- Double/MaxValue x)))))
```

What you are missing here is the metadata that is attached.  On the symbol `f`, you will find:

```
{:my-data 2, :tag String, :arglists (quote ([x])), :doc "Comment", :other-data 3}
```

Mostly there is a pass-through of the metadata on `f` in the defining form, with some additions: the comment gets attached via `:doc`; the options map after the comment gets merged and  `:arglists` metadata is attached. 

 If you look at the form `(clojure.core/fn ...)`, you will find the metadata

```
{:rettag String}
```

The `:tag` from the `defn` symbol has been passed along via the `:rettag` key.
Other metadata that you see gets passed along.

The `fn` form in this case expands pretty directly to an `fn*` form; it doesn't have any fancy arglist destructuring to deal with or other complications.  So the remaining metadata gets passed along.

How is the `:rettag` used?  And what if there is a `:tag` on the `fn*` form itself in addition?

The `:tag` function in this case has nothing to do with the return type of the function being defined.  Rather it is the type (hint) for the function object itself.  An `FnExpr` creates a function class; that class will be instantiated to create the function object that we can invoke.  For `FnExpr` then we have


```C# 
public override bool HasClrType
{
    get
    {
        return true;
    }
}

public override Type ClrType
{
    get
    {
        if (_cachedType == null)
            _cachedType = _tag != null ? HostExpr.TagToType(_tag) : typeof(AFunction);
        return _cachedType;
    }
}
```

Note that the default here in the absence of a tag is `typeof(AFunction)`.  When type analysis is being done and we run into an `FnExpr` node in the AST, seeing `AFunction` as the type tells us that `IFn` is implemented--we can invoke it--and also that it supports attaching metadata. (`IMeta` and `IObj` are implemented.)

The `:rettag` value is passed along to the parser for `FnMethod`.  In the parser we find:

```C#
if (retTag is String)
    retTag = Symbol.intern(null, (string)retTag);
if (retTag is not Symbol)
    retTag = null;
if (retTag is not null)
{
    string retStr = ((Symbol)retTag).Name;
    if (!(retStr.Equals("long") || retStr.Equals("double")))
        retTag = null;
}
method._retType = Compiler.TagType(Compiler.TagOf(parms) ?? retTag);

if (method._retType.IsPrimitive)
{
    if (!(method._retType == typeof(double) || method._retType == typeof(long)))
        throw new ParseException("Only long and double primitives are supported");
}
else
    method._retType = typeof(object);
```

In other words unless the `:rettag` is ``long`` or ``double``, it is ignored.
And it is also ignored if there is a `:tag` on the parameter list of the method.

But there's more.  The code above is followed by:

```C#
if (method._retType.IsPrimitive)
{
    if (!(method._retType == typeof(double) || method._retType == typeof(long)))
        throw new ParseException("Only long and double primitives are supported");
}
else
    method._retType = typeof(object);
```

By the time you get through this code, `method._retType` is either `typeof(long)`, `typeof(double)`, or `typeof(object)`.  Hmmm.  Where have we seen this combo before?  ODL ... primitive interfaces.  We retain just enough information to calculate which prim interface we should implement.  (See [C4: Primitive urges][TBD].)

We've handled:

- a type hint on the symbol name of a `defn`.
- a type hint on the parmamter list of `fn*` method definiition.

The remaining type hints we will encounter in function definitions are the type hints on the parameters themselves.  For each parameter, we do the following:

- Make sure it is a symbol without a namespace.
- Compute the 'declared type' of the parameter.  This is done by:
    - Pull the tag from the symbol metadata and convert a type.  
    - If it is a primitive type, it must be `long` or `double`.  
    - If not primitive, we use `typeof(object)`.
    - If no tag, we use `typeof(object)`.  Again, we are in ODL land.
- Create a `LocalBinding` for the parameter.
    - The `LocalBinding` has `Tag` field.  Here we pass in the `:tag` on the parameter symbol directly, unless the declared type is primitive (`long` or `double` only), in which case this is set to null.  I do not know why.  But we'll be okay. See the kludge below.
    - The `LocalBinding` also has a `DeclaredType` field.  The value computed in the previous step is used here.  This is _only_ used during `Recur` calculations.
    - There is some really weird kludginess of passing a `MethodParamExpr` as initialization expression to `LocalBinding` but only when the declared type is primitive. The sole purpose of that is make sure `LocalBinding.PrimitiveType` returns the primitive type. Massive kludge.  Ick.

There are some provisions to properly deal with `&` and the following "rest" parameter (which will have a type of `ISeq`).

Let's take a brief look at the kludge.  When the parameter has a primitive type, we set its `Tag` to null, its `DeclaredType` to the primitive type, and its `Init` to a `MethodParamExpr`.  The `MethodParamExpr` is defined as:

```C#
public sealed class MethodParamExpr : Expr, MaybePrimitiveExpr
{
    readonly Type _t;
    public Type Type { get { return _t; } }

    public MethodParamExpr(Type t)
    {
        _t = t;
    }

    public bool HasClrType => _t != null;
    public Type ClrType => _t;

    // Other things not relevant here.
}
```

An instance of `MethodParamExpr` is used as the initialzation expression for the `LocalBinding`.
The `ClrType` for `LocalBinding` is this:

```C#
Tag != null ? HostExpr.TagToType(Tag) : Init.ClrType;
```

In our situation, `Tag` is null, so we get the `ClrType` of the `MethodParamExpr`, which is the primitive type.  Perfect.
When we have a `LocalBindingExpr` referring to this `LocalBinding`, things also work.
Recall that `LocalBindingExpr.ClrType` is:

```C#
_tag != null ? HostExpr.TagToType(_tag) : _b.ClrType;
```

Therefore, if the user provided a type hint at the point of reference, that will be used.  Otherwise, we get the `ClrType` of the `LocalBinding`, which is the primitive type.  Perfect.

Does your head hurt yet?  I've been whistling past this graveyard for 15 years.  Nice to finally figure it out and set it down in writing.


`NewInstanceExpr` is used for `deftype` and `reify` implementation.  Type hints here are return types for methods and parameter type hints.  Unlike with `FnExpr`/`FnMethod`, these are passed along as is -- no ODL simplification.  Please appreciate the brevity of this paragraph.


## Recur-ing nightmares

We've had enough. I'll put off discussion of how type information plays into `RecurExpr`  until later.  See [C4: Primitive urges][TBD].

## Processing tags

Most of the forms that deal with attached type hints do a bit of processing on the tag in order to convert it to a `Type`.  The tag can be a `Type` instance, a `Symbol`, or a `string`.  
Handling the tags usually comes in two stages:

- `Compiler.TagOf` is called to extract the tag form the metadata and convert it to a symbol.
- `HostExpr.TagToType` is called to convert the symbol to a `Type`.

`Compiler.TagOf` is defined as:

```C#
internal static Symbol TagOf(object o)
{
    // Get the :tag key value from the metadata
    object tag = RT.get(RT.meta(o), RT.TagKey);

    // Take Symbols as is.
    if (tag == null)
    {
        Symbol sym = tag as Symbol;
        if (sym != null)
            return sym;
    }

    // Convert a string to a symbol.
    {
        if (tag is String str)
            return Symbol.intern(null, str);
    }

    // Convert a Type to a symbol.
    {
        Type t = tag as Type;
        if (t != null && TypeToTagDict.TryGetValue(t, out Symbol sym))
        {
            return sym;
        }
    }

    return null;
}

static readonly Dictionary<Type, Symbol> TypeToTagDict = new()
{
    { typeof(bool), Symbol.create(null,"bool") },
    { typeof(char), Symbol.create(null,"char") },
    { typeof(byte), Symbol.create(null,"byte") },
    { typeof(sbyte), Symbol.create(null,"sbyte") },
    { typeof(short), Symbol.create(null,"short") },
    { typeof(ushort), Symbol.create(null,"ushort") },
    { typeof(int), Symbol.create(null,"int") },
    { typeof(uint), Symbol.create(null,"uint") },
    { typeof(long), Symbol.create(null,"long") },
    { typeof(ulong), Symbol.create(null,"ulong") },
    { typeof(float), Symbol.create(null,"float") },
    { typeof(double), Symbol.create(null,"double") },
};
```



Conversion from symbol to type is handled by:

```C#
internal static Type TagToType(object tag)
{
    Type t = null;

    Symbol sym = tag as Symbol;
    if (sym != null)
    {
        if (sym.Namespace == null)
        {
            t = maybeSpecialTag(sym);
        }
        if (t == null)
        {
            t = HostExpr.MaybeArrayType(sym);
        }
    }

    if (t == null)
        t = MaybeType(tag, true);

    if (t != null)
        return t;

    throw new ArgumentException("Unable to resolve typename: " + tag);
}
```

`maybeSpecialTag` handles the special names such as `doubles` and `longs` (but not `double` or `long` -- they are caught in `MaybeType`).  `MaybeArrayType` handles array types.  `MaybeType` handles the rest.  


```c#
// only handles special names like "doubles", "longs", "ints", etc.
public static Type maybeSpecialTag(Symbol sym)
{
    Type t = Compiler.PrimType(sym);
    switch (sym.Name)
    {
        case "objects": t = typeof(object[]); break;
        case "ints": t = typeof(int[]); break;
        case "longs": t = typeof(long[]); break;
        case "floats": t = typeof(float[]); break;
        case "doubles": t = typeof(double[]); break;
        case "chars": t = typeof(char[]); break;
        case "shorts": t = typeof(short[]); break;
        case "bytes": t = typeof(byte[]); break;
        case "booleans":
        case "bools": t = typeof(bool[]); break;
        case "uints": t = typeof(uint[]); break;
        case "ushorts": t = typeof(ushort[]); break;
        case "ulongs": t = typeof(ulong[]); break;
        case "sbytes": t = typeof(sbyte[]); break;
    }
    return t;
}
```


`HostExpr.MaybeArrayType` is:

```C#
public static Type MaybeArrayType(Symbol sym)
{
    if (!LooksLikeArrayType(sym))
        return null;

    return MaybeType(BuildArrayTypeDescriptor(sym), true);
}

// looks like  Name/digit, e.g. System.Int32/3
public static bool LooksLikeArrayType(Symbol sym)
{
    return sym.Namespace is not null && Util.IsPosDigit(sym.Name);
}

// Take the type name and add enough [] to make an array type of appriopriate nesting.
// int/3  -> System.int32[][][]
public static string BuildArrayTypeDescriptor(Symbol sym)
{
    int dim = sym.Name[0] - '0';
    Symbol componentTypeName = Symbol.intern(null, sym.Namespace);
    Type componentType = Compiler.PrimType(componentTypeName);

    if (componentType is null)
        componentType = MaybeType(componentTypeName, false);

    if (componentType is null)
        throw new TypeNotFoundException(componentTypeName.ToString());

    StringBuilder arrayDescriptor = new();
    arrayDescriptor.Append(componentType.FullName);


    for (int i = 0; i < dim; i++)
    {
        arrayDescriptor.Append("[]");
    }

    return arrayDescriptor.ToString();
}
```        

Finally, the monster of the them all: `MaybeType`.

```C#
public static Type MaybeType(object form, bool stringOk)
{
    // Types are good -- us it as is.
    if (form is Type type)
        return type;

    Type t = null;
    if (form is Symbol sym)
    {
        if (sym.Namespace == null) // if ns-qualified, can't be classname
        {
            // Don't look at this.  The compiler plays some games with stub classes.
            if (Util.equals(sym, Compiler.CompileStubSymVar.get()))
                return (Type)Compiler.CompileStubClassVar.get();

            //  We have a name.with.dots or name[whatever]  
            //  Let our name->Type resolver handle it.  
            if (sym.Name.IndexOf('.') > 0 || sym.Name[sym.Name.Length - 1] == ']')  // Array.  JVM version detects [whatever  notation.

                t = RT.classForNameE(sym.Name);
            else
            {
                // See if we have an alias.
                object o = Compiler.CurrentNamespace.GetMapping(sym);
                if (o is Type type1)
                {
                    t = type1;

                    var tName = type1.FullName;
                    // This has to do with some nastiness in the compiler 
                    // dealing with compilation to file under .Net 9.  Sigh.
                    var compiledType = Compiler.FindDuplicateCompiledType(tName);
                    if (compiledType is not null && Compiler.IsCompiling)
                        t = compiledType;
                }
                // Make sure we don't have a local binding name
                else if (Compiler.LocalEnvVar.deref() != null && ((IPersistentMap)Compiler.LocalEnvVar.deref()).containsKey(form))  // JVM casts to java.util.Map
                    return null;
                else
                {
                    // let our name->Type resolver handle it.
                    try
                    {
                        t = RT.classForName(sym.Name);
                    }
                    catch (Exception)
                    {
                        // aargh
                        // leave t set to null -> return null
                    }
                }
            }

        }
    }
    else if (stringOk && form is string str)
        t = RT.classForNameE(str);

    return t;
}
```

Take a deep breath.  Take a long, hot shower.  Grab a nap.  You deserve it.