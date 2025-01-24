namespace Clojure.Compiler

open Clojure.Collections
open Clojure.Numerics
open System
open System.Collections.Generic
open Clojure.IO
open Clojure.Reflection
open System.Reflection
open Clojure.Lib
open System.Text.RegularExpressions
open System.Text

type SpecialFormParser = CompilerContext * obj -> Expr

exception ParseException of string

[<Sealed;AbstractClass>]
type Compiler private () =

    static let ConstKeyword = Keyword.intern(null,"const")
    static let IdentitySym = Symbol.intern("clojure.core", "identity")
    static let ClassSym = Symbol.intern("System", "Type")

    // Why not a Dictionary<Symbol, SpecialFormParser> ???
    static let SpecialFormToParserMap : IPersistentMap = PersistentHashMap.create(
            RTVar.DefSym, Compiler.DefExprParser,
            RTVar.LoopSym, Compiler.LetExprParser,
            RTVar.RecurSym, Compiler.RecurExprParser,
            RTVar.IfSym, Compiler.IfExprParser,
            RTVar.CaseSym, Compiler.CaseExprParser,
            RTVar.LetSym, Compiler.LetExprParser,
            RTVar.LetfnSym, Compiler.LetFnExprParser,
            RTVar.DoSym, Compiler.BodyExprParser,
            RTVar.FnSym, null,
            RTVar.QuoteSym, Compiler.ConstantExprParser,
            RTVar.TheVarSym, Compiler.TheVarExprParser,
            RTVar.ImportSym, Compiler.ImportExprParser,
            RTVar.DotSym, Compiler.HostExprParser,
            RTVar.AssignSym, Compiler.AssignExprParser,
            RTVar.DeftypeSym, Compiler.DefTypeParser(),
            RTVar.ReifySym, Compiler.ReifyParser(),
            RTVar.TrySym, Compiler.TryExprParser,
            RTVar.ThrowSym, Compiler.ThrowExprParser,
            RTVar.MonitorEnterSym, Compiler.MonitorEnterExprParser,
            RTVar.MonitorExitSym, Compiler.MonitorExitExprParser,
            RTVar.CatchSym, null,
            RTVar.FinallySym, null,
            RTVar.NewSym, Compiler.NewExprParser,
            RTVar.AmpersandSym, null
   
    
        )

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
    
    static member GetSpecialFormParser(op: obj) = SpecialFormToParserMap.valAt(op) 

    static member Analyze(cctx : CompilerContext, form: obj) : Expr = Compiler.Analyze(cctx, form, null)
    static member Analyze(cctx: CompilerContext, form: obj, name: string) : Expr =

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
            | :? ISeq as seq -> Compiler.AnalyzeSeq(cctx, seq, name)
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
                    Compiler.Analyze(cctx.WithParserContext(Expression), RTSeq.list(RTVar.QuoteSym, v))
                else VarExpr({Ctx = cctx; Var = v; Tag = tag})
            | :? Type ->
                LiteralExpr({Type=OtherType; Value = sym;})
            | :? Symbol ->
                UnresolvedVarExpr({Ctx = cctx; Sym = sym})
            | _ -> raise <| CompilerException($"Unable to resolve symbol: {sym} in this context")
                
                    

            


    static member AnalyzeSeq(cctx: CompilerContext, form: ISeq, name:string) : Expr =
        // TODO: deal with source info

        try
            let me = Compiler.MacroexpandSeq1(cctx, form)
            if  Object.ReferenceEquals(me,form) then
                Compiler.Analyze(cctx, me, name)
            else
                
                let op = RTSeq.first(form)
                if isNull op then
                    raise <| ArgumentNullException("form", "Can't call nil")

                let inl = Compiler.IsInline(cctx, op, RT0.count(RTSeq.next(form)))
                if not <| isNull inl then
                    Compiler.Analyze(cctx, Compiler.MaybeTransferSourceInfo( Compiler.PreserveTag(form, inl.applyTo(RTSeq.next(form))), form))
                elif op.Equals(RTVar.FnSym) then
                    Compiler.FnExprParser(cctx, form, name)
                else
                    match Compiler.GetSpecialFormParser(op) with
                    | null -> Compiler.InvokeExprParser(cctx, form)
                    | _ as parseFn ->  (parseFn :?> SpecialFormParser)(cctx, form)
        with
        | :? CompilerException -> reraise()
        | _ as e -> 
            let sym = 
                match RTSeq.first(form) with
                | :? Symbol as sym -> sym
                | _ -> null
            raise <| new CompilerException("help",0,0,sym,e)  // TODO: pass source info

    // Special form parsers

    // Let's start with some easy ones
    
    static member ImportExprParser(cctx: CompilerContext, form: obj) : Expr = 
        ImportExpr({Ctx = cctx; Typename = RTSeq.second(form) :?> string})

    static member MonitorEnterExprParser(cctx: CompilerContext, form: obj) : Expr = 
        let cctx = cctx.WithParserContext(Expression)
        UntypedExpr({Ctx = cctx; Type=MonitorEnter; Target = Some <|  Compiler.Analyze(cctx, RTSeq.second(form))})
        
    static member MonitorExitExprParser(cctx: CompilerContext, form: obj) : Expr = 
        let cctx = cctx.WithParserContext(Expression)
        UntypedExpr({Ctx = cctx; Type=MonitorExit; Target = Some <| Compiler.Analyze(cctx, RTSeq.second(form))})

 
    // cranking up the difficulty level

    static member AssignExprParser(cctx: CompilerContext, form: obj) : Expr = 
        let form = form :?> ISeq

        if RT0.length(form) <> 3 then
            raise <| ParseException("Malformed assignment, expecting (set! target val)")

        let targetCtx = cctx.WithParserContext(Expression).WithIsAssign(true)
        let target = Compiler.Analyze(targetCtx, RTSeq.second(form))

        if not <| Compiler.IsAssignableExpr(target) then
            raise <| ParseException("Invalid assignment target")

        let bodyCtx = cctx.WithParserContext(Expression)
        AssignExpr({Ctx = cctx; Target = target; Value = Compiler.Analyze(bodyCtx, RTSeq.third(form))})


    static member ThrowExprParser(cctx: CompilerContext, form: obj) : Expr = 
        // special case for Eval:  Wrap in FnOnceSym
        let cctx = cctx.WithParserContext(Expression).WithIsAssign(false)
        match RT0.count(form) with
        | 1 ->  UntypedExpr({Ctx = cctx; Type=MonitorExit; Target = None})  
        | 2 ->  UntypedExpr({Ctx = cctx; Type=MonitorExit; Target =  Some <| Compiler.Analyze(cctx, RTSeq.second(form))})  
        | _ -> raise <| InvalidOperationException("Too many arguments to throw, throw expects a single Exception instance")

    static member TheVarExprParser(cctx: CompilerContext, form: obj) : Expr = 
        match RTSeq.second(form) with
        | :? Symbol as sym ->
            match Compiler.LookupVar(cctx, sym, false) with
            | null -> raise <| ParseException($"Unable to resolve var: {sym} in this context")
            | _ as v -> TheVarExpr({Ctx = cctx; Var = v})
        | _ as v -> raise <| ParseException($"Second argument to the-var must be a symbol, found: {v}")        

    static member BodyExprParser(cctx: CompilerContext, forms: obj) : Expr = 
        let forms = 
            if Util.equals(RTSeq.first(forms), RTVar.DoSym) then RTSeq.next(forms)
            else forms :?> ISeq

        let stmtCctx = cctx.WithParserContext(Statement)
        let exprs = List<Expr>()

        let rec loop (seq: ISeq) =
            match seq with
            | null -> ()
            | _ -> 
                let e = 
                    if cctx.ParserContext = ParserContext.Statement || not <| isNull (seq.next())  then
                        Compiler.Analyze(stmtCctx, seq.first())
                    else
                        Compiler.Analyze(cctx, seq.first())
                exprs.Add(e)
                loop(seq.next())
        loop forms

        BodyExpr({Ctx = cctx; Exprs = exprs})

    static member IfExprParser(cctx: CompilerContext, form: obj) : Expr =
        let form = form :?> ISeq

        // (if test then) or (if test then else)

        if form.count() > 4 then
            raise <| ParseException("Too many arguments to if")

        if form.count() < 3 then
            raise <| ParseException("Too few arguments to if")

        let bodyCtx = cctx.WithIsAssign(false)
        let testCtx = bodyCtx.WithParserContext(Expression)

        let testExpr = Compiler.Analyze(testCtx, RTSeq.second(form))
        let thenExpr = Compiler.Analyze(bodyCtx, RTSeq.third(form))
        let elseExpr = Compiler.Analyze(bodyCtx, RTSeq.fourth(form))

        IfExpr({Test = testExpr; Then = thenExpr; Else = elseExpr; SourceInfo = None})  // TODO source info


    static member ConstantExprParser(cctx: CompilerContext, form: obj) : Expr =
        let argCount = RT0.count(form)
        if argCount <> 1 then
            let exData = PersistentArrayMap([| RTVar.FormKeywoard :> obj; form|])
            raise <| ExceptionInfo($"Wrong number of arguments ({argCount}) passed to quote", exData)

        match RTSeq.second(form) with
        | null -> Compiler.NilExprInstance
        | :? bool as b -> if b then Compiler.TrueExprInstance else Compiler.FalseExprInstance
        | _ as n when Numbers.IsNumeric(n) -> Compiler.AnalyzeNumber(cctx,n)
        | :? string as s -> LiteralExpr({Type=StringType; Value=s})
        | :? IPersistentCollection as pc when pc.count() = 0 && ( not <| pc :? IMeta || isNull ((pc :?> IMeta).meta())) ->
            LiteralExpr({Type=EmptyType; Value = pc})
        | _ as v -> LiteralExpr({Type=OtherType; Value = v})




    // Saving for later, when I figure out what I'm doing
    static member DefTypeParser()(cctx: CompilerContext, form: obj) : Expr = Compiler.NilExprInstance
    static member ReifyParser()(cctx: CompilerContext, form: obj) : Expr = Compiler.NilExprInstance
    static member CaseExprParser(cctx: CompilerContext, form: obj) : Expr = Compiler.NilExprInstance

    static member FnExprParser(cctx: CompilerContext, form: obj, name: string) : Expr = Compiler.NilExprInstance
    static member DefExprParser(cctx: CompilerContext, form: obj) : Expr = Compiler.NilExprInstance
    static member RecurExprParser(cctx: CompilerContext, form: obj) : Expr = Compiler.NilExprInstance


    static member LetExprParser(cctx: CompilerContext, form: obj) : Expr = Compiler.NilExprInstance
    static member LetFnExprParser(cctx: CompilerContext, form: obj) : Expr = Compiler.NilExprInstance

    static member HostExprParser(cctx: CompilerContext, form: obj) : Expr = Compiler.NilExprInstance


    static member TryExprParser(cctx: CompilerContext, form: obj) : Expr = Compiler.NilExprInstance

    static member NewExprParser(cctx: CompilerContext, form: obj) : Expr = Compiler.NilExprInstance
    static member InvokeExprParser(cctx: CompilerContext, form: obj) : Expr = Compiler.NilExprInstance
   

    static member TagOf(o: obj) = 
        match RT0.get(RT0.meta(), RTVar.TagKeyword) with
        | :? Symbol as sym -> sym
        | :? string as str -> Symbol.intern(null,str)
        | :? Type as t -> 
            let ok, sym = TypeToTagDict.TryGetValue(t)
            if ok then sym else null
        | _ -> null

    // TODO: Source info needed,   context is not needed (probably)
    static member CreateStaticFieldOrPropertyExpr(cctx: CompilerContext, tag: Symbol, t: Type, memberName: string, info: MemberInfo) : Expr =
        HostExpr({Ctx = cctx; Type=FieldOrPropertyExpr; Tag = tag; Target = None; TargetType = t; MemberName = memberName; TInfo = info; Args = null;  IsTailPosition = false; SourceInfo = None})

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
        elif sym.Equals(RTVar.NsSym) then
            RTVar.NsVar
        elif sym.Equals(RTVar.InNsSym) then
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


    static member IsInline(cctx: CompilerContext, op: obj, arity: int) : IFn =
        let v = 
            match op with
            | :? Var as v -> v
            | :? Symbol as s -> 
                match cctx.GetLocalBinding(s) with
                | Some _  -> Compiler.LookupVar(cctx, s, false)
                | _ -> null
            | _ -> null

        match v with
        | null -> null
        | _ ->
            if not <| Object.ReferenceEquals(v.Namespace, RTVar.getCurrentNamespace())  && not v.isPublic then
                raise <| new InvalidOperationException($"Var: {v} is not public")

            match RT0.get((v :> IMeta).meta(), RTVar.InlineKeyword) :?> IFn with
            | null -> null
            | _ as ret ->
                match RT0.get((v :> IMeta).meta(), RTVar.InlineAritiesKeyword) with
                | null -> ret
                | :? IFn as aritiesPred ->
                    if RT0.booleanCast(aritiesPred.invoke(arity)) then ret
                    else null
                | _ -> ret         // Probably this should be an error: :inline-arities value should be an IFn.  Original code does a cast that might fail


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
            | _ when sym.Equals(RTVar.NsSym) -> RTVar.NsVar
            | _ when sym.Equals(RTVar.InNsSym) -> RTVar.InNSVar
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
        

    // Macroexpansion

    // TODO: Macrochecking via Spec
    static member CheckSpecs(v: Var, form: ISeq) = ()

    static member Macroexpand1(form: obj) = Compiler.Macroexpand1(CompilerContext(Expression), form)
    static member Macroexpand(form : obj) = Compiler.Macroexpand(CompilerContext(Expression), form)

    static member Macroexpand1(cctx: CompilerContext, form: obj) =
        match form with 
        | :? ISeq as s -> Compiler.MacroexpandSeq1(cctx, s)
        | _ -> form

    static member Macroexpand(cctx: CompilerContext, form : obj) = 
        let exf = Compiler.Macroexpand1(cctx, form)
        if Object.ReferenceEquals(exf, form) then form 
        else Compiler.Macroexpand(cctx, exf)

    static member private MacroexpandSeq1(cctx: CompilerContext, form: ISeq) = 
        let op = form.first()
        if (LispReader.IsSpecial(form)) then
            form
        else
            match Compiler.IsMacro(cctx, op) with
            | null -> Compiler.MacroExpandNonSpecial(cctx, op, form)
            | _ as v -> Compiler.MacroExpandSpecial(cctx, v, form)

    static member private MacroExpandSpecial(cctx: CompilerContext, v: Var, form: ISeq) = 
        Compiler.CheckSpecs(v,form)
        try
            // Here is macro magic -- supply the &form and &env args in front
            let args = RTSeq.cons(form, RTSeq.cons(cctx.LocalBindings, form.next()))
            (v :> IFn).applyTo(args)
        with
        | :? ArityException as e ->
            // hide the 2 extra params for a macro
            // This simple test is used in the JVM:   if (e.Name.Equals(munge(v.ns.Name.Name) + "$" + munge(v.sym.Name)))
            // Does not work for us because have to append a __1234 to the type name for functions in order to avoid name collisiions in the eval assembly.
            // So we have to see if the name is of the form   namespace$name__xxxx  where the __xxxx can be repeated.
            let reducedName = Compiler.RemoveFnSuffix(e.name)
            if reducedName.Equals($"{Munger.Munge(v.Namespace.Name.Name)}${Munger.Munge(v.Symbol.Name)}")  then
                raise <| new ArityException(e.actual-2, e.name)
            else
                reraise()
        | :? CompilerException -> 
            reraise()
        | _ as e ->
            if e :? ArgumentException || e :? InvalidOperationException || (e :> obj) :? IExceptionInfo then
                // TODO: Put in source Info
                raise <| CompilerException("Macro failed", e)
                (* 
                                        throw new CompilerException((String)SourcePathVar.deref(), LineVarDeref(), ColumnVarDeref(),
                            op is Symbol symbol ? symbol : null,
                            CompilerException.PhaseMacroSyntaxCheckKeyword,
                            e);
                *)
            else
                // TODO: put in source info
                raise <| CompilerException("Macro failed", e)
                (*
                          throw new CompilerException((String)SourcePathVar.deref(), LineVarDeref(), ColumnVarDeref(),
                            op is Symbol symbol ? symbol : null,
                            (e.GetType().Equals(typeof(Exception)) ? CompilerException.PhaseMacroSyntaxCheckKeyword : CompilerException.PhaseMacroExpandKeyword),
                            e);
                      
                *)

    static member private MacroExpandNonSpecial(cctx: CompilerContext, op: obj, form: ISeq) = 
        match op with
        | :? Symbol as sym ->
            let sname = sym.Name
            // We want to expand  (.name ...args...) to (. name ...args...)
            // want namespace == null to sure that Class/.isntancemethd is not expanded to . form
            if sname.Length > 1 && sname.[0] = '.' && isNull sym.Namespace then
                if form.count() < 2 then
                    raise <| ArgumentException("Malformed member expression, expecting (.member target ...) ")
                let method = Symbol.intern(sname.Substring(1))
                let mutable target = RTSeq.second(form)
                if not <| isNull (Compiler.MaybeType(cctx, target, false)) then
                    target <- (RTSeq.list(IdentitySym, target) :?> IObj).withMeta(RTMap.map(RTVar.TagKeyword, ClassSym))
                Compiler.MaybeTransferSourceInfo(Compiler.PreserveTag(form, RTSeq.listStar(RTVar.DotSym, target, method, form.next().next())), form)
            else
                // (x.substring 2 5) =>  (. x substring 2 5)
                // also (package.class.name ... ) (. package.class name ... )
                let index = sname.IndexOf('.')
                if index = sname.Length-1 then
                    let target = Symbol.intern(sname.Substring(0, index))
                    Compiler.MaybeTransferSourceInfo( RTSeq.listStar(RTVar.NewSym, target, form.next()), form)
                else form
        | _ -> form


            

    //public static Regex UnpackFnNameRE = new Regex("^(.+)/$([^_]+)(__[0-9]+)*$");
    static member val FnNameSuffixRE = new Regex("__[0-9]+$")
    static member RemoveFnSuffix(s: string) =
        let rec loop (s: string) =
            let m = Compiler.FnNameSuffixRE.Match(s)
            if m.Success then
                loop(s.Substring(0, s.Length - m.Groups.[0].Length))
            else s

        loop s

    static member PreserveTag(src: ISeq, dst: obj) : obj =
        match Compiler.TagOf(src) with 
        | null -> dst
        | _ as tag ->
            match dst with
            | :? IObj as iobj -> (dst :?> IObj).withMeta(RTMap.map(RTVar.TagKeyword, tag))
            | _ -> dst

    static member MaybeTransferSourceInfo(newForm: obj, oldForm: obj) : obj =
        match oldForm, newForm with 
        | (:? IObj as oldObj), (:? IObj as newObj) -> 
            match oldObj.meta() with
            | null -> newForm
            | _ as oldMeta ->
                let spanMap = oldMeta.valAt(RTVar.SourceSpanKeyword)
                let mutable newMeta = newObj.meta()
                if isNull newMeta then newMeta <- RTMap.map()
                newMeta <- newMeta.assoc(RTVar.SourceSpanKeyword, spanMap)
                newObj.withMeta(newMeta)
        | _, _ -> newForm
             

    static member IsAssignableExpr(expr: Expr) : bool =
        match expr with
        |  LocalBindingExpr _
        |  VarExpr _ -> true
        |  HostExpr deets when deets.Type = HostExprType.FieldOrPropertyExpr -> true
        | _ -> false