---
layout: post
title: C4 - Key in-site
date: 2025-10-19 00:00:00 -0500
categories: general
---

We look at how `(:keyword arg)` callsites are compiled in Clojure.


Consider a function that uses a keyword as a function:

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

The code generation shown above is fairly straightfoward.  It's the functioning of that `invokeStatic` and what the class `KeywordLookupSite` does that takes some explaining.   Every time I look it at it, it takes me an hour to figure it out, so I'm going to write it down once and hope to remember to look here in the future.

The 'thunk' here provides caching of the target, the value of `x` in `(:kw x)`.
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


Let's say the next time we hit the call site, the value of `x` is an `IPersistentMap`.
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

I'm assuming the implementers of the JVM code did a performance analysis and found that the thunking approach was the fastest way to implement keyword lookup.  Who'da thunk it?
