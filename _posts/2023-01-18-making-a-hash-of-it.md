---
layout: post
title: Making a hash of it
date: 2023-01-18 00:00:00 -0500
categories: general
---

Wherein I look at hashing and equality in Clojure.

## Getting your head in the game

To get started, it helps to have some familiarity with the interaction of equality with collections and numbers.  There is an excellent Clojure guide titled [Equality](https://clojure.org/guides/equality) that you should take a look at. The summary might suffice.  I won't quote it here.


## A code-based approach

Perhaps the most useful fruitful approach would be to look at the code from the top down.  Starting in the Clojure source, from `core.clj`:


```Clojure
;equiv-based
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

Of interest, this is followed by a _commented out_ version that is based on a different method in `Util`

```Clojure
;equals-based
#_(defn =
  "Equality. Returns true if x equals y, false if not. Same as Java
  x.equals(y) except it also works for nil. Boxed numbers must have
  same type. Clojure's immutable data structures define equals() (and
  thus =) as a value, not an identity, comparison."
  {:inline (fn [x y] `(. clojure.lang.Util equals ~x ~y))
   :inline-arities #{2}
   :added "1.0"}
  ([x] true)
  ([x y] (clojure.lang.Util/equals x y))
  ([x y & more]
   (if (= x y)
     (if (next more)
       (recur y (first more) (next more))
       (= y (first more)))
     false)))
```

I leave you to contemplate the change in the comment.  There are some clues toward the bottom of the _Equality_ document.

TThe code difference is between calling `Util.equals` and `Util.equiv`.  They didn't just leave `=` alone and rewrite `Util.equals`.  They wrote `Util.equiv` instead.  As it turns out, `Util.equals` is still there and is used in Clojure code, in both Java/C# and Clojure.

So let's take a look in `Util`.  I'll go with the C# version.

```C#
static public bool equiv(object k1, object k2)
{
    if (k1 == k2)
        return true;
    if (k1 != null)
    {
        if (IsNumeric(k1) && IsNumeric(k2))
            return Numbers.equal(k1, k2);

        else if (k1 is IPersistentCollection || k2 is IPersistentCollection)
            return pcequiv(k1, k2);
        return k1.Equals(k2);
    }
return false;
}

public static bool equals(object k1, object k2)
{
    if (k1 == k2)
        return true;
    return k1 != null && k1.Equals(k2);
}
```

Historical note:  before the big change-over that led to the introduction of `equiv`, `equals` looked like this:

```C#
static public bool equals(object k1, object k2)
{
    if (k1 == k2)
        return true;
    if (k1 != null)
    {
        if (IsNumeric(k1) && IsNumeric(k2))
            return Numbers.equal(k1, k2);

        return k1.Equals(k2);
    }
return false;
```
In other words, the older version of `equals` is just the new `equiv` without a special case for objects that are `IPersistentCollection`.  The historical notes in _Equality_ again give clues.  You can conclude that a point had been reached where the `Equals` method on Clojure collections was no longer capable of doing the work.  

Back to `equiv`. The assertions about equality made in the summary become obligations on `pcequiv` for Clojure collections and on our implementations of `Equals` for everything else.


 `pcequiv` is straightforward:

```C#
public static bool pcequiv(object k1, object k2)
{
    if (k1 is IPersistentCollection ipc1)
        return ipc1.equiv(k2);
    return ((IPersistentCollection)k2).equiv(k1);
}
```

In other words, whichever argument is a `IPersistentCollection`, use its `equiv` method.  So equality testing now has moved to the collection's `equiv` _versus_ its `Equals`.  Let's compare these for a typical collection.  Let's try `PersistentVector`;  these methods are defined for it in its base class, `APersistentVector`.  I'm going to switch the Java code here because there is an important point illustrated there that is not in the C# version (for reasons that will become apparent in a bit):

```Java
public boolean equals(Object obj){
    if(obj == this)
        return true;
	return doEquals(this, obj);
}

public boolean equiv(Object obj){
    if(obj == this)
        return true;
	return doEquiv(this, obj);
}
```

So the entry points here handle reference equality and then delegate for content-based comparison. Starting with

```Java
static boolean doEquals(IPersistentVector v, Object obj){
    if(obj instanceof IPersistentVector)
        {
        IPersistentVector ov = (IPersistentVector) obj;
        if(ov.count() != v.count())
            return false;
        for(int i = 0;i< v.count();i++)
            {
            if(!Util.equals(v.nth(i), ov.nth(i)))
                return false;
            }
        return true;
        }
	else if(obj instanceof List)
        {
		Collection ma = (Collection) obj;
		if(ma.size() != v.count() || ma.hashCode() != v.hashCode())
			return false;
		for(Iterator i1 = ((List) v).iterator(), i2 = ma.iterator();
		    i1.hasNext();)
			{
			if(!Util.equals(i1.next(), i2.next()))
				return false;
			}
		return true;
		}
	else
        {
		if(!(obj instanceof Sequential))
			return false;
		ISeq ms = RT.seq(obj);
		for(int i = 0; i < v.count(); i++, ms = ms.next())
			{
			if(ms == null || !Util.equals(v.nth(i), ms.first()))
				return false;
			}
		if(ms != null)
			return false;
		}

	return true;

}
```

Fairly straightforward.  `doEquiv` looks very similar.  We can take advantage that the first argument is known to be an IPersistentVector, allowing us to use access by index rather than using an iterator.


```Java
static boolean doEquiv(IPersistentVector v, Object obj){
    if(obj instanceof IPersistentVector)
        {
        IPersistentVector ov = (IPersistentVector) obj;
        if(ov.count() != v.count())
            return false;
        for(int i = 0;i< v.count();i++)
            {
            if(!Util.equiv(v.nth(i), ov.nth(i)))
                return false;
            }
        return true;
    }
	else if(obj instanceof List)
		{
		Collection ma = (Collection) obj;

		if((!(ma instanceof IPersistentCollection) || (ma instanceof Counted)) && (ma.size() != v.count()))
			return false;

		Iterator i2 = ma.iterator();

		for(Iterator i1 = ((List) v).iterator(); i1.hasNext();)
			{
			if(!i2.hasNext() || !Util.equiv(i1.next(), i2.next()))
				return false;
			}
		return !i2.hasNext();
		}
	else
		{
		if(!(obj instanceof Sequential))
			return false;
		ISeq ms = RT.seq(obj);
		for(int i = 0; i < v.count(); i++, ms = ms.next())
			{
			if(ms == null || !Util.equiv(v.nth(i), ms.first()))
				return false;
			}
		if(ms != null)
			return false;
		}

	return true;

}
```

There is one major clue in that code. The early escape clause in the `IList` section for `doEquals` is:

```Java
		if(ma.size() != v.count() || ma.hashCode() != v.hashCode())
			return false;
```

while in `doEquiv` we see:

```Java
		if((!(ma instanceof IPersistentCollection) 
            || (ma instanceof Counted)) && (ma.size() != v.count()))
			return false;
```

A size comparison in both.
But only `doEquals` looks at hashcode equality.  And therein lies the historical tale.
There was a need to improve the distribution of hash codes for collections.  Because many Clojure collections implement `java.util.List`, to fit into the Java eco-system, they needed to follow the [contract for hash codes](https://docs.oracle.com/javase/1.5.0/docs/api/java/util/List.html#hashCode()) for that interface:


> `int hashCode()`
>
> Returns the hash code value for this list. The hash code of a list is defined to be the result of the following calculation:

```Java
        hashCode = 1;
        Iterator i = list.iterator();
        while (i.hasNext()) {
            Object obj = i.next();
            hashCode = 31*hashCode + (obj==null ? 0 : obj.hashCode());
        }
```

Thus, `APersistentVector.hashCode()` at its core is:

```Java
hash = 1;
for(int i = 0;i<count();i++)
    {
    Object obj = nth(i);
    hash = 31 * hash + (obj == null ? 0 : obj.hashCode());
    }
this._hash = hash;
```

The hashcode that will be used _internally_ in Clojure, for things such as map key calculations, is `hasheq`.  Its core is

```Java
int n;
hash = 1;

for(n=0;n<count();++n)
    {
    hash = 31 * hash + Util.hasheq(nth(n));
    }

this._hasheq = hash = Murmur3.mixCollHash(hash, n);
```

Same combining of element hashes, but some extra mixing using a Murmur3 method to improve hashcode distribution.

For maps, the case is similar. The Java spec for [`java.util.Map.hashCode()`](https://docs.oracle.com/javase/1.5.0/docs/api/java/util/Map.html#hashCode()) tells us:

> Returns the hash code value for this map. The hash code of a map is defined to be the sum of the hashCodes of each entry in the map's entrySet view. This ensures that `t1.equals(t2)` implies that `t1.hashCode()==t2.hashCode()` for any two maps `t1` and `t2`, as required by the general contract of `Object.hashCode`.

Thus, for `APersistentMap.hashCode()` in Clojure:

```Java
int hash = 0;
for(ISeq s = m.seq(); s != null; s = s.next())
    {
    Map.Entry e = (Map.Entry) s.first();
    hash += (e.getKey() == null ? 0 : e.getKey().hashCode()) ^
            (e.getValue() == null ? 0 : e.getValue().hashCode());
    }
```
while for `APersistentMap.hasheq()`, Murmur3 to the rescue again:

```Java
Murmur3.hashUnordered(m)
```

There were other factors in the change from the older `Equals/hashCode` approach to using `equiv/hasheq`, most prominent being the need to deal with numbers more effectively and consistently.  That is the topic of [the next post]({%  post_url 2023-01-19-a-numbers-game %}).

I hope this explains some of the code we will see later on.  The constraints on `Equals/hashCode` and `equiv/hasheq` need to be kept in mind going forward.


Note: When I first ported the Java code to C#, I did a lot of straight copying, then modifying the code to get rid of compiler errors. 
Not too much thinking required.  When I began testing, I was getting failures on some of the equality tests on collections.  Not surprising, given that hashcode check -- totally different regimen for dealing with hashcodes in `System.Collections.*`.  (As in there is no regimen.) So I had to remove the hashcode short-circuits.  

When `equiv/hasheq` came into the picture, I just put in the changes.  Although I  needed to keep the `Equals`/`equiv` part the same, I'm guessing there was no need to keep around two different hash codes.   Something else to consider as we make our way through the data structure rewrites.
