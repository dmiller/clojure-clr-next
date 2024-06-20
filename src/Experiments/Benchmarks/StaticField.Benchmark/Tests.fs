module Tests

open BenchmarkDotNet.Attributes


type A (v:int) = 

    static let letEmptyA = A(0)
    static member val StaticVal = A(0)

    static member Empty = letEmptyA

    member this.V = v



[<Literal>]
let NumIters = 100



type Tests() = 
    

    [<Benchmark(Baseline=true)>]
    member this.StaticVal() =  
        let mutable i : int =0
        for iter in 0 .. NumIters do
            i <- i + A.StaticVal.V
        i
    
    [<Benchmark>]
    member _.StaticVal2() = 
        let mutable i : int =0
        for iter in 0 .. NumIters do
            i <- i + A.StaticVal.V
        i

    
    [<Benchmark>]
    member _.StaticVal3() = 
        let mutable i : int =0
        for iter in 0 .. NumIters do
            i <- i + A.StaticVal.V
        i





