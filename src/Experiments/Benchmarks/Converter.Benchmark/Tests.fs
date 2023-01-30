module Tests

open BenchmarkDotNet.Attributes
open System
open Converters


let numEntries = 100_000

let generateRandomEntry (r:Random) : obj =
    match r.NextInt64(4) with
    | 0L -> 1.3 :> obj
    | 1L -> "12" :> obj
    | 2L -> 12L :> obj
    | _ -> 12 :> obj

let rnd = Random()

let data : obj array = [| for i in 1 .. numEntries -> generateRandomEntry(rnd) |]

[<GlobalSetup>]
let globalSetup() = Console.WriteLine($"data has {data.Length}")


type TestingConverters() = 

    [<Benchmark>]   
    member _.TypeCode() = 
        for i in 0 .. data.Length - 1 do 
            convertToIntTypeCode data[i] |> ignore
        
    [<Benchmark>]   
    member _.CastingAlpha() = 
        for i in 0 .. data.Length - 1 do 
            convertToIntCastingAlpha data[i] |> ignore

    [<Benchmark>]   
    member _.CastingNasty() = 
        for i in 0 .. data.Length - 1 do 
            convertToIntCastingNasty data[i] |> ignore


    [<Benchmark>]   
    member _.CastingNice() = 
        for i in 0 .. data.Length - 1 do 
            convertToIntCastingNice data[i] |> ignore

    [<Benchmark(Baseline=true)>]   
    member _.Direct() = 
        for i in 0 .. data.Length - 1 do 
            convertToIntDirectly data[i] |> ignore


type TestingCategorizers() = 

    [<Benchmark(Baseline=true)>]    
    member _.TypeCode() = 
        for i in 0 .. data.Length - 1 do 
            categorizeByTypeCode data[i] |> ignore
        
    [<Benchmark>]   
    member _.Type() = 
        for i in 0 .. data.Length - 1 do 
            categorizeByType data[i] |> ignore
