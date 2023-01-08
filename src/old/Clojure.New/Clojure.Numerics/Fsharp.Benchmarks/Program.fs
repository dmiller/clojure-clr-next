open System
open BenchmarkDotNet.Running
open Fsharp.Benchmarks

[<EntryPoint>]
let main argv =
    BenchmarkRunner.Run<BoundedLength>() |> ignore
    0 // return an integer exit code
