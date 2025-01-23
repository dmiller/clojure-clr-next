namespace Clojure.Compiler

open Clojure.Collections
open Clojure.Numerics
open System
open System.Collections.Generic
open Clojure.IO
open Clojure.Reflection
open System.Reflection
open Clojure.Lib



[<Sealed;AbstractClass>]
type Compiler private () =

    static let ConstKeyword = Keyword.intern(null,"const")


    static let TypeToTagDict = Dictionary<Type, Symbol>()

    static do 
        TypeToTagDict.Add(typeof<bool>, Symbol.intern(null,"bool"))
        TypeToTagDict.Add(typeof<char>, Symbol.intern(null,"char"))
        TypeToTagDict.Add(typeof<byte>, Symbol.intern(null,"byte"))
        TypeToTagDict.Add(typeof<sbyte>, Symbol.intern(null,"sbyte"))
        TypeToTagDict.Add(typeof<int16>, Symbol.intern(null,"short"))
        TypeToTagDict.Add(typeof<uint16>, Symbol.intern(null,"ushort"))
        TypeToTagDict.Add(typeof<int32>, Symbol.intern(null,"int"))
        TypeToTagDict.Add(typeof<uint32>, Symbol.intern(null,"uint"))       
        TypeToTagDict.Add(typeof<int64>, Symbol.intern(null,"long"))
        TypeToTagDict.Add(typeof<uint64>, Symbol.intern(null,"ulong"))
        TypeToTagDict.Add(typeof<single>, Symbol.intern(null,"float"))
        TypeToTagDict.Add(typeof<float>, Symbol.intern(null,"long"))
        TypeToTagDict.Add(typeof<decimal>, Symbol.intern(null,"decimal"))







    static member val NilExprInstance = LiteralExpr({Type=NilType; Value=null})
    static member val TrueExprInstance = LiteralExpr({Type=BoolType; Value=true})
    static member val FalseExprInstance = LiteralExpr({Type=BoolType; Value=false})

    static member Analyze(cctx: CompilerContext, form: obj) : Expr =

        try         
            // If we have a LazySeq, realize it and  attach the meta data from the initial form
            let form = 
                match form with
                | :? LazySeq as ls -> 
                    let realized = 
                        match RT0.seq(ls) with 
                        | null -> PersistentList.Empty :> obj
                        | _ as s -> s
                    (realized :?> IObj).withMeta(RT0.meta(form)) :> obj
                | _ -> form

            match form with
            | null -> Compiler.NilExprInstance
            | :? bool as b -> if b then Compiler.TrueExprInstance else Compiler.FalseExprInstance
            | :? Symbol as sym -> Compiler.AnalyzeSymbol(cctx,sym)
            | :? Keyword as kw -> LiteralExpr({Type=KeywordType; Value=kw})       // we'll have to walk to get keywords
            | _ when Numbers.IsNumeric(form) -> Compiler.AnalyzeNumber(cctx,form)
            | :? String as s -> LiteralExpr({Type=StringType; Value=String.Intern(s)})
            | :? IPersistentCollection as coll 
                    when not <| form :? IType && 
                         not <| form :? IRecord && 
                         coll.count() = 0  -> Compiler.OptionallyGenerateMetaInit(cctx, form, LiteralExpr({Type = EmptyType; Value = coll}))
            | :? ISeq -> Compiler.AnalyzeSeq(cctx, form)
            | :? IPersistentVector as pv-> Compiler.AnalyzeVector(cctx, pv)
            | :? IRecord -> LiteralExpr({Type=OtherType; Value=form})
            | :? IType -> LiteralExpr({Type=OtherType; Value=form})
            | :? IPersistentMap as pm -> Compiler.AnalyzeMap(cctx, pm)
            | :? IPersistentSet  as ps -> Compiler.AnalyzeSet(cctx, ps)
            | _ -> LiteralExpr({Type=OtherType; Value=form})

        with 
        | :? CompilerException -> reraise()
        |  _ as e -> raise <| CompilerException("HELP!", e)  // TODO: add source info


    // Let's start with something easy

    static member OptionallyGenerateMetaInit(cctx: CompilerContext, form: obj, expr: Expr) : Expr =
        match RT0.meta(form) with
        | null -> expr
        | _ as meta -> MetaExpr({Ctx = cctx; Target = expr; Meta = Compiler.AnalyzeMap(cctx, meta)})

    static member AnalyzeNumber(cctx: CompilerContext, num: obj) : Expr =
        match num with
        | :? int | :? double | :? int64 -> LiteralExpr({Type=PrimNumericType; Value = num})  // TODO: Why would we not allow other primitive numerics here?
        | _ -> LiteralExpr({Type=OtherType; Value=num})


    // Analyzing the collection types
    // These are quite similar.

    static member AnalyzeVector(cctx: CompilerContext, form: IPersistentVector) : Expr =
        let mutable constant = true
        let mutable args = PersistentVector.Empty :> IPersistentVector

        for i = 0 to form.count() - 1 do
            let v = Compiler.Analyze(cctx, form.nth(i))
            args <- args.cons(v)
            if not <| v.IsLiteralExpr then constant <- false

        let ret =  CollectionExpr({Ctx = cctx; Value = args})

        match form with
        | :? IObj as iobj when not <| isNull (iobj.meta()) -> Compiler.OptionallyGenerateMetaInit(cctx, form, ret)
        | _ when constant -> 
            let mutable rv = PersistentVector.Empty :> IPersistentVector
            for i = 0 to args.count() - 1 do
                rv <- rv.cons(ExprUtils.GetLiteralValue(args.nth(i) :?> Expr))
            LiteralExpr({Type = OtherType; Value = rv})
        | _ -> ret


    static member AnalyzeMap(cctx: CompilerContext, form: IPersistentMap) : Expr =
        let mutable keysConstant = true
        let mutable valsConstant = true
        let mutable allConstantKeysUnique = true

        let mutable constantKeys = PersistentHashSet.Empty :> IPersistentSet
        let mutable keyvals = PersistentVector.Empty :> IPersistentVector

        let rec loop (s:ISeq) =
            match s with
            | null -> ()
            | _ -> 
                let me = s.first() :?> IMapEntry
                let k = Compiler.Analyze(cctx, me.key())
                let v = Compiler.Analyze(cctx, me.value())
                keyvals <- keyvals.cons(k).cons(v)
                if k.IsLiteralExpr then
                    let kval = ExprUtils.GetLiteralValue(k)
                    if constantKeys.contains(kval) then allConstantKeysUnique <- false
                    else constantKeys <- constantKeys.cons(kval) :?> IPersistentSet
                else
                    keysConstant <- false

                if not <| v.IsLiteralExpr then valsConstant <- false
                loop(s.next())
        loop(RT0.seq(form))

        let ret = CollectionExpr({Ctx = cctx; Value = keyvals})

        match form with
        | :? IObj as iobj when not <| isNull (iobj.meta()) -> Compiler.OptionallyGenerateMetaInit(cctx, form, ret)
        | _ when keysConstant -> 
            if not allConstantKeysUnique then raise <| ArgumentException("Duplicate constant keys in map")
            if valsConstant then
                let mutable m = PersistentArrayMap.Empty :> IPersistentMap

                for i in 0 .. 2 .. (keyvals.count()-1)  do
                    m <- m.assoc(ExprUtils.GetLiteralValue(keyvals.nth(i) :?> Expr), ExprUtils.GetLiteralValue(keyvals.nth(i+1) :?> Expr))
                LiteralExpr({Type = OtherType; Value = m})   // TODO: In the old code, this optimization was a big fail when some values were also maps.  Need to think about this.
            else 
                ret
        | _ -> ret


    static member AnalyzeSet(cctx: CompilerContext, form: IPersistentSet) : Expr =
        let mutable constant = true
        let mutable keys = PersistentVector.Empty :> IPersistentVector

        let rec loop (s:ISeq) =
            match s with
            | null -> ()
            | _ -> 
                let k = Compiler.Analyze(cctx, s.first())
                keys <- keys.cons(k)
                if not <| k.IsLiteralExpr then constant <- false
                loop(s.next())
        loop(form.seq())

        let ret = CollectionExpr({Ctx = cctx; Value = keys})

        match form with
        | :? IObj as iobj when not <| isNull (iobj.meta()) -> Compiler.OptionallyGenerateMetaInit(cctx, form, ret)
        | _ when constant -> 
            let mutable rv = PersistentHashSet.Empty :> IPersistentCollection
            let x : Expr = ret
            for i = 0 to keys.count() - 1 do
                rv <- rv.cons(ExprUtils.GetLiteralValue(keys.nth(i) :?> Expr))
            LiteralExpr({Type = OtherType; Value = rv})
        | _ -> ret


    // Time to get into the monsters

    static member AnalyzeSymbol(cctx: CompilerContext, sym: Symbol) : Expr =
        
        let tag = Compiler.TagOf(sym)


        // See if we are a local variable or a field/property/QM reference
        let maybeExpr : Expr option = 
            if isNull sym.Namespace then
                // we might be a local variable
                match cctx.GetLocalBinding(sym) with
                | Some lb -> Some(LocalBindingExpr({Ctx = cctx; Binding = lb; Tag = tag}))
                | None -> None

            elif isNull (RTReader.NamespaceFor(sym)) && not <| RTType.IsPosDigit(sym.Name) then
    
                // we have a namespace, coudl be Typename/Field
                let nsSym = Symbol.intern(sym.Namespace)
                match Compiler.MaybeType(cctx,nsSym,false) with
                | null -> None
                | _ as t -> 
                    let info = Reflector.GetFieldOrPropertyInfo(t,sym.Name,true)
                    if not <| isNull info then
                        Some(Compiler.CreateStaticFieldOrPropertyExpr(cctx,tag, t, sym.Name, info))
                    else Some(QualifiedMethodExpr({Ctx = cctx; SourceInfo = None}))  // TODO: Implement QualifiedMethodExpr)

            else 
                None

        match maybeExpr with 
        | Some e -> e
        | None ->
            match Compiler.Resolve(sym) with
            | :? Var as v -> 
                if not <| isNull (Compiler.IsMacro(cctx, v)) then 
                    raise <| CompilerException($"Can't take the value of a macro: {sym.Name}")
                elif RT0.booleanCast(RT0.get((v :> IMeta).meta(),ConstKeyword))then 
                    Compiler.Analyze(cctx.WithParserContext(Expression), RTSeq.list(LispReader.QuoteSym, v))
                else VarExpr({Ctx = cctx; Var = v; Tag = tag})
            | :? Type ->
                LiteralExpr({Type=OtherType; Value = sym;})
            | :? Symbol ->
                UnresolvedVarExpr({Ctx = cctx; Sym = sym})
            | _ -> raise <| CompilerException($"Unable to resolve symbol: {sym} in this context")
                
                    

            


    static member AnalyzeSeq(cctx: CompilerContext, form: obj) : Expr =
        Compiler.NilExprInstance   // TODO


    

    static member TagOf(o: obj) = 
        match RT0.get(RT0.meta(), RTReader.TagKeyword) with
        | :? Symbol as sym -> sym
        | :? string as str -> Symbol.intern(null,str)
        | :? Type as t -> 
            let ok, sym = TypeToTagDict.TryGetValue(t)
            if ok then sym else null
        | _ -> null

    // TODO: Source info needed,   context is not needed (probably)
    static member CreateStaticFieldOrPropertyExpr(cctx: CompilerContext, tag: Symbol, t: Type, memberName: string, info: MemberInfo) : Expr =
        HostExpr({Ctx = cctx; Tag = tag; Target = None; TargetType = t; MemberName = memberName; TInfo = info; Args = null;  IsTailPosition = false; SourceInfo = None})

    static member MaybeType(cctx: CompilerContext, form: obj, stringOk: bool) = 
        match form with
        | :? Type as t -> t
        | :? Symbol as sym -> 
            if isNull sym.Namespace then 
                // TODO: Original code has check of CompilerStubSymVar and CompilerStubClassVar here -- are we going to need this?
                if sym.Name.IndexOf('.') > 0 || sym.Name.Length > 0 && sym.Name[sym.Name.Length - 1] = ']' then
                    RTType.ClassForNameE(sym.Name)
                else 
                    match RTVar.getCurrentNamespace().getMapping(sym) with
                    | :? Type as t -> t
                    | _ when cctx.ContainsBindingForSym(sym) -> null
                    | _  ->
                        try 
                            RTType.ClassForName(sym.Name)
                        with
                        | _ -> null
            else 
                null
        | _ when stringOk && (form :? string) -> RTType.ClassForName(form :?> string)
        | _ -> null


    static member Resolve(sym: Symbol) : obj  = Compiler.ResolveIn(RTVar.getCurrentNamespace(), sym, false)

    static member ResolveIn(ns: Namespace, sym: Symbol, allowPrivate: bool) : obj = 
        if not <| isNull sym.Namespace then
            match RTReader.NamespaceFor(sym) with
            | null -> 
                match RTType.MaybeArrayType(sym) with
                | null -> raise <| new InvalidOperationException($"No such namespace: {sym.Namespace}")
                | _ as t -> t
            | _ as ns ->
                match ns.findInternedVar(Symbol.intern(sym.Name)) with
                | null -> raise <| new InvalidOperationException($"No such var: {sym}")
                | _ as v when v.Namespace <> RTVar.getCurrentNamespace() && not v.isPublic && not allowPrivate ->
                    raise <| new InvalidOperationException($"Var: {sym} is not public")
                | _ as v -> v
        elif sym.Name.IndexOf('.') > 0 || sym.Name.Length > 0 && sym.Name[sym.Name.Length - 1] = ']' then
            RTType.ClassForNameE(sym.Name)
        elif sym.Equals(RTReader.NsSym) then
            RTVar.NsVar
        elif sym.Equals(RTReader.InNsSym) then
            RTVar.InNSVar
        //elif CompileStubSymVar / CompileStubClassVar  // TODO: Decide on stubs
        else
            match ns.getMapping(sym) with
            | null -> 
                if RT0.booleanCast((RTVar.AllowUnresolvedVarsVar :> IDeref).deref()) then 
                    sym
                else 
                    raise <| new InvalidOperationException($"Unable to resolve symbol: {sym} int this context")
            | _ as o -> o

    static member IsMacro(cctx: CompilerContext, op: obj) : Var = 
        let checkVar(v: Var) = 
            if not <| isNull v && v.isMacro then
                if v.Namespace = RTVar.getCurrentNamespace() && not v.isPublic then
                    raise <| new InvalidOperationException($"Var: {v} is not public")
        match op with
        | :? Var as v -> checkVar(v); v
        | :? Symbol as s -> 
            if cctx.ContainsBindingForSym(s) then
                null
            else
                match Compiler.LookupVar(cctx, s, false, false) with
                | null -> null
                | _ as v -> checkVar(v); v
        | _ -> null

    static member LookupVar(cctx:CompilerContext, sym: Symbol, internNew: bool) = Compiler.LookupVar(cctx, sym, internNew, true)

    static member LookupVar(cctx: CompilerContext, sym: Symbol, internNew: bool, registerMacro: bool) : Var = 
        let var = 
            // Note: ns-qualified vars in other namespaces must exist already
            match sym with
            | _ when not <| isNull sym.Namespace ->
                match RTReader.NamespaceFor(sym) with
                | null -> null
                | _ as ns ->
                    let name = Symbol.intern(sym.Name)
                    if internNew && Object.ReferenceEquals(ns, RTVar.getCurrentNamespace()) then
                        RTVar.getCurrentNamespace().intern(name)
                    else
                        ns.findInternedVar(name)
            | _ when sym.Equals(RTReader.NsSym) -> RTVar.NsVar
            | _ when sym.Equals(RTReader.InNsSym) -> RTVar.InNSVar
            | _ -> 
                match RTVar.getCurrentNamespace().getMapping(sym) with
                | null -> 
                    // introduce a new var in the current ns
                    if internNew then
                        RTVar.getCurrentNamespace().intern(Symbol.intern(sym.Name))
                    else null
                | :? Var as v -> v
                | _ as o -> raise <| new InvalidOperationException($"Expecting var, but {sym} is mapped to {o}")

        if not <| isNull var && (not var.isPublic || registerMacro) then
                cctx.RegisterVar(var)   // TODO: SHould this be done later by walking the tree?
 
        var
        
