open BenchmarkDotNet.Running

open Tests

[<EntryPoint>]
let main argv =
    BenchmarkRunner.Run<Tests32>() |> ignore
    BenchmarkRunner.Run<Tests64>() |> ignore
    0 // return an integer exit code