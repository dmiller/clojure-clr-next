module Clojure.Collections.Test

open Expecto

[<EntryPoint>]
let main argv =


    Clojure.Collections.RTEnvInitialization.initialize ()

    Tests.runTestsInAssembly defaultConfig argv
