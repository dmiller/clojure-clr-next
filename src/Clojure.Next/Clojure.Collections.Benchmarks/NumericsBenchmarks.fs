module NumericsBenchmarks



open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Diagnosers


type EInputType =
| I32 = 0
| I64 = 1
| Dbl = 2
//| U64 = 3



let getValue(inputType : EInputType) : obj =
    match inputType with
    | EInputType.I32 -> 12
    | EInputType.I64 -> 12L
    | EInputType.Dbl -> 1.2
  //  | EInputType.U64 -> 12UL
    | _ -> failwith "Invalid input type"


type NumericEquivTests() = 

    [<ParamsAllValues>]
    member val xInputType: EInputType = EInputType.I32 with get, set

    [<ParamsAllValues>]
    member val yInputType: EInputType = EInputType.I32 with get, set

    member val testedValX : obj = null with get, set
    member val testedValY : obj = null with get, set

    [<GlobalSetup>]
    member this.GlobalSetup() =
        //Clojure.Numerics.Initializer.init() |> ignore
        this.testedValX <- getValue this.xInputType
        this.testedValY <- getValue this.yInputType

    [<Benchmark(Baseline = true)>]
    member this.FirstNumberEqualOnLong() =  clojure.lang.Numbers.equal( this.testedValX, this.testedValY )
 
    [<Benchmark>]
    member this.NextNumberEqualOnLong() = Clojure.Numerics.Numbers.equal( this.testedValX, this.testedValY )

   
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



type CategoryVersusOps() =

    [<ParamsAllValues>]
    member val inputType: EInputType = EInputType.I32 with get, set

    member val testedVal : obj = null with get, set

    [<GlobalSetup>]
    member this.GlobalSetup() =
        this.testedVal <- getValue this.inputType


    [<Benchmark(Baseline = true)>]  
    member this.FirstCategory() = BabyNumbers.CSharp.Numbers.category(this.testedVal)

    [<Benchmark>]
    member this.NextOps() = Clojure.Numerics.OpsSelector.ops(this.testedVal)


    [<Benchmark>]
    member this.NextOps2() = Clojure.Numerics.OpsSelector.ops(this.testedVal)



type HashStringUTests() = 

    [<Params( 100 )>]
    member val size: int = 0 with get, set

    static member getValue (i : int) : string =
        match i % 4 with
        | 0 -> "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
        | 1 -> "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
        | 2 -> "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"
        | _ -> "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd"
  
    [<Benchmark(Baseline = true)>]
    member this.FirstHashStringU() = 
        let mutable x : uint32 = 0u

        for i = 0 to this.size-1 do
            x <- x + clojure.lang.Murmur3.HashStringU( HashStringUTests.getValue(i))
        x
        
    [<Benchmark>]
    member this.NextHashStringU() = 
        let mutable x : uint32 = 0u

        for i = 0 to this.size-1 do
            x <- x + Clojure.Numerics.Murmur3.HashStringU( HashStringUTests.getValue(i))
        x
        
[<MemoryDiagnoser ;HardwareCounters(HardwareCounter.BranchMispredictions,HardwareCounter.BranchInstructions,HardwareCounter.CacheMisses)   >]
type HasheqTests() =

    [<Params( 1_000)>]
    member val size: int = 0 with get, set

    static member getValue (i : int) : obj =
        match i % 4 with
        | 0 -> 1.3 :> obj
        | 1 -> "buckle my shoe" :> obj
        | 2 -> System.DateTime.Now :> obj
        | _ -> 12 :> obj
  
    [<Benchmark(Baseline = true)>]  
    member this.FirstHasheq() =
        let mutable x : int = 0
        for i = 0 to this.size-1 do
            x <- x + clojure.lang.Util.hasheq( HasheqTests.getValue(i))
        x

    [<Benchmark>]  
    member this.NextHasheq() =
        let mutable x : int = 0
        for i = 0 to this.size-1 do
            x <- x + Clojure.Numerics.Hashing.hasheq( HasheqTests.getValue(i))
        x
