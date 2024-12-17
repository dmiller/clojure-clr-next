module TransactionTests

open Expecto
open TxTester




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


          testCase "Transaction one ref-set should exit normally and set the ref"
          <| fun _ ->
              let script1 =
                  { Steps = [ RefSet(0, 5) ]
                    Tests = [ TxTest.Normal; Ref(0, 5) ] }

              let scripts = [ script1 ]
              execute scripts

          ]
