---
layout: post
title: C4 - Functional anatomy
date: 2025-09-04 00:00:00 -0500
categories: general
---

We look at the implementation of functions in ClojureCLR and how evaluation/compilation translates a source code definition of a function to the underlying class representation.

## The universe, and everything

There are a number of interfaces and classes that are used with in ClojureCLR (and this is parallel to ClojureJVM).  We will cover all of these in detail:

<img src="{{site.baseurl | prepend: site.url}}/assets/images/fn.png" alt="Graph of all function-related types" />


## IFn

The essence of a function is that you can invoke it on (zero or more) arguments.
If you want a class to represent a function, the minimum requirement is to implement the `IFn` interface.

```C#
public interface IFn 
{
    object invoke();

    object invoke(object arg1);

    object invoke(object arg1, object arg2);

    object invoke(object arg1, object arg2, object arg3);

    // ...

    object invoke(object arg1, object arg2, object arg3, object arg4, object arg5, 
                    object  arg6, object arg7, object arg8, object arg9, object arg10, 
                    object arg11, object arg12, object arg13, object arg14, object arg15, 
                    object arg16, object arg17, object arg18, object arg19, object arg20);

    object invoke(object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7,
                            object arg8, object arg9, object arg10, object arg11, object arg12, object arg13, object arg14,
                            object arg15, object arg16, object arg17, object arg18, object arg19, object arg20,
                            params object[] args);

    object applyTo(ISeq arglist);
}
```

(In ClojureJVM, `IFn` also implements interfaces `Callable` and `Runnable`; no equivalents exist in ClojureCLR.)  There are `invoke` overloads for zero to twenty arguments, and a final overload that takes arguments past the 20 count in a `params` array.  The `applyTo` method is used to define the behavior of `apply`.

## IFnArity

This interface is not in ClojureJVM.  I implemented it in ClojureCLR to provide a way to interrogat a function about what arities it supports.  It was required to deal with dynamic callsites.  That's going to take us a long way from what we need to understand here, so I'm going to ignore it. (Okay, I'm avoiding it partly because I don't remember what purpose it serves.  Homework for me. ) For reference only:

```C#
public interface IFnArity
{
    bool HasArity(int arity);
}
```

## AFn

If you want something to use as a function in ClojureCLR, just create a class that implements `IFn` and create an instance of it.  You can put that any place a function can go.

As a practical matter, you should avoid that.  To implement `IFn`, you need to provide implementations for all of the `invoke` methods -- most of which are just going to throw a `NotImplementedException` -- as well as `applyTo`.  

The abstract base class `AFn` supplies default implementations of all the `invoke` methods and `applyTo`.  The default implementations throw an `ArityException`, derived from `ArgumentException`, that indicates the function does not support the arity of the `invoke`.


```C#
public abstract class AFn : IFn, IDynamicMetaObjectProvider, IFnArity
{
    #region IFn Members

    public virtual object invoke()
    {
        throw WrongArityException(0);
    }

    public virtual object invoke(object arg1)
    {
        throw WrongArityException(1);
    }

    public virtual object invoke(object arg1, object arg2)
    {
        throw WrongArityException(2);
    }

    // ...

}
```

`AFn` cleverly provides a default implementation of `applyTo` that uses the `invoke` methods to do the work.  It does this by counting the number of arguments in the `ISeq` passed to `applyTo`, and calling the appropriate `invoke` method.  If there are more than 20 arguments, it collects the first 20 into individual variables and the rest into an array, and calls the final `invoke` overload.

```C#
public virtual object applyTo(ISeq arglist)
{
    return ApplyToHelper(this, Util.Ret1(arglist,arglist=null));
}
```

The call to `Util.Ret` is a trick required by the JVM:  it returns the value of its first argument.  The second argument is there to ensure that the `arglist` variable in the caller is set to `null` after the call.  This is to help the garbage collector reclaim the memory used by the `ISeq` if it is no longer needed.  As it turns out, this is not really necessary in the CLR, but I never got around to deleting it -- it occurs in a lot of places, as `ApplyToHelper` illustrates:

```C#
public static object ApplyToHelper(IFn fn, ISeq argList)
{
    switch (RT.BoundedLength(argList, 20))
    {
        case 0:
            argList = null;
            return fn.invoke();
        case 1:
            return fn.invoke(Util.Ret1(argList.first(),argList=null));
        case 2:
            return fn.invoke(argList.first()
                    , Util.Ret1((argList = argList.next()).first(),argList = null)
            );
        case 3:
            return fn.invoke(argList.first()
                    , (argList = argList.next()).first()
                    , Util.Ret1((argList = argList.next()).first(), argList = null)
            );
        case 4:
            return fn.invoke(argList.first()
                    , (argList = argList.next()).first()
                    , (argList = argList.next()).first()
                    , Util.Ret1((argList = argList.next()).first(), argList = null)
            );    

        // ...
}
```

If you want to create a function class yourself, I highly recommend basing it on `AFn`.
As an example, in the test code for ClojureCLR.Next, I use F# object expressions to create functions to test things like reduce functionality:

```F#
let adder =
    { new AFn() with
        member this.ToString() = ""
      interface IFn with
        member this.invoke(x, y) = (x :?> int) + (y :?> int) :> obj
    }
```

## AFunction

The Clojure (CLR+JVM) compiler does not generate `AFn` sublasses directly.  It generates classes derived from `AFunction` (for functions that are not variant) and `RestFunction` (for functions that are variant.) A _variant_ function is one that has a `&` parameter amongst its options:

```
(fn 
   ([x]  ... one-arg definition ... )
   ([x y] .. two-arg definition ... )
   ([x y & ys] .. rest-arg definition ... ))
```

`AFunction` is an abstract base class derived from `AFn`:

```C#
public abstract class AFunction : AFn, IObj, Fn, IComparer
{
    // ...
}
```

It implements interface `IObj`, providing metadata attachment capability.
This is done not simply by having a metadata field, but by implementing the `withMeta` method to return an instance of an `AFunction+MetaWrapper`.  The latter class has a field for the metadata and a pointer back to the originating function.  I guess they wanted to save the space of the field for the metadata.  

It implements interface `IComparer` so that an instance of the class can be passed to things like sort routines, comparers for hash tables, etc.  The `Compare` method just calls the two-argument `invoke`: 

```C#
        public int Compare(object x, object y)
        {
            Object o = invoke(x, y);

            if (o is Boolean)
            {
                if (RT.booleanCast(o))
                    return -1;
                return RT.booleanCast(invoke(y, x)) ? 1 : 0;
            }
            return Util.ConvertToInt(o);
        }
```

`AFunction` also includes some support for a method implementation cache used in implementing protocols.   That's for discussion at another time.

When creating a derived class of `AFunction`, the compiler defines overrides of the `invoke` methods for each arity defined in the function declaration.

## RestFn

On to `RestFn`.  Ah, `RestFn`.  The source file is almost 5000 lines long.  I still have nightmares.  I took the JVM version and did a bunch of search/replace operations.  (For the ClojureCLR.Next, written in F#, I wrote a program to generate it.)

Consider the following:

```
(fn 
   ([x y]        ... two-arg definition ... )
   ([x y z ]     ... three-arg definition ... )
   ([x y z & zs]  ... rest-arg definition ... ))
```

This function should fail if called on zero or one arguments.
It should call the supplied definitions when called on two or three arguments. And it should apply the _variant_ definition when called with four or more arguments.

A key notion here is the _required arity_ of the function.  For this example, the required arity is two.  This is the minimum number of arguments covered by one of the cases in the definition. If we call `invoke()` or `invoke(object arg1)`, the call should fail.

`RestFn` uses a very clever trick to implement this functionality in a general way.  By 'clever', I mean that every time I look at this code I have to take an hour to learn anew what it is actually doing.  I hope by writing it down here, I can make this go faster in the future.

`RestFn` defines standard overrides for all the `invoke` overloads and `applyTo`.  These are all defined in terms of other methods, the `doInvoke` methods that are designed to take a final argument that is an `ISeq` containing the _rest_ args, if needed..

For the example, the compiler would provide overrides for

```C#
invoke(arg1, arg2)
invoke(arg1, arg2, arg3)
doInvoke(arg1, arg2, arg3, args)
```

that implement the code supplied in each case of the function definition.  Ask yourself:  What should be the default implementations for all the other `invoke` and `doInvoke` overloads?

For `invoke` the behavior is different for those with fewer than the required arity and those with more:

- If there are fewer than the required arguments, the call should fail.
- If there are more than the required arguments, the call should delegate to `doInvoke(arg1, arg2, arg3, args)`

The distinguishing factor is the _required arity_.
For an invoke with fewer arguments than the required arity, we should fail.  If we have more arguments than the required arity, then we should call the overload of `doInvoke` that takes the required arity plus the _rest_ argument.

Thus, all the `invoke` overrides are variants on this theme:

```C#
public override Object invoke(Object arg1, Object arg2, Object arg3, Object arg4)
{
    switch (getRequiredArity())
    {
        case 0:
            return doInvoke(
                ArraySeq.create(
                    Util.Ret1(arg1, arg1 = null), 
                    Util.Ret1(arg2, arg2 = null), 
                    Util.Ret1(arg3, arg3 = null), 
                    Util.Ret1(arg4, arg4 = null)));
        case 1:
            return doInvoke(
                Util.Ret1(arg1, arg1 = null), 
                ArraySeq.create(
                    Util.Ret1(arg2, arg2 = null),
                    Util.Ret1(arg3, arg3 = null), 
                    Util.Ret1(arg4, arg4 = null)));
        case 2:
            return doInvoke(
                Util.Ret1(arg1, arg1 = null), 
                Util.Ret1(arg2, arg2 = null), 
                ArraySeq.create(
                    Util.Ret1(arg3, arg3 = null), 
                    Util.Ret1(arg4, arg4 = null)));
        case 3:
            return doInvoke(
                Util.Ret1(arg1, arg1 = null), 
                Util.Ret1(arg2, arg2 = null), 
                Util.Ret1(arg3, arg3 = null), 
                ArraySeq.create(
                    Util.Ret1(arg4, arg4 = null)));
        case 4:
            return doInvoke(
                Util.Ret1(arg1, arg1 = null), 
                Util.Ret1(arg2, arg2 = null), 
                Util.Ret1(arg3, arg3 = null), 
                Util.Ret1(arg4, arg4 = null), 
                null);
        default:
            throw WrongArityException(4);
    }
}
```

This `invoke` will be invoked by call in the code with four arguments: `(f 1 2 3 4)`. If `f` had a clause that takes four arguments, then we woudl be calling `f`'s override of the four-argument `invoke`.  So it does not have such a case.  Let's say `f` has a required arity of 2. We have enought arguments to supply the minimum. Then we would end up calling `doInvoke(1,2, [3, 4])`, which `f` has an overridden.
If, instead, `f` had a required arity of 12, then we do not have arguments.  The `default` case kicks in and we throw an exception: Wrong number of arguments.

The default implementations of `doInvoke` all return null.
They will never be called.  We will only ever call an override of `doInvoke`, the one matching the required arity (+ 1 for the rest args).

I'll leave `applyTo` as an exercise.

## Static invocation

In the previous post  [C4: ISeq clarity]({{site.baseurl}}{% post_url 2025-09-03-iseq-clarity %}), I touched upon the notion of static invocation of functions.  Static invocation is an efficiency hack.  It allows a call such as

```Clojure
(f 1 2 3)
```

to bypass the usual dynamic dispatch that does a lookup of the current value of the `#'f` `Var` and instead links in the code directly to an invocation of a static method on the class defined for `f`.  For this to happen, the first requirement is that the function allows direct invocation, the constraints being:

- The function is not nested inside of another function.
- The function does not close over any lexical variables from an outer scope.
- The function does not use the `this` variable.

When these conditions are met, for each `invoke` the function defines, there will be a `staticInvoke` method of the same arity with the actual function definition.  The `invoke` just calls the `staticInvoke` of the same arity.

I provide more detail on some of the issues of static linking in a previous post outside this series, [The function of naming; the naming of functions]({{site.baseurl}}{% post_url 2025-02-28-function-naming %}). 

## Primitive urges

In the same two posts mentioned just above, I also touched upon primitive invocation.  If one of the `invoke` overloads is typed so that its argument types and return types contain only `^long` or `^double` or (the default) `^object` type hints and at are not all `^object`, then we will create have the class representing the function implement an interface such as

```C#
public interface ODLLD
{

    double invokePrim(object arg0, double arg1, long arg2, long arg3);
}
```

In invocations where we know the types of the arguments, we can avoid boxing by calling the `invokePrim` method directly.  

The interface is named by the type of each argument plus the return type. `ODLLD` = four arguments, of types `Object`, `double`, `long`, and `long`, with a return type of `double`.    These interfaces are in the `clojure.lang.primifs` namespace.  One to four arguments are accommodated.  If you care to count, that comes to 358 interfaces.  (Eventually, I'd like to replace these with the corresponding `Function` interfaces.  We do have _real_ generics in C#.  And in ClojureCLR.Next, I'd like to get rid of the restriction to just `long` and `double` primitives.)

And that pretty much covers the classes that implement functions in ClojureCLR.  Except ...


## The mysterious Fn

If you look above carefully, you will note that `AFunction` (and hence, indirectly, `RestFn`) implements an interface named `Fn`.  This is a marker interface -- no methods:

```C#
public interface Fn
{
}
```

I have found only one use of `Fn` in the Clojure code.  Over in `core_print.clj` you will find this:

```Clojure
(defmethod print-dup clojure.lang.Fn [o, ^System.IO.TextWriter w]
  (print-ctor o (fn [o w]) w))
```

That's it.  It makes sure if you print a function with `*print-dup*` set to true, it prints out a constructor call. Really.

```Clojure
(defn f [x] (inc x))  ; => #'user/f
(bind [*print-dup* true] (prn f)) ; prints: #=(user$f__4237. )
```

Makes my day.

## Code generation

If you have all of that digested, code generation is not as hard as one might think.  
Some complexity is handled in source code macroexpansion. If you have a `defn` with fancy arg destructuring

```Clojure
(defn destr [& {:keys [a b] :as opts}]
  [a b opts])
```

That gets macroexpanded to

```Clojure
(def destr (clojure.core/fn ([& {:keys [a b], :as opts}] [a b opts])))
```

And that `fn` expresssion gets macroexpanded to 

```Clojure
((clojure.core/fn ([& {:as user/opts, :keys [user/a user/b]}] [user/a user/b user/opts])))
```

One more round:

```Clojure
(fn* ([& p__4247] (clojure.core/let [{:keys [a b], :as opts} p__4247] [a b opts])))
```

Which, let's face it, is just

```Clojure
(fn* [& arg] ...line noise---)
```

`fn*` is the underlying primitive for all function definitions.  There's a little work regularizing syntax:

```Clojure
(fn* [x] ...))
```

is the same as 

```Clojure
(fn* ([x] ...))
```

You'd have that nesting anyway if you had multiple arities overloaded.

There can be an optional name to use:

```Clojure
(fn* myfunc ([x]))
```

But after that, it's pretty much just a bunch of method bodies to parse.  The `fn*` parses into an `FnExpr` that contains a list of `FnMethod` instances, one for each arity overload.    When we generate code, the `FnExpr` becomes a class, the `FnMethod` instance each contribute a method (or several if we have static or primitive hackery).  And there we are.

## I'm lying

The actual complexity involved in code-gen for `FnExpr` and `FnMethod` is daunting.
That's going to take another post.




