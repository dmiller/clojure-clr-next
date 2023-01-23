module Clojure.BigArith.Test

open Expecto

[<EntryPoint>]
let main argv =
    Tests.runTestsInAssembly defaultConfig argv
