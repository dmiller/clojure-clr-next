module Tests

open System
open BenchmarkDotNet.Attributes

type Tests() =



    let NumIters = 1000

    static member IsRefEquals (x:obj) (y : obj) = Object.ReferenceEquals(x, y)
    static member IsPhysEquals (x:obj) (y : obj) = LanguagePrimitives.PhysicalEquality x y
    static member IsEqualSignEquals (x:obj) (y : obj) = x = y



    [<Benchmark>]
    member _.RefEquals() =
        let mutable x : bool = false
        for i = 0 to NumIters do
            x <- Tests.IsRefEquals i i || Tests.IsRefEquals i 0
        x


    [<Benchmark(Baseline=true)>]
    member _.PhysEquals() =
        let mutable x : bool = false
        for i = 0 to NumIters do
            x <- Tests.IsPhysEquals i i || Tests.IsPhysEquals i 0
        x


    [<Benchmark>]
    member _.EqualSignEquals() =
        let mutable x : bool = false
        for i = 0 to NumIters do
            x <- Tests.IsEqualSignEquals i i || Tests.IsEqualSignEquals i 0
        x