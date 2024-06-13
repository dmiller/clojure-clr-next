# AllThingsBeingEqual.Benchmark
   
A test comparing different ways of testing the equality of two objects, in F#.

Testing:

```F#
Object.ReferenceEquals(x, y)
LanguagePrimitives.PhysicalEquality x y
x = y
```

## Results

| Method          | Mean      | Error     | StdDev    | Ratio | RatioSD |
|---------------- |----------:|----------:|----------:|------:|--------:|
| RefEquals       |  6.346 us | 0.1169 us | 0.1347 us |  0.99 |    0.05 |
| PhysEquals      |  6.289 us | 0.1255 us | 0.2675 us |  1.00 |    0.00 |
| EqualSignEquals | 13.888 us | 0.1698 us | 0.1325 us |  2.17 |    0.09 |


## Analysis

Not much difference between `PhysicalEquality` and `ReferenceEquals`. The = operator is slower than the other two methods.

I'll be using `LanguagePrimitives.PhysicalEquality` in the code.
