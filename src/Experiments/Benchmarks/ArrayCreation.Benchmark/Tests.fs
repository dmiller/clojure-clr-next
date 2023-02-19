module Tests


open BenchmarkDotNet.Attributes
open ArrayCreation
open ArrayCreation.CSharp


[<MemoryDiagnoser>]
type Tests() =


    let NumIters = 10_000
    let NumIters64 = 100_000L

    [<Benchmark(Baseline=true)>]
    member _.CSharp() =
        let mutable x : obj array = null
        for i = 0 to NumIters do
            x <- CreateArrayLib.CreateArray(32)
        x

    [<Benchmark>]
    member _.FSharpZeroCreate() =
        let mutable x : obj array = null
        for i = 0 to NumIters do
            x <- ZeroCreateArray(32)
        x

    [<Benchmark>]
    member _.CSharpFixed() =
        let mutable x : obj array = null
        for i = 0 to NumIters do
            x <- CreateArrayLib.CreateArrayFixed()
        x

    [<Benchmark>]
    member _.FSharpZeroCreateFixed() =
        let mutable x : obj array = null
        for i = 0 to NumIters do
            x <- ZeroCreateArrayFixed()
        x

    [<Benchmark>]
    member _.FSharpZeroCreateFixedDirect() =
        let mutable x : obj array = null
        for i = 0 to NumIters do
            x <- Array.zeroCreate 32
        x




    //[<Benchmark>]
    //member _.FSharpCreate() =
    //    let mutable x : obj array = null
    //    for i = 0 to NumIters do
    //        x <- CreateArray(32)
    //    x

    //[<Benchmark>]
    //member _.FSharpCloneArray() =
    //    let a = Array.zeroCreate 32
    //    let mutable x : obj array = null
    //    for i = 0 to NumIters do
    //        x <- CloneArray(a)
    //    x

    //[<Benchmark>]
    //member _.FSharpArrayClone() =
    //    let a = Array.zeroCreate 32
    //    let mutable x : obj array = null
    //    for i = 0 to NumIters do
    //        x <- ArrayClone(a)
    //    x

    //[<Benchmark>]
    //member _.FSharpSystemCreate() =
    //    let mutable x : obj array = null
    //    for i = 0 to NumIters do
    //        x <- SystemArrayCreateInstance(32)
    //    x

    //[<Benchmark>]
    //member _.FSharpNaked() =
    //    let mutable x : obj array = null
    //    for i = 0 to NumIters do
    //        x <- Naked(32)
    //    x