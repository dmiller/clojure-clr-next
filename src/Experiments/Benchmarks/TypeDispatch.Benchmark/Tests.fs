module Tests

open BenchmarkDotNet.Attributes
open System.Numerics

type Tests() =

    [<Params( 1_000, 1_000_000 )>]
    member val size: int = 0 with get, set

    member this.GrabType i =
        match i % 4 with
        | 0 -> typeof<string>
        | 1 -> typeof<int>
        | 2 -> typeof<float>
        | 3 -> typeof<BigInteger>
        | _ -> typeof<string>

    [<Benchmark(Baseline=true)>]
    member this.CSharpDirectIsNumericType() =
        let mutable cnt : int = 0
        for i in 0 .. this.size - 1 do
            if TypeDispatch.CSharp.TypeDispatch.IsNumericType (this.GrabType i )
            then cnt <- cnt + 1 
        cnt

    [<Benchmark>]
    member this.FSharpDirectIsNumericType() =
        let mutable cnt : int = 0
        for i in 0 .. this.size - 1 do
            if FsharpTypeDispatch.TypeDispatch.IsNumericType ( this.GrabType i)
            then cnt <- cnt + 1 
        cnt
        
    [<Benchmark>]
    member this.FSharpDirectIsNumericType2() =
        let mutable cnt : int = 0
        for i in 0 .. this.size - 1 do
            if FsharpTypeDispatch.TypeDispatch.IsNumericType2 ( this.GrabType i)
            then cnt <- cnt + 1 
        cnt

    [<Benchmark>]
    member this.FSharpDirectIsNumericType3() =
        let mutable cnt : int = 0
        for i in 0 .. this.size - 1 do
            if FsharpTypeDispatch.TypeDispatch.IsNumericType3 ( this.GrabType i)
            then cnt <- cnt + 1 
        cnt


