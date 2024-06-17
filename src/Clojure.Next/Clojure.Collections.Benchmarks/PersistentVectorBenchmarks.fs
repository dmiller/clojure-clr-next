module PersistentVectorBenchmarks

open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Diagnosers


[<MemoryDiagnoser (* ; HardwareCounters(HardwareCounter.BranchMispredictions, HardwareCounter.BranchInstructions)  *) >]
type PVCons() =

    [<Params(10, 100, 1_000, 10_000, 100_000)>]
    member val size: int = 0 with get, set

    [<Benchmark(Baseline = true)>]
    member this.FirstCons() =
        let mutable pv =
            clojure.lang.PersistentVector.EMPTY :> clojure.lang.IPersistentVector

        for i in 0 .. this.size do
            pv <- pv.cons (i)

        pv

    [<Benchmark>]
    member this.NextCons() =
        let mutable pv =
            Clojure.Collections.PersistentVector.EMPTY :> Clojure.Collections.IPersistentVector

        for i in 0 .. this.size do
            pv <- pv.cons (i)

        pv


[<MemoryDiagnoser>]
type PVTransientConj() =

    [<Params(100, 1_000, 10_000, 100_000)>]
    member val size: int = 0 with get, set


    [<Benchmark(Baseline = true)>]
    member this.FirstTransientConj() =
        let mutable pv = clojure.lang.PersistentVector.EMPTY.asTransient ()

        for i in 0 .. this.size do
            pv <- pv.conj (i)

        pv.persistent ()

    [<Benchmark>]
    member this.NextTransientConj() =
        let mutable pv =
            (Clojure.Collections.PersistentVector.EMPTY :> Clojure.Collections.IEditableCollection)
                .asTransient ()

        for i in 0 .. this.size do
            pv <- pv.conj (i)

        pv.persistent ()


[<MemoryDiagnoser; HardwareCounters(HardwareCounter.BranchMispredictions, HardwareCounter.BranchInstructions)>]
type PVNth() =

    [<Params(10, 100, 1_000, 10_000, 100_000)>]
    member val size: int = 0 with get, set

    member val firstVec: clojure.lang.IPersistentVector = null with get, set
    member val nextVec: Clojure.Collections.IPersistentVector = null with get, set

    [<GlobalSetup>]
    member this.GlobalSetup() =
        let firstR: clojure.lang.ISeq = clojure.lang.LongRange.create (this.size)

        let nextR: Clojure.Collections.ISeq =
            Clojure.Collections.LongRange.create (this.size)

        this.firstVec <- clojure.lang.PersistentVector.create (firstR)
        this.nextVec <- Clojure.Collections.PersistentVector.create (nextR)

        System.Console.WriteLine($"Sizes are {this.firstVec.count ()} and {this.nextVec.count ()}")



    [<Benchmark(Baseline = true)>]
    member this.FirstNth() =
        let pv = this.firstVec :> clojure.lang.Indexed
        let mutable acc: obj = null

        for i in 0 .. (this.size - 1) do
            acc <- pv.nth (i)

        acc

    [<Benchmark>]
    member this.NextNth() =
        let pv = this.nextVec :> Clojure.Collections.Indexed
        let mutable acc: obj = null

        for i in 0 .. (this.size - 1) do
            acc <- pv.nth (i)

        acc


[<MemoryDiagnoser; HardwareCounters(HardwareCounter.BranchMispredictions, HardwareCounter.BranchInstructions)>]
type ArrayCreation() =

    [<Params(10, 100, 1_000, 10_000, 100_000)>]
    member val size: int = 0 with get, set

    member val firstVec: clojure.lang.IPersistentVector = null with get, set
    member val nextVec: Clojure.Collections.IPersistentVector = null with get, set

    [<GlobalSetup>]
    member this.GlobalSetup() =
        let firstR: clojure.lang.ISeq = clojure.lang.LongRange.create (this.size)

        let nextR: Clojure.Collections.ISeq =
            Clojure.Collections.LongRange.create (this.size)

        this.firstVec <- clojure.lang.PersistentVector.create (firstR)
        this.nextVec <- Clojure.Collections.PersistentVector.create (nextR)

        System.Console.WriteLine($"Sizes are {this.firstVec.count ()} and {this.nextVec.count ()}")



    [<Benchmark(Baseline = true)>]
    member this.FirstNth() =
        let pv = this.firstVec :> clojure.lang.Indexed
        let mutable acc: obj = null

        for i in 0 .. (this.size - 1) do
            acc <- pv.nth (i)

        acc

    [<Benchmark>]
    member this.NextNth() =
        let pv = this.nextVec :> Clojure.Collections.Indexed
        let mutable acc: obj = null

        for i in 0 .. (this.size - 1) do
            acc <- pv.nth (i)

        acc

[<MemoryDiagnoser; HardwareCounters(HardwareCounter.BranchMispredictions, HardwareCounter.BranchInstructions)>]
type PersistentVsTransient() = 

    [<Params(10, 100, 1_000, 10_000, 100_000)>]
    member val size: int = 0 with get, set

    [<Benchmark(Baseline=true)>]
    member this.NextTransientConj() =
        let mutable pv =
            (Clojure.Collections.PersistentVector.EMPTY :> Clojure.Collections.IEditableCollection)
                .asTransient ()

        for i in 0 .. this.size do
            pv <- pv.conj (i)

        pv.persistent ()


    [<Benchmark>]
    member this.NextCons() =
        let mutable pv =
            Clojure.Collections.PersistentVector.EMPTY :> Clojure.Collections.IPersistentVector

        for i in 0 .. this.size do
            pv <- pv.cons (i)

        pv