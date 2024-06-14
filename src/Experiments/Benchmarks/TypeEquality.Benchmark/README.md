# TypeEquality.Benchmark

How many different ways can we check if two types are equal?  Or check the type of an object?
And which is the fastest?


- CSharpEquals: Baseline for comparison.  Uses the built-in C# `==` operator, which translates to `Type.op_Equality`.
- FShaprEquals: Uses the F# `=` operator.
- FShaprEquals2: Uses `Type.Equals`.
- FShaprEqualsOp: Uses `Type.op_Equality` directly.
- FSharpRefEquals: Uses `Object.ReferenceEquals`.
- CSharpTypeTestObj: Receives an object, gets its type, uses `==` to compare it to target types.
- FSharpTypeTestObjByCast: Uses the `:?` operator for type testing.
- FSharpTypeTestObjByType:  Calls GetType, uses `=` to compare it to target types.
- FSharpTypeTestObjByInstanceOf: Calls `Type.IsInstanceOfType`, passing the object in question.
- FSharpTypeTestObjByTypeMatch: Uses :? in a match expression.

## Results


| Method                        | size    | Mean      | Error     | StdDev    | Ratio | RatioSD |
|------------------------------ |-------- |----------:|----------:|----------:|------:|--------:|
| CSharpEquals                  | 1000    |  2.370 ms | 0.0466 ms | 0.0436 ms |  1.00 |    0.00 |
| FSharpEquals                  | 1000    | 21.019 ms | 0.2174 ms | 0.2033 ms |  8.87 |    0.18 |
| FSharpEquals2                 | 1000    |  6.368 ms | 0.0614 ms | 0.0574 ms |  2.69 |    0.06 |
| FSharpEqualsOp                | 1000    |  2.324 ms | 0.0139 ms | 0.0123 ms |  0.98 |    0.02 |
| FSharpRefEquals               | 1000    |  2.614 ms | 0.0400 ms | 0.0334 ms |  1.10 |    0.02 |
| CSharpTestTypeObj             | 1000    |  3.663 ms | 0.0667 ms | 0.0624 ms |  1.55 |    0.03 |
| FSharpTestTypeObjByCast       | 1000    |  3.891 ms | 0.0195 ms | 0.0182 ms |  1.64 |    0.03 |
| FSharpTestTypeObjByType       | 1000    | 25.052 ms | 0.2116 ms | 0.1979 ms | 10.58 |    0.22 |
| FSharpTestTypeObjByInstanceOf | 1000    | 13.069 ms | 0.0760 ms | 0.0635 ms |  5.51 |    0.11 |
| FSharpTestTypeObjByTypeMatch  | 1000    |  3.677 ms | 0.0599 ms | 0.0560 ms |  1.55 |    0.04 |
|                               |         |           |           |           |       |         |
| CSharpEquals                  | 1000000 |  2.334 ms | 0.0232 ms | 0.0217 ms |  1.00 |    0.00 |
| FSharpEquals                  | 1000000 | 21.625 ms | 0.2433 ms | 0.2157 ms |  9.26 |    0.14 |
| FSharpEquals2                 | 1000000 |  6.512 ms | 0.1290 ms | 0.1380 ms |  2.80 |    0.07 |
| FSharpEqualsOp                | 1000000 |  2.324 ms | 0.0223 ms | 0.0209 ms |  1.00 |    0.02 |
| FSharpRefEquals               | 1000000 |  2.581 ms | 0.0079 ms | 0.0066 ms |  1.11 |    0.01 |
| CSharpTestTypeObj             | 1000000 |  3.589 ms | 0.0699 ms | 0.0858 ms |  1.54 |    0.04 |
| FSharpTestTypeObjByCast       | 1000000 |  3.773 ms | 0.0750 ms | 0.1076 ms |  1.62 |    0.04 |
| FSharpTestTypeObjByType       | 1000000 | 25.952 ms | 0.4094 ms | 0.3629 ms | 11.12 |    0.16 |
| FSharpTestTypeObjByInstanceOf | 1000000 | 12.782 ms | 0.2347 ms | 0.2196 ms |  5.48 |    0.13 |
| FSharpTestTypeObjByTypeMatch  | 1000000 |  3.601 ms | 0.0719 ms | 0.1119 ms |  1.57 |    0.05 |

## Observations

No real surprises here.  We get performance comparable to C# only by direct use of `op_Equality`.  

