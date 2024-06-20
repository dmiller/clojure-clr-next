open Tests
open BenchmarkDotNet.Running

[<EntryPoint>]
let main argv =
    BenchmarkRunner.Run<Tests>() |> ignore
    0 // return an integer exit code