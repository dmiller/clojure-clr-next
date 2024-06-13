module NumericsBenchmarks



open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Diagnosers



[<MemoryDiagnoser>]
type NumericEquivTests() = 

    //[<Params( 1, 100)>]
    //member val size: int = 0 with get, set

    //[<Benchmark(Baseline = true)>]
    //member this.FirstNumberEqualObjObj() =
    //    let mutable x : bool = false
    //    for i = 0 to this.size-1 do
    //      x <- clojure.lang.Numbers.equal( i, i+1)
    //    x

    //[<Benchmark>]
    //member this.NextNumberEqualObjObj() =
    //    let mutable x : bool = false
    //    for i = 0 to this.size-1 do
    //      x <- Clojure.Numerics.Numbers.equal( i, i+1)
    //    x

    [<Benchmark(Baseline = true)>]
    member this.FirstNumberEqualOnLong() =  clojure.lang.Numbers.equal( 1L, 2L)
 
    [<Benchmark>]
    member this.NextNumberEqualOLong() = Clojure.Numerics.Numbers.equal( 1L, 2L)

   
        

[<MemoryDiagnoser>]
type NumericConverterTests() = 

    [<Params( 1_000_000)>]
    member val size: int = 0 with get, set

    static member getValue (i : int) : obj =
        match i % 4 with
        | 0 -> 1.3 :> obj
        | 1 -> "12" :> obj
        | 2 -> 12L :> obj
        | _ -> 12 :> obj
  
    [<Benchmark(Baseline = true)>]  
    member this.FirstNumberConvert() =
        let mutable x : int64 = 0
        for i = 0 to this.size-1 do
            x <- x + clojure.lang.Util.ConvertToLong( NumericConverterTests.getValue(i))
        x

    [<Benchmark>]  
    member this.NextNumberConvert() =
        let mutable x : int64 = 0
        for i = 0 to this.size-1 do
            x <- x + Clojure.Numerics.Converters.convertToLong( NumericConverterTests.getValue(i))
        x