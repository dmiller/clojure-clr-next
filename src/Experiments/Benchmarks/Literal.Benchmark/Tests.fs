module Tests

open BenchmarkDotNet.Attributes

let c = new VH.ValueHolder()

type Tests() =

    [<Params( 1000, 5000)>]
    member val size: int = 0 with get, set


    [<Benchmark(Baseline=true)>]   
    member this.NoLookup() = 
        let mutable i : int =0
        for iter in 0 .. this.size do
        //for iter in 0 .. 1000 do
            i <- i + 17
        i

    [<Benchmark>]   
    member this.LiteralLookup() = 
        let mutable i : int =0
        for iter in 0 .. this.size do
        //for iter in 0 .. 1000 do
            i <- i + VH.LitConst
        i
              
    [<Benchmark>]   
    member this.StaticValLookup() = 
        let mutable i : int =0
        for iter in 0 .. this.size do
        //for iter in 0 .. 1000 do
            i <- i + VH.ValueHolder.StaticVal

    [<Benchmark>]   
    member this.NonstaticValLookup() = 
        let mutable i : int =0
        for iter in 0 .. this.size do
        //for iter in 0 .. 1000 do
            i <- i + c.NonstaticVal

    [<Benchmark>]   
    member this.GetLetLookup() = 
        let mutable i : int =0
        for iter in 0 .. this.size do
        //for iter in 0 .. 1000 do
            i <- i + c.GetLetVar
