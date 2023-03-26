module PersistentHashMapBenchmarks

open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Diagnosers

[<MemoryDiagnoser>]
type PHMCons() =

    [<Params( 8, 9, 16, 17, 32, 33, 48, 64)>]
    member val size: int = 0 with get, set

    [<Benchmark(Baseline = true)>]
    member this.FirstCons() =
        let mutable pv =
            clojure.lang.PersistentHashMap.EMPTY :> clojure.lang.IPersistentMap

        for i in 0 .. this.size do
            pv <- pv.assoc (i,i)

        pv

    [<Benchmark>]
    member this.NextCons() =
        let mutable pv =
            Clojure.Collections.PersistentHashMap.Empty :> Clojure.Collections.IPersistentMap

        for i in 0 .. this.size do
            pv <- pv.assoc (i,i)

        pv


[<MemoryDiagnoser>]
type PHMTransientConj() =

    //[<Params( 15, 16, 17, 18, 19, 24, 32)>]
    [<Params( 10, 20, 50, 100, 1000)>]
    member val size: int = 0 with get, set


    [<Benchmark(Baseline = true)>]
    member this.FirstTransientConj() =
        let mutable pv = clojure.lang.PersistentHashMap.EMPTY.asTransient () :?> clojure.lang.ITransientAssociative

        for i in 0 .. this.size do
            pv <- pv.assoc (i,i)

        pv.persistent ()

    [<Benchmark>]
    member this.NextTransientConj() =
        let mutable pv =
            (Clojure.Collections.PersistentHashMap.Empty :> Clojure.Collections.IEditableCollection) 
                .asTransient () :?> Clojure.Collections.ITransientAssociative

        for i in 0 .. this.size do
            pv <- pv.assoc(i,i)

        pv.persistent ()

    [<Benchmark>]
    member this.AlternateTransientConj() =
        let mutable pv =
            (Clojure.Collections.Alternate.PHashMap.Empty :> Clojure.Collections.IEditableCollection) 
                .asTransient () :?> Clojure.Collections.ITransientAssociative

        for i in 0 .. this.size do
            pv <- pv.assoc(i,i)

        pv.persistent ()



[<MemoryDiagnoser>]
type PHMContainsKey() =

    [<Params(10, 100, 1_000, 10_000, 100_000)>]
    member val size: int = 0 with get, set

    member val firstMap: clojure.lang.IPersistentMap = null with get, set
    member val nextMap: Clojure.Collections.IPersistentMap = null with get, set

    static member createFirst(n) =
        let mutable pv = clojure.lang.PersistentHashMap.EMPTY.asTransient () :?> clojure.lang.ITransientAssociative

        for i in 0 .. n do
            pv <- pv.assoc (i,i)

        pv.persistent ()

    static member createNext(n) =
        let mutable pv =
            (Clojure.Collections.PersistentHashMap.Empty :> Clojure.Collections.IEditableCollection) 
                .asTransient () :?> Clojure.Collections.ITransientAssociative

        for i in 0 .. n do
            pv <- pv.assoc(i,i)

        pv.persistent ()

        
    [<GlobalSetup>]
    member this.GlobalSetup() =

        this.firstMap <- PHMContainsKey.createFirst(this.size) :?> clojure.lang.IPersistentMap
        this.nextMap <- PHMContainsKey.createNext(this.size) :?> Clojure.Collections.IPersistentMap

        System.Console.WriteLine($"Sizes are {this.firstMap.count ()} and {this.nextMap.count ()}")


    [<Benchmark(Baseline = true)>]
    member this.FirstNth() =
        let pv = this.firstMap 
        let mutable acc: obj = null

        for i in 0 .. (2*this.size - 1) do
            acc <- pv.containsKey (i)

        acc

    [<Benchmark>]
    member this.NextNth() =
        let pv = this.nextMap
        let mutable acc: obj = null

        for i in 0 .. (2*this.size - 1) do
            acc <- pv.containsKey (i)

        acc




