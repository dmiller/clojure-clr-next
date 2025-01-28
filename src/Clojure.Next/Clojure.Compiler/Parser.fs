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

type SpecialFormParser = CompilerEnv * obj -> Expr

exception ParseException of string

[<Sealed;AbstractClass>]
type Parser private () =

    static let ConstKeyword = Keyword.intern(null,"const")
    static let IdentitySym = Symbol.intern("clojure.core", "identity")
    static let ClassSym = Symbol.intern("System", "Type")

    // Why not a Dictionary<Symbol, SpecialFormParser> ???
    static let SpecialFormToParserMap : IPersistentMap = PersistentHashMap.create(
            RTVar.DefSym, Parser.DefExprParser,
            RTVar.LoopSym, Parser.LetExprParser,
            RTVar.RecurSym, Parser.RecurExprParser,
            RTVar.IfSym, Parser.IfExprParser,
            RTVar.CaseSym, Parser.CaseExprParser,
            RTVar.LetSym, Parser.LetExprParser,
            RTVar.LetfnSym, Parser.LetFnExprParser,
            RTVar.DoSym, Parser.BodyExprParser,
            RTVar.FnSym, null,     // FnSym is a special case.  It parser takes an additional argument.
            RTVar.QuoteSym, Parser.ConstantExprParser,
            RTVar.TheVarSym, Parser.TheVarExprParser,
            RTVar.ImportSym, Parser.ImportExprParser,
            RTVar.DotSym, Parser.HostExprParser,
            RTVar.AssignSym, Parser.AssignExprParser,
            RTVar.DeftypeSym, Parser.DefTypeParser(),
            RTVar.ReifySym, Parser.ReifyParser(),
            RTVar.TrySym, Parser.TryExprParser,
            RTVar.ThrowSym, Parser.ThrowExprParser,
            RTVar.MonitorEnterSym, Parser.MonitorEnterExprParser,
            RTVar.MonitorExitSym, Parser.MonitorExitExprParser,
            RTVar.CatchSym, null,
            RTVar.FinallySym, null,
            RTVar.NewSym, Parser.NewExprParser,
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

    static member Analyze(cctx : CompilerEnv, form: obj) : Expr = Parser.Analyze(cctx, form, null)
    static member Analyze(cctx: CompilerEnv, form: obj, name: string) : Expr =

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
            | null -> Parser.NilExprInstance
            | :? bool as b -> if b then Parser.TrueExprInstance else Parser.FalseExprInstance
            | :? Symbol as sym -> Parser.AnalyzeSymbol(cctx,sym)
            | :? Keyword as kw -> LiteralExpr({Type=KeywordType; Value=kw})       // we'll have to walk to get keywords
            | _ when Numbers.IsNumeric(form) -> Parser.AnalyzeNumber(cctx,form)
            | :? String as s -> LiteralExpr({Type=StringType; Value=String.Intern(s)})
            | :? IPersistentCollection as coll 
                    when not <| form :? IType && 
                         not <| form :? IRecord && 
                         coll.count() = 0  -> Parser.OptionallyGenerateMetaInit(cctx, form, LiteralExpr({Type = EmptyType; Value = coll}))
            | :? ISeq as seq -> Parser.AnalyzeSeq(cctx, seq, name)
            | :? IPersistentVector as pv-> Parser.AnalyzeVector(cctx, pv)
            | :? IRecord -> LiteralExpr({Type=OtherType; Value=form})
            | :? IType -> LiteralExpr({Type=OtherType; Value=form})
            | :? IPersistentMap as pm -> Parser.AnalyzeMap(cctx, pm)
            | :? IPersistentSet  as ps -> Parser.AnalyzeSet(cctx, ps)
            | _ -> LiteralExpr({Type=OtherType; Value=form})

        with 
        | :? CompilerException -> reraise()
        |  _ as e -> raise <| CompilerException("HELP!", e)  // TODO: add source info


    // Let's start with something easy

    static member OptionallyGenerateMetaInit(cctx: CompilerEnv, form: obj, expr: Expr) : Expr =
        match RT0.meta(form) with
        | null -> expr
        | _ as meta -> MetaExpr({Ctx = cctx; Target = expr; Meta = Parser.AnalyzeMap(cctx, meta)})

    static member AnalyzeNumber(cctx: CompilerEnv, num: obj) : Expr =
        match num with
        | :? int | :? double | :? int64 -> LiteralExpr({Type=PrimNumericType; Value = num})  // TODO: Why would we not allow other primitive numerics here?
        | _ -> LiteralExpr({Type=OtherType; Value=num})


    // Analyzing the collection types
    // These are quite similar.

    static member AnalyzeVector(cctx: CompilerEnv, form: IPersistentVector) : Expr =
        let mutable constant = true
        let mutable args = PersistentVector.Empty :> IPersistentVector

        for i = 0 to form.count() - 1 do
            let v = Parser.Analyze(cctx, form.nth(i))
            args <- args.cons(v)
            if not <| v.IsLiteralExpr then constant <- false

        let ret =  CollectionExpr({Ctx = cctx; Value = args})

        match form with
        | :? IObj as iobj when not <| isNull (iobj.meta()) -> Parser.OptionallyGenerateMetaInit(cctx, form, ret)
        | _ when constant -> 
            let mutable rv = PersistentVector.Empty :> IPersistentVector
            for i = 0 to args.count() - 1 do
                rv <- rv.cons(ExprUtils.GetLiteralValue(args.nth(i) :?> Expr))
            LiteralExpr({Type = OtherType; Value = rv})
        | _ -> ret


    static member AnalyzeMap(cctx: CompilerEnv, form: IPersistentMap) : Expr =
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
                let k = Parser.Analyze(cctx, me.key())
                let v = Parser.Analyze(cctx, me.value())
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
        | :? IObj as iobj when not <| isNull (iobj.meta()) -> Parser.OptionallyGenerateMetaInit(cctx, form, ret)
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


    static member AnalyzeSet(cctx: CompilerEnv, form: IPersistentSet) : Expr =
        let mutable constant = true
        let mutable keys = PersistentVector.Empty :> IPersistentVector

        let rec loop (s:ISeq) =
            match s with
            | null -> ()
            | _ -> 
                let k = Parser.Analyze(cctx, s.first())
                keys <- keys.cons(k)
                if not <| k.IsLiteralExpr then constant <- false
                loop(s.next())
        loop(form.seq())

        let ret = CollectionExpr({Ctx = cctx; Value = keys})

        match form with
        | :? IObj as iobj when not <| isNull (iobj.meta()) -> Parser.OptionallyGenerateMetaInit(cctx, form, ret)
        | _ when constant -> 
            let mutable rv = PersistentHashSet.Empty :> IPersistentCollection
            let x : Expr = ret
            for i = 0 to keys.count() - 1 do
                rv <- rv.cons(ExprUtils.GetLiteralValue(keys.nth(i) :?> Expr))
            LiteralExpr({Type = OtherType; Value = rv})
        | _ -> ret


    // Time to get into the monsters

    static member AnalyzeSymbol(cctx: CompilerEnv, sym: Symbol) : Expr =
        
        let tag = Parser.TagOf(sym)

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
                match Parser.MaybeType(cctx,nsSym,false) with
                | null -> None
                | _ as t -> 
                    let info = Reflector.GetFieldOrPropertyInfo(t,sym.Name,true)
                    if not <| isNull info then
                        Some(Parser.CreateStaticFieldOrPropertyExpr(cctx,tag, t, sym.Name, info))
                    else Some(QualifiedMethodExpr({Ctx = cctx; SourceInfo = None}))  // TODO: Implement QualifiedMethodExpr)

            else 
                None

        match maybeExpr with 
        | Some e -> e
        | None ->
            match Parser.Resolve(sym) with
            | :? Var as v -> 
                if not <| isNull (Parser.IsMacro(cctx, v)) then 
                    raise <| CompilerException($"Can't take the value of a macro: {sym.Name}")
                elif RT0.booleanCast(RT0.get((v :> IMeta).meta(),ConstKeyword))then 
                    Parser.Analyze(cctx.WithParserContext(Expression), RTSeq.list(RTVar.QuoteSym, v))
                else VarExpr({Ctx = cctx; Var = v; Tag = tag})
            | :? Type ->
                LiteralExpr({Type=OtherType; Value = sym;})
            | :? Symbol ->
                UnresolvedVarExpr({Ctx = cctx; Sym = sym})
            | _ -> raise <| CompilerException($"Unable to resolve symbol: {sym} in this context")
                
                    

            


    static member AnalyzeSeq(cctx: CompilerEnv, form: ISeq, name:string) : Expr =
        // TODO: deal with source info

        try
            let me = Parser.MacroexpandSeq1(cctx, form)
            if  Object.ReferenceEquals(me,form) then
                Parser.Analyze(cctx, me, name)
            else
                
                let op = RTSeq.first(form)
                if isNull op then
                    raise <| ArgumentNullException("form", "Can't call nil")

                let inl = Parser.IsInline(cctx, op, RT0.count(RTSeq.next(form)))
                if not <| isNull inl then
                    Parser.Analyze(cctx, Parser.MaybeTransferSourceInfo( Parser.PreserveTag(form, inl.applyTo(RTSeq.next(form))), form))
                elif op.Equals(RTVar.FnSym) then
                    Parser.FnExprParser(cctx, form, name)
                else
                    match Parser.GetSpecialFormParser(op) with
                    | null -> Parser.InvokeExprParser(cctx, form)
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
    
    static member ImportExprParser(cctx: CompilerEnv, form: obj) : Expr = 
        ImportExpr({Ctx = cctx; Typename = RTSeq.second(form) :?> string})

    static member MonitorEnterExprParser(cctx: CompilerEnv, form: obj) : Expr = 
        let cctx = cctx.WithParserContext(Expression)
        UntypedExpr({Ctx = cctx; Type=MonitorEnter; Target = Some <|  Parser.Analyze(cctx, RTSeq.second(form))})
        
    static member MonitorExitExprParser(cctx: CompilerEnv, form: obj) : Expr = 
        let cctx = cctx.WithParserContext(Expression)
        UntypedExpr({Ctx = cctx; Type=MonitorExit; Target = Some <| Parser.Analyze(cctx, RTSeq.second(form))})

 
    // cranking up the difficulty level

    static member AssignExprParser(cctx: CompilerEnv, form: obj) : Expr = 
        let form = form :?> ISeq

        if RT0.length(form) <> 3 then
            raise <| ParseException("Malformed assignment, expecting (set! target val)")

        let targetCtx = { cctx with Pctx = Expression; IsAssignContext = true }
        let target = Parser.Analyze(targetCtx, RTSeq.second(form))

        if not <| Parser.IsAssignableExpr(target) then
            raise <| ParseException("Invalid assignment target")

        let bodyCtx = cctx.WithParserContext(Expression)
        AssignExpr({Ctx = cctx; Target = target; Value = Parser.Analyze(bodyCtx, RTSeq.third(form))})


    static member ThrowExprParser(cctx: CompilerEnv, form: obj) : Expr = 
        // special case for Eval:  Wrap in FnOnceSym
        let cctx = { cctx with Pctx = Expression; IsAssignContext = false }
        match RT0.count(form) with
        | 1 ->  UntypedExpr({Ctx = cctx; Type=MonitorExit; Target = None})  
        | 2 ->  UntypedExpr({Ctx = cctx; Type=MonitorExit; Target =  Some <| Parser.Analyze(cctx, RTSeq.second(form))})  
        | _ -> raise <| InvalidOperationException("Too many arguments to throw, throw expects a single Exception instance")

    static member TheVarExprParser(cctx: CompilerEnv, form: obj) : Expr = 
        match RTSeq.second(form) with
        | :? Symbol as sym ->
            match Parser.LookupVar(cctx, sym, false) with
            | null -> raise <| ParseException($"Unable to resolve var: {sym} in this context")
            | _ as v -> TheVarExpr({Ctx = cctx; Var = v})
        | _ as v -> raise <| ParseException($"Second argument to the-var must be a symbol, found: {v}")        

    static member BodyExprParser(cctx: CompilerEnv, forms: obj) : Expr = 
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
                    if cctx.Pctx = ParserContext.Statement || not <| isNull (seq.next())  then
                        Parser.Analyze(stmtCctx, seq.first())
                    else
                        Parser.Analyze(cctx, seq.first())
                exprs.Add(e)
                loop(seq.next())
        loop forms

        BodyExpr({Ctx = cctx; Exprs = exprs})

    static member IfExprParser(cctx: CompilerEnv, form: obj) : Expr =
        let form = form :?> ISeq

        // (if test then) or (if test then else)

        if form.count() > 4 then
            raise <| ParseException("Too many arguments to if")

        if form.count() < 3 then
            raise <| ParseException("Too few arguments to if")

        let bodyCtx = { cctx with IsAssignContext = false} 
        let testCtx = { bodyCtx  with  Pctx = Expression }

        let testExpr = Parser.Analyze(testCtx, RTSeq.second(form))
        let thenExpr = Parser.Analyze(bodyCtx, RTSeq.third(form))
        let elseExpr = Parser.Analyze(bodyCtx, RTSeq.fourth(form))

        IfExpr({Test = testExpr; Then = thenExpr; Else = elseExpr; SourceInfo = None})  // TODO source info


    static member ConstantExprParser(cctx: CompilerEnv, form: obj) : Expr =
        let argCount = RT0.count(form)
        if argCount <> 1 then
            let exData = PersistentArrayMap([| RTVar.FormKeywoard :> obj; form|])
            raise <| ExceptionInfo($"Wrong number of arguments ({argCount}) passed to quote", exData)

        match RTSeq.second(form) with
        | null -> Parser.NilExprInstance
        | :? bool as b -> if b then Parser.TrueExprInstance else Parser.FalseExprInstance
        | _ as n when Numbers.IsNumeric(n) -> Parser.AnalyzeNumber(cctx,n)
        | :? string as s -> LiteralExpr({Type=StringType; Value=s})
        | :? IPersistentCollection as pc when pc.count() = 0 && ( not <| pc :? IMeta || isNull ((pc :?> IMeta).meta())) ->
            LiteralExpr({Type=EmptyType; Value = pc})
        | _ as v -> LiteralExpr({Type=OtherType; Value = v})


    static member ValidateBindingSymbol (s : obj) : Symbol = 
        match s with
        | :? Symbol as sym -> 
            if isNull sym.Namespace then sym
            else raise <| ParseException($"Can't let qualified name: {sym}")
        | _ -> raise <| ParseException($"Bad binding form, expected symbol, got: {s}")

    static member LetExprParser(cctx: CompilerEnv, form: obj) : Expr =
       
        // form => (let  [var1 val1 var2 val2 ... ] body ... )
        //      or (loop [var1 val1 var2 val2 ... ] body ... )

        let form = form :?> ISeq
        let isLoop = RTVar.LoopSym.Equals(RTSeq.first(form))

        let bindings = 
            match RTSeq.second(form) with
            | :? IPersistentVector as pv -> pv
            | _ -> raise <| ParseException("Bad binding form, expected vector")

        if bindings.count() % 2 <> 0 then
            raise <| ParseException("Bad binding form, expected even number of forms")

        let body = RTSeq.next(RTSeq.next(form))

        // TODO: do this in a later pass if necessary
        // if (pcon.Rhc == RHC.Eval
        //     || (pcon.Rhc == RHC.Expression && isLoop))
        //     return Compiler.Analyze(pcon, RT.list(RT.list(Compiler.FnOnceSym, PersistentVector.EMPTY, form)), "let__" + RT.nextID());

        // Here the original code access the method and grabs some fields.
        // THis works there because we always end up analyzing a let form inside an fn form.
        // We don't have that here, so we'll have to do something else.

                // ObjMethod method = (ObjMethod)Compiler.MethodVar.deref();
                //IPersistentMap backupMethodLocals = method.Locals;
                //IPersistentMap backupMethodIndexLocals = method.IndexLocals;
        // TODO: The original code also detects recur mismatches and insert boxing code as needed.
        //       We need to do that in a later pass -- hoping to have more powerful type handling here.

        let loopId = if isLoop then Some(RT0.nextID()) else None
        let mutable initCtx =  {cctx with Pctx = Expression; IsAssignContext = false; LoopId = loopId}
        let bindingInits = ResizeArray<BindingInit>()
        let loopLocals = ResizeArray<LocalBinding>()

        for i in 0 .. 2 .. (bindings.count()-1) do
            let sym = Parser.ValidateBindingSymbol(bindings.nth(i))

            let initForm = bindings.nth(i+1)
            let initExpr = Parser.Analyze(initCtx, initForm, sym.Name)

            // A bunch of analysis and validation goes here in the original code -- related to recur mismatches.
            //  And it adds wrapping expressions to handle boxing, plus warnings.
            // TODO: put into a later pass

            let localBinding = { Sym = sym; Tag = null; Init = initExpr; Name = sym.Name; IsArg = false; IsByRef = false; IsRecur = isLoop; IsThis = false; Index = i/2 }
            let bindingInit = { Binding = localBinding; Init = initExpr }
            bindingInits.Add(bindingInit)
            if isLoop then loopLocals.Add(localBinding)

            initCtx <- { initCtx with Locals = (RTMap.assoc(initCtx.Locals, sym, localBinding) :?> IPersistentMap) }

        // TODO: Original code also sets up MethodReturnContextVar , either pass-along or null  Do we need this?
        let bodyCtx = { initCtx with Pctx = (if isLoop then Return else cctx.Pctx); IsRecurContext = isLoop; LoopLocals=(if isLoop then loopLocals else null);}
        let bodyExpr = Parser.BodyExprParser(bodyCtx, body)
        LetExpr({Ctx = cctx; BindingInits = bindingInits; Body = bodyExpr; LoopId = loopId; Mode=(if isLoop then Loop else Let); SourceInfo=None})  // TODO: source info

            
    static member RecurExprParser(cctx: CompilerEnv, form: obj) : Expr = 
        
            // TODO: source info

            let form = form :?> ISeq

            let loopLocals = cctx.LoopLocals
            if cctx.Pctx <> Return  || isNull loopLocals then
                raise <| ParseException("Can only recur from tail position")

            if not <| cctx.IsRecurContext then
                raise <| ParseException("Can only recur across try")

            let args = ResizeArray<Expr>()

            let argCtx = { cctx with Pctx = Expression; IsAssignContext = false }

            let rec argLoop (s: ISeq) =
                match s with
                | null -> ()
                | _ -> 
                    args.Add(Parser.Analyze(argCtx, s.first()))
                    argLoop(s.next())

            argLoop (RTSeq.next(form))

            if args.Count <> loopLocals.Count then
                raise <| ParseException($"Mismatched argument count to recur, expected: {loopLocals.Count} args, got {args.Count}")

            // TODO: original code does type checking on the args here.  We'll have to do that in a later pass.

            RecurExpr({Ctx = cctx; Args = args; LoopLocals = loopLocals; SourceInfo=None})  // TODO: SourceInfo








    // Saving for later, when I figure out what I'm doing
    static member DefTypeParser()(cctx: CompilerEnv, form: obj) : Expr = Parser.NilExprInstance
    static member ReifyParser()(cctx: CompilerEnv, form: obj) : Expr = Parser.NilExprInstance
    static member CaseExprParser(cctx: CompilerEnv, form: obj) : Expr = Parser.NilExprInstance

    static member FnExprParser(cctx: CompilerEnv, form: obj, name: string) : Expr = Parser.NilExprInstance
    static member DefExprParser(cctx: CompilerEnv, form: obj) : Expr = Parser.NilExprInstance




    static member LetFnExprParser(cctx: CompilerEnv, form: obj) : Expr = Parser.NilExprInstance

    static member HostExprParser(cctx: CompilerEnv, form: obj) : Expr = Parser.NilExprInstance


    static member TryExprParser(cctx: CompilerEnv, form: obj) : Expr = Parser.NilExprInstance

    static member NewExprParser(cctx: CompilerEnv, form: obj) : Expr = Parser.NilExprInstance
    static member InvokeExprParser(cctx: CompilerEnv, form: obj) : Expr = Parser.NilExprInstance
   

    static member TagOf(o: obj) = 
        match RT0.get(RT0.meta(), RTVar.TagKeyword) with
        | :? Symbol as sym -> sym
        | :? string as str -> Symbol.intern(null,str)
        | :? Type as t -> 
            let ok, sym = TypeToTagDict.TryGetValue(t)
            if ok then sym else null
        | _ -> null

    // TODO: Source info needed,   context is not needed (probably)
    static member CreateStaticFieldOrPropertyExpr(cctx: CompilerEnv, tag: Symbol, t: Type, memberName: string, info: MemberInfo) : Expr =
        HostExpr({Ctx = cctx; Type=FieldOrPropertyExpr; Tag = tag; Target = None; TargetType = t; MemberName = memberName; TInfo = info; Args = null;  IsTailPosition = false; SourceInfo = None})

    static member MaybeType(cctx: CompilerEnv, form: obj, stringOk: bool) = 
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


    static member Resolve(sym: Symbol) : obj  = Parser.ResolveIn(RTVar.getCurrentNamespace(), sym, false)

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


    static member IsInline(cctx: CompilerEnv, op: obj, arity: int) : IFn =
        let v = 
            match op with
            | :? Var as v -> v
            | :? Symbol as s -> 
                match cctx.GetLocalBinding(s) with
                | Some _  -> Parser.LookupVar(cctx, s, false)
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


    static member IsMacro(cctx: CompilerEnv, op: obj) : Var = 
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
                match Parser.LookupVar(cctx, s, false, false) with
                | null -> null
                | _ as v -> checkVar(v); v
        | _ -> null

    static member LookupVar(cctx:CompilerEnv, sym: Symbol, internNew: bool) = Parser.LookupVar(cctx, sym, internNew, true)

    static member LookupVar(cctx: CompilerEnv, sym: Symbol, internNew: bool, registerMacro: bool) : Var = 
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

    static member Macroexpand1(form: obj) = Parser.Macroexpand1(CompilerEnv.Create(Expression), form)
    static member Macroexpand(form : obj) = Parser.Macroexpand(CompilerEnv.Create(Expression), form)

    static member Macroexpand1(cctx: CompilerEnv, form: obj) =
        match form with 
        | :? ISeq as s -> Parser.MacroexpandSeq1(cctx, s)
        | _ -> form

    static member Macroexpand(cctx: CompilerEnv, form : obj) = 
        let exf = Parser.Macroexpand1(cctx, form)
        if Object.ReferenceEquals(exf, form) then form 
        else Parser.Macroexpand(cctx, exf)

    static member private MacroexpandSeq1(cctx: CompilerEnv, form: ISeq) = 
        let op = form.first()
        if (LispReader.IsSpecial(form)) then
            form
        else
            match Parser.IsMacro(cctx, op) with
            | null -> Parser.MacroExpandNonSpecial(cctx, op, form)
            | _ as v -> Parser.MacroExpandSpecial(cctx, v, form)

    static member private MacroExpandSpecial(cctx: CompilerEnv, v: Var, form: ISeq) = 
        Parser.CheckSpecs(v,form)
        try
            // Here is macro magic -- supply the &form and &env args in front
            let args = RTSeq.cons(form, RTSeq.cons(cctx.Locals, form.next()))
            (v :> IFn).applyTo(args)
        with
        | :? ArityException as e ->
            // hide the 2 extra params for a macro
            // This simple test is used in the JVM:   if (e.Name.Equals(munge(v.ns.Name.Name) + "$" + munge(v.sym.Name)))
            // Does not work for us because have to append a __1234 to the type name for functions in order to avoid name collisiions in the eval assembly.
            // So we have to see if the name is of the form   namespace$name__xxxx  where the __xxxx can be repeated.
            let reducedName = Parser.RemoveFnSuffix(e.name)
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

    static member private MacroExpandNonSpecial(cctx: CompilerEnv, op: obj, form: ISeq) = 
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
                if not <| isNull (Parser.MaybeType(cctx, target, false)) then
                    target <- (RTSeq.list(IdentitySym, target) :?> IObj).withMeta(RTMap.map(RTVar.TagKeyword, ClassSym))
                Parser.MaybeTransferSourceInfo(Parser.PreserveTag(form, RTSeq.listStar(RTVar.DotSym, target, method, form.next().next())), form)
            else
                // (x.substring 2 5) =>  (. x substring 2 5)
                // also (package.class.name ... ) (. package.class name ... )
                let index = sname.IndexOf('.')
                if index = sname.Length-1 then
                    let target = Symbol.intern(sname.Substring(0, index))
                    Parser.MaybeTransferSourceInfo( RTSeq.listStar(RTVar.NewSym, target, form.next()), form)
                else form
        | _ -> form


            

    //public static Regex UnpackFnNameRE = new Regex("^(.+)/$([^_]+)(__[0-9]+)*$");
    static member val FnNameSuffixRE = new Regex("__[0-9]+$")
    static member RemoveFnSuffix(s: string) =
        let rec loop (s: string) =
            let m = Parser.FnNameSuffixRE.Match(s)
            if m.Success then
                loop(s.Substring(0, s.Length - m.Groups.[0].Length))
            else s

        loop s

    static member PreserveTag(src: ISeq, dst: obj) : obj =
        match Parser.TagOf(src) with 
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