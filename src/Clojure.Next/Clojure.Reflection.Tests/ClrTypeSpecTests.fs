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

let testArraySpec(aspec: ClrArraySpec, dimensions: int, isBound: bool) =
    Expect.equal aspec.Dimensions dimensions "Dimension should match"
    Expect.equal aspec.IsBound isBound "IsBound should match"

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
let PointersAndByRefs =
    testList
        "Testing pointer levels and by-ref"
        [ 
        
          testCase "One level of pointer"
          <| fun _ ->
                let result = ClrTypeSpec.Parse "System.String*"

                Expect.isTrue (result.IsOk) "Should parse"

                let spec = getSpec(result)
                doBasicTests(spec, { Name = "System.String"; AssemblyName = null; IsNested = false; IsArray = false; HasGenericParams = false; PointerLevel = 1; IsByRef = false })

          testCase "Three levels of pointer"
          <| fun _ ->
                let result = ClrTypeSpec.Parse "System.String***"

                Expect.isTrue (result.IsOk) "Should parse"

                let spec = getSpec(result)
                doBasicTests(spec, { Name = "System.String"; AssemblyName = null; IsNested = false; IsArray = false; HasGenericParams = false; PointerLevel = 3; IsByRef = false })  
                
          testCase "single by-ref"
          <| fun _ ->
                let result = ClrTypeSpec.Parse "System.String&"

                Expect.isTrue (result.IsOk) "Should parse"

                let spec = getSpec(result)
                doBasicTests(spec, { Name = "System.String"; AssemblyName = null; IsNested = false; IsArray = false; HasGenericParams = false; PointerLevel = 0; IsByRef = true })

          testCase "multiple by-ref fails"
          <| fun _ ->
                let result = ClrTypeSpec.Parse "System.String&&"

                Expect.isTrue (result.IsError) "Should not parse"
                
                let msg = getErrorMsg(result)
                Expect.equal msg "Can't have more than one byref modifier" "Has correct error message"

          testCase "can't have pointer on by-ref"
          <| fun _ ->
                let result = ClrTypeSpec.Parse "System.String&*"

                Expect.isTrue (result.IsError) "Should not parse"
                
                let msg = getErrorMsg(result)
                Expect.equal msg "Can't have pointer modifier on byref type" "Has correct error message"

                
          testCase "can have by-ref on pointers"
          <| fun _ ->
                let result = ClrTypeSpec.Parse "System.String***&"

                Expect.isTrue (result.IsOk) "Should parse"

                let spec = getSpec(result)
                doBasicTests(spec, { Name = "System.String"; AssemblyName = null; IsNested = false; IsArray = false; HasGenericParams = false; PointerLevel = 3; IsByRef = true })

                          
          testCase "cannot have generics or array after by-ref"
          <| fun _ ->
                let result = ClrTypeSpec.Parse "System.String&[]"

                Expect.isTrue (result.IsError) "Should not parse"

                let msg = getErrorMsg(result)
                Expect.equal msg "Byref qualifier must be the last one of a type" "Has correct error message"        
 
        ]

[<Tests>]
let GenericsTests =
    testList
        "Testing generic parameters"
        [ 
        
          testCase "One generic argument"
          <| fun _ ->
                let result = ClrTypeSpec.Parse "MyType[String]"

                Expect.isTrue (result.IsOk) "Should parse"

                let spec = getSpec(result)
                doBasicTests(spec, { Name = "MyType"; AssemblyName = null; IsNested = false; IsArray = false; HasGenericParams = true; PointerLevel = 0; IsByRef = false })
                Expect.equal spec.GenericParams.Count 1 "Should have one generic parameter"
                Expect.equal spec.GenericParams.[0].Name "String" "Generic parameter should be String"

        
          testCase "Two generic arguments"
          <| fun _ ->
                let result = ClrTypeSpec.Parse "MyType[String,int]"

                Expect.isTrue (result.IsOk) "Should parse"

                let spec = getSpec(result)
                doBasicTests(spec, { Name = "MyType"; AssemblyName = null; IsNested = false; IsArray = false; HasGenericParams = true; PointerLevel = 0; IsByRef = false })
                Expect.equal spec.GenericParams.Count 2 "Should have one generic parameter"
                Expect.equal spec.GenericParams.[0].Name "String" "First generic parameter should be String"
                Expect.equal spec.GenericParams.[1].Name "int" "Second generic parameter should be int"

          testCase "generic parameter can't be pointer"
          <| fun _ ->
                let result = ClrTypeSpec.Parse "MyType[String*]"

                Expect.isTrue (result.IsError) "Should not parse"
                
                let msg = getErrorMsg(result)
                Expect.equal msg "Generic argument can't be byref or pointer type" "Has correct error message" 

          testCase "generic parameter can't be by-ref"
          <| fun _ ->
                let result = ClrTypeSpec.Parse "MyType[String&]"

                Expect.isTrue (result.IsError) "Should not parse"
                
                let msg = getErrorMsg(result)
                Expect.equal msg "Generic argument can't be byref or pointer type" "Has correct error message" 

          testCase "no closing ] for generic type parameters is an error (with one arg)"
          <| fun _ ->
                let result = ClrTypeSpec.Parse "MyType[String"

                Expect.isTrue (result.IsError) "Should not parse"
                
                let msg = getErrorMsg(result)
                Expect.equal msg "Invalid generic arguments spec" "Has correct error message" 


          testCase "no closing ] for generic type parameters is an error (with no args)"
          <| fun _ ->
                let result = ClrTypeSpec.Parse "MyType["

                Expect.isTrue (result.IsError) "Should not parse"
                
                let msg = getErrorMsg(result)
                Expect.equal msg "Invalid array/generic spec" "Has correct error message" 

          testCase "generic parameters after array spec is an error"
          <| fun _ ->
                let result = ClrTypeSpec.Parse "MyType[][string]"

                Expect.isTrue (result.IsError) "Should not parse"
                
                let msg = getErrorMsg(result)
                Expect.equal msg "generic args after array spec" "Has correct error message" 

        ]

[<Tests>]
let ArrayTests =
    testList
        "Testing array specs"
        [ 
          testCase "Single array, one dimension"
          <| fun _ ->
                let result = ClrTypeSpec.Parse "MyType[]"

                Expect.isTrue (result.IsOk) "Should parse"

                let spec = getSpec(result)
                doBasicTests(spec, { Name = "MyType"; AssemblyName = null; IsNested = false; IsArray = true; HasGenericParams = false; PointerLevel = 0; IsByRef = false })
                Expect.equal spec.ArraySpec.Count 1 "Should have one array spec"
                testArraySpec(spec.ArraySpec[0], 1, false)

          testCase "Single array, several dimensions"
          <| fun _ ->
                let result = ClrTypeSpec.Parse "MyType[,,]"

                Expect.isTrue (result.IsOk) "Should parse"

                let spec = getSpec(result)
                doBasicTests(spec, { Name = "MyType"; AssemblyName = null; IsNested = false; IsArray = true; HasGenericParams = false; PointerLevel = 0; IsByRef = false })
                Expect.equal spec.ArraySpec.Count 1 "Should have one array spec"
                testArraySpec(spec.ArraySpec[0], 3, false)

          testCase "Several arrays"
          <| fun _ ->
                let result = ClrTypeSpec.Parse "MyType[,,][]"

                Expect.isTrue (result.IsOk) "Should parse"

                let spec = getSpec(result)
                doBasicTests(spec, { Name = "MyType"; AssemblyName = null; IsNested = false; IsArray = true; HasGenericParams = false; PointerLevel = 0; IsByRef = false })
                Expect.equal spec.ArraySpec.Count 2 "Should have one array spec"
                testArraySpec(spec.ArraySpec[0], 3, false)
                testArraySpec(spec.ArraySpec[1], 1, false)

          testCase "Single array, one dimension, bound"
          <| fun _ ->
                let result = ClrTypeSpec.Parse "MyType[*]"

                Expect.isTrue (result.IsOk) "Should parse"

                let spec = getSpec(result)
                doBasicTests(spec, { Name = "MyType"; AssemblyName = null; IsNested = false; IsArray = true; HasGenericParams = false; PointerLevel = 0; IsByRef = false })
                Expect.equal spec.ArraySpec.Count 1 "Should have one array spec"
                testArraySpec(spec.ArraySpec[0], 1, true)

          testCase "Single array, several dimension, bound"
          <| fun _ ->
                let result = ClrTypeSpec.Parse "MyType[*]"

                Expect.isTrue (result.IsOk) "Should parse"

                let spec = getSpec(result)
                doBasicTests(spec, { Name = "MyType"; AssemblyName = null; IsNested = false; IsArray = true; HasGenericParams = false; PointerLevel = 0; IsByRef = false })
                Expect.equal spec.ArraySpec.Count 1 "Should have one array spec"
                testArraySpec(spec.ArraySpec[0], 1, true)

          testCase "two bound dimensions in one spec fails"  
          <| fun _ ->
                let result = ClrTypeSpec.Parse "MyType[**][]"

                Expect.isTrue (result.IsError) "Should not parse"
                
                let msg = getErrorMsg(result)
                Expect.equal msg "Array spec cannot have 2 bound dimensions" "Has correct error message" 


          testCase "multi-dimensional array cannot be bound"  
          <| fun _ ->
                let result = ClrTypeSpec.Parse "MyType[*,]"

                Expect.isTrue (result.IsError) "Should not parse"
                
                let msg = getErrorMsg(result)
                Expect.equal msg "Invalid array spec, multi-dimensional array cannot be bound" "Has correct error message" 


        ]

[<Tests>]
let AssemblyNameTests =
    testList
        "Testing assembly names in specs"
        [ 
          testCase "Simple assembly name"
          <| fun _ ->
                let result = ClrTypeSpec.Parse "MyType, MyAssembly"

                Expect.isTrue (result.IsOk) "Should parse"

                let spec = getSpec(result)
                doBasicTests(spec, { Name = "MyType"; AssemblyName = "MyAssembly"; IsNested = false; IsArray = false; HasGenericParams = false; PointerLevel = 0; IsByRef = false })

          testCase "Not a simple assembly name"
          <| fun _ ->
                let result = ClrTypeSpec.Parse "System.Array, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"

                Expect.isTrue (result.IsOk) "Should parse"

                let spec = getSpec(result)
                doBasicTests(spec, { Name = "System.Array"; AssemblyName = "mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"; IsNested = false; IsArray = false; HasGenericParams = false; PointerLevel = 0; IsByRef = false })


          testCase "Assembly name in generic argument"
          <| fun _ ->
                let result = ClrTypeSpec.Parse "MyType[[Arg, ArgAssembly]]"

                Expect.isTrue (result.IsOk) "Should parse"

                let spec = getSpec(result)
                doBasicTests(spec, { Name = "MyType"; AssemblyName = null; IsNested = false; IsArray = false; HasGenericParams = true; PointerLevel = 0; IsByRef = false })
                Expect.equal spec.GenericParams.Count 1 "Should have one generic parameter"
                doBasicTests(spec.GenericParams[0], 
                             { Name = "Arg"; AssemblyName = "ArgAssembly"; IsNested = false; IsArray = false; HasGenericParams = false; PointerLevel = 0; IsByRef = false })


          testCase "Assembly name in generic argument, with assembly"
          <| fun _ ->
                let result = ClrTypeSpec.Parse "MyType[[Arg, ArgAssembly]], MyAssembly"

                Expect.isTrue (result.IsOk) "Should parse"

                let spec = getSpec(result)
                doBasicTests(spec, { Name = "MyType"; AssemblyName = "MyAssembly"; IsNested = false; IsArray = false; HasGenericParams = true; PointerLevel = 0; IsByRef = false })
                Expect.equal spec.GenericParams.Count 1 "Should have one generic parameter"
                doBasicTests(spec.GenericParams[0], 
                             { Name = "Arg"; AssemblyName = "ArgAssembly"; IsNested = false; IsArray = false; HasGenericParams = false; PointerLevel = 0; IsByRef = false })
                
        ]