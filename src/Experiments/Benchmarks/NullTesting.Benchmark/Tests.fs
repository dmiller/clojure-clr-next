module Tests

open System
open BenchmarkDotNet.Attributes

type Tests() = 

    static member WithMatch(o: obj) = 
        match o with
        | null -> 1
        | _ -> 0

    static member WithIsNull(o: obj) = 
        if isNull o then 1 else 0

    static member WithEquals(o: obj) = 
        if o = null then 1 else 0

    static member WithReferenceEquals(o: obj) =
        if Object.ReferenceEquals(o, null) then 1 else 0    




    [<Benchmark>]
    member _.WithIsNull() =
        let mutable cnt : int = 0
        for i in 0 .. 1_000_000 do
            cnt <- cnt + Tests.WithIsNull (null)
        cnt

    [<Benchmark>]
    member _.WithEquals() =
        let mutable cnt : int = 0
        for i in 0 .. 1_000_000 do
            cnt <- cnt + Tests.WithEquals (null)
        cnt

    [<Benchmark>]
    member _.WithReferenceEquals() =
        let mutable cnt : int = 0
        for i in 0 .. 1_000_000 do
            cnt <- cnt + Tests.WithReferenceEquals (null)
        cnt


    [<Benchmark(Baseline=true)>]
    member _.WithMatch() =
        let mutable cnt : int = 0
        for i in 0 .. 1_000_000 do
            cnt <- cnt + Tests.WithMatch (null)
        cnt