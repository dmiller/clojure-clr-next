module TxTester

open System
open System.Threading
open Clojure.Collections
open Clojure.Lib
open Expecto

type TxAction =
    | RefSet of index: int * value: int
    | CommuteIncr of index: int
    | AlterIncr of index: int
    | Wait of index: int
    | Trigger of index: int

type TxExit =
    | NormalExit
    | ExceptionThrown of ex: exn

type TxTest =
    | Throw of exnType: Type
    | Normal
    | Ref of index: int * value: int


type TxScript =
    { Steps: TxAction list
      Tests: TxTest list }


let incrFn = 
    { new AFn() with
        member this.ToString() = "a"
      interface IFn with
        member this.invoke(arg1) = (arg1 :?> int) + 1 :> obj
    }

let getCount(scripts : TxScript list, indexSelect: TxAction -> int) =
    let maxIndex =
        scripts
        |> List.collect (fun s -> s.Steps)
        |> List.map (fun a -> indexSelect a)
        |> List.max

    maxIndex + 1

let getHandleCount(scripts : TxScript list) =
    getCount(scripts, fun a ->
        match a with
        | Wait i -> i
        | Trigger i -> i
        | _ -> -1)

let getRefCount(scripts : TxScript list) =
    getCount(scripts, fun a ->
        match a with
        | RefSet(i, _) -> i
        | CommuteIncr i -> i
        | AlterIncr i -> i
        | _ -> -1)
let createHandles(scripts : TxScript list) =
    Array.init (getHandleCount(scripts)) (fun i -> new ManualResetEvent(false))

let createRefs(scripts : TxScript list) =
    Array.init (getRefCount(scripts)) (fun i -> new Clojure.Lib.Ref(0, null))

let createExecuteScriptAsync(script : TxScript, handles: ManualResetEvent array, refs: Clojure.Lib.Ref array) =
    let txfn = { new AFn() with
                    member this.ToString() = "a"
                 interface IFn with
                    member _.invoke() = 
                        try 
                            script.Steps 
                            |> List.iter (fun step ->
                                                match step with
                                                | RefSet(i, v) -> refs[i].set(v) |> ignore
                                                | CommuteIncr i -> refs[i].commute(incrFn, null) |> ignore
                                                | AlterIncr i -> refs.[i].alter(incrFn, null)  |> ignore
                                                | Wait i -> handles.[i].WaitOne()|> ignore
                                                | Trigger i -> handles.[i].Set()|> ignore)
                            NormalExit
                        with 
                        | ex -> ExceptionThrown(ex)
                    
                }
    async {
        return LockingTransaction.runInTransaction(txfn);
    }

let performTests(script: TxScript, result : TxExit, refs: Clojure.Lib.Ref array) =

    let testExceptionThrown(exType: Type, result: TxExit) =
        let thrownType = 
            match result with
            | ExceptionThrown(ex) -> ex.GetType()
            | _ -> null
        Expect.equal thrownType exType $"""Expected exception of type {exType}, {if isNull thrownType then "but exited normally" else $"but got {thrownType}"} """

    let testNormalExit(result: TxExit) =
        Expect.isTrue (result.IsNormalExit) "Expected normal exit, but exception was thrown"

    let testRefValue(i: int, v: int, refs: Clojure.Lib.Ref array) =
        Expect.equal ((refs[i] :> IDeref).deref()) v "Ref value incorrect"
 
    script.Tests
    |> List.iter (fun test ->
        match test with
        | Throw exType -> testExceptionThrown(exType, result)
        | Normal -> testNormalExit(result)
        | Ref(i, v) -> testRefValue(i, v, refs)
        )

let runTests(scripts : TxScript list, handles : ManualResetEvent array, refs : Clojure.Lib.Ref array) = 
    let results = 
        scripts
        |> List.map (fun s -> createExecuteScriptAsync(s, handles, refs))
        |> Async.Parallel
        |> Async.RunSynchronously

    let typedResults = results |> Array.map (fun r -> r :?> TxExit)

    scripts
    |> List.zip( typedResults |> Array.toList)
    |> List.iter (fun (r, s) -> performTests(s, r, refs))

let execute(scripts : TxScript list) =
    let handles = createHandles(scripts)
    let refs = createRefs(scripts)
    runTests(scripts, handles, refs)
    handles |> Array.iter (fun h -> h.Dispose())
    refs |> Array.iter (fun r -> (r :> IDisposable).Dispose())