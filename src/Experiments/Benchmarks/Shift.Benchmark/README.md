# Shift.Benchmark


When computing branch indexes in hash array-mapped tries (HAMTs) 
and persitent bit-partitioned vector tries, we need to know the closest multiple of 32 less than a given number.

With some searching, I found several ways to do this:

```F#
// modulus
let im1 = i - 1
im1 - (im1 % 32) 

// divide/multiply
(( i - 1) / 32) *  32
    
// act all shifty
((i-1) >>> 5) <<< 5
```



The `- 1` is there to handle exactly multiples of 32 properly. (Work it out.) 
The ‘shifty’ version is the ‘divide/multiply’ version translated into shift operations: 
Shift to the right to clear the bottom bits out (that is equivalent to dividing by 32),
then shift left (equivalent to multiplying by 32). 

I decided to benchmark these three methods to see which one is the fastest.  
I did the tests for 32-bit and 64-bit integers separately.

## Results


| Method   | Mean     | Error    | StdDev   | Ratio |
|--------- |---------:|---------:|---------:|------:|
| ModOp    | 60.37 us | 0.690 us | 0.645 us |  1.00 |
| DivOp    | 48.54 us | 0.177 us | 0.165 us |  0.80 |
| BitShift | 31.72 us | 0.097 us | 0.086 us |  0.53 |


| Method     | Mean     | Error    | StdDev   | Ratio |
|----------- |---------:|---------:|---------:|------:|
| ModOp64    | 68.96 us | 0.918 us | 0.859 us |  1.00 |
| DivOp64    | 53.51 us | 0.867 us | 0.811 us |  0.78 |
| BitShift64 | 42.20 us | 0.219 us | 0.205 us |  0.61 |


The bit shift version is the fastest in both cases.  As expected