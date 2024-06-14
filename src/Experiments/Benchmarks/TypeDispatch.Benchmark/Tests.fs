module Tests

open BenchmarkDotNet.Attributes
open System.Numerics
open System

type EInputType =
| I32 = 0
| I64 = 1
| Dbl = 2
| Big = 3
| Str = 4


let getType(inputType : EInputType) : Type =
    match inputType with
    | EInputType.I32 -> typeof<int32>
    | EInputType.I64 -> typeof<int64>
    | EInputType.Dbl -> typeof<double>
    | EInputType.Big -> typeof<BigInteger>
    | EInputType.Str -> typeof<string>
    | _ -> failwith "Invalid input type"

type Tests() =

    [<ParamsAllValues>]
    member val inputType: EInputType = EInputType.I32 with get, set

    member val testedType : Type = null with get, set

    [<GlobalSetup>]
    member this.GlobalSetup() =
        this.testedType <- getType this.inputType


    [<Benchmark(Baseline=true)>]
    member this.CSharpDirectIsNumericType() =  TypeDispatch.CSharp.TypeDispatch.IsNumericType this.testedType

    [<Benchmark>]
    member this.FSharpDirectIsNumericType() = FsharpTypeDispatch.TypeDispatch.IsNumericType this.testedType
        
    [<Benchmark>]
    member this.FSharpDirectIsNumericType2() = FsharpTypeDispatch.TypeDispatch.IsNumericType2 this.testedType

    [<Benchmark>]
    member this.FSharpDirectIsNumericType3() = FsharpTypeDispatch.TypeDispatch.IsNumericType3 this.testedType


type Tests2() =

    [<Benchmark(Baseline=true)>]
    member this.CSharpDirectIsNumericType() =  
        TypeDispatch.CSharp.TypeDispatch.IsNumericType typeof<int32> &&
        TypeDispatch.CSharp.TypeDispatch.IsNumericType typeof<int64> &&
        TypeDispatch.CSharp.TypeDispatch.IsNumericType typeof<double> &&
        TypeDispatch.CSharp.TypeDispatch.IsNumericType typeof<BigInteger> &&
        TypeDispatch.CSharp.TypeDispatch.IsNumericType typeof<string>

    [<Benchmark>]
    member this.FSharpDirectIsNumericType() = 
        FsharpTypeDispatch.TypeDispatch.IsNumericType typeof<int32> &&
        FsharpTypeDispatch.TypeDispatch.IsNumericType typeof<int64> &&
        FsharpTypeDispatch.TypeDispatch.IsNumericType typeof<double> &&
        FsharpTypeDispatch.TypeDispatch.IsNumericType typeof<BigInteger> &&
        FsharpTypeDispatch.TypeDispatch.IsNumericType typeof<string>
        
    [<Benchmark>]
    member this.FSharpDirectIsNumericType2() = 
        FsharpTypeDispatch.TypeDispatch.IsNumericType2 typeof<int32> &&
        FsharpTypeDispatch.TypeDispatch.IsNumericType2 typeof<int64> &&
        FsharpTypeDispatch.TypeDispatch.IsNumericType2 typeof<double> &&
        FsharpTypeDispatch.TypeDispatch.IsNumericType2 typeof<BigInteger> &&
        FsharpTypeDispatch.TypeDispatch.IsNumericType2 typeof<string>

    [<Benchmark>]
    member this.FSharpDirectIsNumericType3() = 
        FsharpTypeDispatch.TypeDispatch.IsNumericType3 typeof<int32> &&
        FsharpTypeDispatch.TypeDispatch.IsNumericType3 typeof<int64> &&
        FsharpTypeDispatch.TypeDispatch.IsNumericType3 typeof<double> &&
        FsharpTypeDispatch.TypeDispatch.IsNumericType3 typeof<BigInteger> &&
        FsharpTypeDispatch.TypeDispatch.IsNumericType3 typeof<string>

