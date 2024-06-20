# LetRecIteration.Benchmark

Looking at using recursive let definitions to encode iteration in F#.
The difference between using Option and using a special value in the underlying type to indicate a special condition.

First attempt:
Number of iterations in the loops = 10

| Method           | Mean     | Error     | StdDev    | Ratio |
|----------------- |---------:|----------:|----------:|------:|
| LetRecIterOption | 7.521 ns | 0.1000 ns | 0.0935 ns |  1.00 |
| LetRecIterInt    | 2.972 ns | 0.0661 ns | 0.0618 ns |  0.40 |
| ManualLoopOption | 6.083 ns | 0.0753 ns | 0.0668 ns |  0.81 |
| ManualLoopInt    | 3.387 ns | 0.0354 ns | 0.0331 ns |  0.45 |

# iters = 1000

| Method           | Mean     | Error   | StdDev  | Ratio |
|----------------- |---------:|--------:|--------:|------:|
| LetRecIterOption | 280.8 ns | 2.03 ns | 1.90 ns |  1.00 |
| LetRecIterInt    | 225.1 ns | 2.52 ns | 2.36 ns |  0.80 |
| ManualLoopOption | 263.0 ns | 3.69 ns | 3.46 ns |  0.94 |
| ManualLoopInt    | 259.5 ns | 4.66 ns | 4.36 ns |  0.92 |

The work in the loop start to dominate the time.

I added value options to the mix.  N=10



| Method                | Mean     | Error     | StdDev    | Ratio |
|---------------------- |---------:|----------:|----------:|------:|
| LetRecIterOption      | 7.840 ns | 0.0611 ns | 0.0541 ns |  1.00 |
| LetRecIterInt         | 2.979 ns | 0.0241 ns | 0.0226 ns |  0.38 |
| ManualLoopOption      | 6.372 ns | 0.0890 ns | 0.0789 ns |  0.81 |
| ManualLoopInt         | 3.493 ns | 0.0315 ns | 0.0295 ns |  0.45 |
| LetRecIterValueOption | 4.060 ns | 0.0576 ns | 0.0539 ns |  0.52 |
| ManualLoopValueOption | 3.494 ns | 0.0605 ns | 0.0566 ns |  0.45 |

I added multiple calls to each method  

| Method                | size | Mean      | Error    | StdDev    | Ratio | RatioSD |
|---------------------- |----- |----------:|---------:|----------:|------:|--------:|
| LetRecIterOption      | 10   |  81.81 ns | 1.119 ns |  0.992 ns |  1.00 |    0.00 |
| LetRecIterValueOption | 10   |  43.28 ns | 0.366 ns |  0.343 ns |  0.53 |    0.01 |
| LetRecIterInt         | 10   |  44.94 ns | 0.758 ns |  0.709 ns |  0.55 |    0.01 |
| ManualLoopOption      | 10   |  46.97 ns | 0.958 ns |  1.653 ns |  0.59 |    0.02 |
| ManualLoopValueOption | 10   |  35.77 ns | 0.426 ns |  0.398 ns |  0.44 |    0.01 |
| ManualLoopInt         | 10   |  34.86 ns | 0.338 ns |  0.316 ns |  0.43 |    0.00 |
|                       |      |           |          |           |       |         |
| LetRecIterOption      | 100  | 480.45 ns | 9.562 ns | 19.315 ns |  1.00 |    0.00 |
| LetRecIterValueOption | 100  | 217.52 ns | 1.532 ns |  1.433 ns |  0.45 |    0.02 |
| LetRecIterInt         | 100  | 215.37 ns | 1.332 ns |  1.246 ns |  0.44 |    0.02 |
| ManualLoopOption      | 100  | 324.10 ns | 5.332 ns |  4.987 ns |  0.66 |    0.03 |
| ManualLoopValueOption | 100  | 234.26 ns | 4.590 ns |  5.636 ns |  0.49 |    0.03 |
| ManualLoopInt         | 100  | 183.10 ns | 1.498 ns |  1.328 ns |  0.38 |    0.02 |


