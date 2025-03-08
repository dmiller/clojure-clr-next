# Typing in original Clojure(JVM/CLR)

Interface `Expr` provides two methods of relevance:

```C#
bool HasClrType { get; }
Type ClrType { get; }
```

Calling `ClrType` when `HasClrType` is false can result in an exception being thrown.

## The obvious ones

Starting with the `Expr` subtypes that are pretty obvious. 
First up: Subtypes of `LiteralExpr`:


|  Type  | `HasClrType`   | `ClrType`   |  Comment |
|:-------|:--------------:|:-----------:|:---------|
| `BooleanExpr`  | true      | `bool`    |   |
| `ConstantExpr` | see below | see below |   |
| `KeywordExpr`  | true | `Keyword` |  |
| `NilExpr`      | true |  `null`   |  |
| `NumberExpr`   | true | `int` <br/> `long` <br/> `double` |  Depends on the value.<br/>  These are the only options allowed. |
| `StringExpr`   | true | `String`   |  |

Note that `NilExpr` is the only expression type that has `ClrType` == `null`.

`ConstantExpr` is a bit more complicated. It holds a value, call it `v`.
If `v` is one of `APersistentMap`, `APersistentSet` or `APersistentVector`, then `HasClrType` is true and `ClrType` is the corresponding one of those three types.  Otherwise, we use the type of `v` _provided_ it is `IsPublic` or `IsNestedPublic` or is an instance of `Type`.  For that last condition, we have in code 

```C#
typeof(Type).IsInstanceOfType(v)
```

This relates to the fact that, e.g., the type of `System.Int64` is `RuntimeType` and `RuntimeType` is not public.  Or so it says.

There are three types that encapsulate values of our significant collections types.

|  Type  | `HasClrType`   | `ClrType`   |  Comment |
|:-------|:--------------:|:-----------:|:---------|
| `MapExpr`      | true | `IPersistentMap`|  |
| `VectorExpr`   | true | `IPersistentVector`|  |
| `SetExpr`      | true | `IPersistentSet`|  |

To these we can add: 

|  Type  | `HasClrType`   | `ClrType`   |  Comment |
|:-------|:--------------:|:-----------:|:---------|
| `EmptyExpr`    | true | `IPersistentList` <br/> `IPersistentMap` <br/> `IPersistentSet` <br/> `IPersistentVector` |  Depends on the value we want the 'empty' of |

Some random examples that are still pretty obvious:

|  Type  | `HasClrType`   | `ClrType`   |  Comment |
|:-------|:--------------:|:-----------:|:---------|
| `DefExpr`      | true | `Var` |  |
| `ImportExpr`   | false | throws | _Is this obvious?_ <br/>  Why not null? |
| `NewExpr`      | true | The type we are constructing an instance of |  |
| `TheVarExpr`   | true | `Var`         |  |
| `UntypedExpr`  | false | throws |  Includes `MonitorEnterExpr`, `MonitorExitExpr`, `ThrowExpr` |
| `UnresolvedVarExpr` | false | throws |  |

## Expressions that pass through the type of their subexpression

Some `Expr` types merely pass along the subtypes of one of their subexpressions.

|  Type  | `HasClrType`   | `ClrType`   |  Comment |
|:-------|:--------------:|:-----------:|:---------|
| `AssignExpr` | `Val.HasClrType` | `Val.ClrType` | `Val` is the expression being assigned to the target |
| `BodyExpr`   | `LastExpr.HasClrType` | `LastExpr.ClrType` | `LastExpr` is the last expression in the body |
| `CaseExpr`   | `returnType` is not null | `returnType` | `returnType` is not null if all case branches have the same return type |
| `LetExpr` <br/> `LetFnExpr` | `Body.HasClrType` | `Body.ClrType` | `Body` is the body of the `let` / `letfn` expression |
| `MetaExpr` | `expr.HasClrType` | `expr.ClrType` | `expr` is the expression being wrapped |
| `TryExpr` | `_tryExpr`.HasClrType | `_tryExpr`.ClrType | `_tryExpr` is the expression being wrapped <br/> not sure why the catch clauses are not examined |


## Recur and conditionsals  

One of my favorite classes in the ClojureCLR code is `Recur`:

```C#
    public static class Recur
    {
        public static readonly Type RecurType = typeof(Recur);
    }
```

This class exists only to provide its own type.  The value `Recur.RecurType` is the type of a `recur` expression:

|  Type  | `HasClrType`   | `ClrType`   |  Comment |
|:-------|:--------------:|:-----------:|:---------|
| `RecurExpr` | true | `Recur.RecurType` |  |

A `recur` expression does not have a value.  It is looping construct, basically a go-to with local variable assignments.  Nothing can follow it in the flow of control.  If it is in a `do`, for example, it can only occur as the last expression.  This is essentially a _tail call_ position.

The only place where `Recur.RecurType` is used, other than `RecurExpr`, is in `IfExpr`.  When does an `IfExpr` have a type?  Both its `thenExpr` and its `elseExpr` must have a  type. and the types must be 'compatible'.  The types are compatible if:

1. They are equal.
2. One of them is `Recur.RecurType`
3. One of them is `null` and the other is not a value type (on the JVM, a primitive types)

## Tags

For most of the remaining expression types, a type can be derived in two ways.  One is from an analysis of its constituents, things like subexpressions or method information.  The other is for the expression to have a tag.  Tags always override internal analysis.

The tags usually are interpreted by the method `HostExpr.TagToType`.  There are several possibilities.

- the tag is a `Symbol` without a namespace:
    - We check if it is in the group of special names: `int`, `long`, `ints`, `longs`, etc.
    - We check if it is mapped to a type in the current namespace.
- the tag is a `Symbol` with a namespace: We check if is an array type:  `int/5` or `String/1`, for example.
- the tag is a type: We use the type.
- If the tag is a `Symbol` with no namespace or a string, we try to look up the type (according to what's appropriate for JVM vs CLR).

In the code for tagged forms, we typically see things like:

```C#
public override bool HasClrType => _tag != null || _tinfo != null;
public override Type ClrType => _tag != null ? HostExpr.TagToType(_tag) : _tinfo.FieldType;
```

Sometimes there is nothing besides the tag, with these forms corresponding simplified.
In the discussion below, I'll just say `hasTag` or `tagType` to convey the notion.

## Some tagged expressions

Let's do some of the simpler tagged expreesions first.

|  Type  | `HasClrType`   | `ClrType`   |  Comment |
|:-------|:--------------:|:-----------:|:---------|
| `VarExpr` | _hasTag_ | _tagType_ |  see below for where the tag might come from |
| `LocalBindingExpr` | _hasTag_ or `lb.HasClrType` | _tagType_ or `lb.ClrType` |  `lb` is the local binding |

The way one gets a `VarExpr` is when syntactic analysis hits a symbol that maps to `Var` in the current namespace.  If the symbol itself is tagged, that is used.  Else, we see if the Var itself itself is tagged -- this might have come from when it `def`-d to begin with, the tag coming from the symbol in the `def` form.

The `LocalBinding` referenced in a `LocalBindingExpr`, though not an `Expr`, does have methods `HasClrType` and `ClrType`.  `LocalBinding.HasClrType` is exceptionally ugly. It does some caching of values that complicates the code; removing that we are left with the following:

```C#
public bool HasClrType
{
    get
    {            
        if (Init != null
            && Init.HasClrType
            && Util.IsPrimitive(Init.ClrType)
            && !(Init is MaybePrimitiveExpr))
            return false;
        else
            return Tag != null || (Init != null && Init.HasClrType);
    }
}

public Type ClrType => Tag != null ? HostExpr.TagToType(Tag) : Init.ClrType;
```

This has the overall format of the code for preferring tags over inferred type.
The complicated conditional in `HasClrType` can be put in words as:

> If there is an initializer and it has a type and the type is primitive but the initializer expression is not capable of emitting a primitive, then this local binding does not have a type.

Note that in this case whether we have a tag or not is irrelevant.  Why can a tag on the local binding symbols not override the type of the initializer?  I don't know.  (In the constructor for `LocalBinding`, an exception is thrown if there is tag and the intializer is a `MaybePrimitiveExpr` and the return type is primitive but not void.)  I suppose because you are going to get a boxed primitive and there is no point in trying to pretend otherwise with a a tag.


## `InvokeExpr` and friends

`InvokeExpr` is more complicated.  The parser for `InvokeExpr` is the last resort  when analyzing a form  that looks like `(f arg1 arg2 ... )`.  We've tried macroexanding it, seeing if `f` is a `Var` with an inline definition, checking for `f` representing special forms that have their own handlers, such as `fn*`, `let*`, `if`, `.`, etc.  If none of these work, we call `InvokeExpr`.

The parser for `InvokeExpr` produces several kinds of expressions, depending on the nature of `f`: `InstanceOfExpr`, `StaticInvokeExpr`,  `KeywordInvokeExpr`, `StaticInvokeExpr`,  various kinds of interop call expressions, and `InvokeExpr` itself.  The details of the parser how they parser picks each of these is not so relevant here.

The various kinds of interop calls that are generated will be covered in the next section. `KeywordInvokeExpr` and `InstanceOfExpr` are quite simple (see table below).  `InvokeExpr` and `StaticInvokeExpr` have the same calculation of typing information -- use the tag if it exists.
The twist here is how the tag is calculated.  In order of preference:

1. The tag on the form itself.
2. The signature tag on the `:arglists` metadata of the `Var` that `f` resolves to.
3. The tag on the `Var` itself.

For the second item, the `:arglists` metadata should be a list of signatures, each signature being a vector such as `[coll x]` or `[coll x & xs]`.  We search the list of signatures to find a match according to the number of arguments in the call, with appropriate handling of variadic signatures.  If we find a match, we take the tag on that vector, if it exists.  That give us:


|  Type  | `HasClrType`   | `ClrType`   |  Comment |
|:-------|:--------------:|:-----------:|:---------|
| `KeywordInvokeExpr` | _hasTag_ | _tagType_ |  |
| `InstanceOfExpr` | true | `bool` |  |
| `InvokeExpr` <br/> `StaticInvokeExpr` | _hasTag_ | _tagType_ | Tag computed as described above |

## Interop calls

The interop calls are the subtypes of `HostExpr`.  For typing purposes, they are all the same.  Each tries to identify a method/property/field to call.  If it exists, it will have a return type. If it doesn't exist, we will be coding reflection for the call and we have no information on the call's return type.  If a tag is provided, it is used instead.  The exception to this is `InstanceZeroArityCallExpr`; when it is issued, we are definitely in a reflection situation, so on the tag can be used.

There is one form of interop call that is not a subtype of `HostExpr`: `QualifiedMethodExpr`.  This comes into play when the functional form is of for the form `Type/name`.  If `name` starts with a `.`, this is intneded to be an instance method call. If the `name` is `new`, then a constructor call intended.  Else, it is a static method call.  A QME can come either in the functional position (head of an invocation) or in a value position.
If it is in a functional position, it is converted into of the subtypes of `HostExpr`.  In this case, any tag will be used to indicate the CLR type, but it is not clear where this is used.


|  Type  | `HasClrType`   | `ClrType`   |  Comment |
|:-------|:--------------:|:-----------:|:---------|
| `InstanceFieldExpr` <br/> `InstancePropertyExpr`  <br/> `InstanceMethodExpr` <br/> `StaticFieldExpr` <br/> `StaticPropertyExpr`  <br/> `StaticMethodExpr | _hasTag_ or _hasRetType_ | _tagType_ or _retType_ |  |    
| `InstanceZeroArityCallExpr` | _hasTag_ | _tagType_ |  |
| `QualifiedMethodExpr` | _hasTag_ | _tagType_ |  |

## `ObjExpr` and friends

`ObjExpr` -- a name I do not understand -- is an abstract class with concrete implementations `FnExpr` and `NewInstanceExpr`, the latter a name I also don't understand. `FnExpr` is used for regular `IFn` functions. `NewInstanceExpr` comes from `deftype` and `reify`.   The CLR type of an `FnExpr` or `NewInstanceExpr` is some kind of functional type.  The defaults are `AFunction` and `IFn`, respectively.  Mostly these don't matter, though there are places where the fact that an `FnExpr` is an `AFunction` comes into play, specifically where protocol implementation is involved.  If a tag is provided, it is used instead, but I don't know why.
Even odder is `NewInstanceExpr`.  It inherits `ClrType` from `ObjExpr`, which calculates its type from

1. Compiled class -- the class that is generated for the `deftype` or `reify` form.
2. Tag -- if it exists.
3. `IFn` -- otherwise.

I cannot find any way that `ClrType` could be called with the compiled class having already been generated.


|  Type  | `HasClrType`   | `ClrType`   |  Comment |
|:-------|:--------------:|:-----------:|:---------|
| `FnExpr` | true | _tagType_  or `AFunction` |  |
| `NewInstanceExpr` | true | compiled-type or _tagType_ or `IFn` |  |

Of more interest are the method classes: `FnMethod` and `NewInstanceMethod`, both subclasses of `ObjMethod`.
These are where the actual code for the functions are located, across various arities.

`ObjMethod` defines properties

```C#
        public abstract Type ReturnType { get; }
        public abstract Type[] ArgTypes { get; }
```

In `FnMethod` these are implemented as

```C#
        public override Type[] ArgTypes
        {
            get
            {
                if (IsVariadic && _reqParms.count() == Compiler.MaxPositionalArity)
                {
                    Type[] ret = new Type[Compiler.MaxPositionalArity + 1];
                    for (int i = 0; i < Compiler.MaxPositionalArity + 1; i++)
                        ret[i] = typeof(Object);
                    return ret;
                }

                return Compiler.CreateObjectTypeArray(NumParams);
            }
        }

        public override Type ReturnType
        {
            get
            {
                if (_prim != null) // Objx.IsStatic)
                    return _retType;

                return typeof(object);
            }
        }
```

These are used to generate the signatures for the invoke methods.  
Thus we have an array whose values are all `typeof(Object)` -- that is the typing for invokes.   
The return type is `typeof(object)` unless the method is a primitive method, 
in which case the return type is the primitive type.  
The `retType` value initially is transferred from the `:tag` metadata of the defining 
form on the name in the `defn` form.  But that is the last priority.
Higher priority are the `:tag` metadata on the parameter vector and the `:tag` metadata on the `:arglists` entry (for the signature with matching argument count).


## MaybePrimitiveExpr

Some of the node types implement the `MaybePrimitiveExpr` interface, defined as follows.

```C#
public interface MaybePrimitiveExpr : Expr
{
    bool CanEmitPrimitive { get; }
    void EmitUnboxed(RHC rhc, ObjExpr objx, CljILGen ilg);
}
```

Implementing this interface indicates there is a chance the expression can emit an unboxed primitive.
In the context of classic Clojure(JVM/CLR) the only primitives allowed are `long` and `double`. In ClojureCLR.Next, the intentions is to have a compiler mode that extends this to all value types.

The expression types that implement `MaybePrimitiveExpr` are:

|    |   |   |   |
|:--:|:--:|:--:|:--:|
| `LocalBindingExpr` |  `BodyExpr` | `CaseExpr` | `IfExpr` |
| `InstanceOfExpr` | `LetExpr` | `LetFnExpr` | `MethodParamExpr` |
| `NumberExpr` | `RecurExpr` | `StaticInvokeExpr` | `HostExpr`+subtypes | |


Let us consider one example.  When can `BodyExpr` emit an unboxed primitive?  Its code consists of code for each of the expressions in the body, with all but the last having their values discarded.
The last expression is the one that matters.  If it is a `MaybePrimitiveExpr`, then `BodyExpr` can emit an unboxed primitive.  If it is not, then `BodyExpr` cannot emit an unboxed primitive.

```C#
    public bool CanEmitPrimitive
    {
        get { return LastExpr is MaybePrimitiveExpr expr && expr.CanEmitPrimitive; }
    }
 ```
 What is the difference between the regular `Emit` code and the `EmitUnboxed` code?  The former concludes with:

```C#
LastExpr.Emit(rhc, objx, ilg);
```

The latter concludes with

```C#
MaybePrimitiveExpr mbe = (MaybePrimitiveExpr)LastExpr;
mbe.EmitUnboxed(rhc, objx, ilg);
```

What expression types are not `MaybePrimitiveExpr`?  And can we guess why?


| Type  | Reason? |
|:-----|:--------|
| `BooleanExpr`  | It has a `bool` value, but the only primitives that count are `long` and `double`. |
| `ConstantExpr` | It might hold a value which is a primitive, but `double` or `long` constants will be parsed as `NumberExpr`, so this can't be primitive by our definition. |
| `InvokeExpr` | this will call some variant of `IFn.invoke` which has returns a reference. |
| `KeywordInvokeExpr` | Does a lookup in a map, will return a reference. |
| `DefExpr` <br/>  `TheVarExpr` <br/> `VarExpr` | Returns a `Var`| 
| `KeywordExpr` | Returns a `Keyword` |
| `MetaExpr` | The wrapped expressions must be an `IObj`, which is a reference type. |
| `NewExpr` | Returns a new instance of a class, which is a reference type. |
| `EmptyExpr` <br/> `MapExpr` <br/> `SetExpr` <br/> `VectorExpr` | These are collections, which are reference types. |
| `StringExpr` | Returns a `String` |
| `ObjExpr` <br/> `FnExpr` <br/> `NewInstanceExpr` | These are functional types, which are reference types. | 
| `ImportExpr` <br/> `MonitorEnterExpr` <br/> `MonitorExitExpr` <br/> `ThrowExpr` <br/> `UnresolvedVarExpr` <br/> `UntypedExpr` | `Void` or no return at all |
| 
| `QualifiedMethodExpr` | Generates an `FnExpr` |


There are two expression types for which I'm not sure why they couldn't be `MaybePrimitiveExpr`: `AssignExpr` and `TryExpr`.  I may have to think more.

The real essence of `MaybePrimitiveExpr` becomes apparent when you look at how code is emitted, specifically, the attempts to avoid boxing for known primitive values.  That is for another time and place.