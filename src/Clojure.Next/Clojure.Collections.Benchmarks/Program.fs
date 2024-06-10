open BenchmarkDotNet.Running
open PersistentVectorBenchmarks
open PersistentHashMapBenchmarks


[<EntryPoint>]
let main argv =
    //BenchmarkRunner.Run<PVCons>() |> ignore
    //BenchmarkRunner.Run<PVTransientConj>() |> ignore
    //BenchmarkRunner.Run<PVNth>() |> ignore
    //BenchmarkRunner.Run<PersistentVsTransient>() |> ignore

    BenchmarkRunner.Run<RTEquiv>() |> ignore
    //BenchmarkRunner.Run<PMCreate>() |> ignore

    //BenchmarkRunner.Run<PHMCons>() |> ignore
    //BenchmarkRunner.Run<PHMTransientConj>() |> ignore
    //BenchmarkRunner.Run<PHMContainsKey>() |> ignore

   
    0 // return an integer exit code