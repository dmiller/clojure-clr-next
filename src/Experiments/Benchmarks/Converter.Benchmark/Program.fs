
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Running
open Tests

[<EntryPoint>]
let main argv =
    BenchmarkRunner.Run<TestingConverters>() |> ignore
    BenchmarkRunner.Run<TestingCategorizers>() |> ignore
    0 // return an integer exit code