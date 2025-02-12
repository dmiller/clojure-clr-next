namespace Clojure.Compiler

open System
open System.Collections
open System.Collections.Generic
open System.Linq
open Clojure.Collections
open Clojure.Lib
open System.Reflection
open Clojure.Reflection
open System.Text


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
        TypeArgs: ResizeArray<Type> *
        SourceInfo: SourceInfo option

    | Invoke of
        Env: CompilerEnv *
        Form: obj *
        Fexpr: Expr *
        Args: ResizeArray<Expr> *
        Tag: obj *
        SourceInfo: SourceInfo option

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
        Constructor : MethodBase *
        Args: ResizeArray<HostArg> *
        Type: Type *
        IsNoArgValueTypeCtor: bool *
        SourceInfo: SourceInfo option

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
        HintedSig: ISignatureHint option *  // Problem with circularity -- decided to define an interface to resolve
        MethodSymbol: Symbol *
        MethodName: string *
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

and ObjMethod(_type: ObjXType, _objx: Expr, _objxInternals : ObjXInternals, _objXRegsiter: ObjXRegister, _parent: ObjMethod option) =

    let mutable _body: Expr option = None // will get filled in later

    let mutable _localNum = 0

    do
        if not _objx.IsObj then
            failwith "Method must be an ObjExpr"

    member _.Parent = _parent
    member _.Objx = _objx
    member _.Type = _type
    member _.ObjxRegister = _objXRegsiter
    member _.ObjxInternals = _objxInternals

    member val RestParam: obj option = None with get, set
    member val ReqParams: ResizeArray<LocalBinding> = ResizeArray<LocalBinding>()
    member val ArgLocals: ResizeArray<LocalBinding> = ResizeArray<LocalBinding>()
    member _.Name: string = ""
    member val RetType: Type = typeof<Object> with get, set

    member _.GetAndIncLocalNum() =
        let n = _localNum
        _localNum <- _localNum + 1
        n


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

    member this.AddLocal(lb: LocalBinding) =
        this.Locals <- RTMap.assoc(this.Locals, lb, lb) :?> IPersistentMap

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
                this.CloseOver(lb, m)

                Some lb
            | _ -> Some lb   // TODO: ofiginal code here would have Null. However, this requires a let*, etc. to have a method installed.  TODO:  Can we fake a local context wihtout a method?
        | _ -> None

    member this.ContainsBindingForSym(sym: Symbol) =
        not <| isNull (RT0.get (this.Locals, sym))


    member this.CloseOver(b: LocalBinding, method: ObjMethod) =
        match RT0.get (method.Locals, b) with
        | :? LocalBinding as lb ->
            if lb.Index = 0 then
                method.UsesThis <- true

            if this.InCatchFinally then
                method.LocalsUsedInCatchFinally <- method.LocalsUsedInCatchFinally.cons (b.Index) :?> IPersistentSet
        | _ ->
            // The binding is not in the method's locals.
            // We need to close over it.
            method.ObjxRegister.AddClose(b)

            if method.Parent.IsSome then
                this.CloseOver(b, method.Parent.Value)

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

        let index = 
            match this.Method with
            | Some m -> m.GetAndIncLocalNum()
            | None -> -1 // I'm not sure we should ever call this when there is no method.

        let lb =
            { Sym = sym
              Tag = tag
              Init = init
              Name = sym.Name
              IsArg = isArg
              IsByRef = isByRef
              IsRecur = false
              IsThis = isThis
              Index = index }

        let newLocals = RTMap.assoc (this.Locals, sym, lb) :?> IPersistentMap

        match this.Method with
        | Some m -> m.AddLocal(lb)
        | None -> ()

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

    member this.AddClose(b: LocalBinding) =
        this.Closes <- RTMap.assoc (this.Closes, b, b) :?> IPersistentMap

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

    static member val EmptyTypeList = ResizeArray<Type>()

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
        | :? string as s when stringOk -> RTType.ClassForNameE(s)
        | _ -> null        

    static member MaybeArrayType(cenv:CompilerEnv, sym: Symbol) : Type =
        if isNull sym.Namespace || not <| RTType.IsPosDigit(sym.Name) then
            null
        else
            let dim = sym.Name[0] - '0' |> int
            let componentTypeName = Symbol.intern(null,sym.Namespace)
            let mutable (componentType: Type) =
                match RTType.PrimType(componentTypeName) with
                | null -> TypeUtils.MaybeType(cenv, componentTypeName, false);
                | _ as t -> t

            match componentType with 
            | null -> raise <| TypeNotFoundException($"Unable to resolve component typename: {componentTypeName}")
            | _ ->
                for i = 0 to dim-1 do
                    componentType <- componentType.MakeArrayType()
                componentType
    
    static member TagToType(cenv: CompilerEnv, tag: obj) = 
        let t = 
            match tag with 
            | :? Symbol as sym ->
                let mutable t = null
                if isNull sym.Namespace then
                    t <- RTType.MaybeSpecialTag(sym)
                if isNull t then
                    t <- TypeUtils.MaybeArrayType(cenv, sym)
                t
            | _ -> null
        let t = if isNull t then TypeUtils.MaybeType(cenv, tag, true) else t
        match t with
        | null -> raise <| ArgumentException($"Unable to resolve typename: {tag}")
        | _ -> t


    static member TagOf(o: obj) =
        match RT0.get (RT0.meta (o), RTVar.TagKeyword) with
        | :? Symbol as sym -> sym
        | :? string as str -> Symbol.intern (null, str)
        | :? Type as t ->
            let ok, sym = TypeUtils.TryGetTypeTag(t)
            if ok then sym else null
        | _ -> null


    static member TagType(cenv: CompilerEnv, tag: obj) : Type =
        match tag with
        | null -> typeof<Object>
        | :? Symbol as sym ->
            match RTType.PrimType(sym) with
            | null -> TypeUtils.TagToType(cenv, sym)
            | _ as t -> t
        | _ -> TypeUtils.TagToType(cenv, tag)


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
    static member TagsToClasses(cenv: CompilerEnv, paramTags: ISeq) =
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
                        signature.Add(TypeUtils.TagToType(cenv, t))
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
                new SignatureHint(Some typeArgs, TypeUtils.TagsToClasses(cenv, args))
            | _ ->
                new SignatureHint(None, TypeUtils.TagsToClasses(cenv, RTSeq.seq(tagV)))


    static member MaybeCreate(cenv: CompilerEnv, tagV: IPersistentVector) =
        match tagV with
        | null -> None
        | _ -> Some <| (SignatureHint.Create(cenv, tagV) :> ISignatureHint)


[<AbstractClass; Sealed>]
type QMHelpers private () =

    // Returns a list of methods or ctors matching the name and kind given.
    // Otherwise, will throw if the information provided results in no matches
    static member private MethodsWithName(t:Type, methodName: string, kind: QMMethodKind) =
        match kind with
        | Ctor ->
            let ctors = t.GetConstructors().Cast<MethodBase>().ToList()
            if ctors.Count = 0 then
                raise <| QMHelpers.NoMethodWithNameException(t, methodName, QMMethodKind.Ctor)
            ctors
        | _ ->
            let isStatic = (kind = QMMethodKind.Static)
            let methods =
                t.GetMethods()
                    .Cast<MethodBase>()
                    .Where(fun m -> m.Name.Equals(methodName) && isStatic = m.IsStatic)
                    .ToList()
            if methods.Count = 0 then
                raise <| QMHelpers.NoMethodWithNameException(t, methodName, QMMethodKind.Instance)
            methods
       
    static member ResolveHintedMethod(t:Type, methodName: string, kind: QMMethodKind, hint: ISignatureHint) = 
        
            let methods= QMHelpers.MethodsWithName(t, methodName, kind)

            // If we have generic type args and the list is non-empty, we need to choose only methods that have the same number of generic type args, fully instantiated.

            let methods = 
                let gtaCount = if hint.GenericTypeArgs.IsSome then hint.GenericTypeArgs.Value.Count else 0
                if gtaCount > 0 then
                    methods
                        .Where(fun m -> m.IsGenericMethod && m.GetGenericArguments().Length = gtaCount)
                        .Select(fun m -> (m :?> MethodInfo).MakeGenericMethod(hint.GenericTypeArgs.Value.ToArray()))
                        .Cast<MethodBase>()
                        .ToList()
                else
                    methods

            let arity =
                match hint.Args with
                | null -> 0
                | _ as args-> args.Count

            let filteredMethods =
                methods
                    .Where(fun m -> m.GetParameters().Length = arity)
                    .Where(fun m -> QMHelpers.SignatureMatches(hint.Args, m))
                    .ToList()

            if filteredMethods.Count = 1 then
                filteredMethods[0]
            else
                raise <| QMHelpers.ParamTagsDontResolveException(t, methodName, hint)

    static member private NoMethodWithNameException(t:Type, methodName: string, kind: QMMethodKind) =
        let kindStr = if kind = QMMethodKind.Ctor then "" else kind.ToString().ToLower()
        new ArgumentException($"Error - no matches found for {kindStr} {QMHelpers.MethodDescription(t, methodName)}")


    static member private ParamTagsDontResolveException(t:Type, methodName: string, hint: ISignatureHint) =
        let hintedSig = hint.Args
        let tagNames = hintedSig.Cast<Object>().Select(fun tag -> if isNull tag then RTVar.ParamTagAnySym :> obj else tag)
        let paramTags = PersistentVector.create(tagNames)
        let genericTypeArgs = if hint.GenericTypeArgs.IsSome then "" else $"<{QMHelpers.GenerateGenericTypeArgString(hint.GenericTypeArgs.Value)}>"
        new ArgumentException($"Error - param-tags {genericTypeArgs}{paramTags} insufficient to resolve {QMHelpers.MethodDescription(t, methodName)}")

    static member MethodDescription(t:Type, name: string) =
        let isCtor = t <> null && name.Equals("new")
        let typeType = if isCtor then "constructor" else "method"
        $"""{typeType} {(if isCtor then "" else name)} in class {t.Name}"""

        // TODO: THis is a duplicate of waht is Clojure.Reflection.Reflector
        // TODO: REview use of type of GenericTypeArgList vs original
    // Just a little convenience function for error messages.
    static member GenerateGenericTypeArgString(typeArgs: ResizeArray<Type>) =
        if isNull typeArgs then ""
        else
            let sb = StringBuilder()
            sb.Append("<") |> ignore
            for i = 0 to typeArgs.Count - 1 do
                if i > 0 then
                    sb.Append(", ") |> ignore
                sb.Append(typeArgs[i].Name) |> ignore
            sb.Append(">") |> ignore
            sb.ToString()


    static member SignatureMatches(signature: List<Type>, method: MethodBase) =
        let methodSig = method.GetParameters()

        let rec loop (i:int) =
            if i >= methodSig.Length then
                true
            else
                if not <| isNull signature[i] && not <| signature[i].Equals(methodSig[i].ParameterType) then
                    false
                else
                    loop (i + 1)
        loop 0






