
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Running
open Tests

[<EntryPoint>]
let main argv =
    BenchmarkRunner.Run<Tests>() |> ignore
    0 // return an integer exit code
