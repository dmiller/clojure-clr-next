module Tests


open BenchmarkDotNet.Attributes


type Tests() =

    [<Params( 1_000, 1_000_000)>]
    member val size: int = 0 with get, set

    [<Benchmark(Baseline=true)>]   
    member this.InNoStep() = 
        let mutable i : int =0
        for iter in 0 .. this.size do
            i <- i + 17
        i

    [<Benchmark>]   
    member this.ForToIteration() = 
        let mutable i : int =0
        for iter = 0 to this.size-1 do
            i <- i + 17
        i

    [<Benchmark>]   
    member this.ManualIterationStep1() = 
        let mutable i : int =0
        let mutable iter = 0
        while iter < this.size do
            i <- i + 17
            iter <- iter + 1
        i

    [<Benchmark>]   
    member this.InStep1() = 
        let mutable i : int =0
        for iter in 0 .. 1 .. this.size do
            i <- i + 17
        i

    [<Benchmark>]   
    member this.ManualIterationStep2() = 
        let doubleSize = 2*this.size
        let mutable i : int =0
        let mutable iter = 0
        while iter < doubleSize do
            i <- i + 17
            iter <- iter + 2
        i

    [<Benchmark>]   
    member this.InStep2() = 
        let doubleSize = 2*this.size
        let mutable i : int =0
        for iter in 0 .. 2 .. doubleSize do
            i <- i + 17
        i


