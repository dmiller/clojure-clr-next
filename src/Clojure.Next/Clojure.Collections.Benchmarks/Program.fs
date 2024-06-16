open BenchmarkDotNet.Running
open PersistentVectorBenchmarks
open PersistentHashMapBenchmarks
open NumericsBenchmarks


[<EntryPoint>]
let main argv =
    //BenchmarkRunner.Run<PVCons>() |> ignore
    //BenchmarkRunner.Run<PVTransientConj>() |> ignore
    //BenchmarkRunner.Run<PVNth>() |> ignore
    //BenchmarkRunner.Run<PersistentVsTransient>() |> ignore

    //BenchmarkRunner.Run<RTEquiv>() |> ignore
    //BenchmarkRunner.Run<PMCreate>() |> ignore

    //BenchmarkRunner.Run<PHMCons>() |> ignore
    //BenchmarkRunner.Run<PHMTransientConj>() |> ignore
    //BenchmarkRunner.Run<PHMContainsKey>() |> ignore

    //BenchmarkRunner.Run<NumericEquivTests>() |> ignore
    //BenchmarkRunner.Run<NumericConverterTests>() |> ignore

    BenchmarkRunner.Run<CategoryVersusOps>() |> ignore
   
    0 // return an integer exit code