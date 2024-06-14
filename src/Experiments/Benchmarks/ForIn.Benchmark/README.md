# ForIn.Benchmark

Having noticed a surprisingly slow iteration in one of my tests, I decide to do simple benchmarking on looping constructs to see where the bad bevahior shows up.

The constructs are:

- `InNoStep` - a simple for-in loop with no step

```
for iter in 0 .. this.size do
```

- `ForToIteration` - a simple for-to loop

```
for iter = 0 to this.size-1 do
```

- `ManualIterationStep1` - manual iteration

```
let mutable iter = 0
while iter < this.size do
    ...
    iter <- iter + 1
```


- `InStep1` - a simple for-in loop with step 1

```
for iter in 0 .. 1 .. this.size do
```

- `ManualIterationStep2` - manual iteration with step size 2

```
let mutable iter = 0
while iter < doubleSize do
    ...
    iter <- iter + 2
```

- `InStep2` - a simple for-in loop with step 2

```
let mutable i : int =0
for iter in 0 .. 2 .. doubleSize do
```

| Method               | size    | Mean           | Error        | StdDev       | Ratio | RatioSD |
|--------------------- |-------- |---------------:|-------------:|-------------:|------:|--------:|
| InNoStep             | 1000    |       263.3 ns |      4.61 ns |      4.09 ns |  1.00 |    0.00 |
| ForToIteration       | 1000    |       250.3 ns |      0.79 ns |      0.66 ns |  0.95 |    0.02 |
| ManualIterationStep1 | 1000    |       251.1 ns |      1.31 ns |      1.23 ns |  0.95 |    0.02 |
| InStep1              | 1000    |       258.6 ns |      5.15 ns |      6.70 ns |  0.98 |    0.02 |
| ManualIterationStep2 | 1000    |       262.9 ns |      4.88 ns |      4.79 ns |  1.00 |    0.03 |
| InStep2              | 1000    |     1,170.2 ns |     22.74 ns |     24.34 ns |  4.45 |    0.12 |
|                      |         |                |              |              |       |         |
| InNoStep             | 1000000 |   249,427.6 ns |  4,939.61 ns |  5,880.26 ns |  1.00 |    0.00 |
| ForToIteration       | 1000000 |   246,474.2 ns |  3,712.34 ns |  3,472.52 ns |  0.98 |    0.03 |
| ManualIterationStep1 | 1000000 |   252,830.6 ns |  4,891.61 ns |  4,804.21 ns |  1.01 |    0.04 |
| InStep1              | 1000000 |   251,553.4 ns |  2,729.40 ns |  2,553.08 ns |  1.00 |    0.03 |
| ManualIterationStep2 | 1000000 |   248,743.9 ns |  4,116.35 ns |  4,404.45 ns |  0.99 |    0.02 |
| InStep2              | 1000000 | 1,093,381.9 ns | 21,065.19 ns | 21,632.40 ns |  4.37 |    0.16 |

The conclusion:  don't do `for iter in start .. step .. end` with `step` other than one. It is 4 times slower than the alternatives.
