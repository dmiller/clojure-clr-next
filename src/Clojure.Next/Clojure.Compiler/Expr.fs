namespace Clojure.Compiler

open System
open System.Collections
open System.Collections.Generic
open Clojure.Collections
open Clojure.Lib
open System.Reflection
open Clojure.Reflection


type SourceInfo =
    { Source: string
      Line: int
      Column: int
      EndLine: int
      EndColumn: int }


type ParserContext =
    | Expression
    | Statement
    | Return

type LetExprMode =
    | Let
    | Loop
    | LetFn

type ParameterType =
    | Standard
    | ByRef

type UntypedExprType =
    | MonitorEnter
    | MonitorExit
    | Throw

type LiteralType =
    | NilType
    | BoolType
    | StringType
    | PrimNumericType
    | KeywordType
    | EmptyType
    | OtherType

type HostExprType =
    | FieldOrPropertyExpr
    | MethodExpr
    | InstanceZeroArityCallExpr

type ObjXType =
    | Fn
    | NewInstance

type QMMethodKind =
    | Static
    | Instance
    | Ctor

type ProtocolDetails =
    { ProtocolOn: Type
      OnMethod: MethodInfo
      SiteIndex: int }

[<RequireQualifiedAccess; NoComparison>]
type Expr =

    | Assign of Env: CompilerEnv * Form: obj * Target: Expr * Value: Expr

    | Body of Env: CompilerEnv * Form: obj * Exprs: ResizeArray<Expr>

    | Case of
        Env: CompilerEnv *
        Form: obj *
        Expr: Expr *
        DefaultExpr: Expr *
        Shift: int *
        Mask: int *
        Tests: SortedDictionary<int, Expr> *
        Thens: SortedDictionary<int, Expr> *
        SwitchType: Keyword *
        TestType: Keyword *
        SkipCheck: IPersistentSet *
        ReturnType: Type

    | Collection of  // combines MapExpr, SetExpr, VectorExpr
        Env: CompilerEnv *
        Form: obj *
        Value: obj

    | Def of
        Env: CompilerEnv *
        Form: obj *
        Var: Var *
        Init: Expr *
        Meta: Expr option *
        InitProvided: bool *
        IsDynamic: bool *
        ShadowsCoreMapping: bool *
        SourceInfo: SourceInfo option

    | If of Env: CompilerEnv * Form: obj * Test: Expr * Then: Expr * Else: Expr * SourceInfo: SourceInfo option

    | Import of Env: CompilerEnv * Form: obj * Typename: string

    | InstanceOf of Env: CompilerEnv * Form: obj * Expr: Expr * Type: Type * SourceInfo: SourceInfo option

    | InteropCall of
        Env: CompilerEnv *
        Form: obj *
        Type: HostExprType *
        IsStatic: bool *
        Tag: Symbol *
        Target: Expr option *
        TargetType: Type *
        MemberName: string *
        TInfo: MemberInfo *
        Args: ResizeArray<HostArg> *
        TypeArgs: ResizeArray<Type> option *
        SourceInfo: SourceInfo option

    | Invoke of
        Env: CompilerEnv *
        Form: obj *
        Fexpr: Expr *
        Args: ResizeArray<Expr> *
        Tag: obj *
        TailPosition: bool *
        ProtocolDetails: ProtocolDetails option *
        SourceInfo: SourceInfo

    | KeywordInvoke of
        Env: CompilerEnv *
        Form: obj *
        KwExpr: Expr *
        Target: Expr *
        Tag: obj *
        SiteIndex: int *
        SourceInfo: SourceInfo option

    | Let of  // combines LetExpr, LoopExpr, LetFnExpr
        Env: CompilerEnv *
        Form: obj *
        Mode: LetExprMode *
        BindingInits: ResizeArray<BindingInit> *
        Body: Expr *
        LoopId: int option *
        SourceInfo: SourceInfo option

    | Literal of  // Combines ConstExpr, NumberExpr, NilExpr, StringExpr, BoolExpr , KeywordExpr, EmptyExpr
        Env: CompilerEnv *
        Form: obj *
        Type: LiteralType *
        Value: obj

    | LocalBinding of Env: CompilerEnv * Form: obj * Binding: LocalBinding * Tag: Symbol

    | Meta of Env: CompilerEnv * Form: obj * Target: Expr * Meta: Expr

    | Method of Env: CompilerEnv * Form: obj * Args: ResizeArray<HostArg>

    | New of
        Env: CompilerEnv *
        Form: obj *
        Args: ResizeArray<HostArg> *
        Type: Type *
        IsNoArgValueTypeCtor: bool *
        SourceInfo: SourceInfo

    | Obj of
        Env: CompilerEnv *
        Form: obj *
        Type: ObjXType *
        Internals: ObjXInternals *
        Register: ObjXRegister *
        SourceInfo: SourceInfo option

    | QualifiedMethod of
        Env: CompilerEnv *
        Form: obj *
        MethodType: Type *
        HintedSig: ISignatureHint *
        MethodSymbol: Symbol *
        Kind: QMMethodKind *
        TagClass: Type *
        SourceInfo: SourceInfo option

    | Recur of
        Env: CompilerEnv *
        Form: obj *
        Args: ResizeArray<Expr> *
        LoopLocals: ResizeArray<LocalBinding> *
        SourceInfo: SourceInfo option

    | StaticInvoke of
        Env: CompilerEnv *
        Form: obj *
        Target: Type *
        Method: MethodInfo *
        RetType: Type *
        Args: ResizeArray<Expr> *
        IsVariadic: bool *
        Tag: obj

    | TheVar of Env: CompilerEnv * Form: obj * Var: Var

    | Try of Env: CompilerEnv * Form: obj * TryExpr: Expr * Catches: ResizeArray<CatchClause> * Finally: Expr option

    | UnresolvedVar of Env: CompilerEnv * Form: obj * Sym: Symbol

    | Var of Env: CompilerEnv * Form: obj * Var: Var * Tag: obj

    | Untyped of  // combines MonitorEnterExpr, MonitorExitExpr, ThrowExpr
        Env: CompilerEnv *
        Form: obj *
        Type: UntypedExprType *
        Target: Expr option

//and ObjBaseDetails =
//    { RequiredArity: int
//      RestArg: string option
//      IsVariadic: bool
//      PrePostMeta: ResizeArray<Expr> }

and BindingInit = { Binding: LocalBinding; Init: Expr }

and LocalBinding =
    { Sym: Symbol
      Tag: obj
      mutable Init: Expr option // Needs to be mutable for LetFn -- we have to create all bindings, parse the inits, then go back and fill in the inits.
      Name: string
      IsArg: bool
      IsByRef: bool
      IsRecur: bool
      IsThis: bool
      Index: int }

and CatchClause =
    { CaughtType: Type
      LocalBinding: LocalBinding
      Handler: Expr }

and HostArg =
    { ParamType: ParameterType
      ArgExpr: Expr
      LocalBinding: LocalBinding option }

and ISignatureHint = 
    abstract member GenericTypeArgs: ResizeArray<Type> option
    abstract member Args: ResizeArray<Type>
    abstract member ArgCount: int
    abstract member HasGenericTypeArgs: bool

and ObjMethod(_type: ObjXType, _objx: Expr, _parent: ObjMethod option) =

    let mutable _body: Expr option = None // will get filled in later

    do
        if not _objx.IsObj then
            failwith "Method must be an ObjExpr"

    member _.Parent = _parent
    member _.Objx = _objx
    member _.Type = _type

    member val RestParam: obj option = None with get, set
    member _.ReqParams: ResizeArray<LocalBinding> = ResizeArray<LocalBinding>()
    member _.ArgLocals: ResizeArray<LocalBinding> = ResizeArray<LocalBinding>()
    member _.Name: string = ""
    member val RetType: Type = typeof<Object> with get, set


    member val Locals: IPersistentMap = null with get, set
    member val UsesThis = false with get, set
    member val LocalsUsedInCatchFinally: IPersistentSet = PersistentHashSet.Empty with get, set

    member _.Body
        with get () =
            match _body with
            | Some b -> b
            | None -> failwith "Body not yet set"
        and set b = _body <- Some b


    member this.IsVariadic =
        match _type with
        | Fn -> this.RestParam.IsSome
        | NewInstance -> false

    member this.NumParams =
        match _type with
        | Fn -> this.ReqParams.Count + (if this.IsVariadic then 1 else 0)
        | NewInstance -> this.ArgLocals.Count

    member this.RequiredArity =
        match _type with
        | Fn -> this.ReqParams.Count
        | NewInstance -> this.ArgLocals.Count

    member this.MethodName =
        match _type with
        | Fn -> if this.IsVariadic then "doInvoke" else "invoke"
        | NewInstance -> this.Name

    member this.ReturnType = this.RetType // FNMethod had a check of prim here with typeof(Object) as default.
//member _.ArgTypes =

and CompilerEnv =
    { Pctx: ParserContext
      Locals: IPersistentMap
      Method: ObjMethod option
      IsAssignContext: bool
      IsRecurContext: bool // TOOD: can we just check the loop id != None?
      InTry: bool
      InCatchFinally: bool
      LoopId: int option
      LoopLocals: ResizeArray<LocalBinding>
      NoRecur: bool
      ObjXRegister: ObjXRegister option }

    // Locals = Map from Symbol to LocalBinding

    static member Create(ctx: ParserContext) =
        { Pctx = ctx
          Locals = PersistentHashMap.Empty
          Method = None
          IsAssignContext = false
          IsRecurContext = false
          InTry = false
          InCatchFinally = false
          LoopId = None
          LoopLocals = null
          NoRecur = false
          ObjXRegister = None }

    static member val Empty = CompilerEnv.Create(ParserContext.Expression)

    member this.IsExpr = this.Pctx = ParserContext.Expression
    member this.IsStmt = this.Pctx = ParserContext.Statement
    member this.IsReturn = this.Pctx = ParserContext.Return

    member this.WithParserContext(ctx: ParserContext) = { this with Pctx = ctx }

    member this.ReferenceLocal(sym: Symbol) =
        match RT0.get (this.Locals, sym) with
        | :? LocalBinding as lb ->
            match this.Method with
            | Some m ->
                if lb.Index = 0 then
                    m.UsesThis <- true
                    this.ClosesOver(lb, m)

                Some lb
            | _ -> None
        | _ -> None

    member this.ContainsBindingForSym(sym: Symbol) =
        not <| isNull (RT0.get (this.Locals, sym))


    member this.ClosesOver(b: LocalBinding, method: ObjMethod) =
        match RT0.get (method.Locals, b) with
        | :? LocalBinding as lb ->
            if lb.Index = 0 then
                method.UsesThis <- true

            if this.InCatchFinally then
                method.LocalsUsedInCatchFinally <- method.LocalsUsedInCatchFinally.cons (b.Index) :?> IPersistentSet
        | _ ->
            // The binding is not already in the method's locals
            // Add it, then move up the chain
            method.Locals <- RTMap.assoc (method.Locals, b, b) :?> IPersistentMap

            if method.Parent.IsSome then
                this.ClosesOver(b, method.Parent.Value)

    // Bindings and registrations
    member this.RegisterLocalThis(sym: Symbol, tag: Symbol, init: Expr option) =
        this.RegisterLocalInternal(sym, tag, init, typeof<Object>, true, false, false)

    member this.RegisterLocal(sym: Symbol, tag: Symbol, init: Expr option, declaredType: Type, isArg: bool) =
        this.RegisterLocalInternal(sym, tag, init, declaredType, false, isArg, false)

    member this.RegisterLocal
        (
            sym: Symbol,
            tag: Symbol,
            init: Expr option,
            declaredType: Type,
            isArg: bool,
            isByRef: bool
        ) =
        this.RegisterLocalInternal(sym, tag, init, declaredType, false, isArg, false)

    member private this.RegisterLocalInternal
        (
            sym: Symbol,
            tag: Symbol,
            init: Expr option,
            declaredType: Type,
            isThis: bool,
            isArg: bool,
            isByRef: bool
        ) =
        if isThis && this.Locals.count () > 0 then
            failwith "Registration of 'this' must precede other locals"

        let lb =
            { Sym = sym
              Tag = tag
              Init = init
              Name = sym.Name
              IsArg = isArg
              IsByRef = isByRef
              IsRecur = false
              IsThis = isThis
              Index = this.Locals.count () }

        let newLocals = RTMap.assoc (this.Locals, sym, lb) :?> IPersistentMap
        { this with Locals = newLocals }, lb


    member this.RegisterVar(v: Var) =
        match this.ObjXRegister with
        | Some r -> r.RegisterVar(v)
        | None -> ()

    member this.RegisterConstant(o: obj) =
        match this.ObjXRegister with
        | Some r -> r.RegisterConstant(o)
        | None -> -1

    member this.RegisterKeyword(kw: Keyword) =
        match this.ObjXRegister with
        | Some r -> r.RegisterKeyword(kw)
        | None -> ()

    member this.RegisterKeywordCallSite(kw: Keyword) =
        match this.ObjXRegister with
        | Some r -> r.RegisterKeywordCallsite(kw)
        | None ->
            raise
            <| new InvalidOperationException("ObjXRegister is not bound in envinroment")


    member this.RegisterProtoclCallsite(v: Var) =
        match this.ObjXRegister with
        | Some r -> r.RegisterProtocolCallsite(v)
        | None ->
            raise
            <| new InvalidOperationException("ObjXRegister is not bound in envinroment")


and ObjXInternals() =

    member val Tag: obj = null with get, set
    member val Name: string = null with get, set
    member val InternalName: string = null with get, set
    member val ThisName: string = null with get, set
    member val OnceOnly: bool = false with get, set
    member val HasEnclosingMethod: bool = false with get, set
    member val Methods: List<ObjMethod> = null with get, set
    member val VariadicMethod: ObjMethod option = None with get, set


and ObjXRegister(parent: ObjXRegister option) =

    let mutable _parent = parent

    static member val ReferenceEqualityComparer: IEqualityComparer<obj> =
        { new Object() with
            override _.ToString() = "#<Default comparer>"
          interface IEqualityComparer<obj> with
              member _.Equals(x, y) = Object.ReferenceEquals(x, y)
              member _.GetHashCode(x) = x.GetHashCode()
              //member _.compare(x, y) = Util.compare(x,y) -- can't do this. needed for core.clj compatbility -- we can get around this when we get there
               }

    static member NewIdentityHashMap() =
        new Dictionary<obj, int>(ObjXRegister.ReferenceEqualityComparer)

    member this.Parent
        with get () = _parent
        and set p = _parent <- p

    member val Closes: IPersistentMap = PersistentHashMap.Empty with get, set
    member val Constants: IPersistentVector = PersistentVector.Empty with get, set
    member val ConstantIds: Dictionary<obj, int> = ObjXRegister.NewIdentityHashMap() with get, set
    member val Keywords: IPersistentMap = PersistentHashMap.Empty with get, set
    member val Vars: IPersistentMap = PersistentHashMap.Empty with get, set
    member val KeywordCallsites: IPersistentVector = PersistentVector.Empty with get, set
    member val ProtocolCallsites: IPersistentVector = PersistentVector.Empty with get, set
    member val VarCallsites: IPersistentSet = PersistentHashSet.Empty with get, set

    member this.RegisterConstant(o: obj) =
        let v = this.Constants
        let ids = this.ConstantIds
        let ok, i = ids.TryGetValue(o)

        if ok then
            i
        else
            let count = v.count ()
            let newV = RTSeq.conj (v, o)
            this.Constants <- newV :?> IPersistentVector
            this.ConstantIds[o] <- count
            count

    member this.RegisterVar(v: Var) =
        let varsMap = this.Vars

        let id = RT0.get (varsMap, v)

        if isNull id then
            let newVarsMap = RTMap.assoc (varsMap, v, this.RegisterConstant(v))
            this.Vars <- newVarsMap :?> IPersistentMap

    member this.RegisterKeyword(kw: Keyword) =
        let kwMap = this.Keywords
        let id = RT0.get (kwMap, kw)

        if isNull id then
            let newKwMap = RTMap.assoc (kwMap, kw, this.RegisterConstant(kw))
            this.Keywords <- newKwMap :?> IPersistentMap

    member this.RegisterKeywordCallsite(kw: Keyword) =
        let sites = this.KeywordCallsites
        this.KeywordCallsites <- sites.cons (kw)
        this.KeywordCallsites.count () - 1

    member this.RegisterProtocolCallsite(v: Var) =
        let sites = this.ProtocolCallsites
        this.ProtocolCallsites <- sites.cons (v)
        this.ProtocolCallsites.count () - 1

    member this.RegisterVarCallsite(v: Var) =
        let sites = this.VarCallsites :> IPersistentCollection
        this.VarCallsites <- sites.cons (v) :?> IPersistentSet



[<AbstractClass; Sealed>]
type ExprUtils private () =

    static member GetLiteralValue(e: Expr) =
        match e with
        | Expr.Literal(Value = value) -> value
        | _ -> failwith "Not a literal expression"


[<AbstractClass; Sealed>]
type TypeUtils private () =

    static let TypeToTagDict = Dictionary<Type, Symbol>()

    static do
        TypeToTagDict.Add(typeof<bool>, Symbol.intern (null, "bool"))
        TypeToTagDict.Add(typeof<char>, Symbol.intern (null, "char"))
        TypeToTagDict.Add(typeof<byte>, Symbol.intern (null, "byte"))
        TypeToTagDict.Add(typeof<sbyte>, Symbol.intern (null, "sbyte"))
        TypeToTagDict.Add(typeof<int16>, Symbol.intern (null, "short"))
        TypeToTagDict.Add(typeof<uint16>, Symbol.intern (null, "ushort"))
        TypeToTagDict.Add(typeof<int32>, Symbol.intern (null, "int"))
        TypeToTagDict.Add(typeof<uint32>, Symbol.intern (null, "uint"))
        TypeToTagDict.Add(typeof<int64>, Symbol.intern (null, "long"))
        TypeToTagDict.Add(typeof<uint64>, Symbol.intern (null, "ulong"))
        TypeToTagDict.Add(typeof<single>, Symbol.intern (null, "float"))
        TypeToTagDict.Add(typeof<float>, Symbol.intern (null, "long"))
        TypeToTagDict.Add(typeof<decimal>, Symbol.intern (null, "decimal"))

    static member TryGetTypeTag(t:Type) = TypeToTagDict.TryGetValue(t)

    static member TagOf(o: obj) =
        match RT0.get (RT0.meta (), RTVar.TagKeyword) with
        | :? Symbol as sym -> sym
        | :? string as str -> Symbol.intern (null, str)
        | :? Type as t ->
            let ok, sym = TypeUtils.TryGetTypeTag(t)
            if ok then sym else null
        | _ -> null


    static member TagType(tag: obj) : Type =
        match tag with
        | null -> typeof<Object>
        | :? Symbol as sym ->
            match RTType.PrimType(sym) with
            | null -> RTType.TagToType(sym)
            | _ as t -> t
        | _ -> RTType.TagToType(tag)


    static member SigTag(argCount: int, v: Var) = 

        let rec loop (s:ISeq) =
            if isNull s then null
            else 
                let signature : APersistentVector = s.first() :?> APersistentVector
                let restOffset = (signature :> IList).IndexOf(RTVar.AmpersandSym)
                if argCount = (signature :> Counted).count() || (restOffset >= 0 && argCount >= restOffset) then
                    TypeUtils.TagOf(signature)
                else
                    loop (s.next())

        let arglists = RT0.get(RT0.meta(v), RTVar.ArglistsKeyword)
        loop (RT0.seq(arglists))




    static member MaybeType(cenv: CompilerEnv, form: obj, stringOk: bool) =
        match form with
        | :? Type as t -> t
        | :? Symbol as sym ->
            if isNull sym.Namespace then
                // TODO: Original code has check of CompilerStubSymVar and CompilerStubClassVar here -- are we going to need this?
                if
                    sym.Name.IndexOf('.') > 0
                    || sym.Name.Length > 0 && sym.Name[sym.Name.Length - 1] = ']'
                then
                    RTType.ClassForNameE(sym.Name)
                else
                    match RTVar.getCurrentNamespace().getMapping (sym) with
                    | :? Type as t -> t
                    | _ when cenv.ContainsBindingForSym(sym) -> null
                    | _ ->
                        try
                            RTType.ClassForName(sym.Name)
                        with _ ->
                            null
            else
                null
        | _ when stringOk && (form :? string) -> RTType.ClassForName(form :?> string)
        | _ -> null

    static member CreateTypeArgList(cenv: CompilerEnv, targs: ISeq) =
        let types = ResizeArray<Type>()

        let rec loop (s:ISeq) =
            match s with
            | null -> ()
            | _ ->
                let arg = s.first()
                if not <| arg :? Symbol then
                    raise <| ArgumentException("Malformed generic method designator: type arg must be a Symbol")
                let t = TypeUtils.MaybeType(cenv, arg, false)
                if isNull t then
                    raise <| ArgumentException($"Malformed generic method designator: invalid type arg: {arg}")
                types.Add(TypeUtils.MaybeType(cenv, s.first(), false))
                loop (s.next())

        loop targs
        types

    // calls TagToType on every element, unless it encounters _ which becomes null
    static member TagsToClasses(paramTags: ISeq) =
        if isNull paramTags then null
        else
            let signature = ResizeArray<Type>()
            let rec loop (s:ISeq) =
                match s with
                | null -> ()
                | _ ->
                    let t = s.first()
                    if t.Equals(RTVar.ParamTagAnySym) then
                        signature.Add(null)
                    else
                        signature.Add(RTType.TagToType(t))
                    loop (s.next())
            loop paramTags
            signature

                  
and SignatureHint private (_genericTypeArgs: ResizeArray<Type> option, _args: ResizeArray<Type>) =

    interface ISignatureHint with
        member _.HasGenericTypeArgs = _genericTypeArgs.IsSome
        member _.GenericTypeArgs = _genericTypeArgs
        member _.Args = _args
        member _.ArgCount = _args.Count


    static member private Create(cenv: CompilerEnv, tagV: IPersistentVector) =
        // tagV is not null (though I'll check anyway), but might be empty.
        // tagV == []  -> no type-args, zero-argument method or property or field.
        if isNull tagV || tagV.count() = 0 then
            new SignatureHint(None, ResizeArray<Type>())
        else

            let isTypeArgs(item: obj) =
                match item with
                | :? Symbol as sym  -> sym.Equals(RTVar.TypeArgsSym)
                | _ -> false

            match tagV.nth(0) with
            | :? ISeq as firstItem when isTypeArgs(RTSeq.first(firstItem)) ->
                let typeArgs = TypeUtils.CreateTypeArgList(cenv, RTSeq.next(firstItem))
                let args = RTSeq.next(tagV)
                new SignatureHint(Some typeArgs, TypeUtils.TagsToClasses(args))
            | _ ->
                new SignatureHint(None, TypeUtils.TagsToClasses(RTSeq.seq(tagV)))


    static member MaybeCreate(cenv: CompilerEnv, tagV: IPersistentVector) =
        match tagV with
        | null -> None
        | _ -> Some <| SignatureHint.Create(cenv, tagV)
