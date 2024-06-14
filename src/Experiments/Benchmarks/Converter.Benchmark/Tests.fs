module Tests

open BenchmarkDotNet.Attributes
open System
open Converters


let numEntries = 100_000

type EInputType =
| I32 = 0
| I64 = 1
| Dbl = 2
| Str = 3


let getValue(inputType : EInputType) : obj =
    match inputType with
    | EInputType.I32 -> 12
    | EInputType.I64 -> 12L
    | EInputType.Dbl -> 1.2
    | EInputType.Str -> "12"
    | _ -> failwith "Invalid input type"


type TestingConverters() = 

    [<ParamsAllValues>]
    member val inputType: EInputType = EInputType.I32 with get, set

    member val testedVal : obj = null with get, set

    [<GlobalSetup>]
    member this.GlobalSetup() =
        this.testedVal <- getValue this.inputType


    [<Benchmark>]   
    member this.TypeCode() = 
        ////let v = getValue this.inputType
        //for i =  0 to numEntries - 1 do 
        convertToInt64TypeCode this.testedVal |> ignore
        
    [<Benchmark>]   
    member this.CastingAlpha() = 
        //let v = getValue this.inputType
        //for i =  0 to numEntries - 1 do 
        convertToInt64CastingAlpha this.testedVal |> ignore

    [<Benchmark>]   
    member this.CastingNasty() = 
        //let v = getValue this.inputType
        //for i =  0 to numEntries - 1 do 
        convertToInt64CastingNasty this.testedVal |> ignore


    [<Benchmark>]   
    member this.CastingNice() = 
        //let v = getValue this.inputType
        //for i =  0 to numEntries - 1 do 
        convertToInt64CastingNice this.testedVal |> ignore

    [<Benchmark(Baseline=true)>]   
    member this.Direct() = 
        //let v = getValue this.inputType
        //for i =  0 to numEntries - 1 do 
        convertToInt64Directly this.testedVal |> ignore


type TestingCategorizers() = 

    [<ParamsAllValues>]
    member val inputType: EInputType = EInputType.I32 with get, set

    member val testedVal : obj = null with get, set

    [<GlobalSetup>]
    member this.GlobalSetup() =
        this.testedVal <- getValue this.inputType

    [<Benchmark(Baseline=true)>]    
    member this.TypeCode() = 
        //let v = getValue this.inputType
        //for i =  0 to numEntries - 1 do 
        categorizeByTypeCode this.testedVal |> ignore
        
    [<Benchmark>]   
    member this.Type() = 
        //let v = getValue this.inputType
        //for i =  0 to numEntries - 1 do 
        categorizeByType this.testedVal |> ignore
