open BenchmarkDotNet.Running
open PersistentVectorBenchmarks
open PersistentHashMapBenchmarks
open NumericsBenchmarks


[<EntryPoint>]
let main argv =

    // tests on numerics implementation

    //BenchmarkRunner.Run<isNumericTests>() |> ignore
    //BenchmarkRunner.Run<NumericEquivTests>() |> ignore
    //BenchmarkRunner.Run<NumericConverterTests>() |> ignore
    //BenchmarkRunner.Run<CategoryVersusOps>() |> ignore
    //BenchmarkRunner.Run<HashStringUTests>() |> ignore
    //BenchmarkRunner.Run<HasheqTests>() |> ignore
    BenchmarkRunner.Run<UtilEquivTests>() |> ignore


    // Tests on PersistentVector 

    //BenchmarkRunner.Run<PVCons>() |> ignore
    //BenchmarkRunner.Run<PVTransientConj>() |> ignore
    //BenchmarkRunner.Run<PVNth>() |> ignore
    //BenchmarkRunner.Run<PersistentVsTransient>() |> ignore


    // Tests on PersistentArrayMap

    //BenchmarkRunner.Run<PAMCreateWithCheck>() |> ignore
    //BenchmarkRunner.Run<PAMCreateByAssoc>() |> ignore


    // Tests on PersistentHashMap

    //BenchmarkRunner.Run<PHMAssoc>() |> ignore
    //BenchmarkRunner.Run<PHMTransientAssoc>() |> ignore
    //BenchmarkRunner.Run<PHMContainsKey>() |> ignore
    //BenchmarkRunner.Run<PHMContainsMissingKey>() |> ignore




    0 // return an integer exit code