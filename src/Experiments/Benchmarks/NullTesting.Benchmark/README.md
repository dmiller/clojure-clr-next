# NullTesting.Benchmark

I wanted to benchmark the various ways of testing for null.

- WithMatch: `match` expression with a `null` case
- WithIsNull: using the `isNull` function
- WithReferenceEquals: using `ReferenceEquals`
- WithEquals: using the `=` operator

## Results

```
| Method              | Mean       | Error    | StdDev   | Ratio | RatioSD |
|-------------------- |-----------:|---------:|---------:|------:|--------:|
| NoTestBaseline      |   261.4 ns |  4.67 ns |  4.37 ns |  1.00 |    0.00 |
| WithIsNull          |   337.8 ns |  2.31 ns |  2.16 ns |  1.29 |    0.02 |
| WithEquals          | 2,771.4 ns | 14.72 ns | 13.05 ns | 10.62 |    0.17 |
| WithReferenceEquals |   342.2 ns |  4.14 ns |  3.87 ns |  1.31 |    0.02 |
| WithMatch           |   335.8 ns |  1.58 ns |  1.48 ns |  1.28 |    0.02 |
```

## Conclusion

What one finds online is advice to use `match` or `isNull` for null testing. 
Believe it.
All ways are one in the end, except for `=`.
