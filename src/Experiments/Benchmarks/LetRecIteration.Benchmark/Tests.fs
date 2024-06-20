module Tests

open BenchmarkDotNet.Attributes

let letRecIterOption (n:int) = 
    let rec loop (i:int) (acc:int) =
        if i = 0 then 
           if acc % 2 = 0 then Some acc else None
        else loop (i-1) (acc + n)
    loop n 0


let letRecIterValueOption (n:int) = 
    let rec loop (i:int) (acc:int) =
        if i = 0 then 
           if acc % 2 = 0 then ValueSome acc else ValueNone
        else loop (i-1) (acc + n)
    loop n 0

let letRecIterInt (n:int) = 
    let rec loop (i:int) (acc:int) =
        if i = 0 then 
           if acc % 2 = 0 then acc else -1
        else loop (i-1) (acc + n)
    loop n 0


let manualLoopOption (n:int) = 
    let mutable i = n
    let mutable acc = 0
    while i > 0 do
        acc <- acc + n
        i <- i - 1
    if acc % 2 = 0 then Some acc else None

let manualLoopValueOption (n:int) = 
    let mutable i = n
    let mutable acc = 0
    while i > 0 do
        acc <- acc + n
        i <- i - 1
    if acc % 2 = 0 then ValueSome acc else ValueNone


let manualLoopInt (n:int) =
    let mutable i = n
    let mutable acc = 0
    while i > 0 do
        acc <- acc + n
        i <- i - 1
    if acc % 2 = 0 then acc else -1

type Tests() =

    
    [<Params(10,100)>]
    member val size: int = 0 with get, set


    [<Benchmark(Baseline=true)>]
    member this.LetRecIterOption() = 
        let mutable x = 0
        for iter = 0 to this.size do
            x <- match letRecIterOption 10 with
                 | Some k -> x + k 
                 | None -> x
        x


    [<Benchmark>]
    member this.LetRecIterValueOption() =
        let mutable x = 0
        for iter = 0 to this.size do
            x <- match letRecIterValueOption 10 with
                 | ValueSome k -> x + k 
                 | ValueNone -> x
        x

    [<Benchmark>]
    member this.LetRecIterInt() = 
        let mutable x = 0
        for iter = 0 to this.size do
            x <- match letRecIterInt 10 with
                 | -1 -> x
                 | k -> x + k
        x


    [<Benchmark>]
    member this.ManualLoopOption() =
        let mutable x = 0
        for iter = 0 to this.size do
            x <- match manualLoopOption 10 with
                 | Some k -> x + k 
                 | None -> x
        x


    [<Benchmark>]   
    member this.ManualLoopValueOption() =
        let mutable x = 0
        for iter = 0 to this.size do
            x <- match manualLoopValueOption 10 with
                 | ValueSome k -> x + k 
                 | ValueNone -> x
        x


    [<Benchmark>]
    member this.ManualLoopInt() = 
        let mutable x = 0
        for iter = 0 to this.size do
            x <- match manualLoopInt 10 with
                 | -1 -> x
                 | k -> x + k
        x


