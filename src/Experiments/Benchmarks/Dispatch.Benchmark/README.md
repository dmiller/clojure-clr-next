# DispatchBenchmark

We compare methods of doing two-way type dispatch.


The `Numbers` package implements arithmetic, comparison, and related operations on numeric types.
Ideally, when doing numeric operations, we know the type(s) of the operand(s) and can generate IL code that is specialized for the type(s) of the operand(s).
`Numbers` comes into play when we don't know the type of the operand(s) at compile time.
Here is how we define an operation such as addition:

```F#
    static member add(x: obj, y: obj) = Numbers.getOps(x, y).add (x, y)
```


The `getOps` function returns an object that implements `Ops` interface.
It is essentially a double-dispatch -- looking at the types of `x` and `y` we categorize each (say as integer and floating point) and then use the rules of contagion to determine the the proper operations set to use.
For example, if `x` is an integer and `y` is a floating point number, we use the floating point operations set.

A separate benchmark project, Converter.Benchmark, looks at the categorization of an object from its type and the the conversion of objects to different types.
This benchmark looks at the performance of the `getOps` function _after_ `x` and `y` have been categorized, to find the object to use to perform the `add` operation.

The ClojureJVM and ClojureCLR.First implementations use a two-staged lookup.  Lookup the type of the first argument and get an operations object for that type.
The operations object has a `combine` method that takes the category of the second object and returns the operations object to use for operations on the combination of the two types.

For example,  if `x` was an `int`, from `x` we would get an `IntOps` object.  If `y` was a `Double`, we would call `IntOps.combine DoubleOps` to get the operations object to use for the addition.  
Due to the contagion rules of Clojure, this would yield a `DoubleOps` object.

I figured using a two-dimensional lookup table would be faster than the two-stage lookup.


## Results

| Method          | Mean      | Error    | StdDev    | Ratio |
|---------------- |----------:|---------:|----------:|------:|
| TypeCombine     | 260.58 us | 5.087 us | 10.729 us |  1.00 |
| LookupCombine2D |  87.07 us | 1.450 us |  1.356 us |  0.33 |

Two-dimensional lookup is about 3 times faster than the two-stage lookup.

