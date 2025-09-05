---
layout: post
title: C4 - AST me anything
date: 2025-09-01 00:00:00 -0500
categories: general
---

We give a quick overview of all the AST node types used in the Clojure compiler..
We finish with a quick look at `Compiler.Analyze`, which generates an AST from a form.

Alternative title:  _Analyze this!_   


## The roots

To be an an AST node, a class must implement the `clojure.lang.Compiler.Expr` inteface:

```C#
    public interface Expr
    {
        // Supports direct evaluation and code generation
        object Eval();
        void Emit(RHC rhc, ObjExpr objx, CljILGen ilg);
        
        // provides typing information
        bool HasClrType { get; }
        Type ClrType { get; }

        // Technical detail -- more later.
        bool HasNormalExit();
    }
```

In addition, some node types support the possibility of generating unboxed primitive values and have special code generation code for this.  These types implement `MaybePrimitiveExpr` instead of just `Expr`:

```C#
    public interface MaybePrimitiveExpr : Expr
    {
        // Whether or not this instance can 
        bool CanEmitPrimitive { get; }

        // Emit without boxing
        void EmitUnboxed(RHC rhc, ObjExpr objx, CljILGen ilg);
    }
```

Finally, one interface indicates that the expression can be the target of a `set!`.  These node types must supply their own code generation for doing assignment, either in direct evaluation or for code generation:

```C#
    public interface AssignableExpr
    {
        object EvalAssign(Expr val);
        void EmitAssign(RHC rhc, ObjExpr objx, CljILGen ilg, Expr val);
    }
```

## The (really) big picture

See and believe.

<img src="{{site.baseurl | prepend: site.url}}/assets/images/xpr-type-dependencies.dot.png" alt="Graph of all AST node types" />


To make this is a bit more manageable, we can cluster the node types according to some common traits beyond just inheritance.

__Cluster #1 -- Data values__: Some of the node types wrap a data value.  This includes all of the subtypes of `LiteralExpr` -- literal constants.   

| Expr type | Description |
|:----------|:------------|
| `BooleanExpr` |   Holds a true/false value  |
| `KeywordExpr` |   Holds a keyword  |
| `NilExpr` |   Nil, just nil.  |
| `NumberExpr ` |   Holds an `int`, `long`, or `double`.  Other numeric types will be held in a `ConstantExpr`.  |
| `StringExpr ` |   You guessed it.  |
| `ConstantExpr ` |  Literal values not of the types listed above.  This includes maps, sets, and vectors that are constants, not formed by evaluating expressions.   Compare `[1 2 3]` to `[x y z]`.  It also includes numeric values not covered by `NumberExpr`, types, and variety of other things.  Some values might cause errors during code gen because we just don't know how to deal with them.  (For evalution, we can just return the value that was given.) |

Those that are not under `LiteralExpr` typically have more specialized code for construction, typing calcuations,  or evaluation/code-gen. One subcluster relates to maps, sets, and vectors

| Expr type | Description |
|:----------|:------------|
| `EmptyExpr` |   Comes from a collection type value that is empty and has no meta data.  Will evaluate to or generate code to create an empty list, map, set, or vector.  |
| `MapExpr` |  Create a map from supplied key/value pairs, some of which themselves are expressions.  |
| `SetExpr` |  Similarly for sets.  |
| `VectorExpr  ` |   Similarly for vectors.  |


And two node types relate specifically to `Var`s:

| Expr type | Description |
|:----------|:------------|
| `TheVarExpr ` |  Related to the `#'name`  syntax |
| `VarExpr` |   A symbol in the code text maps to a `Var`.  |
  

__Cluster #2 -- Control flow__: There are a few node types related to the flow of control:


| Expr type | Description |
|:----------|:------------|
| `BodyExpr ` |  A sequence of expressions to evaluate, returning the value of the last one.  The `do` form translates to this, but other constructs, such as the body of a function method, have _implicit do_ structure. |
| `CaseExpr ` |  Obvious, I hope. |
| `IfExpr ` |    Even more obvious. |
| `ThrowExpr` |  Duh. |
| `TryExpr` |  This has the main body, catch clauses (can be none), and an optional finally clause. A `try` form without any `catch`es and no `finally` will be output as a `BodyExpr`.|


__Cluster #3 -- Miscellaneous__: The things that I couldn't figure out a better place for.

| Expr type | Description |
|:----------|:------------|
| `AssignExpr` | Comes from a `(set! target value)` expression |
| `DefExpr`  | Comes from a `(def name value)` expression.  This includes `defn`, `defmacro` and other forms that macroexpand to a `def` | 
| `ImportExpr`  | Yep, `(import* ...)` gets its own node type |
| `InstanceOfExpr  `  | Yep, `(instance-of ...)` ... |
| `MetaExpr` | When we need to attach metadata to some other construct as we eval/code-gen.  Primarily used for functions and the collections (map, set, vector) |
| `MonitorEnterExpr` | Yep. | 
| `MonitorExitExpr` | Just what you think. |
| `UnresolvedVarExpr`  | There is a compiler mode that allows a symbol that prevents an error being thrown when we run into `Var` we can't resolve to something.  We create this node for one of those.  It is supposed to be an extension point for the compiler.  No known uses of it.  I've never turned on the flag allowing this, so I have never created a node of this type other than in tests. |
| `UntypedExpr` | This is just an abstract base class for certain other node types that don't have a return type:  `MonitorEnterExpr`, `MonitorExitExpr`, `ThrowExpr` |

__Cluster #4 -- Host interop__:  

The subtypes of `HostExpr` are all related to platform calls:  methods, properties, fields, etc.  There are a few node types similarly oriented that are not under `HostExpr`.   I may went a little overboard with the class hierarchy, but there was common code that seemed to make this structure sensible.    Things are a little more complicated on the CLR vs the JVM because the CLR has properties as first-class objects.  That also increases the confusion level for no-argument constructs:  Is `(.m x)` a field access, a property get, or a call to a 0-arity method? 

Ignoring some of the intermediate abstract classes, we can organize the majority of the concrete classes as follows:

| Mode    |  Field   |  Property     |  Method    |
|:-------:|:-------:|:-------:|:-------:|
| Instance  | `InstanceFieldExpr` | `InstancePropertyExpr` | `InstanceMethodExpr`  |
| Static    | `StaticFieldExpr` | `StaticPropertyExpr` | `StaticMethodExpr` |

The remaining `HostExpr`-derived class is:

| Expr type | Description |
|:----------|:------------|
| `InstanceZeroArityCallExpr` | An interop call with no arguments, but we can't figure out at analysis time if it is a field, property, or expression.  There will be reflection happenging when we actually evaluate this node. |

There are two other node types I think go into this cluster:


| Expr type | Description |
|:----------|:------------|
| `QualifiedMethodExpr` | This results from the recently-introduced qualified method expressions. They look like `Type/.Name` for instance method calls, `Type/new` for constructor calls, and `Type/Name` for static calls.  During analysis, this node type sometimes resolves into one of the `HostExpr` types, but sometimes it generates a value (a 'thunk') to be passed as an argumment. |
| `NewExpr` | A constructor call to 'new' a type. |


__Cluster #5 -- Functions and local binding scopes__: These node types related to the definition of functions or local-binding scopes.

`ObjExpr` is the abstract base class for two concrete classes:

| Expr type | Description |
|:----------|:------------|
| `FnExpr` | From `(fn* name ...)` forms |
| `NewInstanceExpr` | from `deftype` and `reify` definitions |

I have no idea why `ObjExpr` and `NewInstanceExpr` are so named.  (I get confused every time I see them in the code -- and `ObjExpr` is _everywhere_.)  These create a scope for the definition of and reference to local bindings.
They cause the creation of types that implement the `IFn` interface of `invoke` methods.

Though not AST nodes themselves, the types `FnMethod` and `NewInstanceMethod` encode the individual `invoke` method definitions.

Also creating scopes are:

| Expr type | Description |
|:----------|:------------|
| `LetExpr` | From `(let [...] ...)` and `(loop [...] ...)` forms |
| `LetFnExpr` | From `(letfn [...] ...)` |

(We should perhaps include `ThrowExpr` here also -- `catch` classes introduce a binding scope for the `catch` variable.)

For technical reasons, parsing of these constructs at the top level involves wrapping them with a dummy function.  The dummy function provides the machinery for local binding creation, scope and reference.  Thus, the analyzer would transform

```clojure
(let [x 12] (inc x))
```

into

```clojure
( (fn* [] (let [x 12] (inc x))) )
```

This need only happen at the top level. If a `let` form was inside a function definition already, this wrapping would not be necessary.  (This is one of the places the 'context' flag mentioned in the previous post comes into play.)

The `loop` special form explictly creates a scope for iteration via `recur`; function bodies implicly create such a scope. A `recur` form in such a context will be represented by


| Expr type | Description |
|:----------|:------------|
| `RecurExpr` | From a `(recur ...)` form |


We need to reference the values of local bindings in our code.  This is handled by:

| Expr type | Description |
|:----------|:------------|
| `LocalBindingExpr` | A reference to a `LocalBinding`  |


A `LocalBinding` encodes information about the local binding definition, such as its name and its initialization expression.

Finally, there is the node type:

| Expr type | Description |
|:----------|:------------|
| `MethodParamExpr` | Holds the type of a method parameter. |

You'll never see one of these running free.  They are used internally only.  They can't be evaluated and cannot generate code.  Why they are a subtype of `Expr` is a bit of a mystery to me.

__Cluster #6: -- Invocation__: These are involved with function invocation.


| Expr type | Description |
|:----------|:------------|
| `InvokeExpr` |  A general invocation of the form `(f ...args)` |
| `KeywordInvokeExpr`  | An invocation fo the form `(:kw arg)` |
| `StaticInvokeExpr`  | for direct linking a function invocation, call a `staticInvoke` overload (more later) |


And that's all, folks!

## Analysis

Creation of the AST tree for a form proceeds by recursive descent through the form.
The basic structure of the form is identified and the appropriate specialized parser is called.
For example, if a `(do form1 form2 ...) ` construct was seen, the `do` indicates that the `BodyExpr` parser is appropriate.  That parser would receive `(form1 form2 ...)`.  It would recursively analyze each of the forms in turn, creating `Expr`s for each. Finally, it would create a `BodyExpr` holding that sequence of `Expr`s. 

The main body of the `Compiler.Analyze` method just steps through a series of tests to determine what auxiliary parser to call.

|  Test: the form is ... | Action |
|:------|:-------|
| `nil` | Create a `NilExpr` |
| `true` or `false` | Create a `BooleanExpr` |
| a symbol | Call the symbol analyzer (see below) |
| a keyword | Create a `KeywordExpr` |
| a number | Create a `NumberExpr` (if an `int`, `long`, or `double`) or a `ConstantExpr` |
| a string | Create a `StringExpr` |
| an `IPersistentCollection` <br/> not an `IRecord` or `IType` <br/> has no elements | Create an `EmptyExpr` |
| an `ISeq` | Call the `ISeq` analyzer (see below) |
| an `IRecord` or `IType` | Create a `ConstantExpr` |
| `IPersistentVector` | Create a `VectorExpr` or a `ConstantExpr` |
| `IPersistentMap` | Create a `MapExpr` or a `ConstantExpr` |
| `IPersistentSet` | Create a `SetExpr` or a `ConstantExpr` |
| otherwise | Create a `ConstantExpr`.  If we are eval'ing, we just return it.  If we are compiling, let's hope we can figure out how to handle it. |

The node types mentioned in this list from a very small subset of the all the node types.
Here we see pretty much a few data-oriented node types.  Clearly the `Symbol` and `ISeq` analyzers are doing the heavy lifting.  Enough that each gets its own post:

- __Symbolic of what?__
- __ISeq clarity__
