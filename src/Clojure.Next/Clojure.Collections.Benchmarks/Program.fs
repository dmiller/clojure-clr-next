﻿open BenchmarkDotNet.Running
open PersistentVectorBenchmarks


[<EntryPoint>]
let main argv =
    //BenchmarkRunner.Run<PVCons>() |> ignore
    //BenchmarkRunner.Run<PVTransientConj>() |> ignore
    //BenchmarkRunner.Run<PVNth>() |> ignore
    BenchmarkRunner.Run<PersistentVsTransient>() |> ignore

   
    0 // return an integer exit code