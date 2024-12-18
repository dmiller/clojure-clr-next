module TxTester

open System
open System.Threading
open Clojure.Collections
open Clojure.Lib
open Expecto
open System.Diagnostics

type TxAction =
    | RefSet of index: int * value: int
    | CommuteIncr of index: int
    | AlterIncr of index: int
    | Wait of index: int
    | Trigger of index: int
    | SleepMilliseconds of int

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


let writeDebugMsg(id: int, stepNum: int, msg: string) =
    Debug.WriteLine($"Script {id}, Step {stepNum}: {msg}")
    Debug.Flush()

let createExecuteScriptAsync(id: int, script : TxScript, handles: ManualResetEvent array, refs: Clojure.Lib.Ref array) =
    let txfn = { new AFn() with
                    member this.ToString() = "a"
                 interface IFn with
                    member _.invoke() = 
                        script.Steps 
                        |> List.iteri (fun stepNum step ->
                            match step with
                            | RefSet(i, v) -> writeDebugMsg(id, stepNum, $"ref-set {i} = {v}");  refs[i].set(v) |> ignore
                            | CommuteIncr i -> writeDebugMsg(id, stepNum, $"commute {i}"); refs[i].commute(incrFn, null) |> ignore
                            | AlterIncr i -> writeDebugMsg(id, stepNum, $"alter {i}"); refs.[i].alter(incrFn, null)  |> ignore
                            | Wait i -> writeDebugMsg(id, stepNum, $"wait {i}");  handles.[i].WaitOne() |> ignore; writeDebugMsg(id, stepNum, $"wait {i} completed")|> ignore
                            | Trigger i -> writeDebugMsg(id, stepNum, $"trigger {i}"); handles.[i].Set()|> ignore
                            | SleepMilliseconds ms -> writeDebugMsg(id, stepNum, $"sleep {ms}"); Thread.Sleep(ms) |> ignore; writeDebugMsg(id, stepNum, $"sleep {ms} completed") |> ignore)
                        12                   
                }
    async {        
        let result = 
            try 
                LockingTransaction.runInTransaction(txfn) |> ignore
                NormalExit
            with 
            | ex -> ExceptionThrown(ex)
        return result
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
        |> List.mapi (fun i s -> createExecuteScriptAsync(i, s, handles, refs))
        |> Async.Parallel
        |> Async.RunSynchronously

    scripts
    |> List.zip( results |> Array.toList)
    |> List.iter (fun (r, s) -> performTests(s, r, refs))

let execute(scripts : TxScript list) =
    let handles = createHandles(scripts)
    let refs = createRefs(scripts)
    runTests(scripts, handles, refs)
    handles |> Array.iter (fun h -> h.Dispose())
    refs |> Array.iter (fun r -> (r :> IDisposable).Dispose())