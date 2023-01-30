module Tests

open BenchmarkDotNet.Attributes
open Modding

type Tests() =


    let NumIters = 100_000
    let NumIters64 = 100_000L

    [<Benchmark(Baseline=true)>]
    member _.ModOp() =
        let mutable x : int = 0
        for i = 0 to NumIters do
            x <- Modders.doMod(i)
        x

    [<Benchmark>]
    member _.DivOp() =
        let mutable x : int = 0
        for i = 0 to NumIters do
            x <- Modders.doDiv(i)
        x
            
    [<Benchmark>]
    member _.BitShift() =
        let mutable x : int = 0
        for i = 0 to NumIters do
            x <- Modders.doShift(i)
        x

    [<Benchmark>]
    member _.ModOp64() =
        let mutable x : int64 = 0
        for i = 0 to NumIters do
            x <- Modders.doMod64(i)
        x

    [<Benchmark>]
    member _.DivOp64() =
        let mutable x : int64 = 0
        for i = 0 to NumIters do
            x <- Modders.doDiv64(i)
        x
            
    [<Benchmark>]
    member _.BitShift64() =
        let mutable x : int64 = 0
        for i = 0 to NumIters do
            x <- Modders.doShift64(i)
        x
