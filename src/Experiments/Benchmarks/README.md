# Benchmarks

Lots of little experiments.

## Converter.Benchmark

Testing different was to map an object to its numeric type.

- __TypeCode__: match on Type.GetTypeCode to catch the primitive numerics, then call `Convert.ToInt32`.  I've seen this method used in some MS code.

- __CastingAlpha__: match on the type of the object.  
- __CastingNasty__: match on the type of the object, but I arranged the order of the tests to put the ones I used in testing at the end.
- __CastingNice__: match on the type of the object, but arrange the order of the tests to put the ones being tested first.  (We can guess the most common ones for Clojure.)
- __Direct__: just call `Convert.ToInt32`.


|       Method |     Mean |     Error |    StdDev | Ratio | RatioSD |
|------------- |---------:|----------:|----------:|------:|--------:|
|     TypeCode | 1.774 ms | 0.0346 ms | 0.0462 ms |  1.42 |    0.04 |
| CastingAlpha | 1.165 ms | 0.0165 ms | 0.0146 ms |  0.93 |    0.02 |
| CastingNasty | 1.302 ms | 0.0158 ms | 0.0140 ms |  1.04 |    0.02 |
|  CastingNice | 1.087 ms | 0.0201 ms | 0.0198 ms |  0.87 |    0.02 |
|       Direct | 1.251 ms | 0.0154 ms | 0.0137 ms |  1.00 |    0.00 |

I'll be going with the approach in `CastingNice`.

All of these convert the input object to int after figuring out what they have.
This is a disadvantage for __TypeCode__ as it has to do the match on the type code, then convert to the underlying type, then convert to `Int32`.  

There is another type of matching we have to do, that maps an object to a numeric category.  No conversion is required.  I code two versions, one using type code, one just matching on type:

|   Method |     Mean |    Error |   StdDev | Ratio | RatioSD |
|--------- |---------:|---------:|---------:|------:|--------:|
| TypeCode | 999.2 us | 17.67 us | 21.70 us |  1.00 |    0.00 |
|     Type | 858.7 us |  9.79 us |  9.16 us |  0.86 |    0.02 |

In these, I had the tests ordered to group them by category, as I would likely have coded them.
no messing around with ordering.

I was surprised to see that type comparison still had an advantage.

I will be some recoding as a result of this benchmark.


## Dispatch.Benchmark

Looking at two different ways of mapping a pair of numerc type categories to a common category.
In the existing Clojure code, this is done with a combination of overloads and virtual methods.

I thought I'd compare that to a table lookup.  For this situation, the combining operation using linked types returns an object implementing the desired operation set.  For my table lookup version, a 2D table encodes the pairings (Category x Category -> Category). then the result is used to index into another array to get the implementing object.

|          Method |     Mean |   Error |  StdDev | Ratio | RatioSD |
|---------------- |---------:|--------:|--------:|------:|--------:|
|     TypeCombine | 315.7 us | 6.11 us | 8.36 us |  1.00 |    0.00 |
| LookupCombine2D | 162.7 us | 2.32 us | 2.17 us |  0.51 |    0.02 |

No big surprise.

## Modding vs bit-shifting

In the code for `PersistentVector` there is a numeric calculation that uses left- and right-bitshift operations to compute the closest multiple of 32 below the input argument.
For technical reasons, if the number is an exact multiple of 32, we want the next lower mulitple.

The following expressions are equivalent:

```F#
let im1 = i - 1
im1 - (im1 % 32) 

(( i - 1) / 32) *  32

((i-1) >>> 5) <<< 5
```

Each requires a subtraction by one.  So you are comparing a subtraction + a remainder to a division and multiplication to two shifts.  Generally, shifts are faster so we'd expect the third one to be faster.  However, some compilers will see a multiplcation/division by a power of 2 and convert it to a shift.   Here's the comparison:



|   Method |     Mean |    Error |   StdDev | Ratio | RatioSD |
|--------- |---------:|---------:|---------:|------:|--------:|
|    ModOp | 57.36 us | 0.863 us | 0.807 us |  1.00 |    0.00 |
|    DivOp | 48.37 us | 0.607 us | 0.567 us |  0.84 |    0.02 |
| BitShift | 32.68 us | 0.572 us | 0.535 us |  0.57 |    0.01 |

No surprise: bit-shifting wins.