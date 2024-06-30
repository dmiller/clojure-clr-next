---
layout: post
title: `PersistentHashMap`, part 4 -- Other matters
date: 2024-06-28 00:00:00 -0500
categories: general
---

We conclude our discussion of `PersistentHashMap` with a discussion of transiency, performance, and alternative F# coding techniques.

This is the final post in a series on `PersistentHashMap`. The previous posts are:

- [Part 1: Making a hash of things (this post)]({{site.baseurl}}{% post_url 2024-06-28-persisent-hash-map-part-1 %})
- [Part 2: The root]({{site.baseurl}}{% post_url 2024-06-28-persisent-hash-map-part-2 %})
- [Part 3: The guts]({{site.baseurl}}{% post_url 2024-06-28-persisent-hash-map-part-3 %})

# Transiency

I refer you to the [discussion]({{site.baseurl}}{% post_url 2023-02-18-PersisentVector-part-3 %}) of transiency in the `PersistentVector` series.

As for `PersistentVector`, `PersistentHashMap` supports interfaces such as `IEditableCollection`, `ITransientCollection`, `ITransientAssociative`.  There is one additional, map-specific transiency inteface:

```F#
[<AllowNullLiteral>]
type ITransientMap =
    inherit ITransientAssociative
    inherit Counted
    abstract assoc: key: obj * value: obj -> ITransientMap
    abstract without: key: obj -> ITransientMap
    abstract persistent: unit -> IPersistentMap
```

. The same mechanisms are used to implement transiency in `PersistentHashMap` as in `PersistentVector`.  I leave the implementation to you as an exercise.

# Performance

As I'm learning my way around F#, I remain sensitive to writing code that is not as performant as it might be.  Efficiency trumps beauty in these parts.  I did some benchmarking against the original C# code.





# Alternative coding techniques



