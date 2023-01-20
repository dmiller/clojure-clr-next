module Tests

open BenchmarkDotNet.Attributes
open System
open Dispatchers

let numIters = 1000

let data : obj array = [| 12L; 1.2; 'c'; 12;  1.2M |]

type Tests() =
    
    [<Benchmark(Baseline=true)>]   
    member _.TypeCombine() = 
        for iter in 0 .. numIters - 1 do 
            for i in 0 .. 4 do
                for j in 0 .. 4 do
                    typeCombine(data[i], data[j]) |> ignore
              
    [<Benchmark>]   
    member _.LookupCombine() = 
        for iter in 0 .. numIters - 1 do 
            for i in 0 .. 4 do
                for j in 0 .. 4 do
                    lookupCombine(data[i], data[j]) |> ignore
              
    [<Benchmark>]   
    member _.LookupCombine2D() = 
        for iter in 0 .. numIters - 1 do 
            for i in 0 .. 4 do
                for j in 0 .. 4 do
                    lookupCombine2D(data[i], data[j]) |> ignore
              