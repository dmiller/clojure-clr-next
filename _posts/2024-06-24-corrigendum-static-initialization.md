---
layout: post
title: Corrigendum -- Static initialization   
date: 2024-06-18 00:00:00 -0500
categories: general
---

I must have made an error benchmarking static initialziations, detailed in a preceding post.  Here I do a little more analysis and provide a correction to my comments and to the code.

In  [A mega-dose of micro-benchmarks, Part 2 -- By the numbers]({{site.baseurl}}{% post_url 2024-06-18-mega-dose-of-micro-benchmarks-part-2 %}), there was a section toward the end that discussed the performance hit of static initialization. Further investigation proved that previous analysis was incorrect.

__TL;DR__: I made a claim that a static initialization being done in the `Numbers` package was causing a non-trivial performance hit compared to the C# code. The earlier analysis was wrong. And in the long run, it really doesn't matter.

Did I mention that micro-benchmarking is hard?

## The example

Here is a very reduced model of the kind of situation I was running into.


```F#
type C(v:int) = 
    member val V = v with get, set

type B(v:int ) =
    static member val StaticEmptyC = C(0) with get, set
    member val InstanceEmptyC = C(0) with get, set
    member this.V = v
```

 Compiling to IL and decompiling to C# (thanks, [sharplab.io](https://sharplab.io)), we get (with some editing):

 ```C#
    public class C
    {        
        internal int V@;
        public C(int v)  {  V@ = v; }
    
        public int V
        {  
            get { return V@;  }
            set { V@ = value; }
        }
    }
     
    public class B
    {
        internal int v;
        internal static C StaticEmptyC@;
        internal C InstanceEmptyC@;
        internal static int init@3;

        public static C StaticEmptyC
        {
            get
            {
                if (init@3 < 1)
                {
                    LanguagePrimitives.IntrinsicFunctions.FailStaticInit();
                }
                return StaticEmptyC@;
            }
            set
            {
                if (init@3 < 1)
                {
                    LanguagePrimitives.IntrinsicFunctions.FailStaticInit();
                }
                StaticEmptyC@ = value;
            }
        }

        public C InstanceEmptyC
        {
            get { return InstanceEmptyC@; }
            set { InstanceEmptyC@ = value;  }
        }

        public int V { get { return v; } }

        public B(int v)
        {
            this.v = v;
            InstanceEmptyC@ = new C(0);
        }

        static B()
        {
            $_.init@ = 0;
            int init@4 = $_.init@;
        }
    }

namespace <StartupCode$_>
{
    internal static class $_
    {
        internal static int init@;

        public static void main@()
        {
            B.StaticEmptyC@ = new C(0);
            B.init@3 = 1;
        }
    }
}
```

The variables such as `$_.init@` and `B.init@3` are used to detect circularity conditions in static field initializations.  It appears that one pays a small price on every static field reference to test that initialization has happened properly.
It's an F# thing; you won't see this in C# compiled code.  

I had read in a few places online that the tiered compilation of the modern JITter would get rid of this overhead eventually.  But my earlier benchmark was showing a big it.

I did something wrong in that benchmark.  There may have been other factors I had not isolated.

And I was suspicious, so I did a much more specific benchmark isolating the static initialization issue.

Regarding the static initialization checks, I had read online in a few places that tiered compilation would eventually get around this overhead.  But I was not seeing that in my benchmarks.  I thought surely all the warmup that BenchmarkDotNet does on the code before doing the actual benchmarking runs would be enough.  Not so.

Patience, padawan.

I discovered the need for patience quite by accident.  While writing the benchmarks, I committed a copy-and-paste error and accidentally executed the exact same method call on three different runs.  And they got faster.  The first run was noticeably slower than the second and third runs.  The second and third runs were essentially identical.

Here is my final benchmark output.  Three runs test calling `B.StaticEmptyC.V`; you can see that the first run is slowest.  Two runs call  `b.InstanceEmptyC.V` to compare static field access to instance field access; a little better, but we are way down in sub-nanosecond range here  (each call is doing 100 iterations).  For comparision, I added the C# equivalent code to the benchmark.  Here we don't see any warmup effect and the static and instance versions are essentially identical.



| Method                         | Mean     | Error    | StdDev   | Ratio |
|------------------------------- |---------:|---------:|---------:|------:|
| Static_EmptyC                  | 39.98 ns | 0.554 ns | 0.491 ns |  1.00 |
| Static_EmptyC_2ndTime          | 37.09 ns | 0.296 ns | 0.277 ns |  0.93 |
| Static_EmptyC_3rdTime          | 36.94 ns | 0.222 ns | 0.197 ns |  0.92 |
| Instance_EmptyC                | 34.13 ns | 0.247 ns | 0.231 ns |  0.85 |
| Instance_EmptyC_2ndTime        | 34.04 ns | 0.352 ns | 0.329 ns |  0.85 |
| CSharp_Static_EmptyC           | 32.85 ns | 0.384 ns | 0.359 ns |  0.82 |
| CSharp_Static_EmptyC_2ndTime   | 33.24 ns | 0.312 ns | 0.276 ns |  0.83 |
| CSharp_Instance_EmptyC         | 33.61 ns | 0.349 ns | 0.326 ns |  0.84 |
| CSharp_Instance_EmptyC_2ndTime | 33.64 ns | 0.327 ns | 0.273 ns |  0.84 |



In the earlier post, I described a technique to get rid of the static initialization checks but at the cost of having the consumer of the numerics package do an initialization step.  I went back to my original benchmarks and ran them twice.  Once with static initializations and the checks you see above and once with the user initialization code that got rid of the checks.  No essential difference.  I went back to the original code.

I"m sure I learned some lessons here.  Mostly on the uselessness of benchmarking in the sub-nanosecond range.  I would not have bothered, but I wanted to measure the hit of the static initialization checks.  And I would not have spent any more time on it, being essentially trivial, if I had not been misled by some mistake I made in my first benchmark.

Onward.