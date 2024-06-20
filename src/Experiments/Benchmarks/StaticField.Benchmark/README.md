# StaticField.Benchmark

I was trying to find out what the overhead was of using static fields in a class. 
I created a simple benchmark to test this. 
I accidentally miscoded the test runs and ended up running the same code twice.
The benchmark showed the same code being twice as fast on the second call.


And there we have evidence that tiered compilation really does reduce the overhead of the static initialization checks significantly.

What I wondered is how long that it would take and roughly what the speedup would be.

BenchmarkDotNet does a lot of warmup runs.  Perhaps the evidence is there.
What I needed to do was to get a variable number of calls to the static field lookup and see what happened as increased the number of calls.


Here is the benchmark code:

```F#
type A (v:int) = 

    static let letEmptyA = A(0)
    static member val StaticVal = A(0)

    static member Empty = letEmptyA

    member this.V = v

[<Literal>]
let NumIters = 0

type Tests() = 
    

    [<Benchmark(Baseline=true)>]
    member this.StaticVal() =  
        let mutable i : int =0
        for iter in 0 .. NumIters do
            i <- i + A.StaticVal.V
        i    
    
    // More probes with the same iteration follo

```

Running the benchmark with 0 iterations allows us to get a sense of the loop overhead and what tiered compilation might do to it.

```
OverheadJitting  1: 1 op, 156400.00 ns, 156.4000 us/op
WorkloadJitting  1: 1 op, 78260700.00 ns, 78.2607 ms/op

WorkloadPilot    1: 2 op, 700.00 ns, 350.0000 ns/op
WorkloadPilot    2: 3 op, 600.00 ns, 200.0000 ns/op
WorkloadPilot    3: 4 op, 300.00 ns, 75.0000 ns/op
WorkloadPilot    4: 5 op, 600.00 ns, 120.0000 ns/op
WorkloadPilot    5: 6 op, 600.00 ns, 100.0000 ns/op
WorkloadPilot    6: 7 op, 400.00 ns, 57.1429 ns/op
WorkloadPilot    7: 8 op, 600.00 ns, 75.0000 ns/op
WorkloadPilot    8: 9 op, 400.00 ns, 44.4444 ns/op
WorkloadPilot    9: 10 op, 400.00 ns, 40.0000 ns/op
WorkloadPilot   10: 11 op, 300.00 ns, 27.2727 ns/op
WorkloadPilot   11: 12 op, 400.00 ns, 33.3333 ns/op
WorkloadPilot   12: 13 op, 400.00 ns, 30.7692 ns/op
WorkloadPilot   13: 14 op, 600.00 ns, 42.8571 ns/op
WorkloadPilot   14: 15 op, 400.00 ns, 26.6667 ns/op
WorkloadPilot   15: 16 op, 500.00 ns, 31.2500 ns/op
WorkloadPilot   16: 32 op, 600.00 ns, 18.7500 ns/op
WorkloadPilot   17: 64 op, 900.00 ns, 14.0625 ns/op
WorkloadPilot   18: 128 op, 1700.00 ns, 13.2812 ns/op
WorkloadPilot   19: 256 op, 3100.00 ns, 12.1094 ns/op
WorkloadPilot   20: 512 op, 6100.00 ns, 11.9141 ns/op
WorkloadPilot   21: 1024 op, 11900.00 ns, 11.6211 ns/op
WorkloadPilot   22: 2048 op, 23400.00 ns, 11.4258 ns/op
WorkloadPilot   23: 4096 op, 47300.00 ns, 11.5479 ns/op
WorkloadPilot   24: 8192 op, 130400.00 ns, 15.9180 ns/op
WorkloadPilot   25: 16384 op, 175000.00 ns, 10.6812 ns/op
WorkloadPilot   26: 32768 op, 268500.00 ns, 8.1940 ns/op
WorkloadPilot   27: 65536 op, 466800.00 ns, 7.1228 ns/op
WorkloadPilot   28: 131072 op, 863600.00 ns, 6.5887 ns/op
WorkloadPilot   29: 262144 op, 1656800.00 ns, 6.3202 ns/op
WorkloadPilot   30: 524288 op, 3142300.00 ns, 5.9935 ns/op
WorkloadPilot   31: 1048576 op, 6319200.00 ns, 6.0265 ns/op
WorkloadPilot   32: 2097152 op, 12535500.00 ns, 5.9774 ns/op
WorkloadPilot   33: 4194304 op, 25018600.00 ns, 5.9649 ns/op
WorkloadPilot   34: 8388608 op, 52115300.00 ns, 6.2126 ns/op
WorkloadPilot   35: 16777216 op, 74198300.00 ns, 4.4226 ns/op
WorkloadPilot   36: 33554432 op, 48448400.00 ns, 1.4439 ns/op
WorkloadPilot   37: 67108864 op, 93443800.00 ns, 1.3924 ns/op
WorkloadPilot   38: 134217728 op, 186581900.00 ns, 1.3901 ns/op
WorkloadPilot   39: 268435456 op, 376728900.00 ns, 1.4034 ns/op
WorkloadPilot   40: 536870912 op, 771220400.00 ns, 1.4365 ns/op
```

And the times stabilize from there.
The second run starts a bit faster, but we see a decone down from 50 ns/op to .14 ns/op.
Makes sense -- the loop is doing nothing.

But we can still see the drop in times:

| Method     | Mean      | Error     | StdDev    | Ratio |
|----------- |----------:|----------:|----------:|------:|
| StaticVal  | 1.3706 ns | 0.0262 ns | 0.0257 ns |  1.00 |
| StaticVal2 | 0.1286 ns | 0.0193 ns | 0.0180 ns |  0.09 |
| StaticVal3 | 0.1096 ns | 0.0093 ns | 0.0082 ns |  0.08 |




Now we can run the benchmark with a non-zero number of iterations.  
Let's start with 1.  
That will give us one call to the static field plus all the loop and return overhead.


| Method     | Mean      | Error     | StdDev    | Ratio |
|----------- |----------:|----------:|----------:|------:|
| StaticVal  | 1.3047 ns | 0.0217 ns | 0.0192 ns |  1.00 |
| StaticVal2 | 0.1290 ns | 0.0086 ns | 0.0081 ns |  0.10 |
| StaticVal3 | 0.1115 ns | 0.0062 ns | 0.0058 ns |  0.09 |

Hmm.  It might have figured something out.  With only one iteration, the loop can be jettisoned.
And if the static call is improved by Tier 1, hard to detect.


N=2
| Method     | Mean      | Error     | StdDev    | Ratio |
|----------- |----------:|----------:|----------:|------:|
| StaticVal  | 1.5153 ns | 0.0157 ns | 0.0139 ns |  1.00 |
| StaticVal2 | 0.1279 ns | 0.0113 ns | 0.0100 ns |  0.08 |
| StaticVal3 | 0.1295 ns | 0.0098 ns | 0.0087 ns |  0.09 |


Let's jump to 10 iterations:

| Method     | Mean     | Error     | StdDev    | Ratio |
|----------- |---------:|----------:|----------:|------:|
| StaticVal  | 4.593 ns | 0.0756 ns | 0.0707 ns |  1.00 |
| StaticVal2 | 3.145 ns | 0.0287 ns | 0.0269 ns |  0.68 |
| StaticVal3 | 3.268 ns | 0.0341 ns | 0.0319 ns |  0.71 |

Now we can guess that the Tier 1 optimization of the static initialization overhead is kicking in during the first run.

N=100

| Method     | Mean     | Error    | StdDev   | Ratio |
|----------- |---------:|---------:|---------:|------:|
| StaticVal  | 38.87 ns | 0.424 ns | 0.396 ns |  1.00 |
| StaticVal2 | 34.61 ns | 0.138 ns | 0.129 ns |  0.89 |
| StaticVal3 | 34.99 ns | 0.334 ns | 0.312 ns |  0.90 |





