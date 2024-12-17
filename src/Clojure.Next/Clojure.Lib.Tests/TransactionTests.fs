module TransactionTests

open Expecto
open TxTester
open System


[<Tests>]
let BasicRefTests =
    testList
        "Basic Ref Tests"
        [

          testCase "Transaction with no body should exit normally"
          <| fun _ ->
              let script1 =
                  { Steps = [ Trigger(0) ]
                    Tests = [ TxTest.Normal ] }

              let scripts = [ script1 ]
              execute scripts


          testCase "Transaction with one ref-set should exit normally and set the ref"
          <| fun _ ->
              let script1 =
                  { Steps = [ RefSet(0, 5) ]
                    Tests = [ TxTest.Normal; Ref(0, 5) ] }

              let scripts = [ script1 ]
              execute scripts


          testCase "Transaction with one alter should exit normally and alter the ref"
          <| fun _ ->
              let script1 =
                  { Steps = [ AlterIncr(0) ]
                    Tests = [ TxTest.Normal; Ref(0, 1) ] }

              let scripts = [ script1 ]
              execute scripts

          
          testCase "Transaction with two alters on same ref should exit normally and the ref should be doubly altered"
          <| fun _ ->
              let script1 =
                  { Steps = [ AlterIncr(0); AlterIncr(0) ]
                    Tests = [ TxTest.Normal; Ref(0, 2) ] }

              let scripts = [ script1 ]
              execute scripts
          
          testCase "Transaction with a commute should exit normally and the ref should be altered"
          <| fun _ ->
              let script1 =
                  { Steps = [ CommuteIncr(0) ]
                    Tests = [ TxTest.Normal; Ref(0, 1) ] }

              let scripts = [ script1 ]
              execute scripts          

          
          testCase "Set after commute should thrown an exception"
          <| fun _ ->
              let script1 =
                  { Steps = [ CommuteIncr(0); RefSet(0,5)]
                    Tests = [ TxTest.Throw(typeof<InvalidOperationException>); Ref(0, 0) ] }

              let scripts = [ script1 ]
              execute scripts          

          testCase "Commute after set should see results of both"
          <| fun _ ->
              let script1 =
                  { Steps = [ RefSet(0,5); CommuteIncr(0);]
                    Tests = [ TxTest.Normal; Ref(0, 6) ] }

              let scripts = [ script1 ]
              execute scripts          


          testCase "Double commute should see results of both"
          <| fun _ ->
              let script1 =
                  { Steps = [ CommuteIncr(0); CommuteIncr(0);]
                    Tests = [ TxTest.Normal; Ref(0, 2) ] }

              let scripts = [ script1 ]
              execute scripts             

          ]



[<Tests>]
let ConflictingRefTests =
    testList
        "Conflicting Ref Tests"
        [

          ftestCase "Commute should change cause by other transaction"
          <| fun _ ->
              let script1 =
                  { Steps = [ CommuteIncr(0); Trigger(0); Wait(1) ]
                    Tests = [ TxTest.Normal; Ref(0,99)] }

              let script2 =
                  { Steps = [ Wait(0); RefSet(0,98); Trigger(1) ]
                    Tests = [ TxTest.Normal; Ref(0,99)] }

              let scripts = [ script1; script2 ]
              execute scripts


        ]