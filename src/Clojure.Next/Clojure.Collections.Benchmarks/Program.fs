open BenchmarkDotNet.Running
open PersistentVectorBenchmarks
open PersistentHashMapBenchmarks
open NumericsBenchmarks
open BigDecimalBenchmarks
open System.Numerics


[<EntryPoint>]
let main argv =

    // tests on numerics implementation

    //BenchmarkRunner.Run<isNumericTests>() |> ignore
    //BenchmarkRunner.Run<NumericEquivTests>() |> ignore
    //BenchmarkRunner.Run<NumericConverterTests>() |> ignore
    //BenchmarkRunner.Run<CategoryVersusOps>() |> ignore
    //BenchmarkRunner.Run<HashStringUTests>() |> ignore
    //BenchmarkRunner.Run<HasheqTests>() |> ignore
    //BenchmarkRunner.Run<UtilEquivTests>() |> ignore

    BenchmarkRunner.Run<BigDecimalBenmark>() |> ignore

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


    //let mutable biSN = BigInteger.One
    //let mutable biCL = clojure.lang.BigInteger.One

    //for i = 1 to 100 do
    //    biSN <- biSN * BigInteger(10)
    //    biCL <- biCL.Multiply(clojure.lang.BigInteger.Ten)

    //    let precSNUA = getBIPrecisionUArray biSN
    //    let precSNS = getBIPrecisionString  biSN
    //    let precCL = biCL.Precision

    //    if precSNUA <> precCL  || precSNS <> precCL then
    //        printfn "Precision mismatch: %A %A %A %A" i precSNUA precSNS precCL

        
        


    0 // return an integer exit code