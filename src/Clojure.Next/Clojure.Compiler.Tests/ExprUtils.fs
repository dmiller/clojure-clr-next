module ExprUtils

open Expecto
open Clojure.Compiler
open Clojure.Collections


let honk (x: obj, y: obj) =

    let result = x.Equals(y)

    if not result then
        printfn "x: %A" x
        printfn "y: %A" y

    result


    
    

let compareGenericLists (a: ResizeArray<'T>, b: ResizeArray<'T>) =
    if isNull a then
        Expect.isTrue (isNull b) "First list is null, second list is not"
    else
        Expect.isNotNull b "First list is not null, second list is"

        let comp =
            a
            |> Seq.zip b
            |> Seq.forall (fun (x, y) -> if isNull x then isNull y else honk (x, y))

        Expect.isTrue comp "Expect same list"



let compareLocalsMaps (lm1: IPersistentMap, lm2: IPersistentMap) =
    Expect.equal (lm1.count ()) (lm2.count ()) "Locals count should be equal"
    // A locals map uses a local binding for both both key and value.
    // Given that the local bindings between the two maps are different, we can't use the keys in one to look up the values in the other
    // Even the symbols used in the bindings will be different for the this variable.
    // So let's create our own map of local names to bindings, with the the IsThis variables pulled out.

    let lbs1 = RTMap.keys (lm1)

    if not <| isNull lbs1 then
        let this1 = lbs1 |> Seq.cast<LocalBinding> |> Seq.tryFind (fun x -> x.IsThis)

        let regular1 =
            lbs1
            |> Seq.cast<LocalBinding>
            |> Seq.filter (fun x -> not x.IsThis)
            |> Seq.toList

        let lbs2 = RTMap.keys (lm2)
        let this2 = lbs2 |> Seq.cast<LocalBinding> |> Seq.tryFind (fun x -> x.IsThis)

        let regular2 =
            lbs2
            |> Seq.cast<LocalBinding>
            |> Seq.filter (fun x -> not x.IsThis)
            |> Seq.toList

        Expect.equal this1.IsSome this2.IsSome "Both locals maps should have IsThis variable"

        // the regular locals should match directly
        regular1
        |> Seq.zip regular2
        |> Seq.iter (fun (a, b) -> Expect.equal a b "Local binding should match")


let compareObjxRegisters (x: ObjXRegister, y: ObjXRegister) =
    compareLocalsMaps (x.Closes, y.Closes)
    Expect.equal x.Constants y.Constants "Constants should be equal"
    Expect.equal x.Keywords y.Keywords "Keywords should be equal"
    Expect.equal x.Vars y.Vars "Vars should be equal"
    Expect.equal x.KeywordCallsites y.KeywordCallsites "KeywordCallsites should be equal"
    Expect.equal x.ProtocolCallsites y.ProtocolCallsites "ProtocolCallsites should be equal"
    Expect.equal x.VarCallsites y.VarCallsites "VarCallsites should be equal"



let compareEnvLocalsMaps (lm1: IPersistentMap, lm2: IPersistentMap) =
    Expect.equal (lm1.count ()) (lm2.count ()) "Locals count should be equal"
    // A locals map in an CompilerEnv uses Symbols for keys and LocalBindings as values 
    // With the same counts, we can look up using the key from one and compare the values.

    if lm1.count() > 0 then
        let keys1 = RTMap.keys(lm1)
        keys1 
        |> Seq.iter( fun k1 -> Expect.equal (lm1.valAt(k1)) (lm2.valAt(k1)) "Should have matching values for same key" )

let compareCompilerEnvs(a: CompilerEnv, b: CompilerEnv) = 
    Expect.equal a.Pctx b.Pctx "ParserContext should be equal"
    compareEnvLocalsMaps (a.Locals, b.Locals)
    Expect.equal a.Method b.Method "Method should be equal"
    Expect.equal a.IsAssignContext b.IsAssignContext "IsAssignContext should be equal"
    Expect.equal a.IsRecurContext b.IsRecurContext "IsRecurContext should be equal"
    Expect.equal a.InTry b.InTry "InTry should be equal"
    Expect.equal a.InCatchFinally b.InCatchFinally "IsInCatchFinally should be equal"
    Expect.equal a.LoopId b.LoopId "LoopId should be equal"
    compareGenericLists (a.LoopLocals, b.LoopLocals)
    Expect.equal a.NoRecur b.NoRecur "NoRecur should be equal"
    match a.ObjXRegister, b.ObjXRegister with
    | None, None -> Expect.isTrue true "Both are None"
    | None, _ -> failtest "First has no Register, second has register"
    | _, None -> failtest "First a Register, second has no register"
    | Some ar, Some br ->
        compareObjxRegisters (ar, br)


let compareInteropCalls (a: AST, b: AST) =
    match a, b with
    | AST.InteropCall(aexpr), AST.InteropCall(bexpr) ->
        Expect.equal aexpr.Env bexpr.Env "Env should be equal"
        Expect.equal aexpr.Form bexpr.Form "Form should be equal"
        Expect.equal aexpr.HostExprType bexpr.HostExprType "Type should be equal"
        Expect.equal aexpr.IsStatic bexpr.IsStatic "IsStatic should be equal"
        Expect.equal aexpr.Tag bexpr.Tag "Tag should be equal"
        Expect.equal aexpr.Target bexpr.Target "Target should be equal"
        Expect.equal aexpr.TargetType bexpr.TargetType "TargetType should be equal"
        Expect.equal aexpr.MemberName bexpr.MemberName "MemberName should be equal"
        Expect.equal aexpr.TInfo bexpr.TInfo "TInfo should be equal"
        compareGenericLists (aexpr.Args, bexpr.Args)
        compareGenericLists (aexpr.TypeArgs, bexpr.TypeArgs)
        Expect.equal aexpr.SourceInfo bexpr.SourceInfo "SourceInfo should be equal"
    | _ -> failwith "Not an InteropCall"


let compareSignatureHints (a: ISignatureHint, b: ISignatureHint) =
    compareGenericLists (a.Args, b.Args)

    match a.GenericTypeArgs, b.GenericTypeArgs with
    | None, None -> Expect.isTrue true "Both are None"
    | None, _ -> Expect.isTrue false "First is None, second is not"
    | _, None -> Expect.isTrue false "First is not None, second is"
    | Some a, Some b -> compareGenericLists (a, b)


let compareSignatureHintOptions (a: ISignatureHint option, b: ISignatureHint option) =
    match a, b with
    | None, None -> Expect.isTrue true "Both are None"
    | None, _ -> Expect.isTrue false "First is None, second is not"
    | _, None -> Expect.isTrue false "First is not None, second is"
    | Some a, Some b -> compareSignatureHints (a, b)


let compareQualifiedMethods (a: AST, b: AST) =
    match a, b with
    | AST.QualifiedMethod(aExpr), AST.QualifiedMethod(bExpr) ->
        Expect.equal aExpr.Env bExpr.Env "Env should be equal"
        Expect.equal aExpr.Form bExpr.Form "Form should be equal"
        Expect.equal aExpr.MethodType bExpr.MethodType "Type should be equal"
        Expect.equal aExpr.TagClass bExpr.TagClass "Tag should be equal"
        compareSignatureHintOptions (aExpr.HintedSig, bExpr.HintedSig)
        Expect.equal aExpr.MethodName bExpr.MethodName "MethodName should be equal"
        Expect.equal aExpr.Kind bExpr.Kind "Kind should be equal"
        Expect.equal aExpr.SourceInfo bExpr.SourceInfo "SourceInfo should be equal"
    | _ -> failwith "Not a QualifiedMethod"


let compareNewExprs (a: AST, b: AST) =
    match a, b with
    | AST.New(a), AST.New(b) ->
        Expect.equal a.Env b.Env "Env should be equal"
        Expect.equal a.Form b.Form "Form should be equal"
        Expect.equal a.Type b.Type "Type should be equal"
        Expect.equal a.Constructor b.Constructor "Constructor should be equal"
        compareGenericLists (a.Args, b.Args)
        Expect.equal a.IsNoArgValueTypeCtor b.IsNoArgValueTypeCtor "IsNoArgValueTypeCtor should be equal"
        Expect.equal a.SourceInfo b.SourceInfo "SourceInfo should be equal"
    | _ -> failwith "Not an InteropCall"

let compareBodies (a: AST, b: AST) =
    match a, b with
    | AST.Body(aexpr), AST.Body(bexpr) ->
        compareCompilerEnvs (aexpr.Env, bexpr.Env)
        Expect.equal aexpr.Form bexpr.Form "Form should be equal"
        compareGenericLists (aexpr.Exprs, bexpr.Exprs)
    | _ -> failwith "Not a Body"


let withLocals (cenv: CompilerEnv, localNames: string array) =

    let mutable bindings = PersistentHashMap.Empty :> IPersistentMap

    for name in localNames do
        let sym = Symbol.intern name

        let binding =
            { Sym = sym
              Tag = null
              Init = None
              Name = name
              IsArg = false
              IsByRef = false
              IsRecur = false
              IsThis = false
              Index = 20 }

        bindings <- RTMap.assoc (bindings, sym, binding) :?> IPersistentMap


    { cenv with Locals = bindings }

let withMethod (cenv: CompilerEnv) =

    let register = ObjXRegister(None)
    let internals = ObjXInternals()

    let objx =
        AST.Obj(
            ObjExpr(
                env = cenv,
                form = null,
                objxType = ObjXType.Fn,
                internals = internals,
                register = register,
                sourceInfo = None
            )
        )

    let method = ObjMethod(ObjXType.Fn, objx, internals, register, None)

    let cenv =
        { cenv with
            Method = Some method
            ObjXRegister = Some register }

    cenv, method, register, internals


let createBinding (sym: Symbol, init: AST option, index: int, isRecur: bool) =
    { Sym = sym
      Tag = null
      Init = None
      Name = sym.Name
      IsArg = false
      IsByRef = false
      IsRecur = isRecur
      IsThis = false
      Index = index }

let createBindingInit (sym: Symbol, init: AST, index: int, isRecur: bool) =
    let lb =
        { Sym = sym
          Tag = null
          Init = Some init
          Name = sym.Name
          IsArg = false
          IsByRef = false
          IsRecur = isRecur
          IsThis = false
          Index = index }

    { BindingInit.Binding = lb
      Init = init }

let compareFnNames (name1: string, name2: string) = ()
// TODO: figure out how to compare names


let compareObjMethods (a: ObjMethod, b: ObjMethod) =
    Expect.equal a.IsVariadic b.IsVariadic "IsVariadic should be equal"
    Expect.equal a.NumParams b.NumParams "NumParms should be equal"
    Expect.equal a.RequiredArity b.RequiredArity "RequiredArity should be equal"
    Expect.equal a.RetType b.RetType "RetType should be equal"
    Expect.equal a.UsesThis b.UsesThis "UsesThis should be equal"

    // We can't compare Locals directly because a generated This variable will have a non-deterministic name
    //Expect.equal a.Locals b.Locals "Locals should be equal"

    compareLocalsMaps (a.Locals, b.Locals)

    Expect.equal a.LocalsUsedInCatchFinally b.LocalsUsedInCatchFinally "LocalsUsedInCatchFinally should be equal"
    Expect.equal a.RestParam b.RestParam "RestParam should be equal"
    compareGenericLists (a.ReqParams, b.ReqParams)
    compareGenericLists (a.ArgLocals, b.ArgLocals)

// I'd love to compare bodies, but the code to do this would be horrendous.
// We'd need a way to map over the context and all local bindings to catch the differences in `this` variable names.
// If we get the other factors, such as locals to match, the bodies _should_ match.
//Expect.equal a.Body b.Body "Body should be equal"


let compareObjxInternals (x: ObjXInternals, y: ObjXInternals) =

    Expect.equal x.Tag y.Tag "Tags should be equal"
    compareFnNames (x.Name, y.Name)
    compareFnNames (x.InternalName, y.InternalName)
    Expect.equal x.ThisName y.ThisName "ThisName values should be equal"
    Expect.equal x.OnceOnly y.OnceOnly "OnceOnly values should be equal"
    Expect.equal x.HasEnclosingMethod y.HasEnclosingMethod "HasEnclosingMethod values should be equal"

    Expect.equal x.Methods.Count y.Methods.Count "Method counts should be equal"

    x.Methods
    |> Seq.zip y.Methods
    |> Seq.iter (fun (a, b) -> compareObjMethods (a, b))





let compareObjExprs (ast1: AST, ast2: AST) =

    match ast1, ast2 with
    | AST.Obj(obj1), AST.Obj(obj2) ->
        Expect.equal obj1.Env obj2.Env "Should have same env"
        Expect.equal obj1.Type obj2.Type "Should have same type"
        // We do not compare Form values -- there might have been a normalization of the body
        compareObjxInternals (obj1.Internals, obj2.Internals)
        compareObjxRegisters (obj1.Register, obj2.Register)
        Expect.equal obj1.SourceInfo obj2.SourceInfo "Should have same source info"
    | _ -> failtest "Should be an Obj"
