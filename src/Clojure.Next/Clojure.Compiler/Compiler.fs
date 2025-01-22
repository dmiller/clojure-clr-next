namespace Clojure.Compiler

open Clojure.Collections
open Clojure.Numerics
open System
open System.Collections.Generic
open Clojure.IO
open Clojure.Reflection
open System.Reflection



[<Sealed;AbstractClass>]
type Compiler private () =


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
        |  _ as e -> raise <| CompilerException(e)  // TODO: add source info


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

            elif isNull (LispReader.NamespaceFor(sym)) && not RTType.IsPosDigit(sym.Name) then
    
                // we have a namespace, coudl be Typename/Field
                let nsSym = Symbol.intern(sym.Namespace)
                match Compiler.MaybeType(nsSym,false) with
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

    static member MaybeType(form: obj, stringOk: bool) = 
        match form with
        | :? Type as t -> t
        | :? Symbol as sym -> 
            if isNull sym.Namespace then 
                // TODO: Original code has check of CompilerStubSymVar and CompilerStubClassVar here -- are we going to need this?
                if sym.Name.IndexOf('.') > 0 || sym.Name.Length > 0 && sym.Name[sym.Name.Length - 1] = ']' then
                    RTType.ClassForNameE(sym.Name)
                else 
                    match RTVar.getCurrentNamespace().GetMapping(sym) with
                    | :? Type as t -> t
                    | _ when cctx.ContainsBindingForSym(sym) -> null
                    | _  ->
                        try 
                            RTType.ClassForName(sym.Name
                        with
                        | _ -> null
        | _ when stringOk && form :? string -> RTType.ClassForName(form :?> string)
        | _ -> null





        static member MaybeResolveIn(n: Namespace, sym: Symbol) : obj =
        // note: ns-qualified vars must already exist
        if not <| isNull sym.Namespace then
            let ns = RTReader.NamespaceFor(sym)

            if isNull ns then
                RTType.MaybeArrayType(sym)
            else
                ns.findInternedVar (Symbol.intern (sym.Name))
        elif
            sym.Name.IndexOf('.') > 0 && not <| sym.Name.EndsWith(".")
            || sym.Name.Length > 0 && sym.Name[sym.Name.Length - 1] = ']'
        then // TODO: What is this?  I don't remember what this is for.
            RTType.ClassForName(sym.Name)
        elif sym.Equals(RTReader.NsSym) then
            RTVar.NsVar
        elif sym.Equals(RTReader.InNsSym) then
            RTVar.InNSVar
        else
            n.getMapping (sym)