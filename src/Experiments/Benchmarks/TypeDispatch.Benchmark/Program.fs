open Tests
open BenchmarkDotNet.Running
open BenchmarkDotNet.Configs

[<EntryPoint>]
let main argv =
    BenchmarkRunner.Run<Tests2>() |> ignore

    //BenchmarkSwitcher.FromAssembly(typeof<FsharpTypeDispatch.TypeDispatch>.Assembly).Run(argv, new DebugInProcessConfig()) |> ignore
    0 // return an integer exit code
