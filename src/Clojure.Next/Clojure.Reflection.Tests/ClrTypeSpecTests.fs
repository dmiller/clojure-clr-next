module ClrTypeSpecTests

open Expecto
open Clojure.Reflection

let getSpec (result: Result<ClrTypeSpec, string>) =
    match result with
    | Ok spec -> spec
    | _ -> failwith "Expected to have a spec"

let getErrorMsg (result: Result<ClrTypeSpec, string>) =
    match result with
    | Error msg -> msg
    | _ -> failwith "Expected to have an error message"

type SpecMatrix = { Name: string; AssemblyName: string; IsNested: bool; IsArray: bool; HasGenericParams: bool; PointerLevel: int; IsByRef: bool }

let doBasicTests(spec: ClrTypeSpec, matrix: SpecMatrix) =
    Expect.equal spec.Name matrix.Name "Name should match"
    Expect.equal spec.AssemblyName matrix.AssemblyName "AssemblyName should match"
    Expect.equal spec.IsNested matrix.IsNested "IsNested should match"
    Expect.equal spec.IsArray matrix.IsArray "IsArray should match"
    Expect.equal spec.HasGenericParams matrix.HasGenericParams "HasGenericParams should match"
    Expect.equal spec.PointerLevel matrix.PointerLevel "PointerLevel should match"
    Expect.equal spec.IsByRef matrix.IsByRef "IsByRef should match"

[<Tests>]
let BasicNames =
    testList
        "Basic Naming Tests"
        [ 
        
          testCase "Simple name parses"
          <| fun _ ->
                let result = ClrTypeSpec.Parse "MyType"

                Expect.isTrue (result.IsOk) "Should parse"

                let spec = getSpec(result)
                doBasicTests(spec, { Name = "MyType"; AssemblyName = null; IsNested = false; IsArray = false; HasGenericParams = false; PointerLevel = 0; IsByRef = false })

        
          testCase "Skip leading spaces"
          <| fun _ ->
                let result = ClrTypeSpec.Parse "   MyType"

                Expect.isTrue (result.IsOk) "Should parse"

                let spec = getSpec(result)
                doBasicTests(spec, { Name = "MyType"; AssemblyName = null; IsNested = false; IsArray = false; HasGenericParams = false; PointerLevel = 0; IsByRef = false })


          testCase "Typename with namespace parses"
          <| fun _ ->
                let result = ClrTypeSpec.Parse "System.String"

                Expect.isTrue (result.IsOk) "Should parse"

                let spec = getSpec(result)
                doBasicTests(spec, { Name = "System.String"; AssemblyName = null; IsNested = false; IsArray = false; HasGenericParams = false; PointerLevel = 0; IsByRef = false })


          testCase "Typename with single nesting parses"
          <| fun _ ->
                let result = ClrTypeSpec.Parse "clojure.lang.Compiler+CompilerException"

                Expect.isTrue (result.IsOk) "Should parse"

                let spec = getSpec(result)
                doBasicTests(spec, { Name = "clojure.lang.Compiler"; AssemblyName = null; IsNested = true; IsArray = false; HasGenericParams = false; PointerLevel = 0; IsByRef = false })
                Expect.equal spec.Nested.Count 1 "Should have one nested type"
                Expect.equal spec.Nested.[0] "CompilerException" "Nested type should be CompilerException"

          testCase "Typename with multiple nestings parses"
          <| fun _ ->
                let result = ClrTypeSpec.Parse "clojure.lang.Compiler+CompilerException+NestedType"

                Expect.isTrue (result.IsOk) "Should parse"

                let spec = getSpec(result)
                doBasicTests(spec, { Name = "clojure.lang.Compiler"; AssemblyName = null; IsNested = true; IsArray = false; HasGenericParams = false; PointerLevel = 0; IsByRef = false })
                Expect.equal spec.Nested.Count 2 "Should have one nested type"
                Expect.equal spec.Nested.[0] "CompilerException" "Nested type should be CompilerException"
                Expect.equal spec.Nested.[1] "NestedType" "Nested type should be NestedType"

        ]

[<Tests>]
let PointerLevels =
    testList
        "Testing pointer levels and by-ref"
        [ 
        
          ftestCase "One level of pointer"
          <| fun _ ->
                let result = ClrTypeSpec.Parse "System.String*"

                Expect.isTrue (result.IsOk) "Should parse"

                let spec = getSpec(result)
                doBasicTests(spec, { Name = "System.String"; AssemblyName = null; IsNested = false; IsArray = false; HasGenericParams = false; PointerLevel = 1; IsByRef = false })

          ftestCase "Three levels of pointer"
          <| fun _ ->
                let result = ClrTypeSpec.Parse "System.String***"

                Expect.isTrue (result.IsOk) "Should parse"

                let spec = getSpec(result)
                doBasicTests(spec, { Name = "System.String"; AssemblyName = null; IsNested = false; IsArray = false; HasGenericParams = false; PointerLevel = 3; IsByRef = false })  
                
          ftestCase "single by-ref"
          <| fun _ ->
                let result = ClrTypeSpec.Parse "System.String&"

                Expect.isTrue (result.IsOk) "Should parse"

                let spec = getSpec(result)
                doBasicTests(spec, { Name = "System.String"; AssemblyName = null; IsNested = false; IsArray = false; HasGenericParams = false; PointerLevel = 0; IsByRef = true })

          ftestCase "multiple by-ref fails"
          <| fun _ ->
                let result = ClrTypeSpec.Parse "System.String&&"

                Expect.isTrue (result.IsError) "Should not parse"
                
                let msg = getErrorMsg(result)
                Expect.equal msg "Can't have more than one byref modifier" "Has correct error message"

          ftestCase "can't have pointer on by-ref"
          <| fun _ ->
                let result = ClrTypeSpec.Parse "System.String&*"

                Expect.isTrue (result.IsError) "Should not parse"
                
                let msg = getErrorMsg(result)
                Expect.equal msg "Can't have pointer modifier on byref type" "Has correct error message"

                
          ftestCase "can have by-ref on pointers"
          <| fun _ ->
                let result = ClrTypeSpec.Parse "System.String***&"

                Expect.isTrue (result.IsOk) "Should parse"

                let spec = getSpec(result)
                doBasicTests(spec, { Name = "System.String"; AssemblyName = null; IsNested = false; IsArray = false; HasGenericParams = false; PointerLevel = 3; IsByRef = true })

 
        ]