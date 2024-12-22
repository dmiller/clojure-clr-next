---
layout: post
title: STM in Clojure - Testing
date: 2024-12-22 00:10:00 -0500
categories: general
---

We develop a small framework for testing transaction interaction.

This is the third in a series on implementing STM in Clojure.
Previous posts:

- [Part 1: STM in Clojure - Design]({{site.baseurl}}{% post_url 2024-12-22-STM-design %}).
- [Part 2: STM in Clojure - Code]({{site.baseurl}}{% post_url 2024-12-22-STM-code %})


## Testing transactions

How do we test transactions?  Who knows?

In the Clojure test suite, there is a file `clojure/test_clojure/refs.clj`. Leaving out the copyright and author info, here are the tests:

```Clojure
(ns clojure.test-clojure.refs
  (:use clojure.test))

; http://clojure.org/refs

; ref
; deref, @-reader-macro
; dosync io!
; ensure ref-set alter commute
; set-validator get-validator
```

Yeah, no.  


 I'd like to do some tests for my `Ref` and `LockingTransaction` code.  No question that Clojure would be great place to do this, but at this point in building ClojureCLR.Next, well, we don't have Clojure yet.

 I could generate some tests based on some of the illustrative samples found in various places.  And I'll do that.  But I wanted to some very specific tests.  Like checking that doing a set after a commute fails.  Or that if T1 commutes a Ref, but T2 gets done before T2, the commute runs on T2's value for the Ref.  And so on.

 So I decided to develop a modest framework for writing tests of this type.  We need to be able to write a script for transaction that can do things like `commute`s and `ref-set`s.  We need to be able to coordinate between two different transactions.  I decided to use `ManualResetEvent`s that are shared between transactions.  One transaction can trigger the MRE, another can wait for the MRE to be set.
 I wanted to be able to specify tests for after the transaction finishes: normal exit vs exception thrown; the values for specific Refs.  And I'd like for it to be integrated with the testing framework I'm use (Expecto).  I'd like to be able to write a test like this:

 ```F#
           testCase "Force one to complete before other see final change"
          <| fun _ ->
              let script1 =
                  { Steps = [ RefSet(0,10); Trigger(0); ]
                    Tests = [ TxTest.Normal; Ref(0,99)] }

              let script2 =
                  { Steps = [ Wait(0); RefSet(0,99);  ]
                    Tests = [ TxTest.Normal; Ref(0,99)] }

              let scripts = [ script1; script2 ]
              execute scripts
```

## Code

I used disciminated unions for the steps in a transaction:

```F#
type TxAction =
    | RefSet of index: int * value: int
    | CommuteIncr of index: int
    | AlterIncr of index: int
    | Wait of index: int
    | Trigger of index: int
    | SleepMilliseconds of int
```

Since I only care about thing running or not, I don't need to pass arbitrary functions to `commute` and `alter`, so I'm only going to increment an integer value.

For `RefSet`, `CommutIncr` and `AlterIncr`, the index specifies a `Ref` in an array of `Refs` shared by all scripts in the test case.  Similarly for `Wait` and `Trigger` for a shared array of `ManualResetEvents`.  My intention is that each event get used only once.

We'll want to know how the transaction completed:

```F#
type TxExit =
    | NormalExit
    | ExceptionThrown of ex: exn
```

We'll need to specify what tests to perform.  We need to test whether the outcome was a normal exit or an exception being thrown.  And we'll need to check the final values of the various `Ref`s.

```F#
type TxTest =
    | Throw of exnType: Type
    | Normal
    | Ref of index: int * value: int
```

A script for a transaction is a sequence of actions and a set of tests.  

```F#
type TxTest =
    | Throw of exnType: Type
    | Normal
    | Ref of index: int * value: int
```

Now we can code.  We will need to examine a set of scripts and determine how many `Ref`s and how many `ManualResetEvent`s to create.  The maximum index + 1 will suffice.



```F#
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
```

I'm sure there are more elegant ways, but this works.
From the counts, we can create the arrays we need.

```F#
let createHandles(scripts : TxScript list) =
    Array.init (getHandleCount(scripts)) (fun i -> new ManualResetEvent(false))

let createRefs(scripts : TxScript list) =
    Array.init (getRefCount(scripts)) (fun i -> new Clojure.Lib.Ref(0, null))
```

We'll need to pass an `IFn` that has an invoke of one argument that adds one to the (integer) argument.
There is a neat trick for building `IFn`'s in F#.  The class `AFn` defines a working `IFn` that throws on all `invoke` overloads. We can use an object expression to overload just the `IFn.invoke(arg1)` method.

```F#
let incrFn = 
    { new AFn() with
        member this.ToString() = "a"
      interface IFn with
        member this.invoke(arg1) = (arg1 :?> int) + 1 :> obj
    }
```

(I was so excited when I discovered object expressions in F#.)

Jumping to end and working back to the hard spot, we will execute scripts using `execute`.

```F#
let execute(scripts : TxScript list) =
    let handles = createHandles(scripts)
    let refs = createRefs(scripts)
    runTests(scripts, handles, refs)
    handles |> Array.iter (fun h -> h.Dispose())
    refs |> Array.iter (fun r -> (r :> IDisposable).Dispose())
```
We pass a list of scripts execute.  It generates the arrays of `Ref`s and event handles, runs the scripts and tests them, and then cleans up.

We have to run the tests and then test when they are all done.  We use the `async` mechanism to coordinate running the tasks.  We take each script and generate an async script, run them all in parallel and wait for them all to finish. Then we run the tests.

```F#
let runTests(scripts : TxScript list, handles : ManualResetEvent array, refs : Clojure.Lib.Ref array) = 
    let results = 
        scripts
        |> List.mapi (fun i s -> createExecuteScriptAsync(i, s, handles, refs))
        |> Async.Parallel
        |> Async.RunSynchronously

    scripts
    |> List.zip( results |> Array.toList)
    |> List.iter (fun (r, s) -> performTests(s, r, refs))
```

The `Async.RunSynchronously` returns an array of the return values from each async script.  We pair scripts and results and send them off to `performTests`:

```F#
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
```

I think the testing of return results is a little inelegant, but I'm saving a rewrite for another day.
Notice here that calls to `Expect.equal` and `Expect.isTrue`.  This ties us into the Expecto testing framework.

Finally, the biggie.  Generating an async script from a sequence (list) of steps.
Essentially, we need the equivalent of what the `dosync` macro call does: take the body, wrap it with a `(fn [] ...)` and pass that to `LockingTransaction.runInTransaction`.  In the code below, the value of `txfn` is just that: an `IFn` that has a zero-arg `invoke` that iterates through the sequence of actions and executes them.  The script proper passes `txfn` to `LockingTransaction.runInTransaction` and returns `NormalExit` or `ExceptionThrown(dx)`, depending.

```F#
let createExecuteScriptAsync(id: int, script : TxScript, handles: ManualResetEvent array, refs: Clojure.Lib.Ref array) =
    let txfn = { new AFn() with
                    member this.ToString() = "a"
                 interface IFn with
                    member _.invoke() = 
                        script.Steps 
                        |> List.iteri (fun stepNum step ->
                            match step with
                            | RefSet(i, v) ->  refs[i].set(v) |> ignore
                            | CommuteIncr i -> refs[i].commute(incrFn, null) |> ignore
                            | AlterIncr i ->  refs.[i].alter(incrFn, null)  |> ignore
                            | Wait i ->  handles.[i].WaitOne() |> ignore 
                            | Trigger i -> handles.[i].Set()|> ignore
                            | SleepMilliseconds ms ->  Thread.Sleep(ms) |> ignore
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
```

And that's the whole enchilada.

Realistically, there are only a few meaningful scenario that we can test this way.
Some more complicated things, like testing many retries to failure, are just too hard.
Contemplate how long it takes for a 10,000 retries with 100 millisecond waits on each retry.
Contemplate how you even detect a retry has happened -- we'd have to set up another mechanism for side-effecting actions such as counter external to the transaction.  At least this mechanism allowed me to do some simple testing to check for basic operational validity.  That was enough reward for the effort involved.

