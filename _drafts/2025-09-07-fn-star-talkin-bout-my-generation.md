---
layout: post
title: C4 - fn*: talkin' 'bout my generation
date: 2025-09-07 00:00:00 -0500
categories: general
---

We look at code generation for functions in ClojureCLR.

In a previous post ([C4: Functional anatomy]({{site.baseurl}}{% post_url 2025-09-04-functional-anatomy %})), we looked at how functions are represented in ClojureCLR.  That post focused on the interfaces and classes that form the basis of the representation of functions.  When we generate a function in ClojureCLR, though we are deriving a class from one of the base classes (typically `AFunction` or `RestFn`), there is a significant amount of support structure that gets added.  That is the topic of this post.

## Our playground

The primary classes involved in function code generation are:

<img src="{{site.baseurl | prepend: site.url}}/assets/images/objexpr.png" alt="Graph of all types related to ObjExpr" />

I have no idea why `ObjExpr` and `ObjMethod` are named what they are.
`FnExpr` is the AST node that represents an `fn*` form;  `FnMethod` represents an `invoke` method of the generated class. 
`NewInstanceExpr` represents a `deftype` or `reify` form; `NewInstanceMethod` represents a method of the generated class.
For these, a significant amount of code lies in the base classes `ObjExpr` and `ObjMethod`. We will focus here on `FnExpr` and `FnMethod`.  Most of this analysis applies to `NewInstanceExpr` and `NewInstanceMethod` as well.

Note: Do not confuse `NewInstanceExpr` with `NewExpr` -- the latter represents a `new` form that creates an instance of a class.

## Let me count the lines

| File | Source lines | Executable lines |
|------|--------------:|------------------:|
| ObjExpr.cs |  1,195 | 450 |
| FnExpr.cs  |  362 |  102 |
| NewInstanceExpr.cs |  681 | 227 |
| ObjMethod.cs |  176 |  50 | 
| FnMethod.cs  |  458 |  157 | 
| NewInstanceMethod.cs |  321 |  117 |

Doesn't seem like much?  It's packed.  And dependent on a some other goodies that we will get to in due course.

## Out of control

If you try to track the flow of data in the parsing and code generation, particularly with respect to functions, you will soon find yourself in a pit of despair.    I am going to ignore that aspect of the code for now.
You can get the details in the upcoming [C4: Out of control][TBD] post.

What is important for now is to know that, by whatever obscure means, the parsing of forms in the methods of an `fn*` collects information that eventually is folded into either the `FnMethod` or the `FnExpr` instance that is being generated.


## What's inside

I'll focus on `FnExpr` and `FnMethod` here.  `NewInstanceExpr` and `NewInstanceMethod` are similar; the complications they introduce are fairly straightforward.  Shared code is in the base classes `ObjExpr` and `ObjMethod`.

We parse an individual invoke method of the function as follows:

- Determine the return type of the method.  This is typically `object`, but can be a more specific type if type hints are present.  The only primitive types supported are `long` and `double`.

- Process the parameters.  This includes:
    - Determine the number of fixed parameters and whether there is a rest parameter (and not more than one)..
    - Determine the types of the parameters.  This is typically `object`, but can be a more specific type if type hints are present.  The only primitive types supported are `long` and `double`.
    - Create a list of `LocalBinding` instances, one for each parameter.  These are used to represent the parameters in the body of the method.
- Process the body of the method.  This involves parsing the forms in the body and generating an abstract syntax tree (AST) for the body of the method; this AST has a `BodyExpr` as the root.  The context of this parsing is the FnExpr itself, as well as the FnMethod being generated.  The latter contributes mostly the list of local bindings, allowing us to resolve symbols in the body appropriately.

As we parse the body, the various forms encountered may contribute to both the `FnMethod` and the `FnExpr`.  For example, special forms that define local bindings--`let*`, `letfn*`, and `try` (in its `catch` forms) --  piggy-back on the `FnMethod` by adding their local bindings to the local bindings of the method.  In fact, if you try to evaluate a `let` form, say, not in the body of the function, it wraps itself in an anonymous function, so that it has an `FnMethod` to manage its local bindings.

What information from the parsing of the body is collected in the `FnExpr` object we are building? The class we will ultimately define to represent the function we are defining is fairly simple:  various flavors of `invoke` methods and static fields holding data needed by those methods.  And a constructor.  The information on the static fields is contributed by the forms parsed in the bodies of the methods.

The `ObjExpr` class maintains the following collections:

- `Constants`: a list of all the constants (literal values) used in the function.  These are contributed when by nodes of type `ConstantExpr`, `NumberExpr` (when not `long` or `double`), `KeywordExpr`, and  any references to `Var`s (when the symbol is not resolved to a local binding).  Any constant that is needed in the final code generation are stored in static fields.
- `Keywords`: a list of all the `Keyword`s encountered.  These are also in `Constants`.
- `Vars`: a list of all the `Var`s encountered.  These are also in `Constants`.
- `Closes`: a map of local bindings that are closed over by the methods.  These are references to local bindings from outer scopes.
- `KeywordCallsites`: places where keywords are used as functions, as in `(:key map)`.
- `ProtocolCallsites`: places where protocol methods are called.  


`Constants` contributes static fields to the function class. `Closes` defines the values needed for the constructor of the function class. `KeywordCallsites` and `ProtocolCallsites` also contribute static fields.  We'll discuss keyword callsites below.  We'll discuss protocol callsites in [C4: Is there a protocol for that?][TBD].

With this in hand, let's look at some examples.

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

The `clojure.core/fn` expands to an `fn*` form; in this case, it has the same body as shown here.  The parser for `def` creates a context for the parsing the `fn*` form that provides the name `f` for the function.   Parsing the `fn*` generates a class that holds the definition of the function. (That is actually a side-effect of parsing;  even if the parse fails, you have a class floating around.  What fun.)

The code generated for the `def` itself is roughly this:

```clojure
RT.var("test.compiler", "f1").bindRoot(new compiler$f1());
```

In other words, find the `Var` for `test.compiler/f1` and bind it to a new instance of the class `compiler$f1`, which is the class generated for the `fn*` form.  Our interest here is the generated class.  We'll talk more about how the code for `def` is generated and used during loading in [C4: Some assembly required][TBD].

Here is the generated code for the class `compiler$f1`.  My comments are interposed.

```C#
public class compiler$f1 : AFunction
{
    // There is only one constant reference in the body: The Var for `clojure.core/str`.
    // We create a static field to hold that and initialize it in the static constructor.
	protected internal static Var const__0;
  	static compiler$f1()
	{
		const__0 = RT.var("clojure.core", "str");
	}

    // There is only one arity defined: 1 parameter, no rest.
    // Thus we need only a single `invoke` method.
    // Because this function allows direct linking, 
    //     any invoke method will delegate to a static method of the same signature.
    public override object invoke(object P_0)
	{
		return invokeStatic(P_0);
	}

    // This essentially is: (str x).
    ///
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

    // The invoke methods all delegate.
    public override object invoke() => invokeStatic();
   	public override object invoke(object P_0)  => invokeStatic(P_0);
	public override object invoke(object P_0, object P_1) => invokeStatic(P_0, P_1);

    // The static methods implement the actual logic.

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

If we have type hints for our arguments or return type and either a `long` or `double` type hint is involved, there are additionaly interfaces implemented and additional methods generated.  For example:

```clojure
(defn t 
 (^long [^String x ^double y] (long (+ (double (count x)) y))))
 ```
 
 We generate:

```C#

// Note the additional interface ODL.
// This is for the signature (object, double) -> long.
// Note that Object is specified instead of String.  
// Reference types are always Object.  
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

    // Our invokeStatic has the designated signure
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
 compiles to the `staticInvoke` method:

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


## Key in-site

For our final example, consider a function that uses a keyword as a function:

```clojure
(defn k [x] (:kw x))
```

The generated code is:

```C#
public class compiler$k : AFunction
{
    // Two fields are generated to support the keyword callsite.
	protected internal static KeywordLookupSite __site__0__;
	protected internal static ILookupThunk __thunk__0__;

    // They are initialized to a KeywordLookupSite instance.
    // The KeywordLookupSite implements the ILookupThunk interface, 
    //   and is used for the value for both fields.
    static compiler$k()
	{
		__thunk__0__ = (__site__0__ = new KeywordLookupSite(RT.keyword(null, "kw")));
	}

    // This is going require some explanation.
	public static object invokeStatic(object P_0)
	{
		ILookupThunk _thunk__0__ = __thunk__0__;
		object obj = _thunk__0__.get(P_0);
		return (_thunk__0__ == obj) ? (__thunk__0__ = ((ILookupSite)__site__0__).fault(P_0)).get(P_0) : obj;
	}

    // Skipping the rest
}
```

The code generation is fairly straightfoward.  It's the functioning of that `invokeStatic` and the what the class `KeywordLookupSite` does that takes some explaining.   Every time I look it at it, it takes me an hour, so I'm going to write it down once and hope to remember to look here in the future.

The 'thunk' here provides caching of the target, the value of `x` in `(:kw x)`.
I can think of several more direct ways to implement this call.

- Call the `invoke(arg1, arg2)` method of the `Keyword` instance directly.  Class `Keyword` implements `IFn`, with two versions of `invoke`.  Here they are:

```C#
public object invoke(object arg1)
{
    if (arg1 is ILookup ilu)
        return ilu.valAt(this);
    return RT.get(arg1, this);
}

public object invoke(object arg1, object notFound)
{
    if (arg1 is ILookup ilu)
        return ilu.valAt(this, notFound);
    return RT.get(arg1, this, notFound);
}
```

The cost is a type check, then a call either to `ILookup.valAt` or to `RT.get`. 

- Call `RT.get(arg1, this)`.  It has a similar type check, then special cases a few more possibilities.   (This is a place where protocols would help.)  Here is `RT.get`. (I show only the one that takes a `notFound` argument; the other is similar.)

```C#
static public Object get(Object coll, Object key, Object notFound)
{
    if (coll is ILookup ilu)
        return ilu.valAt(key, notFound);

    return GetFrom(coll, key, notFound);
}

static object GetFrom(object coll, object key, object notFound)
{
    if (coll == null)
        return notFound;

    if (coll is IDictionary m)
    {
        if (m.Contains(key))
            return m[key];
        return notFound;
    }

    if (coll is IPersistentSet set)
    {
        if (set.contains(key))
            return set.get(key);
        return notFound;
    }

    if (Util.IsNumeric(key) && (coll is string || coll.GetType().IsArray))
    {
        int n = Util.ConvertToInt(key);
        return n >= 0 && n < count(coll) ? nth(coll, n) : notFound;
    }

    if (coll is ITransientSet tset)
    {
        if (tset.contains(key))
            return tset.get(key);
        return notFound;
    }

    return notFound;
}
```

I'm not sure why `Keyword.invoke` does not call `RT.get` directly and skip the type check that `RT.get` does anyway.  Maybe avoiding the call is faster given that the type check is likely to succeed most of the time?  Along with the possibility that the call to `RT.get` can't be inlined because it is too big?

The thunk here just takes this kind of dance and goes one step further.  Let's dig in.

There are two interfaces involved: `ILookupThunk` and `ILookupSite`.  

```C#
public interface ILookupSite
{
    ILookupThunk fault(object target);
}

public interface ILookupThunk
{
    object get(object target);
}
```



An `ILookupThunk` will try to get the value for a target.  If it can't, it will return itself to indicate failure to apply.  An `ILookupSite` can create a new `ILookupThunk` for a target.  This is roughly what is coded in the `invokeStatic` method above.  In pseudo-code:

```
  let t = thunk.get()
  if ( t == thunk )
  { 
    // failure to apply
    // ask the site to get a new thunk for this target
    thunk = site.fault()
    return thunk.get()
  }
  else
  {
    // the thunk worked and gave us the desired value.  Use it.
    return t
  }
```

The class `KeywordLookupSite` implements both interfaces.  It holds a `Keyword` instance and uses that to get the value for a target.  Here is the code:

```C#
 public sealed class KeywordLookupSite: ILookupSite, ILookupThunk
 {
    // The keyword that we are looking up.
     readonly Keyword _k;

    // Obvious constructor
     public KeywordLookupSite(Keyword k)
     {
         _k = k;
     }

    //  See discussion below.
     public object get(object target)
     {
         if (target is IKeywordLookup || target is ILookup)
             return this;
         return RT.get(target, _k);
     }

    //  See discussion below.
     public ILookupThunk fault(object target)
     {
         if (target is IKeywordLookup)
             return Install(target);
         else if (target is ILookup)
         {
             return CreateThunk(target.GetType());
         }
         return this;
     }

     private ILookupThunk Install(object target)
     {
         ILookupThunk t = ((IKeywordLookup)target).getLookupThunk(_k);
         if (t != null)
             return t;

         return CreateThunk(target.GetType());
     }

     private ILookupThunk CreateThunk(Type type)
     {
         return new SimpleThunk(type,_k);

     }
     // ... nested class SimpleThunk shown belo ...
 }
```

When we setup the callsite, the `KeywordLookupSite` is both the site and the thunk. 
Let's say the first time we call `(:kw x)`, the value of `x` is an `IPersistentSet`.  `KeywordLookupSite.get` is called, the target being the set.  An `IPersistentSet` is not an `ILookup` or `IKeywordLookup`, so we call `RT.get(x, :kw)` -- does the set contain keyword `:kw`?  


Let's say the next we hit the call site, the value of `x` is an `IPersistentMap`.
Again we call `KeywordLookupSite.get`, the target being the map.  `IPersistentMap` implements `ILookup`, so we return `this`, the thunk itself.  This indicates that we need to call the `fault` method.  That method ends up creating and returning a `SimpleThunk` instance, which we install as the thunk, and then call its `get` method to get the value to return.

Here is `SimpleThunk` (a class nested in `KeywordLookupSite`):

```C#
class SimpleThunk : ILookupThunk
{
    readonly Type _type;
    readonly Keyword _kw;

    public SimpleThunk(Type type, Keyword kw)
    {
        _type = type;
        _kw = kw;
    }

    #region ILookupThunk Members

    public object get(object target)
    {
        if (target != null && target.GetType() == _type)
            return ((ILookup)target).valAt(_kw);
        return this;  

    }

    #endregion
}
``` 

The `SimpleThunk` holds the type of the target and the keyword.  When its `get` method is called, it checks that the target is of the expected type, then calls `ILookup.valAt` to get the value for the keyword.  If the type does not match, it returns itself to indicate failure to apply (which will result in the `KeywordLookupSite.fault` method being called).

In addition to the checks for `ILookup`, you see also checks for `IKeywordLookup`:

```C#
public interface IKeywordLookup
{
    ILookupThunk getLookupThunk(Keyword k);
}
```

This interface is implemented only by types defined by `deftype`.  We ignore the details of that here.

I'm assuming Rich Hickey did a performance analysis and found that the thunking approach was the fastest way to implement keyword lookup.  Who'da thunk it?

And with that, I think I'd better quit.

