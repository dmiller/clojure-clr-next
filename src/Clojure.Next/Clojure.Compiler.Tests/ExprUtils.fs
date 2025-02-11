module ExprUtils

open Expecto
open Clojure.Compiler
open Clojure.Collections


let honk(x: obj, y: obj) = 

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
            |> Seq.forall (fun (x, y) -> if isNull x then isNull y else honk(x,y))

        Expect.isTrue comp "Expect same list"


let compareInteropCalls (a: Expr, b: Expr) =
    match a, b with
    | Expr.InteropCall(
        Env = aenv
        Form = aform
        Type = atype
        IsStatic = aisstatic
        Tag = atag
        Target = atarget
        TargetType = atargettype
        MemberName = amembername
        TInfo = atinfo
        Args = aargs
        TypeArgs = atypeargs
        SourceInfo = asourceinfo),
      Expr.InteropCall(
          Env = benv
          Form = bform
          Type = btype
          IsStatic = bisstatic
          Tag = btag
          Target = btarget
          TargetType = btargettype
          MemberName = bmembername
          TInfo = btinfo
          Args = bargs
          TypeArgs = btypeargs
          SourceInfo = bsourceinfo) ->
        Expect.equal aenv benv "Env should be equal"
        Expect.equal aform bform "Form should be equal"
        Expect.equal atype btype "Type should be equal"
        Expect.equal aisstatic bisstatic "IsStatic should be equal"
        Expect.equal atag btag "Tag should be equal"
        Expect.equal atarget btarget "Target should be equal"
        Expect.equal atargettype btargettype "TargetType should be equal"
        Expect.equal amembername bmembername "MemberName should be equal"
        Expect.equal atinfo btinfo "TInfo should be equal"
        compareGenericLists (aargs, bargs)
        compareGenericLists (atypeargs, btypeargs)
        Expect.equal asourceinfo bsourceinfo "SourceInfo should be equal"
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


let compareQualifiedMethods(a: Expr, b:Expr) =
    match a, b with
    | Expr.QualifiedMethod(
        Env = aenv
        Form = aform
        MethodType = atype
        HintedSig = asig
        TagClass = atag
        MethodName = aemethodname
        Kind = akind
        SourceInfo = asourceinfo),
      Expr.QualifiedMethod(
          Env = benv
          Form = bform
          HintedSig = bsig
          MethodType = btype
          TagClass = btag
          MethodName = bemethodname
          Kind = bkind
          SourceInfo = bsourceinfo) ->
        Expect.equal aenv benv "Env should be equal"
        Expect.equal aform bform "Form should be equal"
        Expect.equal atype btype "Type should be equal"
        Expect.equal atag btag "Tag should be equal"
        compareSignatureHintOptions (asig, bsig)
        Expect.equal aemethodname bemethodname "MethodName should be equal"
        Expect.equal akind bkind "Kind should be equal"
        Expect.equal asourceinfo bsourceinfo "SourceInfo should be equal"
    | _ -> failwith "Not a QualifiedMethod"


let compareNewExprs (a: Expr, b: Expr) =
    match a, b with
    | Expr.New(
        Env = aenv
        Form = aform
        Type = atype
        Constructor = aconstructor
        Args = aargs
        IsNoArgValueTypeCtor = aisnoargvaluetypector
        SourceInfo = asourceinfo),
      Expr.New(
          Env = benv
          Form = bform
          Type = btype
          Constructor = bconstructor
          Args = bargs
          IsNoArgValueTypeCtor = bIsNoArgValueTypeCtor
          SourceInfo = bsourceinfo) ->
        Expect.equal aenv benv "Env should be equal"
        Expect.equal aform bform "Form should be equal"
        Expect.equal atype btype "Type should be equal"
        Expect.equal aconstructor bconstructor "Constructor should be equal"
        compareGenericLists (aargs, bargs)
        Expect.equal aisnoargvaluetypector bIsNoArgValueTypeCtor "IsNoArgValueTypeCtor should be equal"
        Expect.equal asourceinfo bsourceinfo "SourceInfo should be equal"
    | _ -> failwith "Not an InteropCall"

let compareBodies(a: Expr, b: Expr) = 
    match a,b with
    | Expr.Body(aenv, aform, abody),
      Expr.Body(benv, bform, bbody) ->
        Expect.equal aenv benv "Env should be equal"
        Expect.equal aform bform "Form should be equal"
        compareGenericLists (abody, bbody)
    | _ -> failwith "Not a Body"


let withLocals(cenv: CompilerEnv, localNames: string array) =

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
        bindings <- RTMap.assoc(bindings, sym, binding) :?> IPersistentMap


    { cenv with Locals = bindings }

let withMethod(cenv: CompilerEnv ) = 

    let register = ObjXRegister(None)
    let internals = ObjXInternals()

    let objx =
        Expr.Obj(
            Env = cenv,
            Form = null,
            Type = ObjXType.Fn,
            Internals = internals,
            Register = register,
            SourceInfo = None
        )

    let method = ObjMethod(ObjXType.Fn, objx, internals, register, None)

    let cenv =
        { cenv with
            Method = Some method
            ObjXRegister = Some register }

    cenv, method, register, internals


let createBinding(sym: Symbol, init: Expr option, index: int, isRecur: bool) =
    { Sym = sym
      Tag = null
      Init = None
      Name = sym.Name
      IsArg = false
      IsByRef = false
      IsRecur = isRecur
      IsThis = false
      Index = index }

let createBindingInit(sym: Symbol, init: Expr, index: int, isRecur: bool) =
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

    {BindingInit.Binding = lb; Init = init }
