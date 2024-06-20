module Tests


open BenchmarkDotNet.Attributes
open ArrayCreation
open ArrayCreation.CSharp



let arrayToClone : obj array = Array.zeroCreate 32

[<MemoryDiagnoser>]
type Tests() =

    static member val arrayToClone = Array.zeroCreate 32 with get

    [<Benchmark(Baseline=true)>]
    member _.CSharp() = CreateArrayLib.CreateArray(32)
 
    [<Benchmark>]
    member _.CSharpFixed() = CreateArrayLib.CreateArrayFixed()
        
    [<Benchmark>]
    member _.FSharpZeroCreate() = ZeroCreateArray(32)

    [<Benchmark>]
    member _.FSharpZeroCreateFixedDirect() : obj array = Array.zeroCreate 32

    [<Benchmark>]
    member _.FSharpCreate() = CreateArray(32)    

    //[<Benchmark>]
    //member _.FSharpCloneArray() = CloneArray(Tests.arrayToClone)

    //[<Benchmark>]
    //member _.FSharpArrayClone() = ArrayClone(Tests.arrayToClone)

    //[<Benchmark>]
    //member _.FSharpSystemCreate() = SystemArrayCreateInstance(32)

