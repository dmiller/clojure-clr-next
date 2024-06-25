---
layout: post
title: Corrigendum -- Static initialization   
date: 2024-06-18 00:00:00 -0500
categories: general
---

I must have made an error in one of benchmarks mentioned in an earlier post.  Here I do a little more analysis and provide a correction to my commens and to the code.

In  [A mega-dose of micro-benchmarks, Part 2 -- By the numbers]({{site.baseurl}}{% post_url 2024-06-18-mega-dose-of-micro-benchmarks-part-2 %}), there was a section toward the end that discussed the performance hit of static initialization. I believe the analysis given there is incorrect.

Did I mention that micro-benchmarking is hard?

__TL;DR__: I made a claim that a static initialization being done in the `Numbers` package was causing a performance hit compared to the C# code. Something was very wrong in those numbers, but there was an element of truth.  And in the long run, it really doesn't matter.

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

The variable such as `$_.init@` and `B.init@3` are used to detect circularity conditions in static field initializations.   It appears that one pays a small price on every static field reference to test that initialization has happened properly.

I had read in a few places online that the tiered compilation of the modern JITter would get rid of this overhead eventually.  But I wasn't seeing it.  

It takes patience.

I thought surely all the warmup that BenchmarkDotNet does on the code before doing the actual benchmarking runs would be enough.  Not so.

I discovered this by accident.  I was benchmarking something else (still involving a static field reference) and accidentally compared the same code to itself three times.  The first run was considerably slower than the second and third runs.  The second and third runs were essentially identical.

For the classes above, I ran two copies of the code for calling StaticEmptyC and for Instance_EmptyC.


| Method                  | Mean     | Error    | StdDev   | Ratio | RatioSD |
|------------------------ |---------:|---------:|---------:|------:|--------:|
| Static_EmptyC           | 39.70 ns | 0.794 ns | 0.779 ns |  1.00 |    0.00 |
| Static_EmptyC_2ndTime   | 37.10 ns | 0.743 ns | 0.730 ns |  0.93 |    0.02 |
| Instance_EmptyC         | 32.88 ns | 0.493 ns | 0.437 ns |  0.83 |    0.02 |
| Instance_EmptyC_2ndTime | 33.26 ns | 0.425 ns | 0.397 ns |  0.84 |    0.02 |

We see that we get a speed up in Static_EmptyC_2ndTime running the same code as Static_EmptyC.  Also, accessing an instance variable is faster.  I do not know why this is the case.

In the earlier post, I described a technique to get rid of the static initialization cheks but having the consumer of the numerics package do an initialization step.  I went back to my original benchmarks and ran them twice.  Once with static initializations and the checks you see above and once with the user initialization code that got rid of the checks.  

No essential difference.

I thought it better not to put the burden of remembering to call an initialization function on the user of the package, so I reverted that change and went back to the code using static initialization.

I"m sure I learned some lesson here.  Not sure what it is.
