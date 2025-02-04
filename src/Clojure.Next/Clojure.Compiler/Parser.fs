namespace Clojure.Compiler

open Clojure.Collections
open Clojure.IO
open Clojure.Lib
open Clojure.Numerics
open Clojure.Reflection
open System
open System.Collections.Generic
open System.Linq
open System.Reflection
open System.Text.RegularExpressions

type SpecialFormParser = CompilerEnv * ISeq -> Expr

exception ParseException of string

type ParamParseState = 
| Required
| Rest
| Done

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

    static member val MaxPositionalArity = 20  // TODO: Do we want to adjust this down to 16? (Match for System.Func)

    static member val NilExprInstance = Expr.Literal(Env = CompilerEnv.Empty, Form="nil", Type=NilType, Value=null)
    static member val TrueExprInstance = Expr.Literal(Env = CompilerEnv.Empty, Form="true", Type=BoolType, Value=true)
    static member val FalseExprInstance = Expr.Literal(Env = CompilerEnv.Empty, Form="false", Type=BoolType, Value=false)
    
    static member GetSpecialFormParser(op: obj) = SpecialFormToParserMap.valAt(op) 

    static member Analyze(cenv : CompilerEnv, form: obj) : Expr = Parser.Analyze(cenv, form, null)
    static member Analyze(cenv: CompilerEnv, form: obj, name: string) : Expr =

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
            | :? Symbol as sym -> Parser.AnalyzeSymbol(cenv,sym)
            | :? Keyword as kw -> 
                cenv.RegisterKeyword(kw)
                Expr.Literal(Env=cenv, Form = form, Type=KeywordType, Value=kw)  
            | _ when Numbers.IsNumeric(form) -> Parser.AnalyzeNumber(cenv,form)
            | :? String as s -> Expr.Literal(Env=cenv, Form = form, Type=StringType, Value=String.Intern(s))
            | :? IPersistentCollection as coll 
                    when not <| form :? IType && 
                         not <| form :? IRecord && 
                         coll.count() = 0  -> Parser.OptionallyGenerateMetaInit(cenv, form, Expr.Literal(Env=cenv, Form = form, Type = EmptyType, Value = coll))
            | :? ISeq as seq -> Parser.AnalyzeSeq(cenv, seq, name)
            | :? IPersistentVector as pv-> Parser.AnalyzeVector(cenv, pv)
            | :? IRecord -> Expr.Literal(Env=cenv, Form = form, Type=OtherType, Value=form)
            | :? IType -> Expr.Literal(Env=cenv, Form = form, Type=OtherType, Value=form)
            | :? IPersistentMap as pm -> Parser.AnalyzeMap(cenv, pm)
            | :? IPersistentSet  as ps -> Parser.AnalyzeSet(cenv, ps)
            | _ -> Expr.Literal(Env=cenv, Form = form, Type=OtherType, Value=form)

        with 
        | :? CompilerException -> reraise()
        |  _ as e -> raise <| CompilerException("HELP!", e)  // TODO: add source info


    // Let's start with something easy


    static member AnalyzeNumber(cenv: CompilerEnv, num: obj) : Expr =
        match num with
        | :? int | :? double | :? int64 -> Expr.Literal(Env=cenv, Form = num, Type=PrimNumericType, Value = num)  // TODO: Why would we not allow other primitive numerics here?
        | _ -> Expr.Literal(Env=cenv, Form = num, Type=OtherType, Value=num)


    // Analyzing the collection types
    // These are quite similar.

    static member AnalyzeVector(cenv: CompilerEnv, form: IPersistentVector) : Expr =
        let mutable constant = true
        let mutable args = PersistentVector.Empty :> IPersistentVector

        for i = 0 to form.count() - 1 do
            let v = Parser.Analyze(cenv, form.nth(i))
            args <- args.cons(v)
            if not <| v.IsLiteral then constant <- false

        let ret =  Expr.Collection(Env = cenv, Form = form, Value = args)

        match form with
        | :? IObj as iobj when not <| isNull (iobj.meta()) -> Parser.OptionallyGenerateMetaInit(cenv, form, ret)
        | _ when constant -> 
            let mutable rv = PersistentVector.Empty :> IPersistentVector
            for i = 0 to args.count() - 1 do
                rv <- rv.cons(ExprUtils.GetLiteralValue(args.nth(i) :?> Expr))
            Expr.Literal(Env = cenv, Form = form, Type = OtherType, Value = rv)
        | _ -> ret


    static member AnalyzeMap(cenv: CompilerEnv, form: IPersistentMap) : Expr =
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
                let k = Parser.Analyze(cenv, me.key())
                let v = Parser.Analyze(cenv, me.value())
                keyvals <- keyvals.cons(k).cons(v)
                if k.IsLiteral then
                    let kval = ExprUtils.GetLiteralValue(k)
                    if constantKeys.contains(kval) then allConstantKeysUnique <- false
                    else constantKeys <- constantKeys.cons(kval) :?> IPersistentSet
                else
                    keysConstant <- false

                if not <| v.IsLiteral then valsConstant <- false
                loop(s.next())
        loop(RT0.seq(form))

        let ret = Expr.Collection(Env = cenv, Form=form, Value = keyvals)

        match form with
        | :? IObj as iobj when not <| isNull (iobj.meta()) -> Parser.OptionallyGenerateMetaInit(cenv, form, ret)
        | _ when keysConstant -> 
            if not allConstantKeysUnique then raise <| ArgumentException("Duplicate constant keys in map")
            if valsConstant then
                let mutable m = PersistentArrayMap.Empty :> IPersistentMap

                for i in 0 .. 2 .. (keyvals.count()-1)  do
                    m <- m.assoc(ExprUtils.GetLiteralValue(keyvals.nth(i) :?> Expr), ExprUtils.GetLiteralValue(keyvals.nth(i+1) :?> Expr))
                Expr.Literal(Env = cenv, Form = form, Type = OtherType, Value = m)   // TODO: In the old code, this optimization was a big fail when some values were also maps.  Need to think about this.
            else 
                ret
        | _ -> ret


    static member AnalyzeSet(cenv: CompilerEnv, form: IPersistentSet) : Expr =
        let mutable constant = true
        let mutable keys = PersistentVector.Empty :> IPersistentVector

        let rec loop (s:ISeq) =
            match s with
            | null -> ()
            | _ -> 
                let k = Parser.Analyze(cenv, s.first())
                keys <- keys.cons(k)
                if not <| k.IsLiteral then constant <- false
                loop(s.next())
        loop(form.seq())

        let ret = Expr.Collection(Env = cenv, Form = form, Value = keys)

        match form with
        | :? IObj as iobj when not <| isNull (iobj.meta()) -> Parser.OptionallyGenerateMetaInit(cenv, form, ret)
        | _ when constant -> 
            let mutable rv = PersistentHashSet.Empty :> IPersistentCollection
            let x : Expr = ret
            for i = 0 to keys.count() - 1 do
                rv <- rv.cons(ExprUtils.GetLiteralValue(keys.nth(i) :?> Expr))
            Expr.Literal(Env = cenv, Form = form, Type = OtherType, Value = rv)
        | _ -> ret


    // Time to get into the monsters

    static member AnalyzeSymbol(cenv: CompilerEnv, sym: Symbol) : Expr =
        
        let tag = Parser.TagOf(sym)

        // See if we are a local variable or a field/property/QM reference
        let maybeExpr : Expr option = 
            if isNull sym.Namespace then
                // we might be a local variable
                match cenv.GetLocalBinding(sym) with
                | Some lb -> Some(Expr.LocalBinding(Env = cenv, Form=sym, Binding = lb, Tag = tag))
                | None -> None

            elif isNull (RTReader.NamespaceFor(sym)) && not <| RTType.IsPosDigit(sym.Name) then
    
                // we have a namespace, coudl be Typename/Field
                let nsSym = Symbol.intern(sym.Namespace)
                match Parser.MaybeType(cenv,nsSym,false) with
                | null -> None
                | _ as t -> 
                    let info = Reflector.GetFieldOrPropertyInfo(t,sym.Name,true)
                    if not <| isNull info then
                        Some(Parser.CreateStaticFieldOrPropertyExpr(cenv, sym, tag, t, sym.Name, info))
                    else Some(Expr.QualifiedMethod(Env = cenv, Form = sym, SourceInfo = None))  // TODO: Implement QualifiedMethodExpr

            else 
                None

        match maybeExpr with 
        | Some e -> e
        | None ->
            match Parser.Resolve(sym) with
            | :? Var as v -> 
                if not <| isNull (Parser.IsMacro(cenv, v)) then 
                    raise <| CompilerException($"Can't take the value of a macro: {sym.Name}")
                elif RT0.booleanCast(RT0.get((v :> IMeta).meta(),ConstKeyword))then 
                    Parser.Analyze(cenv.WithParserContext(Expression), RTSeq.list(RTVar.QuoteSym, v))
                else Expr.Var(Env = cenv, Form = sym, Var = v, Tag = tag)
            | :? Type ->
                Expr.Literal(Env = cenv, Form = sym, Type=OtherType, Value = sym)
            | :? Symbol ->
                Expr.UnresolvedVar(Env = cenv, Form = sym, Sym = sym)
            | _ -> raise <| CompilerException($"Unable to resolve symbol: {sym} in this context")
 

    static member AnalyzeSeq(cenv: CompilerEnv, form: ISeq, name:string) : Expr =
        // TODO: deal with source info

        try
            let me = Parser.MacroexpandSeq1(cenv, form)
            if  Object.ReferenceEquals(me,form) then
                Parser.Analyze(cenv, me, name)
            else
                
                let op = RTSeq.first(form)
                if isNull op then
                    raise <| ArgumentNullException("form", "Can't call nil")

                let inl = Parser.IsInline(cenv, op, RT0.count(RTSeq.next(form)))
                if not <| isNull inl then
                    Parser.Analyze(cenv, Parser.MaybeTransferSourceInfo( Parser.PreserveTag(form, inl.applyTo(RTSeq.next(form))), form))
                elif op.Equals(RTVar.FnSym) then
                    Parser.FnExprParser(cenv, form, name)
                else
                    match Parser.GetSpecialFormParser(op) with
                    | null -> Parser.InvokeExprParser(cenv, form)
                    | _ as parseFn ->  (parseFn :?> SpecialFormParser)(cenv, form)
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
    
    static member ImportExprParser(cenv: CompilerEnv, form: ISeq) : Expr = 
        Expr.Import(Env = cenv, Form = form, Typename = (RTSeq.second(form) :?> string))

    static member MonitorEnterExprParser(cenv: CompilerEnv, form: ISeq) : Expr = 
        let cenv = cenv.WithParserContext(Expression)
        Expr.Untyped(Env = cenv, Form = form, Type=MonitorEnter, Target = (Some <|  Parser.Analyze(cenv, RTSeq.second(form))))
        
    static member MonitorExitExprParser(cenv: CompilerEnv, form: ISeq) : Expr = 
        let cenv = cenv.WithParserContext(Expression)
        Expr.Untyped(Env = cenv, Form = form, Type=MonitorExit, Target = (Some <| Parser.Analyze(cenv, RTSeq.second(form))))

 
    // cranking up the difficulty level

    static member AssignExprParser(cenv: CompilerEnv, form: ISeq) : Expr = 

        if RT0.length(form) <> 3 then
            raise <| ParseException("Malformed assignment, expecting (set! target val)")

        let targetCtx = { cenv with Pctx = Expression; IsAssignContext = true }
        let target = Parser.Analyze(targetCtx, RTSeq.second(form))

        if not <| Parser.IsAssignableExpr(target) then
            raise <| ParseException("Invalid assignment target")

        let bodyCtx = cenv.WithParserContext(Expression)
        Expr.Assign(Env = cenv, Form = form, Target = target, Value = Parser.Analyze(bodyCtx, RTSeq.third(form)))


    static member ThrowExprParser(cenv: CompilerEnv, form: ISeq) : Expr = 
        // special case for Eval:  Wrap in FnOnceSym
        let cenv = { cenv with Pctx = Expression; IsAssignContext = false }
        match RT0.count(form) with
        | 1 ->  Expr.Untyped(Env = cenv, Form = form, Type = Throw, Target = None)  
        | 2 ->  Expr.Untyped(Env = cenv, Form = form, Type= Throw, Target =  (Some <| Parser.Analyze(cenv, RTSeq.second(form))))
        | _ -> raise <| InvalidOperationException("Too many arguments to throw, throw expects a single Exception instance")

    static member TheVarExprParser(cenv: CompilerEnv, form: ISeq) : Expr = 
        match RTSeq.second(form) with
        | :? Symbol as sym ->
            match Parser.LookupVar(cenv, sym, false) with
            | null -> raise <| ParseException($"Unable to resolve var: {sym} in this context")
            | _ as v -> Expr.TheVar(Env = cenv, Form = form, Var = v)
        | _ as v -> raise <| ParseException($"Second argument to the-var must be a symbol, found: {v}")        

    static member BodyExprParser(cenv: CompilerEnv, forms: ISeq) : Expr = 
        let forms = 
            if Util.equals(RTSeq.first(forms), RTVar.DoSym) then RTSeq.next(forms)
            else forms

        let stmtcenv = cenv.WithParserContext(Statement)
        let exprs = List<Expr>()

        let rec loop (seq: ISeq) =
            match seq with
            | null -> ()
            | _ -> 
                let e = 
                    if cenv.Pctx = ParserContext.Statement || not <| isNull (seq.next())  then
                        Parser.Analyze(stmtcenv, seq.first())
                    else
                        Parser.Analyze(cenv, seq.first())
                exprs.Add(e)
                loop(seq.next())
        loop forms

        Expr.Body(Env = cenv, Form = forms, Exprs = exprs)

    static member IfExprParser(cenv: CompilerEnv, form: ISeq) : Expr =

        // (if test then) or (if test then else)

        if form.count() > 4 then
            raise <| ParseException("Too many arguments to if")

        if form.count() < 3 then
            raise <| ParseException("Too few arguments to if")

        let bodyCtx = { cenv with IsAssignContext = false} 
        let testCtx = { bodyCtx  with  Pctx = Expression }

        let testExpr = Parser.Analyze(testCtx, RTSeq.second(form))
        let thenExpr = Parser.Analyze(bodyCtx, RTSeq.third(form))
        let elseExpr = Parser.Analyze(bodyCtx, RTSeq.fourth(form))

        Expr.If(Env = cenv, Form = form, Test = testExpr, Then = thenExpr, Else = elseExpr, SourceInfo = None)  // TODO source info


    static member ConstantExprParser(cenv: CompilerEnv, form: ISeq) : Expr =
        let argCount = RT0.count(form)
        if argCount <> 1 then
            let exData = PersistentArrayMap([| RTVar.FormKeywoard :> obj; form|])
            raise <| ExceptionInfo($"Wrong number of arguments ({argCount}) passed to quote", exData)

        match RTSeq.second(form) with
        | null -> Parser.NilExprInstance
        | :? bool as b -> if b then Parser.TrueExprInstance else Parser.FalseExprInstance
        | _ as n when Numbers.IsNumeric(n) -> Parser.AnalyzeNumber(cenv,n)
        | :? string as s -> Expr.Literal(Env = cenv, Form = form, Type=StringType, Value=s)
        | :? IPersistentCollection as pc when pc.count() = 0 && ( not <| pc :? IMeta || isNull ((pc :?> IMeta).meta())) ->
            Expr.Literal(Env = cenv, Form = form, Type=EmptyType, Value = pc)
        | _ as v -> Expr.Literal(Env = cenv, Form = form, Type=OtherType, Value = v)


    static member ValidateBindingSymbol (s : obj) : Symbol = 
        match s with
        | :? Symbol as sym -> 
            if isNull sym.Namespace then sym
            else raise <| ParseException($"Can't let qualified name: {sym}")
        | _ -> raise <| ParseException($"Bad binding form, expected symbol, got: {s}")

    static member LetExprParser(cenv: CompilerEnv, form: ISeq) : Expr =
       
        // form => (let  [var1 val1 var2 val2 ... ] body ... )
        //      or (loop [var1 val1 var2 val2 ... ] body ... )

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
        let mutable initCtx =  {cenv with Pctx = Expression; IsAssignContext = false; LoopId = loopId}
        let bindingInits = ResizeArray<BindingInit>()
        let loopLocals = ResizeArray<LocalBinding>()

        for i in 0 .. 2 .. (bindings.count()-1) do
            let sym = Parser.ValidateBindingSymbol(bindings.nth(i))

            let initForm = bindings.nth(i+1)
            let initExpr = Parser.Analyze(initCtx, initForm, sym.Name)

            // A bunch of analysis and validation goes here in the original code -- related to recur mismatches.
            //  And it adds wrapping expressions to handle boxing, plus warnings.
            // TODO: put into a later pass

            let localBinding = { Sym = sym; Tag = null; Init = Some initExpr; Name = sym.Name; IsArg = false; IsByRef = false; IsRecur = isLoop; IsThis = false; Index = i/2 }
            let bindingInit = { Binding = localBinding; Init = initExpr }
            bindingInits.Add(bindingInit)
            if isLoop then loopLocals.Add(localBinding)

            // progressive enhancement of bindings
            initCtx <- { initCtx with Locals = (RTMap.assoc(initCtx.Locals, sym, localBinding) :?> IPersistentMap) }

        // TODO: Original code also sets up MethodReturnContextVar , either pass-along or null  Do we need this?
        let bodyCtx = { initCtx with Pctx = (if isLoop then Return else cenv.Pctx); IsRecurContext = isLoop; LoopLocals=(if isLoop then loopLocals else null);}
        let bodyExpr = Parser.BodyExprParser(bodyCtx, body)
        Expr.Let(Env = cenv, Form = form, BindingInits = bindingInits, Body = bodyExpr, LoopId = loopId, Mode=(if isLoop then Loop else Let), SourceInfo=None)  // TODO: source info

            
    static member RecurExprParser(cenv: CompilerEnv, form: ISeq) : Expr = 
        
            // TODO: source info

            let loopLocals = cenv.LoopLocals
            if cenv.Pctx <> Return  || isNull loopLocals then
                raise <| ParseException("Can only recur from tail position")

            if not <| cenv.IsRecurContext then
                raise <| ParseException("Can only recur across try")

            let args = ResizeArray<Expr>()

            let argCtx = { cenv with Pctx = Expression; IsAssignContext = false }

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

            Expr.Recur(Env = cenv, Form = form, Args = args, LoopLocals = loopLocals, SourceInfo=None)  // TODO: SourceInfo


    static member LetFnExprParser(cenv: CompilerEnv, form: ISeq) : Expr = 

        // form => (letfn*  [var1 (fn [args] body) ... ] body ... )

        let bindings = 
            match RTSeq.second(form) with
            | :? IPersistentVector as pv -> pv
            | _ -> raise <| ParseException("Bad binding form, expected vector")

        if bindings.count() % 2 <> 0 then
            raise <| ParseException("Bad binding form, expected matched symbol/expression pairs")

        let body = RTSeq.next(RTSeq.next(form))

        // Original code did a wrapper here.  Do we need to do this?
                    //   if (pcon.Rhc == RHC.Eval)
                    //return Compiler.Analyze(pcon, RT.list(RT.list(Compiler.FnOnceSym, PersistentVector.EMPTY, form)), "letfn__" + RT.nextID());

        // because mutually recursive functions are allowed, we need to pre-seed the locals map before we parse the functions in the initializations
        let mutable contextLocalBindings = cenv.Locals
        let lbs = ResizeArray<LocalBinding>()

        for i in 0 .. 2 .. (bindings.count()-1) do
            let sym = Parser.ValidateBindingSymbol(bindings.nth(i))

            let lb = { Sym = sym; Tag = null; Init = None; Name = sym.Name; IsArg = false; IsByRef = false; IsRecur = false; IsThis = false; Index = i/2 }
            lbs.Add(lb)
            contextLocalBindings <- contextLocalBindings.assoc(sym,lb)

        let bindingInits = ResizeArray<BindingInit>()
        let initCtxt = { cenv with Locals = contextLocalBindings; Pctx = Expression; IsAssignContext = false }

        for i in 0 .. 2 .. (bindings.count()-1) do
            let sym = Parser.ValidateBindingSymbol(bindings.nth(i))
            let init = Parser.Analyze(initCtxt, bindings.nth(i+1), sym.Name)
            let lb = lbs[i/2]
            lb.Init <- Some init
            let bindingInit = { Binding = lb; Init = init }
            bindingInits.Add(bindingInit)

        Expr.Let(Env = cenv, Form = form, BindingInits = bindingInits, Body = Parser.BodyExprParser(cenv, body), Mode=LetFn, LoopId = None, SourceInfo=None)  // TODO: source info



    static member DefExprParser(cenv: CompilerEnv, form: ISeq) : Expr =

        // (def x) or (def x initexpr) or (def x "docstring" initexpr)

        let docString, form =
            if RT0.count(form) = 4 && RTSeq.third(form) :? String then
                (RTSeq.third(form) :?> string), RTSeq.list(RTSeq.first(form), RTSeq.second(form), RTSeq.fourth(form))
            else
                null, form

        if RT0.count(form) > 3 then
            raise <| ParseException("Too many  arguments to def")

        if RT0.count(form) < 2 then
            raise <| ParseException("Too few arguments to def")

        let sym = 
            match RTSeq.second(form) with
            | :? Symbol as sym -> sym
            | _ -> raise <| ParseException("First argument to def must be a symbol")

        let mutable var = Parser.LookupVar(cenv,sym,true)

        if isNull var then
            raise <| ParseException($"Can't refer to qualified var that doesn't exist: {sym}")

        let currentNS = RTVar.getCurrentNamespace()
        let mutable shadowsCoreMapping = false

        if not <| Object.ReferenceEquals(var.Namespace, currentNS) then
            if isNull sym.Namespace then
                var <- currentNS.intern(sym)
                shadowsCoreMapping <- true
                Parser.RegisterVar(var)
            else
                raise <| ParseException($"Can't create defs outside of current namespace: {sym}")

        let mm = RT0.meta(form)
        let isDynamic = RT0.booleanCast(RT0.get(mm, RTVar.DynamicKeyword))
        if isDynamic then
            var.setDynamic() |> ignore

        if not isDynamic && sym.Name.StartsWith("*") && sym.Name.EndsWith("*") then
            RTVar.errPrintWriter().WriteLine($"Warning: {sym} not declared dynamic and thus is not dynamically rebindable, but its name suggests otherwise. Please either indicate ^:dynamic {sym} or change the name.")
            // TODO: source info
            RTVar.errPrintWriter().Flush()

        if RT0.booleanCast(RT0.get(mm, RTVar.ArglistsKeyword)) then
            let vm = RTMap.assoc( (var :> IMeta).meta(), RTVar.ArglistsKeyword, RTSeq.second(mm.valAt(RTVar.ArglistsKeyword)))
            var.setMeta(vm :?> IPersistentMap)

        // TODO: Source-info stuff

        let mm =
            if isNull docString then mm
            else RTMap.assoc(mm, RTVar.DocKeyword, docString) :?> IPersistentMap

        let mm = Parser.ElideMeta(mm);

        let exprCtx = { cenv with Pctx = Expression}
        let meta = if isNull mm || (mm :> Counted).count() = 0 then None else Some <| Parser.Analyze(exprCtx, mm)
        let init = Parser.Analyze(exprCtx, RTSeq.third(form), var.Symbol.Name)
        let initProvided = RT0.count(form) = 3

        Expr.Def(Env = cenv, Form = form, Var = var, Init = init, InitProvided = initProvided, IsDynamic = isDynamic, ShadowsCoreMapping = shadowsCoreMapping, Meta = meta, SourceInfo = None)  // TODO: source info})


    // This naming convention drawn from the Java code.   
    // Can this be deferred until it is time time to emit?
    static member ComputeFnNames(cenv: CompilerEnv, form: ISeq, name:string) : string * string =
        let enclosingMethod = cenv.Method

        let baseName =
            match enclosingMethod with
            | Some m -> m.Name
            | None -> Munger.Munge(RTVar.getCurrentNamespace().Name.Name + "$")

        let newName =
            match RTSeq.second(form) with
            | :? Symbol as sym -> $"{sym.Name}__{RT0.nextID()}"
            | _ when isNull name -> $"fn__{RT0.nextID()}"
            | _ when enclosingMethod.IsNone -> name
            | _ -> $"{name}__{RT0.nextID()}"

        let simpleName = Munger.Munge(newName).Replace(".", "_DOT_")

        let finalName = baseName + simpleName

        // TODO: The original code has a comment that indicates the second value used to be finalName.Replace('.','/') 
        // I'm not sure why I took it out.  I'm going to put it back in to see what happens.
        // BTW, noe that simpleName had all dots replace, but baseName might still have dots?
        (finalName, finalName.Replace('.', '/') )
               
        

    static member FnExprParser(cenv: CompilerEnv, form: ISeq, name: string) : Expr = 

            let origForm = form

            let enclosingMethod = cenv.Method
            let onceOnly = 
                match RT0.meta(form.first()) with
                | null -> false
                | _ as m -> RT0.booleanCast(RT0.get(m, RTVar.OnceOnlyKeyword))

            //arglist might be preceded by symbol naming this fn
            let nm, form =
                match RTSeq.second(form) with
                | :? Symbol as sym -> sym, (RTSeq.cons(RTVar.FnSym, RTSeq.next(form)))
                | _ -> null, form

            let name, internalName = Parser.ComputeFnNames(cenv, form, name)

            let register = ObjXRegister(cenv.ObjXRegister)
            let internals =ObjXInternals(
                Name = name, 
                InternalName = internalName, 
                ThisName = (if isNull nm then null else nm.Name), 
                Tag = Parser.TagOf(form),
                OnceOnly = onceOnly, 
                HasEnclosingMethod = enclosingMethod.IsSome)

            let fnExpr = Expr.Obj(
                Env = cenv, 
                Form = origForm,
                Type = ObjXType.Fn,
                Internals = internals,
                Register = register,
                SourceInfo = None )  // TODO: source info  -- original has a SpanMap field

            let newCenv = { cenv with ObjXRegister = Some register; NoRecur = false }

            let retTag = RT0.get(RT0.meta(form), RTVar.RettagKeyword)

            // Normalize body
            // Desired form is (fn ([args] body...) ...)
            // Mignt have (fn [args] body...) -- if so, convert to desired form
            let form = 
                match RTSeq.second(form) with
                | :? IPersistentVector -> RTSeq.list(RTVar.FnSym, RTSeq.next(form))
                | _ -> form

            // Because generation happens during parsing in the original code, we generate a context with a dynInitHelper here
            //  and push it as the value of CompilerContextVar.  
            // TODO: move this to a later pass

            // Here the original code pushes a bunch of Var bindings.

            //Var.pushThreadBindings(RT.mapUniqueKeys(
            //    Compiler.ConstantsVar, PersistentVector.EMPTY,
            //    Compiler.ConstantIdsVar, new IdentityHashMap(),
            //    Compiler.KeywordsVar, PersistentHashMap.EMPTY,
            //    Compiler.VarsVar, PersistentHashMap.EMPTY,
            //    Compiler.KeywordCallsitesVar, PersistentVector.EMPTY,
            //    Compiler.ProtocolCallsitesVar, PersistentVector.EMPTY,
            //    Compiler.VarCallsitesVar, Compiler.EmptyVarCallSites(),
            //    Compiler.NoRecurVar, null));

            let methods = SortedDictionary<int, ObjMethod>()
            let mutable variadicMethod : ObjMethod option = None
            let mutable usesThis = false

            let rec loop (s: ISeq) =
                match s with
                | null -> ()
                | _ -> 
                    let m : ObjMethod = Parser.FnMethodParser(newCenv, s.first(), fnExpr, internals, retTag)
                    if m.IsVariadic then
                        if variadicMethod.IsSome then
                            raise <| ParseException("Can't have more than one variadic overload")
                        variadicMethod <- Some m
                    elif methods.ContainsKey(m.NumParams) then
                        raise <| ParseException("Can't have two overloads with the same arity")
                    else
                        methods.Add(m.NumParams, m)
                    usesThis <- usesThis || m.UsesThis
                    loop(s.next())
            loop(RTSeq.next(form))

            match variadicMethod with
            | Some vm when methods.Count > 0 && methods.Keys.Max() >= vm.NumParams ->
                raise <| ParseException("Can't have fixed arity methods with more params than the variadic method.")
            | _ -> ()

            // TODO: Defer this until emit time
            //fn.CanBeDirect <- ! fn.HasEnclosingMethod && not usesThis && fn.Closes.Count() == 0

            
            //if ( fn.CanBeDirect )
            //{
            //    for (ISeq s = RT.seq(allMethods); s != null; s = s.next())
            //    {
            //        FnMethod fm = s.first() as FnMethod;
            //        if ( fm.Locals != null)
            //        {
            //            for (ISeq sl = RT.seq(RT.keys(fm.Locals)); sl != null; sl = sl.next())
            //            {
            //                LocalBinding lb = sl.first() as LocalBinding;
            //                if ( lb.IsArg)
            //                    lb.Index -= 1;
            //            }
            //        }
            //    }
            //}

            let allMethods = 
                match variadicMethod with
                | Some vm -> methods.Values |> Seq.append [vm]
                | None -> methods.Values

            internals.Methods <-  allMethods.ToList()
            internals.VariadicMethod <- variadicMethod

            fnExpr


    static member FnMethodParser(cenv: CompilerEnv, form: obj, objx: Expr, objxInternals: ObjXInternals, retTag: obj) =
        // ([args] body ... )

        let parameters = RTSeq.first(form) :?> IPersistentVector
        let body = RTSeq.next(form)

        let method = ObjMethod(ObjXType.Fn, objx, cenv.Method)  // TODO: source info



        // TODO: Original code does prim interface calculation based on the params.

        let symRetTag =
            match retTag with
            | :? Symbol as sym -> sym
            | :? String as str -> Symbol.intern(null, str)
            | _ -> null

        // TODO: original code sets symRetTag to null if not symRetTag.name = "long" or "double"
        //       Need to figure this out.  Classic mode?

        method.RetType <- Parser.TagType(
            match Parser.TagOf(parameters) with
            | null -> symRetTag
            | _ as tag -> tag)

        // TODO: original code has check for primitive here.  Rests to typeof<Object> if not primitive

        // register 'this' as local 0  
        let thisName = 
            match objxInternals.ThisName with
            | null -> $"fn__{RT0.nextID()}"
            | _ as n  -> n

        let mutable newEnv, _ = { cenv with Method = Some method }.RegisterLocalThis(Symbol.intern(null,thisName),null,None)

        let validateParam(param: obj) =
            match param with
            | :? Symbol as sym -> 
                if not <| isNull sym.Namespace then
                    raise <| ParseException("Can't use qualified name as paramter: {sym}")
                sym
            | _ -> raise <| ParseException("fn params must be Symbols")

        let mutable paramState = ParamParseState.Required
        let paramCount = parameters.count()
        let argTypes = ResizeArray<Type>()

        for i = 0 to paramCount - 1 do
            let param = validateParam(parameters.nth(i))
            if RTVar.AmpersandSym.Equals(param) then
                if paramState = ParamParseState.Required then
                    paramState <- ParamParseState.Rest
                else
                    raise <| ParseException("Invalid parameter list")
            else
                // TODO: original code does primitive type analysis here
                let paramTag = Parser.TagOf(param)

                if paramState = ParamParseState.Rest && not <| isNull paramTag  then
                    raise <| ParseException("& arg cannot have type hint")

                // original code checks for primtive type signature on method + variadic

                let paramType = 
                    if paramState = ParamParseState.Rest then
                        typeof<ISeq>
                    else
                        Parser.TagType(paramTag)
                argTypes.Add(paramType)

                let env, b = newEnv.RegisterLocal(param, (if paramState = Rest then RTVar.ISeqSym else paramTag), None, paramType, true )
                newEnv <- env
                method.ArgLocals.Add(b)

                match paramState with
                | ParamParseState.Required -> method.ReqParams.Add(b)
                | ParamParseState.Rest -> 
                    method.RestParam <- Some b
                    paramState <- Done
                | _ -> raise <| ParseException("Unexpected parameter")

        if method.ReqParams.Count > Parser.MaxPositionalArity then 
            raise <| ParseException("Can't specify more than {Parser.MaxPositionalArity} positional arguments")
        
        let bodyEnv = { newEnv with Pctx = Return; LoopLocals = method.ArgLocals }
        method.Body <- Parser.BodyExprParser(bodyEnv, body)

        method



    static member TryExprParser(cenv: CompilerEnv, form: obj) : Expr =
        let form = form :?> ISeq


        if cenv.Pctx <> ParserContext.Return then        
            // I'm not sure why we do this.
            Parser.Analyze(cenv, RTSeq.list(RTSeq.list(RTVar.FnOnceSym, PersistentVector.Empty, form)), $"try__{RT0.nextID()}")
        else

            // (try try-expr* catch-expr* finally-expr?)
            // catch-expr: (catch class sym expr*)
            // finally-expr: (finally expr*)

            let body = ResizeArray<obj>()
            let catches = ResizeArray<CatchClause>()
            let mutable bodyExpr : Expr option = None
            let mutable finallyExpr : Expr option = None
            let mutable caught = false


            let catchEnv = { cenv with Pctx = Expression; IsAssignContext = false; InCatchFinally = true }

            let rec loop (fs:ISeq) = 
                let f = fs.first()
                let op = 
                    match f with
                    | :? ISeq as fseq -> fseq.first()
                    | _ -> null
                if not <| Util.equals(op,RTVar.CatchSym) && not <| Util.equals(op,RTVar.FinallySym) then
                    if caught then
                        raise <| ParseException("Only catch or finally can follow catch")
                    body.Add(f)
                else
                    // We have either a catch or finally.  Process accordingly
                    
                    if bodyExpr.IsNone then
                        // We are on our first catch or finally.  
                        // All body forms are collected, so build the expression for the body
                        let bodyEnv = { cenv with NoRecur = true }
                        bodyExpr <- Some(Parser.BodyExprParser(bodyEnv, RT0.seq(body)))


                    if Util.equals(op,RTVar.CatchSym) then
                        let second =  RTSeq.second(f)
                        let t = Parser.MaybeType(catchEnv, second, false)
                        if isNull t then
                            raise <| ParseException($"Unable to resolve classname: {RTSeq.second(f)}")
                        match RTSeq.third(f) with
                        | :? Symbol as sym ->
                            if not <| isNull sym.Namespace then
                                raise <| ParseException($"Can't bind qualified name: {sym}")

                            // Everything is looking good, let's parse the catch clause
                            let clauseTag =
                                match second with 
                                | :? Symbol as sym -> sym
                                | _ -> null
                            let boundEnv, lb = catchEnv.RegisterLocal(sym, clauseTag, None, typeof<Object>, false)
                            let handler = Parser.BodyExprParser(boundEnv, RTSeq.next(RTSeq.next(RTSeq.next(f))))
                            catches.Add({CaughtType = t; LocalBinding = lb; Handler = handler})
                            
                        | _ as third -> raise <| ParseException($"Bad binding form, expected Symbol, got: {third}")

                    else
                        // finally clause
                        if not <| isNull (fs.next()) then
                            raise <| InvalidOperationException("finally clause must be last in try expression")
                        let finallyEnv = { cenv with Pctx = Statement; IsAssignContext = false; InCatchFinally = true }
                        finallyExpr <- Some(Parser.BodyExprParser(finallyEnv, RTSeq.next(f)))
                loop (fs.next()) 
                
            loop (form.next())

            if bodyExpr.IsNone then

                // the only way this happens if there were catch or finally clauses.  
                // We can return just the body expression itself.
                Parser.BodyExprParser(cenv, RT0.seq(body))

            else
                Expr.Try(Env = cenv, Form = form, TryExpr = bodyExpr.Value, Catches = catches, Finally = finallyExpr)  // TODO: source info)


    static member InvokeExprParser(cenv: CompilerEnv, form: ISeq) : Expr = Parser.NilExprInstance



    static member NewExprParser(cenv: CompilerEnv, form: ISeq) : Expr = Parser.NilExprInstance

    static member HostExprParser(cenv: CompilerEnv, form: ISeq) : Expr = Parser.NilExprInstance



    // Saving for later, when I figure out what I'm doing
    static member DefTypeParser()(cenv: CompilerEnv, form: ISeq) : Expr = Parser.NilExprInstance
    static member ReifyParser()(cenv: CompilerEnv, form: ISeq) : Expr = Parser.NilExprInstance
    static member CaseExprParser(cenv: CompilerEnv, form: ISeq) : Expr = Parser.NilExprInstance
   
    // helper methods
   
    static member OptionallyGenerateMetaInit(cenv: CompilerEnv, form: obj, expr: Expr) : Expr =
        match RT0.meta(form) with
        | null -> expr
        | _ as meta -> Expr.Meta(Env = cenv, Form = form, Target = expr, Meta = Parser.AnalyzeMap(cenv, meta))

    static member TagOf(o: obj) = 
        match RT0.get(RT0.meta(), RTVar.TagKeyword) with
        | :? Symbol as sym -> sym
        | :? string as str -> Symbol.intern(null,str)
        | :? Type as t -> 
            let ok, sym = TypeToTagDict.TryGetValue(t)
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
            
            

    // TODO: Source info needed,   context is not needed (probably)
    static member CreateStaticFieldOrPropertyExpr(cenv: CompilerEnv, form: obj, tag: Symbol, t: Type, memberName: string, info: MemberInfo) : Expr =
        Expr.Host(Env = cenv, Form = form, Type=FieldOrPropertyExpr, Tag = tag, Target = None, TargetType = t, MemberName = memberName, TInfo = info, Args = null,  IsTailPosition = false, SourceInfo = None)

    static member MaybeType(cenv: CompilerEnv, form: obj, stringOk: bool) = 
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
                    | _ when cenv.ContainsBindingForSym(sym) -> null
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


    static member IsInline(cenv: CompilerEnv, op: obj, arity: int) : IFn =
        let v = 
            match op with
            | :? Var as v -> v
            | :? Symbol as s -> 
                match cenv.GetLocalBinding(s) with
                | Some _  -> Parser.LookupVar(cenv, s, false)
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


    static member IsMacro(cenv: CompilerEnv, op: obj) : Var = 
        let checkVar(v: Var) = 
            if not <| isNull v && v.isMacro then
                if v.Namespace = RTVar.getCurrentNamespace() && not v.isPublic then
                    raise <| new InvalidOperationException($"Var: {v} is not public")
        match op with
        | :? Var as v -> checkVar(v); v
        | :? Symbol as s -> 
            if cenv.ContainsBindingForSym(s) then
                null
            else
                match Parser.LookupVar(cenv, s, false, false) with
                | null -> null
                | _ as v -> checkVar(v); v
        | _ -> null

    static member LookupVar(cenv:CompilerEnv, sym: Symbol, internNew: bool) = Parser.LookupVar(cenv, sym, internNew, true)

    static member LookupVar(cenv: CompilerEnv, sym: Symbol, internNew: bool, registerMacro: bool) : Var = 
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
                cenv.RegisterVar(var)   
 
        var
        

    // Macroexpansion

    // TODO: Macrochecking via Spec
    static member CheckSpecs(v: Var, form: ISeq) = ()

    static member Macroexpand1(form: obj) = Parser.Macroexpand1(CompilerEnv.Create(Expression), form)
    static member Macroexpand(form : obj) = Parser.Macroexpand(CompilerEnv.Create(Expression), form)

    static member Macroexpand1(cenv: CompilerEnv, form: obj) =
        match form with 
        | :? ISeq as s -> Parser.MacroexpandSeq1(cenv, s)
        | _ -> form

    static member Macroexpand(cenv: CompilerEnv, form : obj) = 
        let exf = Parser.Macroexpand1(cenv, form)
        if Object.ReferenceEquals(exf, form) then form 
        else Parser.Macroexpand(cenv, exf)

    static member private MacroexpandSeq1(cenv: CompilerEnv, form: ISeq) = 
        let op = form.first()
        if (LispReader.IsSpecial(form)) then
            form
        else
            match Parser.IsMacro(cenv, op) with
            | null -> Parser.MacroExpandNonSpecial(cenv, op, form)
            | _ as v -> Parser.MacroExpandSpecial(cenv, v, form)

    static member private MacroExpandSpecial(cenv: CompilerEnv, v: Var, form: ISeq) = 
        Parser.CheckSpecs(v,form)
        try
            // Here is macro magic -- supply the &form and &env args in front
            let args = RTSeq.cons(form, RTSeq.cons(cenv.Locals, form.next()))
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

    static member private MacroExpandNonSpecial(cenv: CompilerEnv, op: obj, form: ISeq) = 
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
                if not <| isNull (Parser.MaybeType(cenv, target, false)) then
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
        |  Expr.LocalBinding _
        |  Expr.Var _ -> true
        |  Expr.Host(Type=hostType)  when hostType = HostExprType.FieldOrPropertyExpr -> true
        | _ -> false

    static member ElideMeta(m: IPersistentMap) = m  // TODO: Source-info

    static member RegisterVar(v: Var) = ()  // TODO: constants registration