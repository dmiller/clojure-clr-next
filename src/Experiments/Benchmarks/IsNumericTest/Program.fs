// For more information see https://aka.ms/fsharp-console-apps
printfn "Hello from F#"

let items : obj[] = [|1; 1.0; "abc"|]
printfn $"{TypeDispatch.TypeDispatch.IsNumeric(items[0])}"
printfn $"{TypeDispatch.TypeDispatch.IsNumeric(items[1])}"
printfn $"{TypeDispatch.TypeDispatch.IsNumeric(items[2])}"
