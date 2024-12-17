module TransactionTests

open Expecto
open TxSession



[<Tests>]
let BasicRefTests =
    testList
        "Basic Ref Tests"
        [

          testCase "Transaction with no body should exit normally"
          <| fun _ ->
               let script1 = { Steps = [Trigger(0)]; Tests = [TxTest.Normal] }
               let tester = TxSession([script1])
               tester.Execute()


          testCase "Transaction with no body should exit normally"
          <| fun _ ->
               let script1 = { Steps = [Trigger(0)]; Tests = [TxTest.Normal] }
               let tester = TxSession([script1])
               tester.Execute()



              ]