module Tests

open BenchmarkDotNet.Attributes


type B(v:int ) =
    
    static member val StaticEmptyC = C(0) with get, set

    member val InstanceEmptyC = C(0) with get, set

    member this.V = v

and C(v:int) = 
    member val V = v with get, set



[<Literal>]
let NumIters = 100


type Tests() = 
    
    member val SomeB = B(20) with get, set

    [<GlobalSetup>]
    member this.GlobalSetup() =
        this.SomeB <- B(10)



    [<Benchmark(Baseline=true)>]
    member this.Static_EmptyC() =  
        let mutable i : int =0
        for iter in 0 .. NumIters do
            i <- i + B.StaticEmptyC.V
        i
    
    [<Benchmark>]
    member _.Static_EmptyC_2ndTime() = 
        let mutable i : int =0
        for iter in 0 .. NumIters do
            i <- i + B.StaticEmptyC.V

    
    [<Benchmark>]
    member this.Instance_EmptyC() = 
        let mutable i : int =0
        for iter in 0 .. NumIters do
            i <- i + this.SomeB.InstanceEmptyC.V
        i


    [<Benchmark>]
    member this.Instance_EmptyC_2ndTime() = 
        let mutable i : int =0
        for iter in 0 .. NumIters do
            i <- i + this.SomeB.InstanceEmptyC.V
        i





