---
layout: post
title: C4 - fn* -- talkin' 'bout my generation
date: 2025-10-15 00:00:00 -0500
categories: general
---

We look at code generation for functions in ClojureCLR.

In a previous post ([C4: Functional anatomy]({{site.baseurl}}{% post_url 2025-09-04-functional-anatomy %})), we looked at how functions are represented in ClojureCLR.  That post focused on the interfaces and classes that form the basis of the representation of functions.  When we define a function in ClojureCLR,  we generate a class derived from one of the base classes (typically `AFunction` or `RestFn`); there is a significant amount of support code that gets added. That support code is the topic of this post.

## Our playground

The primary classes involved in function code generation are:

<img src="{{site.baseurl | prepend: site.url}}/assets/images/objexpr.png" alt="Graph of all types related to ObjExpr" />

I have no idea why `ObjExpr` and `ObjMethod` are named what they are.
`FnExpr` is the AST node type that represents an `fn*` form;  `FnMethod` represents an `invoke` method of the generated class. 
`NewInstanceExpr` represents a `deftype` or `reify` form; `NewInstanceMethod` represents a method of the generated class.
There is a significant amount of shared code in the base classes `ObjExpr` and `ObjMethod`. 

Note: Do not confuse `NewInstanceExpr` with `NewExpr` -- the latter represents a `new` form that creates an instance of a class; that falls under host platform interop.

## Out of control

If you look at code for the these classes and try to track the flow of data, well, good luck to you.  It is a pit of despair.
We are going to ignore completely (for now, at least) those grungy details. You will be able to get the details in the upcoming [C4: Out of control][TBD] post.

Just know that as we parse the forms in the body of the function, the parser is not only creating the AST node at that point but also pushing information into the `FnMethod` and `FnExpr` instances being built.  

## What's inside

I'll focus on `FnExpr` and `FnMethod` here.  Most of this analysis applies to `NewInstanceExpr` and `NewInstanceMethod` as well.

We parse an individual `invoke` method of the function (one arity of the `fn*` definition) as follows:

- Determine the return type of the method.  This is typically `object`, but can be a more specific type if type hints are present.  The only primitive types supported are `long` and `double`.

- Process the parameters.  This includes:
    - Determine the number of fixed parameters and whether there is a rest parameter (and not more than one)..
    - Determine the type of each parameter.  This is typically `object`, but can be a more specific type if type hints are present.  The only primitive types supported are `long` and `double`.
    - Create a list of `LocalBinding` instances, one for each parameter.  These are used to represent the parameters in the body of the method.
- Process the body of the method.  This involves parsing the forms in the body and generating an abstract syntax tree (AST) for the body of the method; this AST has a `BodyExpr` as the root.  The context of this parsing is the `FnExpr` itself, as well as the `FnMethod` being generated.  

In addition to its `BodyExpr`, the `FnMethod` object holds a list of local bindings.  This list is consulted when we are trying to resolve symbols occurring in the body code.   This list will include the function parameters. However,  special forms that define local bindings--`let*`, `letfn*`, and `try` (in its `catch` forms)--piggy-back on the `FnMethod` by adding their local bindings to the local bindings of the method.  (In fact, if you try to evaluate a `let*` form not in the body of a function, it wraps itself in an anonymous function and parses that, so that it has an `FnMethod` to manage its local bindings.)

What information from the parsing of the body is collected in the `FnExpr` object we are building? The class we will ultimately define to represent the function we are defining is fairly simple:  various flavors of `invoke` methods and static fields holding data needed by those methods.  And a constructor.  The information on the static fields is contributed by the forms parsed in the bodies of the methods.

The `ObjExpr` class maintains the following collections:

- `Constants`: a list of all the constants (literal values) used in the function.  These are contributed by nodes of type `ConstantExpr`, `NumberExpr` (when not `long` or `double`), `KeywordExpr`, and  any references to `Var`s (when the symbol is not resolved to a local binding).  Any constant that is needed in the final code generation will be stored in static fields in the generated class.
- `Closes`: a map of local bindings that are closed over by the methods.  These are references to local bindings from outer scopes.
- `KeywordCallsites`: places where keywords are used as functions, as in `(:key map)`.
- `ProtocolCallsites`: places where protocol methods are called.  


`Constants` contributes static fields to the function class. `Closes` defines the values needed for the constructor of the function class. `KeywordCallsites` and `ProtocolCallsites` also contribute static fields.  We'll discuss keyword callsites in [C4: Key in-site]{{site.baseurl}}{% post_url 2025-10-19-key-in-site %}.  We'll discuss protocol callsites in [C4: Is there a protocol for that?]{{site.baseurl}}{% post_url 2025-10-20-is-there-a-protocol-for-that %}.

Let's look at some examples.

## Basic examples

For the examples, we will show the Clojure code, then the generated code.  The generated code is the decompilation into C# of the IL generated by ClojureCLR.   Thanks goes to ILSpy.  

Let's start with a simple function:

```clojure
(defn f1 [x] (str x))
```

We will give our examples as `defn` forms for convenience.  However, this obscures a few important details.  The `defn` form expands to a `def` of a `fn` form.  

```clojure
(def f (clojure.core/fn ([x] (str x))))
```

The `clojure.core/fn` itself is a macro that expands to an `fn*` form; in this case, it has the same body as shown here.  The parser for `def` creates a context for the parsing the `fn*` form that provides the name `f` for the function.   Parsing the `fn*` generates a class that holds the definition of the function. (That is actually a side-effect of parsing;  even if the parse fails, you have a class floating around.  What fun.)

The code generated for the `def` itself is roughly this:

```clojure
RT.var("test.compiler", "f1").bindRoot(new compiler$f1());
```

In other words, find the `Var` for `test.compiler/f1` and bind it to a new instance of the class `compiler$f1`, which is the class generated for the `fn*` form.  Our interest here is the generated class.  We'll talk more about how the code for the `def` initialization is generated and used during loading in [C4: Some assembly required][TBD].

Here is the generated code for the class `compiler$f1`.  My comments are interposed.

```C#
// AFunction is one of the standard base classes for function implementations.
public class compiler$f1 : AFunction
{
    // There is only one constant reference in the body: The Var for `clojure.core/str`.
    // We create a static field to hold that and initialize it in the static constructor.
	protected internal static Var const__0;

  	static compiler$f1()
	{
		const__0 = RT.var("clojure.core", "str");
	}

    // There is only one arity defined: 1 parameter, no '& arg'.
    // Thus we need only a single `invoke` method.
    // This function allows direct linking.
	// For such a function, any invoke method will delegate to a static method of the same signature.
    public override object invoke(object P_0)
	{
		return invokeStatic(P_0);
	}

    // This essentially is: (str x).
    public static object invokeStatic(object P_0)
	{
		return ((IFn)const__0.getRawRoot()).invoke(P_0);
	}

    // Not in the JVM version, but CLR needs this for certain operations.
    // We support only arity 1.
	public override bool HasArity(int P_0)
	{
		if (P_0 != 1)
		{
			return false;
		}
		return true;
	}
}
```

 Now would be a good time to review [C4: Functional anatomy]({{site.baseurl}}{% post_url 2025-09-04-functional-anatomy %}) to understand the base class `AFunction`,  details on direct linking, and other background information.

If we have several arities, we get multiple `invoke` and `invokeStatic` methods.  For example:

```clojure
(defn f3
  ([] (f3 1))
  ([x] (f3 x 2))
  ([x y] (str x y)))
```

We also have some self-reference here.  The generated code is:

```C#
public class compiler$f3 : AFunction
{
    // We have some additional constants.
	protected internal static Var const__0;
	protected internal static object const__1;
	protected internal static object const__2;
	protected internal static Var const__3;

	static compiler$f3()
	{
		const__0 = RT.var("test.compiler", "f3");
		const__1 = 1L;  // Note: implicit boxing
		const__2 = 2L;  // Note: implicit boxing
		const__3 = RT.var("clojure.core", "str");
	}

    // The invoke methods each delegate to the corresponding invokeStatic method.
    public override object invoke() => invokeStatic();
   	public override object invoke(object P_0)  => invokeStatic(P_0);
	public override object invoke(object P_0, object P_1) => invokeStatic(P_0, P_1);

    // This is essentially: (f3 1)
    // Note that we need to box the long value 1.
    // So that we only box once, we have a static field holding the boxed value.
	public static object invokeStatic(object P_0)
	{
		return ((IFn)const__0.getRawRoot()).invoke(P_0, const__2);
	}

    // Similarly (f2 x 2)
	public static object invokeStatic(object P_0)
	{
		return ((IFn)const__0.getRawRoot()).invoke(P_0, const__2);
	}

    // This is essentially: (str x y)
	public static object invokeStatic(object P_0, object P_1)
	{
		return ((IFn)const__3.getRawRoot()).invoke(P_0, P_1);
	}

    // We support arities 0, 1, and 2.
	public override bool HasArity(int P_0)
	{
		if (P_0 != 2 && P_0 != 1 && P_0 != 0)
		{
			return false;
		}
		return true;
	}
}
```

## Primitive typing

If we have type hints for our arguments or return type and either a `long` or `double` type hint is involved, there are additional interfaces implemented and additional methods generated.  For example:

```clojure
(defn t 
 (^long [^String x ^double y] (long (+ (double (count x)) y))))
 ```
 
 We generate:

```C#
// Note the additional interface ODL.
// This is for the signature (object, double) -> long.
// Note that Object is specified instead of String.  
// Reference types are always reduced to Object in the signatures. 
// It actually is valid Clojure to pass any reference type instance as the first argument.
// The main purpose here is to avoid boxing of the double argument and the long return if possible.

public class compiler$t : AFunction, ODL
{
    // No static fields needed.  
    // calls to long, double and count are inlined by the compiler.
    static compiler$t()
	{
	}

    // The invoke method still defers to the invokeStatic method.
    // However, we cast the second parameter to double because that is the required type.
	public override object invoke(object P_0, object P_1)
	{
		return invokeStatic(P_0, RT.doubleCast(P_1));
	}

    // Our invokeStatic has the designated signature
	// Direct linking this can avoid boxing of the double argument and the long return value.
	public static long invokeStatic(object P_0, double P_1)
	{
		return RT.longCast((double)RT.count(P_0) + P_1);
	}

    // This is the additional method needed for the ODL interface.
	public override long invokePrim(object P_0, double P_1)
	{
		return invokeStatic(P_0, P_1);
	}

	public override bool HasArity(int P_0)
	{
		if (P_0 != 2)
		{
			return false;
		}
		return true;
	}
}
```

Code calling this function can use the `ODL` interface to avoid boxing the `double` argument and the `long` return value.  This can lead to improved performance in scenarios where these functions are called frequently or in tight loops.

For example, the code:

```clojure
(defn ut [x y](t (str x) (double y)))
```
 compiles to having the `staticInvoke` method:

```C#
	public static object invokeStatic(object P_0, object P_1)
	{
		return ((ODL)const__0.getRawRoot())
           .invokePrim(((IFn)const__1.getRawRoot()).invoke(P_0), 
                       RT.doubleCast(P_1));
	}
```

## Closures

If a function closes over local bindings from an outer scope, those local bindings are passed to the constructor of the function class and stored in instance fields.  For example, consider a very silly function that returns a function that concatenates its argument to a designated string:

```clojure
(defn h [x] (fn [y] (str x y)))
```

Looking first at the generated code for the inner function:

```C#
public class compiler$hfn__4264__4268 : AFunction
{
    // An instance field to hold the closed-over binding value.
	public object x;

    // The constant for the Var `clojure.core/str`.
	protected internal static Var const__0;
	static compiler$hfn__4264__4268()
	{
		const__0 = RT.var("clojure.core", "str");
	}

    // There is no staticInvoke method.
    // A method with closed-over bindings cannot be directly linked.
    // Thus, there is no invokeStatic method defined.
    // The invoke method must do the work.
	public override object invoke(object P_0)
	{
		return ((IFn)const__0.getRawRoot()).invoke(x, P_0);
	}

    public override bool HasArity(int P_0)
	{
		if (P_0 != 1)
		{
			return false;
		}
		return true;
	}

    // HERE is the secret sauce.
    // The constructor takes the closed-over binding as a parameter.    
	public compiler$hfn__4264__4268(object P_0)
	{
		x = P_0;
	}
}
```

Classes for functions that do not close over outer-scope bindings need only a no-argument constructor.  Here, we require a constructor that takes the closed-over binding `x` as a parameter and stores it in an instance field.

The outer function `h` generates the following code:

```C#
	public static object invokeStatic(object P_0)
	{
		return new compiler$hfn__4264__4268(P_0);
	}
```

When we call `(h 12)`, say, we get back a new instance of the inner function class, with its `x` field set to `12`.  


## Direct linking

Direct linking is a performance optimization that allows calls to functions to bypass some of the overhead of the general function call mechanism.  It is described in [C4: Functional anatomy]({{site.baseurl}}{% post_url 2025-09-04-functional-anatomy %}).

The examples above were compiled with direct linking turned off; I wanted to show the generation of `Var` static fields in the function classes.  When compiled with direct linking turned on,

```C#
public class compiler$f1 : AFunction
{
    // a static field for #'clojure.core/str
	protected internal static Var const__0;

  	static compiler$f1()
	{
		const__0 = RT.var("clojure.core", "str");
	}

    // Lookup of Var value required
    public static object invokeStatic(object P_0)
	{
		return ((IFn)const__0.getRawRoot()).invoke(P_0);
	}
 
    // ...
}   

```

becomes

```C#
public class compiler$f1 : AFunction
{
    // no static fields needed
   	static compiler$f1()
	{
	}

	public static object invokeStatic(object P_0)
	{
		return core$str.invokeStatic(P_0);
	}

    // ...
}
```

Removing the `Var` value lookup measurably improves performance.

That's enough.