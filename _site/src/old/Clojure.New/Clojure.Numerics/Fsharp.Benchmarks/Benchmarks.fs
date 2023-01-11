module Fsharp.Benchmarks

open System
open BenchmarkDotNet
open BenchmarkDotNet.Attributes

//type Benchmarks () =
//    [<Params(0, 1, 15, 100)>]
//    member val public sleepTime = 0 with get, set

//    // [<GlobalSetup>]
//    // member self.GlobalSetup() =
//    //     printfn "%s" "Global Setup"

//    // [<GlobalCleanup>]
//    // member self.GlobalCleanup() =
//    //     printfn "%s" "Global Cleanup"

//    // [<IterationSetup>]
//    // member self.IterationSetup() =
//    //     printfn "%s" "Iteration Setup"

//    // [<IterationCleanup>]
//    // member self.IterationCleanup() =
//    //     printfn "%s" "Iteration Cleanup"

//    [<Benchmark>]
//    member this.Thread () = System.Threading.Thread.Sleep(this.sleepTime)

//    [<Benchmark>]
//    member this.Task () = System.Threading.Tasks.Task.Delay(this.sleepTime)

//    [<Benchmark>]
//    member this.AsyncToTask () = Async.Sleep(this.sleepTime) |> Async.StartAsTask

//    [<Benchmark>]
//    member this.AsyncToSync () = Async.Sleep(this.sleepTime) |> Async.RunSynchronously


[<AllowNullLiteral>]
type FakeSeq(n1) =
    let numItems = n1

    member x.next() =
        if numItems = 0 then null else FakeSeq(numItems - 1)



type BoundedLength() =

    [<Params(10, 100, 1000)>]
    member val public count = 0 with get, set

    [<Benchmark>]
    member x.IterativeBoundReached() =
        let mutable i = 0
        let mutable c = FakeSeq(x.count)
        let limit = x.count - 2

        while c <> null && i <= limit do
            c <- c.next ()
            i <- i + 1

        i

    [<Benchmark>]
    member x.IterativeBoundNotReached() =
        let mutable i = 0
        let mutable c = FakeSeq(x.count)
        let limit = x.count + 10

        while c <> null && i <= limit do
            c <- c.next ()
            i <- i + 1

        i

    [<Benchmark>]
    member x.RecursiveBoundReached() =
        let limit = x.count - 2

        let rec step (c: FakeSeq) i =
            if c <> null && i <= limit then step (c.next ()) (i + 1) else i

        step (FakeSeq(x.count)) 0

    [<Benchmark>]
    member x.RecursiveBoundNotReached() =
        let limit = x.count + 10

        let rec step (c: FakeSeq) i =
            if c <> null && i <= limit then step (c.next ()) (i + 1) else i

        step (FakeSeq(x.count)) 0
