---
layout: post
title: Corrigendum -- Static initialization   
date: 2024-06-18 00:00:00 -0500
categories: general
---

I believe I made an error in one of benchmarks mentioned in an earlier post.  Here I do a little more analysis and provide a correction to the code.

In  [A mega-dose of micro-benchmarks, Part 2 -- By the numbers]({{site.baseurl}}{% post_url 2024-06-18-mega-dose-of-micro-benchmarks-part-2 %}), there was a section toward the end that discussed the performance hit of static initialization. I believe that analysis is incorrect.

Did I mention that micro-benchmarking is hard?

I made a claim that a static initialization being done in the `Numbers` package was causing a performance hit compared to the C# code. Something was very wrong in those numbers, but there was an element of truth.  And in the long run, it really doesn't matter.

Here is a very reduced model of the kind of situation one gets into.
