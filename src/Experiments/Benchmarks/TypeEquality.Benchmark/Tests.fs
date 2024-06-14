module Tests

open BenchmarkDotNet.Attributes

type Tests() = 

    [<Params( 1_000, 1_000_000 )>]
    member val size: int = 0 with get, set

    member this.GrabType i =
        match i % 4 with
        | 0 -> "aaa".GetType()
        | 1 -> (7).GetType()
        | 2 -> (7.4f).GetType()
        | _ -> "aaa".GetType()



    [<Benchmark(Baseline=true)>]
    member this.CSharpEquals() =
        let mutable cnt : int = 0
        for i in 0 .. 1_000_000 do
            if TypeDispatch.CSharp.TypeDispatch.HasSpecialType (this.GrabType i )
            then cnt <- cnt + 1 
        cnt

    [<Benchmark>]
    member this.FSharpEquals() =
        let mutable cnt : int = 0
        for i in 0 .. 1_000_000 do
            if TypeEquality.HasSpecialTypeEquals (this.GrabType i )
            then cnt <- cnt + 1 
        cnt


    [<Benchmark>]
    member this.FSharpEquals2() =
        let mutable cnt : int = 0
        for i in 0 .. 1_000_000 do
            if TypeEquality.HasSpecialTypeEquals2 (this.GrabType i )
            then cnt <- cnt + 1 
        cnt

    [<Benchmark>]
    member this.FSharpEqualsOp() =
        let mutable cnt : int = 0
        for i in 0 .. 1_000_000 do
            if TypeEquality.HasSpecialTypeEqualsOp (this.GrabType i )
            then cnt <- cnt + 1 
        cnt


    [<Benchmark>]
    member this.FSharpRefEquals() =
        let mutable cnt : int = 0
        for i in 0 .. 1_000_000 do
            if TypeEquality.HasSpecialTypeRefEquals (this.GrabType i )
            then cnt <- cnt + 1 
        cnt

   
    member this.GrabObject i : obj =
        match i % 4 with
        | 0 -> "aaa"
        | 1 -> 7
        | 2 -> 7.4
        | _ -> "aaa"


    [<Benchmark>]
    member this.CSharpTestTypeObj() =
        let mutable cnt : int = 0
        for i in 0 .. 1_000_000 do
            if TypeDispatch.CSharp.TypeDispatch.TestObjectType (this.GrabObject i )
            then cnt <- cnt + 1 
        cnt

    [<Benchmark>]
    member this.FSharpTestTypeObjByCast() =
        let mutable cnt : int = 0
        for i in 0 .. 1_000_000 do
            if TypeEquality.TestObjTypeByCast (this.GrabObject i )
            then cnt <- cnt + 1 
        cnt

    [<Benchmark>]
    member this.FSharpTestTypeObjByType() =
        let mutable cnt : int = 0
        for i in 0 .. 1_000_000 do
            if TypeEquality.TestObjTypeByType (this.GrabObject i )
            then cnt <- cnt + 1 
        cnt


    [<Benchmark>]
    member this.FSharpTestTypeObjByInstanceOf() =
        let mutable cnt : int = 0
        for i in 0 .. 1_000_000 do
            if TypeEquality.TestObjByInstanceOfCheck (this.GrabObject i )
            then cnt <- cnt + 1 
        cnt


    [<Benchmark>]
    member this.FSharpTestTypeObjByTypeMatch() =
        let mutable cnt : int = 0
        for i in 0 .. 1_000_000 do
            if TypeEquality.TestObjTypeByTypeMatch (this.GrabObject i )
            then cnt <- cnt + 1 
        cnt




