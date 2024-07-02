module PersistentHashMapBenchmarks

open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Diagnosers
open System



[<MemoryDiagnoser ;HardwareCounters(HardwareCounter.BranchMispredictions,HardwareCounter.BranchInstructions,HardwareCounter.CacheMisses)  >]
type UtilEquivTests() =

    [<Params(12)>]
    member val size: int = 0 with get, set

    static member getValue (i : int) : obj =
        match i % 6 with
        | 0 -> 1.3 :> obj
        | 1 -> "characters" :> obj
        | 2 -> "different characters" :> obj
        | 3 -> 12L :> obj
        | 4 -> DateTime.Now
        | _ -> 12 :> obj


    [<DefaultValue>]
    val mutable items : obj[]

    [<GlobalSetup>]
    member this.GlobalSetup() =
        this.items <- Array.zeroCreate (this.size)
        for i in 0 .. this.size - 1 do
            this.items.[i] <- i


    [<Benchmark(Baseline = true)>]
    member this.FirstEquiv() =
        let mutable cnt : int = 0
        for i in 0 .. this.size - 1 do
            for j in 0 .. this.size - 1 do
                if clojure.lang.Util.equiv (this.items.[i], this.items.[j]) 
                then cnt <- cnt + 1
        cnt

    [<Benchmark>]
    member this.NextEquiv() =
        let mutable cnt : int = 0
        for i in 0 .. this.size - 1 do
            for j in 0 .. this.size - 1 do
                if Clojure.Collections.Util.equiv (this.items.[i], this.items.[j]) 
                then cnt <- cnt + 1
        cnt



[<MemoryDiagnoser>]
type IsNumericTests() =

    [<Params(10)>]
    member val size: int = 0 with get, set

    [<DefaultValue>]
    val mutable items : obj[]

    [<GlobalSetup>]
    member this.GlobalSetup() =
        this.items <- Array.zeroCreate (this.size*2)
        for i in 0 .. this.size - 1 do
            this.items.[2*i] <- i
            this.items.[2*i + 1] <- i

    static member private IsNumericType2(t: Type) =
        match Type.GetTypeCode(t) with
        | TypeCode.SByte
        | TypeCode.Byte
        | TypeCode.Int16
        | TypeCode.Int32
        | TypeCode.Int64
        | TypeCode.Double
        | TypeCode.Single
        | TypeCode.UInt16
        | TypeCode.UInt32
        | TypeCode.UInt64 -> true
        | _ -> System.Type.op_Equality(t, typeof<Clojure.Numerics.BigInt>) 
                || System.Type.op_Equality(t, typeof<System.Numerics.BigInteger>)
                || System.Type.op_Equality(t, typeof<Clojure.BigArith.BigDecimal>)
                || System.Type.op_Equality(t, typeof<Clojure.Numerics.Ratio>)


    static member IsNumeric2(o: obj) =
        match o with
        | null -> false
        | _ -> IsNumericTests.IsNumericType2(o.GetType())

    
    [<Benchmark(Baseline = true)>]
    member this.FirstIsNumeric() =
        let mutable cnt : int = 0
        for i in 0 .. this.size - 1 do
            if clojure.lang.Util.IsNumeric (this.items.[2*i]) 
            then cnt <- cnt + 1 
        cnt

    [<Benchmark>]
    member this.NextIsNumeric() =
        let mutable cnt : int = 0
        for i in 0 .. this.size - 1 do
            if Clojure.Numerics.Numbers.IsNumeric (this.items.[2*i]) 
            then cnt <- cnt + 1 
        cnt

    [<Benchmark>]
    member this.MyIsNumeric() =
        let mutable cnt : int = 0
        for i in 0 .. this.size - 1 do
            if IsNumericTests.IsNumeric2 (this.items.[2*i]) 
            then cnt <- cnt + 1 
        cnt



[<MemoryDiagnoser>]
type PAMCreateWithCheck() = 

    [<Params( 0, 1, 2, 3, 4, 6, 8, 12, 16)>]
    member val size: int = 0 with get, set

    [<DefaultValue>]
    val mutable items : obj[]

    [<GlobalSetup>]
    member this.GlobalSetup() =
        //Clojure.Numerics.Initializer.init() |> ignore
        this.items <- Array.zeroCreate (this.size*2)
        for i in 0 .. this.size - 1 do
            this.items.[2*i] <- i
            this.items.[2*i + 1] <- i

    [<Benchmark(Baseline = true)>]
    member this.FirstCreatePAMCheck() =
        let mutable pv =
            clojure.lang.PersistentArrayMap.createWithCheck  this.items :> clojure.lang.IPersistentMap
        pv  
   
    [<Benchmark>]
    member this.NextCreatePAMCheck() =
        let mutable pv =
            Clojure.Collections.PersistentArrayMap.createWithCheck  this.items :> Clojure.Collections.IPersistentMap
        pv  

 [<MemoryDiagnoser>]
type PAMCreateByAssoc() = 

    [<Params( 0, 1, 2, 3, 4, 6, 8, 12, 16)>]
    member val size: int = 0 with get, set

    [<DefaultValue>]
    val mutable items : obj[]

    [<GlobalSetup>]
    member this.GlobalSetup() =
        //Clojure.Numerics.Initializer.init() |> ignore
        this.items <- Array.zeroCreate (this.size*2)
        for i in 0 .. this.size - 1 do
            this.items.[2*i] <- i
            this.items.[2*i + 1] <- i

    [<Benchmark(Baseline = true)>]
    member this.FirstCreatePAMAssoc() =
        let mutable pv =
            clojure.lang.PersistentArrayMap.createAsIfByAssoc  this.items :> clojure.lang.IPersistentMap
        pv
  
    
    [<Benchmark>]
    member this.NextCreatePAMAssoc() =
        let mutable pv =
            Clojure.Collections.PersistentArrayMap.createAsIfByAssoc  this.items :> Clojure.Collections.IPersistentMap
        pv
   
    
[<MemoryDiagnoser ;HardwareCounters(HardwareCounter.BranchMispredictions,HardwareCounter.BranchInstructions,HardwareCounter.CacheMisses)  >]
type PHMAssoc() =

    [<Params( 32, 100, 500, 1000)>]
    member val size: int = 0 with get, set

    [<Benchmark(Baseline = true)>]
    member this.FirstAssoc() =
        let mutable pv =
            clojure.lang.PersistentHashMap.EMPTY :> clojure.lang.IPersistentMap

        for i in 0 .. this.size do
            //pv <- pv.assoc (i,i)
            pv <- pv.assoc ("a"+i.ToString(),i)

        pv

    [<Benchmark>]
    member this.NextAssoc() =
        let mutable pv =
            Clojure.Collections.PersistentHashMap.Empty :> Clojure.Collections.IPersistentMap

        for i in 0 .. this.size do
            //pv <- pv.assoc (i,i)
            pv <- pv.assoc ("a"+i.ToString(),i)

        pv

    //[<Benchmark>]
    //member this.NextConsAlt() =
    //    let mutable pv =
    //        Clojure.Collections.Alternate.PHashMap.Empty :> Clojure.Collections.IPersistentMap

    //    for i in 0 .. this.size do
    //        pv <- pv.assoc (i,i)
            
    //    pv


    //[<Benchmark>]
    //member this.NextConsAlt2() =
    //    let mutable pv =
    //        Clojure.Collections.Alternate2.PHashMap.Empty :> Clojure.Collections.IPersistentMap

    //    for i in 0 .. this.size do
    //        pv <- pv.assoc (i,i)
            
    //    pv


[<MemoryDiagnoser; HardwareCounters(HardwareCounter.BranchMispredictions,HardwareCounter.BranchInstructions,HardwareCounter.CacheMisses)>]
type PHMTransientAssoc() =

    //[<Params( 15, 16, 17, 18, 19, 24, 32)>]
    //[<Params( 5, 10,15, 16, 17, 18, 19, 24, 32, 50, 100, 500)>]
    [<Params( 500, 100, 50, 32)>]
    member val size: int = 0 with get, set

    
    //[<GlobalSetup>]
    //member this.GlobalSetup() =
    //    Clojure.Numerics.Initializer.init() |> ignore


    [<Benchmark(Baseline = true)>]
    member this.FirstTransientAssoc() =
        let mutable pv = clojure.lang.PersistentHashMap.EMPTY.asTransient () :?> clojure.lang.ITransientAssociative

        for i in 0 .. this.size do
            //pv <- pv.assoc (i,i)
            pv <- pv.assoc ("a"+i.ToString(),i)

        pv.persistent ()

    [<Benchmark>]
    member this.NextTransientAssoc() =
        let mutable pv =
            (Clojure.Collections.PersistentHashMap.Empty :> Clojure.Collections.IEditableCollection) 
                .asTransient () :?> Clojure.Collections.ITransientAssociative

        for i in 0 .. this.size do
            //pv <- pv.assoc (i,i)
            pv <- pv.assoc ("a"+i.ToString(),i)

        pv.persistent ()


[<MemoryDiagnoser ;HardwareCounters(HardwareCounter.BranchMispredictions,HardwareCounter.BranchInstructions,HardwareCounter.CacheMisses)  >]
type PHMContainsKey() =

    [<Params(10, 100, 1_000, 10_000, 100_000)>]
    member val size: int = 0 with get, set

    member val firstMap: clojure.lang.IPersistentMap = null with get, set
    member val nextMap: Clojure.Collections.IPersistentMap = null with get, set

    static member createFirst(n) =
        let mutable pv = clojure.lang.PersistentHashMap.EMPTY.asTransient () :?> clojure.lang.ITransientAssociative

        for i in 0 .. n do
            //pv <- pv.assoc (i,i)
            pv <- pv.assoc ("a"+i.ToString(),i)

        pv.persistent ()

    static member createNext(n) =
        let mutable pv =
            (Clojure.Collections.PersistentHashMap.Empty :> Clojure.Collections.IEditableCollection) 
                .asTransient () :?> Clojure.Collections.ITransientAssociative

        for i in 0 .. n do
            //pv <- pv.assoc (i,i)
            pv <- pv.assoc ("a"+i.ToString(),i)

        pv.persistent ()

        
    [<GlobalSetup>]
    member this.GlobalSetup() =

        this.firstMap <- PHMContainsKey.createFirst(this.size) :?> clojure.lang.IPersistentMap
        this.nextMap <- PHMContainsKey.createNext(this.size) :?> Clojure.Collections.IPersistentMap

        System.Console.WriteLine($"Sizes are {this.firstMap.count ()} and {this.nextMap.count ()}")


    [<Benchmark(Baseline = true)>]
    member this.FirstContainsKey() =
        let pv = this.firstMap 
        let mutable acc: obj = null

        for i in 0 .. (2*this.size - 1) do
            //acc <- pv.containsKey (i)
            acc <- pv.containsKey ("a"+i.ToString())


        acc

    [<Benchmark>]
    member this.NextContainsKey() =
        let pv = this.nextMap
        let mutable acc: obj = null

        for i in 0 .. (2*this.size - 1) do
            //acc <- pv.containsKey (i)
            acc <- pv.containsKey ("a"+i.ToString())

        acc


[<MemoryDiagnoser ;HardwareCounters(HardwareCounter.BranchMispredictions,HardwareCounter.BranchInstructions,HardwareCounter.CacheMisses)   >]
type PHMContainsMissingKey() =

    [<Params(10, 100, 100_000)>]
    member val size: int = 0 with get, set

    member val firstMap: clojure.lang.IPersistentMap = null with get, set
    member val nextMap: Clojure.Collections.IPersistentMap = null with get, set

    static member createFirst(n) =
        let mutable pv = clojure.lang.PersistentHashMap.EMPTY.asTransient () :?> clojure.lang.ITransientAssociative

        for i in 0 .. n do
            //pv <- pv.assoc (i,i)
            pv <- pv.assoc ("a"+i.ToString(),i)

        pv.persistent ()

    static member createNext(n) =
        let mutable pv =
            (Clojure.Collections.PersistentHashMap.Empty :> Clojure.Collections.IEditableCollection) 
                .asTransient () :?> Clojure.Collections.ITransientAssociative

        for i in 0 .. n do
            //pv <- pv.assoc (i,i)
            pv <- pv.assoc ("a"+i.ToString(),i)

        pv.persistent ()

        
    [<GlobalSetup>]
    member this.GlobalSetup() =

        this.firstMap <- PHMContainsKey.createFirst(this.size) :?> clojure.lang.IPersistentMap
        this.nextMap <- PHMContainsKey.createNext(this.size) :?> Clojure.Collections.IPersistentMap

        System.Console.WriteLine($"Sizes are {this.firstMap.count ()} and {this.nextMap.count ()}")


    [<Benchmark(Baseline = true)>]
    member this.FirstContainsKeyMissing() =
        let pv = this.firstMap 
        let mutable acc: obj = null

        for i in 0 .. (2*this.size - 1) do
            //acc <- pv.containsKey(-1000)
            acc <- pv.containsKey ("Missing")


        acc

    [<Benchmark>]
    member this.NextContainsKeyMissing() =
        let pv = this.nextMap
        let mutable acc: obj = null

        for i in 0 .. (2*this.size - 1) do
            //acc <- pv.containsKey(-1000)
            acc <- pv.containsKey ("Missing")

        acc



