---
layout: post
title: A mega-dose of micro-benchmarks
date: 2024-06-18 10:36:00 -0500
categories: general
---

# A mega-dose of micro-benchmarks

I finally have time to work on ClojureCLR.Next again.  Thing had been busy.

One concern that I have had is that the performance of the new ClojureCLR might not be as good as the old one. Being relatively new to F#, I don't know all the little pitfalls that can cause performance problems.  Before this recent hiatus, I had been working on hash maps implementations and had been checking performance.  Some things looked okay, some not.  To get back in the groove of things, I decided to focus on something simple: creating a `PersistentArrayMap`.



You may want to review two previous posts:

- [This map is the territory]({{site.baseurl}}{% post_url 2023-01-11 %}) discusses the interfaces that a map must implement and presents a naive implementation of a map.
- [A road to maps]({{site.baseurl}}{% post_url 2023-02-03-a-road-to-maps %}) discuses the various classes that are involved in getting to implementations of `PersistentVector`, `PersistentHashMap`, and company.  (There are four articles here on `PersistentVector` -- we won't need them at this time.)


# `PersistentArrayMap`

`PersistentArrayMap` is a simple map that uses an array to store the keys and values.  The keys are stored in the even indexes and the values in the odd indexes.  For immutability, the array is never modified.  When we perform an operation such as deleting or adding an key/value pair, a new object holding a new array is createed.

`PersistentArrayMap` performs key lookup via linear search in the array.  This is not efficient.  This data structure is intended to be used only for small maps.  The idea is that linear search will be efficient up to certain size.  After that, we switch to a data structure (`PersistentHashMap`) that has larger memory footprint but faster access time.  (The threshold in Clojure(JVM) and ClojureCLR is set at 16 entries.)  When you add a key/value pair to a `PersistentArrayMap` and cause the size to move above the threshold, the operation will return a `PersistentHashMap`.  Thus, you need to develop both maps in order to deliver. 


The implementation of `PersistentArrayMap` is straightforward.  The data structure holds an array (and a map for metadata, if needed).
There are some little oddities in there.  For example, because Clojure keywords are commonly used as keys, there is some special case code to speed things up for that case.  But no need to go into details here.  What I'd like to focus on is just one thing: creating a `PersistentArrayMap` from an array of alternating key/value pairs.  Here is the code I originally wrote:


```F#
    static member createWithCheck(init: obj array) =
        for i in 0 .. 2 .. init.Length-1 do
            for j in i + 2 .. 2 .. init.Length - 1  do
                if PersistentArrayMap.equalKey (init[i], init[j]) then
                    raise <| ArgumentException($"Duplicate key: {init[i]}")
                    
        PersistentArrayMap(init)
```



Yes, it is an O(n^2) algorithm:  We check each key against the keys following it in the array.  This is okay.  Only for small `n`.
What is not okay is that my first benchmark showed this running 20-30% slower than the C# ClojureCLR version.  Now, granted, times are down in the two-digit nanosecond range, but I was worried that things I was doing incorrectly in F# were going to bite me even more later on.  So I decided to investigate.

[I'm not going to cover my investigations in historical order. Too many blind alleys and weird turns.  I'll just try to make sense of it in the aftermath.] 

## It matters how you iterate

I looked at the IL generated from the code above, and found it untidy.
In fact, one version actually iterated down a range enumerator.  
(For some reason, I'm not seeing that anymore.)





```F#
   static member createWithCheck(init: obj array) =
        
        let mutable i = 0;
        while i < init.Length do
            let mutable j = i + 2
            while j < init.Length do
                if PersistentArrayMap.equalKey (init.[i], init.[j]) then
                    raise <| ArgumentException($"Duplicate key: {init.[i]}")
                j <- j + 2
            i <- i + 2

        PersistentArrayMap(init)
```