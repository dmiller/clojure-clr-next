open System
open BenchmarkDotNet.Running
open SimpleHashMap.Benchmarks

[<EntryPoint>]
let main argv =
    BenchmarkRunner.Run<Benchmarks>() |> ignore
    0 // return an integer exit code
