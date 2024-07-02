---
layout: post
title: PersistentHashMap, part 5 -- At a loss
date: 2024-07-02 00:00:00 -0500
categories: general
---

We look at performance of the new F# version of     `PersistentHashMap` and compare it to the Clojure version.  And in the end, declare ourselved mystified.

This is the final post in a series on `PersistentHashMap`. The previous posts are:

- [Part 1: Making a hash of things (this post)]({{site.baseurl}}{% post_url 2024-07-02-persisent-hash-map-part-1 %})
- [Part 2: The root]({{site.baseurl}}{% post_url 2024-07-02-persisent-hash-map-part-2 %})
- [Part 3: The guts]({{site.baseurl}}{% post_url 2024-07-02-persisent-hash-map-part-3 %})
- [Part 4: Other matters]({{site.baseurl}}{% post_url 2024-07-02-persisent-hash-map-part-4 %})

# Performance

Given my level of F# experience, I'm always concerned about doing something stupid that will kill performance.  
(See [this serieds]({{site.baseurl}}{% post_url 2024-06-18-mega-dose-of-micro-benchmarks-part-1 %}) for an example.
So I decided to do some performance testing.  

I focused on several operations:  `assoc`; `assoc` on a transient map; and `containsKey`.  Construction one element at a time, efficient construction, and lookup.  I coded some benchmarks. I ran them using ints for keys and using strings for keys -- I wanted to make sure that the numeric comparisons were not a problem (see the link above).

Here, _First_ is the current ClojureCLR code, _Next_ is the F# code as described in these posts.
With integer keys and various sizes of maps, the results for `assoc` were:


| Method     | size | Mean       | Ratio | BranchInstructions/Op | CacheMisses/Op | BranchMispredictions/Op | Allocated | Alloc Ratio |
|----------- |----- |-----------:|------:|----------------------:|---------------:|------------------------:|----------:|------------:|
| FirstAssoc | 32   |   4.025 us |  1.00 |                10,315 |            108 |                      45 |  15.28 KB |        1.00 |
| NextAssoc  | 32   |   5.637 us |  1.40 |                14,817 |            142 |                      66 |  15.28 KB |        1.00 |
|            |      |            |       |                       |                |                         |           |             |
| FirstAssoc | 100  |  13.240 us |  1.00 |                31,093 |            382 |                     205 |  53.45 KB |        1.00 |
| NextAssoc  | 100  |  16.556 us |  1.27 |                43,083 |            209 |                     262 |  53.45 KB |        1.00 |
|            |      |            |       |                       |                |                         |           |             |
| FirstAssoc | 500  |  90.823 us |  1.00 |               196,351 |          2,112 |                   2,161 | 332.38 KB |        1.00 |
| NextAssoc  | 500  | 106.330 us |  1.19 |               256,438 |          1,515 |                   2,435 | 332.38 KB |        1.00 |
|            |      |            |       |                       |                |                         |           |             |
| FirstAssoc | 1000 | 202.504 us |  1.00 |               468,839 |          3,011 |                   5,344 | 766.19 KB |        1.00 |
| NextAssoc  | 1000 | 239.588 us |  1.20 |               593,876 |          3,469 |                   5,925 | 766.19 KB |        1.00 |

Not great. But not terrible.  Note that memory allocation matches exactly. Things such as branch mispredictions are not too bad.  

For transient `assoc` the results were:

| Method              | size | Mean      | Ratio | BranchInstructions/Op | BranchMispredictions/Op | CacheMisses/Op | Allocated | Alloc Ratio |
|-------------------- |----- |----------:|------:|----------------------:|------------------------:|---------------:|----------:|------------:|
| FirstTransientAssoc | 32   |  3.062 us |  1.00 |                 9,494 |                      30 |             51 |   6.07 KB |        1.00 |
| NextTransientAssoc  | 32   |  3.364 us |  1.09 |                11,292 |                      38 |             38 |   6.07 KB |        1.00 |
|                     |      |           |       |                       |                         |                |           |             |
| FirstTransientAssoc | 50   |  3.809 us |  1.00 |                12,838 |                      37 |             44 |   7.76 KB |        1.00 |
| NextTransientAssoc  | 50   |  4.612 us |  1.21 |                15,190 |                      52 |             48 |   7.76 KB |        1.00 |
|                     |      |           |       |                       |                         |                |           |             |
| FirstTransientAssoc | 100  |  6.618 us |  1.00 |                22,187 |                      79 |             70 |  12.51 KB |        1.00 |
| NextTransientAssoc  | 100  |  8.281 us |  1.25 |                26,706 |                     114 |            113 |  12.51 KB |        1.00 |
|                     |      |           |       |                       |                         |                |           |             |
| FirstTransientAssoc | 500  | 43.971 us |  1.00 |               126,296 |                     916 |            529 |  69.75 KB |        1.00 |
| NextTransientAssoc  | 500  | 49.280 us |  1.12 |               146,675 |                   1,067 |            556 |  69.75 KB |        1.00 |

A little better.

For lookups, I ran one benchmark where I looked up every key in the map.  Then one where I looked up a key not in the map.  The results were:

| Method           | size   | Mean            | Ratio | BranchInstructions/Op | CacheMisses/Op | BranchMispredictions/Op | Allocated | Alloc Ratio |
|----------------- |------- |----------------:|------:|----------------------:|---------------:|------------------------:|----------:|------------:|
| FirstContainsKey | 10     |        719.9 ns |  1.00 |                 2,256 |             12 |                       5 |     960 B |        1.00 |
| NextContainsKey  | 10     |      1,263.1 ns |  1.76 |                 4,931 |             10 |                      14 |     960 B |        1.00 |
|                  |        |                 |       |                       |                |                         |           |             |
| FirstContainsKey | 100    |      5,869.5 ns |  1.00 |                19,794 |             67 |                      32 |    9600 B |        1.00 |
| NextContainsKey  | 100    |     11,709.8 ns |  1.99 |                46,725 |             94 |                     111 |    9600 B |        1.00 |
|                  |        |                 |       |                       |                |                         |           |             |
| FirstContainsKey | 1000   |     68,987.0 ns |  1.00 |               211,902 |            683 |                     618 |   96001 B |        1.00 |
| NextContainsKey  | 1000   |    136,083.2 ns |  1.97 |               479,782 |          1,154 |                   1,278 |   96001 B |        1.00 |
|                  |        |                 |       |                       |                |                         |           |             |
| FirstContainsKey | 10000  |    817,806.8 ns |  1.00 |             2,263,791 |          9,010 |                   8,062 |  960012 B |        1.00 |
| NextContainsKey  | 10000  |  1,416,671.7 ns |  1.73 |             4,959,326 |         12,955 |                  12,030 |  960013 B |        1.00 |
|                  |        |                 |       |                       |                |                         |           |             |
| FirstContainsKey | 100000 | 11,913,599.8 ns |  1.00 |            21,925,495 |        198,848 |                  44,257 | 9600126 B |        1.00 |
| NextContainsKey  | 100000 | 19,679,065.8 ns |  1.64 |            49,517,508 |        258,334 |                  85,677 | 9600132 B |        1.00 |



| Method                  | size   | Mean            | Ratio | BranchInstructions/Op | CacheMisses/Op | BranchMispredictions/Op | Allocated | Alloc Ratio |
|------------------------ |------- |----------------:|------:|----------------------:|---------------:|------------------------:|----------:|------------:|
| FirstContainsKeyMissing | 10     |        824.4 ns |  1.00 |                 2,752 |             12 |                       4 |     960 B |        1.00 |
| NextContainsKeyMissing  | 10     |        988.0 ns |  1.20 |                 4,118 |              6 |                       5 |     480 B |        0.50 |
|                         |        |                 |       |                       |                |                         |           |             |
| FirstContainsKeyMissing | 100    |      2,739.3 ns |  1.00 |                 9,576 |             41 |                      10 |    9600 B |        1.00 |
| NextContainsKeyMissing  | 100    |      5,411.5 ns |  1.98 |                21,266 |             42 |                      18 |    4800 B |        0.50 |
|                         |        |                 |       |                       |                |                         |           |             |
| FirstContainsKeyMissing | 100000 |  3,035,531.8 ns |  1.00 |            11,578,970 |         30,002 |                   8,429 | 9600120 B |        1.00 |
| NextContainsKeyMissing  | 100000 | 10,824,464.3 ns |  3.57 |            44,755,575 |         59,803 |                  35,367 | 4800066 B |        0.50 |

We have better memory allocation in the latter, but branch mispredictions and cache misses are much higher in both.
And the performance ratio is _really bad_.

I ran the same tests for string keys.  Similar results.  Looking at just the time/op ratios:

|Operation | size   | int keys | string keys |
|--------- | ------ | --------:| -----------:|
| Assoc    |     32 |    1.40  |  1.12       |
| Assoc    |    100 |    1.27  |  1.20       |
| Assoc    |    500 |    1.19  |  1.17       |
| Assoc    |   1000 |    1.20  |  1.19       |
|          |        |          |             |
| TAssoc   |     32 |    1.09  |  1.10       |
| TAssoc   |     50 |    1.21  |  1.20       |
| TAssoc   |    100 |    1.25  |  1.16       |
| TAssoc   |    500 |    1.12  |  1.11       |
|          |        |          |             |
| CKey     |     10 |    1.76  |  1.83       |
| CKey     |    100 |    1.99  |  1.57       |
| CKey     |   1000 |    1.97  |  1.42       |
| CKey     |  10000 |    1.73  |  1.45       |
| CKey     | 100000 |    1.64  |  1.37       |
|          |        |          |             |
| Miss     |    10  |    1.20  |  2.97       |
| Miss     |   100  |    1.98  |  3.05       |
| Miss     | 100000 |    3.57  |  2.45       |

## Lookups

Looking for a key not in a map is horrendously less efficient.
Now, if you look at the actual timings, we are talking about 17 ns vs 51 ns or 25ns vs 60 ns, but a few nanoseconds here and there and soon you are talking hours.

I decided to focus on `containsKey`.  The code involved is much simpler than that for `assoc`.

In a first walkthrough, I found two simple coding errors. One involved our old friend `=` translating to complicated generic equality check.  (As covered in the post mentioned above.)  This one I missed because it was actually `<>`.  Same difference.  Oops.  Another involved a hash function that was generic and not specialized for `obj`.  I fixed these and reran just the lookup benchmarks on string keys.  There was more improvement than I would have guessed:

|Operation | size   | int keys | string keys |  after fixes |
|--------- | ------ | --------:| -----------:|-------------:|
| CKey     |     10 |    1.76  |  1.83       | 1.11         |
| CKey     |    100 |    1.99  |  1.57       | 1.20         |
| CKey     |   1000 |    1.97  |  1.42       | 1.09         |
| CKey     |  10000 |    1.73  |  1.45       | 1.07         |
| CKey     | 100000 |    1.64  |  1.37       | 1.03         |
|          |        |          |             |              |
| Miss     |    10  |    1.20  |  2.97       | 1.88         |
| Miss     |   100  |    1.98  |  3.05       | 1.97         |
| Miss     | 100000 |    3.57  |  2.45       | 1.87         |

Nice improvement.  But the 'Miss' case is still troublesome.

I went in for a comparison of the underlying code, function by function.
The new F#, the F# translated to IL and decompiled to C# (thanks, ILSpy) and the original C#.

To give you the gist, here is `PersistentMap.containsKey`:

The F# code:
```F#
        override _.containsKey(k) =
            if isNull k then
                hasNull
            else
                (not (isNull root))
                &&  not <| LanguagePrimitives.PhysicalEquality  (root.find (0, NodeOps.hash (k), k, PersistentHashMap.notFoundValue)) PersistentHashMap.notFoundValue
```				

The F# translated to C#:

```C#
virtual bool Associative.containsKey(object k)
{
	if (k == null)
	{
		return hasNull;
	}
	if (root != null && 0 == 0)
	{
		bool flag = root.find(0, NodeOps.hash(k), k, notFoundValue) == notFoundValue;
		return !flag;
	}
	return false;
}
```

The original C#:

```C#
public override bool containsKey(object key)
{
	if (key == null)
	{
		return _hasNull;
	}
	return _root != null && _root.Find(0, Hash(key), key, NotFoundValue) != NotFoundValue;
}
```

I don't think we can get closer than that.

I looked at the hashing code.  I looked at the implementations of `find` in each of the three node types.
Because some branches down the call tree seemed to go to far (hashing) I ran some direct benchmarks.

For example, given that we had already worked out numeric comparison issues previously, I concentrated on string keys.
We need to look at hashing and equality.  String hashing takes one to the method `HashStringU` in the Murmur3 library. I ran a benchmark just for that:


| Method           | size | Mean     | Ratio |
|----------------- |----- |---------:|------:|
| FirstHashStringU | 100  | 3.254 us |  1.00 |
| NextHashStringU  | 100  | 3.117 us |  0.96 |

When I looked at the code for hashing in general, the thing that calls `HashStringU`.  Here is the F# translated to C#: 

```C#
public static int hasheq(object o)
{
	if (o != null)
	{
		if (!(o is IHashEq))
		{
			object x2;
			if (o is string)
			{
				object x3 = o;
				if (!Numbers.IsNumeric(x3))
				{
					string s = (string)o;
					return Murmur3.HashString(s);
				}
				x2 = o;
			}
			else
			{
				object x = o;
				if (!Numbers.IsNumeric(x))
				{
					return o.GetHashCode();
				}
				x2 = o;
			}
			return Numbers.hasheq(x2);
		}
		IHashEq ihe = (IHashEq)o;
		return ihe.hasheq();
	}
	return 0;
}
```

The original C#:

```C#
public static int hasheq(object o)
{
	if (o == null)
	{
		return 0;
	}
	if (o is IHashEq ihe)
	{
		return dohasheq(ihe);
	}
	if (IsNumeric(o))
	{
		return Numbers.hasheq(o);
	}
	if (o is string s)
	{
		return Murmur3.HashString(s);
	}
	return o.GetHashCode();
}
```

I'm not great on these architectural issues, but I do know that branching misprediction are a thing.
How are we doing?  I coded up a benchmark to hash keys of various types.  (Many times.)

				   
| Method      | size | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0   | BranchInstructions/Op | CacheMisses/Op | BranchMispredictions/Op | Allocated | Alloc Ratio |
|------------ |----- |---------:|---------:|---------:|------:|--------:|-------:|----------------------:|---------------:|------------------------:|----------:|------------:|
| FirstHasheq | 1000 | 35.61 us | 0.708 us | 0.695 us |  1.00 |    0.00 | 1.8311 |               105,506 |            321 |                      96 |  23.44 KB |        1.00 |
| NextHasheq  | 1000 | 34.68 us | 0.199 us | 0.186 us |  0.97 |    0.02 | 1.3428 |               114,459 |            179 |                     123 |  17.58 KB |        0.75 | 

Well, we are as performant and allocate less. Let's move on.

I went down in  the lookup code in each node type.  We have to compare keys against keys.  That is an `equiv` method.
Might we have problems?  I tested comparing keys of various types agaisnt each other.  (many times)


| Method     | size | Mean     | Error     | StdDev    | Ratio | RatioSD | BranchInstructions/Op | BranchMispredictions/Op | CacheMisses/Op | Allocated | Alloc Ratio |
|----------- |----- |---------:|----------:|----------:|------:|--------:|----------------------:|------------------------:|---------------:|----------:|------------:|
| FirstEquiv | 12   | 3.252 us | 0.0639 us | 0.1417 us |  1.00 |    0.00 |                11,900 |                      30 |              2 |         - |          NA |
| NextEquiv  | 12   | 2.350 us | 0.0288 us | 0.0270 us |  0.73 |    0.04 |                10,328 |                      44 |              1 |         - |          NA |

Actually, the F# version does better.

What's next to look at?

I don't know.

I went through every line of code that is hit doing `containsKey`.  Or I wrote benchmarks for the smaller things that had too many moving parts to look at.  The F# code translated to C# is almost identical throughout.

As a last resort, I considered the possibility that the tree structure itself was the problem.  Maybe the F# code was creating a deep tree structure due to some bug I missed.  This is unlikely, given that the algorithm has the exact same memory allocation as the C# version.  Nevertheless, I wrote  printer for `PersistentHashMap` in both the F# and C# versions.  I ran it on maps of 10, 100, and 500 elements.  The trees produced by the two versions were identical.

I don't know what else to look at.

I'm open to suggestions.
