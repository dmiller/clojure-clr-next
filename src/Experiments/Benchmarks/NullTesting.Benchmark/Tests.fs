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

    [<Benchmark(Baseline=true)>]
    member _.NoTestBaseline() =
        let mutable cnt : int = 0
        for i in 0 .. 1_000 do
            cnt <- cnt + (if i % 2 = 0 then 12 else 2)
        cnt

    [<Benchmark>]
    member _.WithIsNull() =
        let mutable cnt : int = 0
        for i in 0 .. 1_000 do
            cnt <- cnt + Tests.WithIsNull (if i%2 = 0 then null else "abc" )
        cnt

    [<Benchmark>]
    member _.WithEquals() =
        let mutable cnt : int = 0
        for i in 0 .. 1_000 do
            cnt <- cnt + Tests.WithEquals (if i%2 = 0 then null else "abc" )
        cnt

    [<Benchmark>]
    member _.WithReferenceEquals() =
        let mutable cnt : int = 0
        for i in 0 .. 1_000 do
            cnt <- cnt + Tests.WithReferenceEquals (if i%2 = 0 then null else "abc" )
        cnt


    [<Benchmark>]
    member _.WithMatch() =
        let mutable cnt : int = 0
        for i in 0 .. 1_000 do
            cnt <- cnt + Tests.WithMatch (if i%2 = 0 then null else "abc" )
        cnt