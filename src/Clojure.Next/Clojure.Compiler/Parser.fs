namespace Clojure.Compiler

open Clojure.Collections
open Clojure.IO
open Clojure.Lib
open Clojure.Numerics
open Clojure.Reflection
open System
open System.Collections
open System.Collections.Generic
open System.IO
open System.Linq
open System.Reflection
open System.Text.RegularExpressions

// Signature for special form parser methods
type SpecialFormParser = CompilerEnv * ISeq -> Expr

exception ParseException of string

type ParamParseState =
    | Required
    | Rest
    | Done

[<Sealed; AbstractClass>]
type Parser private () =

    /////////////////////////////////
    //
    //  Some preliminaries
    //
    ////////////////////////////////

    static let ConstKeyword = Keyword.intern (null, "const")
    static let IdentitySym = Symbol.intern ("clojure.core", "identity")
    static let ClassSym = Symbol.intern ("System", "Type")

    static let mutable CompilerOptionsVar: Var = null

    // Why not a Dictionary<Symbol, SpecialFormParser> ???
    static let SpecialFormToParserMap: IPersistentMap =
        PersistentHashMap.create (
            RTVar.DefSym,
            Parser.DefExprParser,
            RTVar.LoopSym,
            Parser.LetExprParser,
            RTVar.RecurSym,
            Parser.RecurExprParser,
            RTVar.IfSym,
            Parser.IfExprParser,
            RTVar.CaseSym,
            Parser.CaseExprParser,
            RTVar.LetSym,
            Parser.LetExprParser,
            RTVar.LetfnSym,
            Parser.LetFnExprParser,
            RTVar.DoSym,
            Parser.BodyExprParser,
            RTVar.FnSym,
            null, // FnSym is a special case.  It parser takes an additional argument.
            RTVar.QuoteSym,
            Parser.ConstantExprParser,
            RTVar.TheVarSym,
            Parser.TheVarExprParser,
            RTVar.ImportSym,
            Parser.ImportExprParser,
            RTVar.DotSym,
            Parser.HostExprParser,
            RTVar.AssignSym,
            Parser.AssignExprParser,
            RTVar.DeftypeSym,
            Parser.DefTypeParser(),
            RTVar.ReifySym,
            Parser.ReifyParser(),
            RTVar.TrySym,
            Parser.TryExprParser,
            RTVar.ThrowSym,
            Parser.ThrowExprParser,
            RTVar.MonitorEnterSym,
            Parser.MonitorEnterExprParser,
            RTVar.MonitorExitSym,
            Parser.MonitorExitExprParser,
            RTVar.CatchSym,
            null,
            RTVar.FinallySym,
            null,
            RTVar.NewSym,
            Parser.NewExprParser,
            RTVar.AmpersandSym,
            null
        )

    static do Parser.InitializeCompilerOptions() // TODO: in the original code, we had to move this call from here to the RT initialization


    static member val MaxPositionalArity = 20 // TODO: Do we want to adjust this down to 16? (Match for System.Func)

    static member val NilExprInstance = Expr.Literal(Env = CompilerEnv.Empty, Form = null, Type = NilType, Value = null)

    static member val TrueExprInstance =
        Expr.Literal(Env = CompilerEnv.Empty, Form = true, Type = BoolType, Value = true)

    static member val FalseExprInstance =
        Expr.Literal(Env = CompilerEnv.Empty, Form = false, Type = BoolType, Value = false)

    static member GetSpecialFormParser(op: obj) = SpecialFormToParserMap.valAt (op)


    /////////////////////////////////
    //
    //  Main entry points for parsing
    //
    ////////////////////////////////

    static member Analyze(cenv: CompilerEnv, form: obj) : Expr = Parser.Analyze(cenv, form, null)

    static member Analyze(cenv: CompilerEnv, form: obj, name: string) : Expr =

        try
            // If we have a LazySeq, realize it and  attach the meta data from the initial form
            let form =
                match form with
                | :? LazySeq as ls ->
                    let realized =
                        match RT0.seq (ls) with
                        | null -> PersistentList.Empty :> obj
                        | _ as s -> s

                    (realized :?> IObj).withMeta (RT0.meta (form)) :> obj
                | _ -> form

            match form with
            | null -> Parser.NilExprInstance
            | :? bool as b ->
                if b then
                    Parser.TrueExprInstance
                else
                    Parser.FalseExprInstance
            | :? Symbol as sym -> Parser.AnalyzeSymbol(cenv, sym)
            | :? Keyword as kw ->
                cenv.RegisterKeyword(kw)
                Expr.Literal(Env = cenv, Form = form, Type = KeywordType, Value = kw)
            | _ when Numbers.IsNumeric(form) -> Parser.AnalyzeNumber(cenv, form)
            | :? String as s -> Expr.Literal(Env = cenv, Form = form, Type = StringType, Value = String.Intern(s))
            | :? IPersistentCollection as coll when not <| form :? IType && not <| form :? IRecord && coll.count () = 0 ->
                Parser.OptionallyGenerateMetaInit(
                    cenv,
                    form,
                    Expr.Literal(Env = cenv, Form = form, Type = EmptyType, Value = coll)
                )
            | :? ISeq as seq -> Parser.AnalyzeSeq(cenv, seq, name)
            | :? IPersistentVector as pv -> Parser.AnalyzeVector(cenv, pv)
            | :? IRecord -> Expr.Literal(Env = cenv, Form = form, Type = OtherType, Value = form)
            | :? IType -> Expr.Literal(Env = cenv, Form = form, Type = OtherType, Value = form)
            | :? IPersistentMap as pm -> Parser.AnalyzeMap(cenv, pm)
            | :? IPersistentSet as ps -> Parser.AnalyzeSet(cenv, ps)
            | _ -> Expr.Literal(Env = cenv, Form = form, Type = OtherType, Value = form)

        with
        | :? CompilerException -> reraise ()
        | _ as e -> raise <| CompilerException("HELP!", e) // TODO: add source info


    /////////////////////////////////
    //
    //  Analyzers  (the simpler ones)
    //
    ////////////////////////////////

    // Analyzers
    // The naming distinguishes those methods called directly from Analyze().
    // The methods named ParseXXX are parsers for special forms, called from AnalyzeSeq().

    static member AnalyzeNumber(cenv: CompilerEnv, num: obj) : Expr =
        match num with
        | :? int
        | :? double
        | :? int64 -> Expr.Literal(Env = cenv, Form = num, Type = PrimNumericType, Value = num) // TODO: Why would we not allow other primitive numerics here?
        | _ -> Expr.Literal(Env = cenv, Form = num, Type = OtherType, Value = num)


    // Analyzing the collection types
    // The three are very similar to each other.

    static member AnalyzeVector(cenv: CompilerEnv, form: IPersistentVector) : Expr =
        let mutable constant = true
        let mutable args = PersistentVector.Empty :> IPersistentVector

        for i = 0 to form.count () - 1 do
            let v = Parser.Analyze(cenv, form.nth (i))
            args <- args.cons (v)

            if not <| v.IsLiteral then
                constant <- false

        let ret = Expr.Collection(Env = cenv, Form = form, Value = args)

        match form with
        | :? IObj as iobj when not <| isNull (iobj.meta ()) -> Parser.OptionallyGenerateMetaInit(cenv, form, ret)
        | _ when constant ->
            let mutable rv = PersistentVector.Empty :> IPersistentVector

            for i = 0 to args.count () - 1 do
                rv <- rv.cons (ExprUtils.GetLiteralValue(args.nth (i) :?> Expr))

            Expr.Literal(Env = cenv, Form = form, Type = OtherType, Value = rv)
        | _ -> ret


    static member AnalyzeMap(cenv: CompilerEnv, form: IPersistentMap) : Expr =
        let mutable keysConstant = true
        let mutable valsConstant = true
        let mutable allConstantKeysUnique = true

        let mutable constantKeys = PersistentHashSet.Empty :> IPersistentSet
        let mutable keyvals = PersistentVector.Empty :> IPersistentVector

        let rec loop (s: ISeq) =
            match s with
            | null -> ()
            | _ ->
                let me = s.first () :?> IMapEntry
                let k = Parser.Analyze(cenv, me.key ())
                let v = Parser.Analyze(cenv, me.value ())
                keyvals <- keyvals.cons(k).cons (v)

                if k.IsLiteral then
                    let kval = ExprUtils.GetLiteralValue(k)

                    if constantKeys.contains (kval) then
                        allConstantKeysUnique <- false
                    else
                        constantKeys <- constantKeys.cons (kval) :?> IPersistentSet
                else
                    keysConstant <- false

                if not <| v.IsLiteral then
                    valsConstant <- false

                loop (s.next ())

        loop (RT0.seq (form))

        let ret = Expr.Collection(Env = cenv, Form = form, Value = keyvals)

        match form with
        | :? IObj as iobj when not <| isNull (iobj.meta ()) -> Parser.OptionallyGenerateMetaInit(cenv, form, ret)
        | _ when keysConstant ->
            if not allConstantKeysUnique then
                raise <| ArgumentException("Duplicate constant keys in map")

            if valsConstant then
                let mutable m = PersistentArrayMap.Empty :> IPersistentMap

                for i in 0..2 .. (keyvals.count () - 1) do
                    m <-
                        m.assoc (
                            ExprUtils.GetLiteralValue(keyvals.nth (i) :?> Expr),
                            ExprUtils.GetLiteralValue(keyvals.nth (i + 1) :?> Expr)
                        )

                Expr.Literal(Env = cenv, Form = form, Type = OtherType, Value = m) // TODO: In the old code, this optimization was a big fail when some values were also maps.  Need to think about this.
            else
                ret
        | _ -> ret


    static member AnalyzeSet(cenv: CompilerEnv, form: IPersistentSet) : Expr =
        let mutable constant = true
        let mutable keys = PersistentVector.Empty :> IPersistentVector

        let rec loop (s: ISeq) =
            match s with
            | null -> ()
            | _ ->
                let k = Parser.Analyze(cenv, s.first ())
                keys <- keys.cons (k)

                if not <| k.IsLiteral then
                    constant <- false

                loop (s.next ())

        loop (form.seq ())

        let ret = Expr.Collection(Env = cenv, Form = form, Value = keys)

        match form with
        | :? IObj as iobj when not <| isNull (iobj.meta ()) -> Parser.OptionallyGenerateMetaInit(cenv, form, ret)
        | _ when constant ->
            let mutable rv = PersistentHashSet.Empty :> IPersistentCollection
            let x: Expr = ret

            for i = 0 to keys.count () - 1 do
                rv <- rv.cons (ExprUtils.GetLiteralValue(keys.nth (i) :?> Expr))

            Expr.Literal(Env = cenv, Form = form, Type = OtherType, Value = rv)
        | _ -> ret


    /////////////////////////////////
    //
    //  AnalyzeSymbol  & AnalyzeSeq
    //
    ////////////////////////////////

    static member AnalyzeSymbol(cenv: CompilerEnv, sym: Symbol) : Expr =

        let tag = TypeUtils.TagOf(sym)

        // See if we are a local variable or a field/property/QM reference
        let maybeExpr: Expr option =
            if isNull sym.Namespace then
                // we might be a local variable
                match cenv.ReferenceLocal(sym) with
                | Some lb -> Some(Expr.LocalBinding(Env = cenv, Form = sym, Binding = lb, Tag = tag))
                | None -> None

            elif isNull (RTReader.NamespaceFor(sym)) && not <| RTType.IsPosDigit(sym.Name) then

                // we have a namespace, coudl be Typename/Field
                let nsSym = Symbol.intern (sym.Namespace)

                match TypeUtils.MaybeType(cenv, nsSym, false) with
                | null -> None
                | _ as t ->
                    let info = Reflector.GetFieldOrPropertyInfo(t, sym.Name, true)

                    if not <| isNull info then
                        Some(Parser.CreateStaticFieldOrPropertyExpr(cenv, sym, tag, t, sym.Name, info))
                    else
                        Some(Parser.CreateQualifiedMethodExpr(cenv, t, sym))

            else
                None

        match maybeExpr with
        | Some e -> e
        | None ->
            match Parser.Resolve(cenv, sym) with
            | :? Var as v ->
                if not <| isNull (Parser.IsMacro(cenv, v)) then
                    raise <| CompilerException($"Can't take the value of a macro: {sym.Name}")
                elif RT0.booleanCast (RT0.get ((v :> IMeta).meta (), ConstKeyword)) then
                    Parser.Analyze(cenv.WithParserContext(Expression), RTSeq.list (RTVar.QuoteSym, v))
                else
                    Expr.Var(Env = cenv, Form = sym, Var = v, Tag = tag)
            | :? Type as t -> Expr.Literal(Env = cenv, Form = sym, Type = OtherType, Value = t)
            | :? Symbol -> Expr.UnresolvedVar(Env = cenv, Form = sym, Sym = sym)
            | _ -> raise <| CompilerException($"Unable to resolve symbol: {sym} in this context")


    static member AnalyzeSeq(cenv: CompilerEnv, form: ISeq, name: string) : Expr =
        // TODO: deal with source info

        try
            let me = Parser.MacroexpandSeq1(cenv, form)

            if not <| Object.ReferenceEquals(me, form) then
                Parser.Analyze(cenv, me, name)
            else

                let op = RTSeq.first (form)

                if isNull op then
                    raise <| ArgumentNullException("form", "Can't call nil")

                let inl = Parser.IsInline(cenv, op, RT0.count (RTSeq.next (form)))

                if not <| isNull inl then
                    Parser.Analyze(
                        cenv,
                        Parser.MaybeTransferSourceInfo(Parser.PreserveTag(form, inl.applyTo (RTSeq.next (form))), form)
                    )
                elif op.Equals(RTVar.FnSym) then
                    Parser.FnExprParser(cenv, form, name)
                else
                    match Parser.GetSpecialFormParser(op) with
                    | null -> Parser.InvokeExprParser(cenv, form)
                    | _ as parseFn -> (parseFn :?> SpecialFormParser) (cenv, form)
        with
        | :? CompilerException -> reraise ()
        | _ as e ->
            let sym =
                match RTSeq.first (form) with
                | :? Symbol as sym -> sym
                | _ -> null

            raise <| new CompilerException("help", 0, 0, sym, e) // TODO: pass source info



    /////////////////////////////////
    //
    //  Special form parsers (the simpler ones)
    //
    ////////////////////////////////

    // These are called from AnalyzeSeq() to parse the various special forms
    // Let's start with some easy ones

    static member ImportExprParser(cenv: CompilerEnv, form: ISeq) : Expr =
        Expr.Import(Env = cenv, Form = form, Typename = (RTSeq.second (form) :?> string))

    static member MonitorEnterExprParser(cenv: CompilerEnv, form: ISeq) : Expr =
        let cenv = cenv.WithParserContext(Expression)

        Expr.Untyped(
            Env = cenv,
            Form = form,
            Type = MonitorEnter,
            Target = (Some <| Parser.Analyze(cenv, RTSeq.second (form)))
        )

    static member MonitorExitExprParser(cenv: CompilerEnv, form: ISeq) : Expr =
        let cenv = cenv.WithParserContext(Expression)

        Expr.Untyped(
            Env = cenv,
            Form = form,
            Type = MonitorExit,
            Target = (Some <| Parser.Analyze(cenv, RTSeq.second (form)))
        )


    // cranking up the difficulty level

    static member AssignExprParser(cenv: CompilerEnv, form: ISeq) : Expr =

        if RT0.length (form) <> 3 then
            raise <| ParseException("Malformed assignment, expecting (set! target val)")

        let targetCtx =
            { cenv with
                Pctx = Expression
                IsAssignContext = true }

        let target = Parser.Analyze(targetCtx, RTSeq.second (form))

        if not <| Parser.IsAssignableExpr(target) then
            raise <| ParseException("Invalid assignment target")

        let bodyCtx = cenv.WithParserContext(Expression)
        Expr.Assign(Env = cenv, Form = form, Target = target, Value = Parser.Analyze(bodyCtx, RTSeq.third (form)))


    static member ThrowExprParser(cenv: CompilerEnv, form: ISeq) : Expr =
        // special case for Eval:  Wrap in FnOnceSym
        let cenv =
            { cenv with
                Pctx = Expression
                IsAssignContext = false }

        match RT0.count (form) with
        | 1 -> Expr.Untyped(Env = cenv, Form = form, Type = Throw, Target = None)
        | 2 ->
            Expr.Untyped(
                Env = cenv,
                Form = form,
                Type = Throw,
                Target = (Some <| Parser.Analyze(cenv, RTSeq.second (form)))
            )
        | _ ->
            raise
            <| InvalidOperationException("Too many arguments to throw, throw expects a single Exception instance")

    static member TheVarExprParser(cenv: CompilerEnv, form: ISeq) : Expr =
        match RTSeq.second (form) with
        | :? Symbol as sym ->
            match Parser.LookupVar(cenv, sym, false) with
            | null -> raise <| ParseException($"Unable to resolve var: {sym} in this context")
            | _ as v -> Expr.TheVar(Env = cenv, Form = form, Var = v)
        | _ as v ->
            raise
            <| ParseException($"Second argument to the-var must be a symbol, found: {v}")

    static member BodyExprParser(cenv: CompilerEnv, forms: ISeq) : Expr =
        let forms =
            if Util.equals (RTSeq.first (forms), RTVar.DoSym) then
                RTSeq.next (forms)
            else
                forms

        let stmtcenv = cenv.WithParserContext(Statement)
        let exprs = List<Expr>()

        let rec loop (seq: ISeq) =
            match seq with
            | null -> ()
            | _ ->
                let e =
                    if cenv.Pctx = ParserContext.Statement || not <| isNull (seq.next ()) then
                        Parser.Analyze(stmtcenv, seq.first ())
                    else
                        Parser.Analyze(cenv, seq.first ())

                exprs.Add(e)
                loop (seq.next ())

        loop forms

        Expr.Body(Env = cenv, Form = forms, Exprs = exprs)

    static member IfExprParser(cenv: CompilerEnv, form: ISeq) : Expr =

        // (if test then) or (if test then else)

        if form.count () > 4 then
            raise <| ParseException("Too many arguments to if")

        if form.count () < 3 then
            raise <| ParseException("Too few arguments to if")

        let bodyCtx = { cenv with IsAssignContext = false }
        let testCtx = { bodyCtx with Pctx = Expression }

        let testExpr = Parser.Analyze(testCtx, RTSeq.second (form))
        let thenExpr = Parser.Analyze(bodyCtx, RTSeq.third (form))
        let elseExpr = Parser.Analyze(bodyCtx, RTSeq.fourth (form))

        Expr.If(Env = cenv, Form = form, Test = testExpr, Then = thenExpr, Else = elseExpr, SourceInfo = None) // TODO source info


    static member ConstantExprParser(cenv: CompilerEnv, form: ISeq) : Expr =
        let argCount = RT0.count (form) - 1

        if argCount <> 1 then
            let exData = PersistentArrayMap([| RTVar.FormKeywoard :> obj; form |])

            raise
            <| ExceptionInfo($"Wrong number of arguments ({argCount}) passed to quote", exData)

        match RTSeq.second (form) with
        | null -> Parser.NilExprInstance
        | :? bool as b ->
            if b then
                Parser.TrueExprInstance
            else
                Parser.FalseExprInstance
        | _ as n when Numbers.IsNumeric(n) -> Parser.AnalyzeNumber(cenv, n)
        | :? string as s -> Expr.Literal(Env = cenv, Form = s, Type = StringType, Value = s)
        | :? IPersistentCollection as pc when pc.count () = 0 && (not <| pc :? IMeta || isNull ((pc :?> IMeta).meta ())) ->
            Expr.Literal(Env = cenv, Form = pc, Type = EmptyType, Value = pc)
        | _ as v -> Expr.Literal(Env = cenv, Form = v, Type = OtherType, Value = v)


    /////////////////////////////////
    //
    //  Parser for binding forms: let*, letfn*, loop* + recur
    //
    ////////////////////////////////   

    // Cranking up the difficulty level.
    // let*, letfn*, loop*  all introduce new bindings, causing us to augment the compiler environment as we go

    static member ValidateBindingSymbol(s: obj) : Symbol =
        match s with
        | :? Symbol as sym ->
            if isNull sym.Namespace then
                sym
            else
                raise <| ParseException($"Can't let qualified name: {sym}")
        | _ -> raise <| ParseException($"Bad binding form, expected symbol, got: {s}")

    static member LetExprParser(cenv: CompilerEnv, form: ISeq) : Expr =

        // form => (let  [var1 val1 var2 val2 ... ] body ... )
        //      or (loop [var1 val1 var2 val2 ... ] body ... )

        let isLoop = RTVar.LoopSym.Equals(RTSeq.first (form))

        let bindings =
            match RTSeq.second (form) with
            | :? IPersistentVector as pv -> pv
            | _ -> raise <| ParseException("Bad binding form, expected vector")

        if bindings.count () % 2 <> 0 then
            raise <| ParseException("Bad binding form, expected even number of forms")

        let body = RTSeq.next (RTSeq.next (form))

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

        let loopId = if isLoop then Some(RT0.nextID ()) else None

        let mutable initCtx =
            { cenv with
                Pctx = Expression
                IsAssignContext = false
                LoopId = loopId }

        let bindingInits = ResizeArray<BindingInit>()
        let loopLocals = ResizeArray<LocalBinding>()

        for i in 0..2 .. (bindings.count () - 1) do
            let sym = Parser.ValidateBindingSymbol(bindings.nth (i))

            let initForm = bindings.nth (i + 1)
            let initExpr = Parser.Analyze(initCtx, initForm, sym.Name)

            // A bunch of analysis and validation goes here in the original code -- related to recur mismatches.
            //  And it adds wrapping expressions to handle boxing, plus warnings.
            // TODO: put into a later pass

            let localBinding =
                { Sym = sym
                  Tag = null
                  Init = Some initExpr
                  Name = sym.Name
                  IsArg = false
                  IsByRef = false
                  IsRecur = isLoop
                  IsThis = false
                  Index = i / 2 }

            let bindingInit =
                { Binding = localBinding
                  Init = initExpr }

            bindingInits.Add(bindingInit)

            if isLoop then
                loopLocals.Add(localBinding)

            // progressive enhancement of bindings
            initCtx <- { initCtx with Locals = (RTMap.assoc (initCtx.Locals, sym, localBinding) :?> IPersistentMap) }

        // TODO: Original code also sets up MethodReturnContextVar , either pass-along or null  Do we need this?
        let bodyCtx =
            { initCtx with
                Pctx = (if isLoop then Return else cenv.Pctx)
                IsRecurContext = isLoop
                LoopLocals = (if isLoop then loopLocals else null) }

        let bodyExpr = Parser.BodyExprParser(bodyCtx, body)

        Expr.Let(
            Env = cenv,
            Form = form,
            BindingInits = bindingInits,
            Body = bodyExpr,
            LoopId = loopId,
            Mode = (if isLoop then Loop else Let),
            SourceInfo = None
        ) // TODO: source info


    static member RecurExprParser(cenv: CompilerEnv, form: ISeq) : Expr =

        // TODO: source info

        let loopLocals = cenv.LoopLocals

        if cenv.Pctx <> Return || isNull loopLocals then
            raise <| ParseException("Can only recur from tail position")

        if cenv.NoRecur then
            raise <| ParseException("Cannot recur across try")

        let args = ResizeArray<Expr>()

        let argCtx =
            { cenv with
                Pctx = Expression
                IsAssignContext = false }

        let rec argLoop (s: ISeq) =
            match s with
            | null -> ()
            | _ ->
                args.Add(Parser.Analyze(argCtx, s.first ()))
                argLoop (s.next ())

        argLoop (RTSeq.next (form))

        if args.Count <> loopLocals.Count then
            raise
            <| ParseException(
                $"Mismatched argument count to recur, expected: {loopLocals.Count} args, got {args.Count}"
            )

        // TODO: original code does type checking on the args here.  We'll have to do that in a later pass.

        Expr.Recur(Env = cenv, Form = form, Args = args, LoopLocals = loopLocals, SourceInfo = None) // TODO: SourceInfo


    static member LetFnExprParser(cenv: CompilerEnv, form: ISeq) : Expr =

        // form => (letfn*  [var1 (fn [args] body) ... ] body ... )

        let bindings =
            match RTSeq.second (form) with
            | :? IPersistentVector as pv -> pv
            | _ -> raise <| ParseException("Bad binding form, expected vector")

        if bindings.count () % 2 <> 0 then
            raise
            <| ParseException("Bad binding form, expected matched symbol/expression pairs")

        let body = RTSeq.next (RTSeq.next (form))

        // Original code did a wrapper here.  Do we need to do this?
        //   if (pcon.Rhc == RHC.Eval)
        //return Compiler.Analyze(pcon, RT.list(RT.list(Compiler.FnOnceSym, PersistentVector.EMPTY, form)), "letfn__" + RT.nextID());

        // because mutually recursive functions are allowed, we need to pre-seed the locals map before we parse the functions in the initializations
        let mutable contextLocalBindings = cenv.Locals
        let lbs = ResizeArray<LocalBinding>()

        for i in 0..2 .. (bindings.count () - 1) do
            let sym = Parser.ValidateBindingSymbol(bindings.nth (i))

            let lb =
                { Sym = sym
                  Tag = null
                  Init = None
                  Name = sym.Name
                  IsArg = false
                  IsByRef = false
                  IsRecur = false
                  IsThis = false
                  Index = i / 2 }

            lbs.Add(lb)
            contextLocalBindings <- contextLocalBindings.assoc (sym, lb)

        let bindingInits = ResizeArray<BindingInit>()

        let initCtxt =
            { cenv with
                Locals = contextLocalBindings
                Pctx = Expression
                IsAssignContext = false }

        for i in 0..2 .. (bindings.count () - 1) do
            let sym = Parser.ValidateBindingSymbol(bindings.nth (i))
            let init = Parser.Analyze(initCtxt, bindings.nth (i + 1), sym.Name)
            let lb = lbs[i / 2]
            lb.Init <- Some init
            let bindingInit = { Binding = lb; Init = init }
            bindingInits.Add(bindingInit)

        Expr.Let(
            Env = cenv,
            Form = form,
            BindingInits = bindingInits,
            Body = Parser.BodyExprParser(cenv, body),
            Mode = LetFn,
            LoopId = None,
            SourceInfo = None
        ) // TODO: source info


    /////////////////////////////////
    //
    //  DefExprParser
    //
    ////////////////////////////////

    static member DefExprParser(cenv: CompilerEnv, form: ISeq) : Expr =

        // (def x) or (def x initexpr) or (def x "docstring" initexpr)

        let docString, form =
            if RT0.count (form) = 4 && RTSeq.third (form) :? String then
                (RTSeq.third (form) :?> string),
                RTSeq.list (RTSeq.first (form), RTSeq.second (form), RTSeq.fourth (form))
            else
                null, form

        if RT0.count (form) > 3 then
            raise <| ParseException("Too many  arguments to def")

        if RT0.count (form) < 2 then
            raise <| ParseException("Too few arguments to def")

        let sym =
            match RTSeq.second (form) with
            | :? Symbol as sym -> sym
            | _ -> raise <| ParseException("First argument to def must be a symbol")

        let mutable var = Parser.LookupVar(cenv, sym, true)

        if isNull var then
            raise
            <| ParseException($"Can't refer to qualified var that doesn't exist: {sym}")

        let currentNS = RTVar.getCurrentNamespace ()
        let mutable shadowsCoreMapping = false

        if not <| Object.ReferenceEquals(var.Namespace, currentNS) then
            if isNull sym.Namespace then
                var <- currentNS.intern (sym)
                shadowsCoreMapping <- true
                Parser.RegisterVar(var) // TODO -- call on CENV, get new environment
            else
                raise
                <| ParseException($"Can't create defs outside of current namespace: {sym}")

        let mm = RT0.meta (form)
        let isDynamic = RT0.booleanCast (RT0.get (mm, RTVar.DynamicKeyword))

        if isDynamic then
            var.setDynamic () |> ignore

        if not isDynamic && sym.Name.StartsWith("*") && sym.Name.EndsWith("*") then
            RTVar
                .errPrintWriter()
                .WriteLine(
                    $"Warning: {sym} not declared dynamic and thus is not dynamically rebindable, but its name suggests otherwise. Please either indicate ^:dynamic {sym} or change the name."
                )
            // TODO: source info
            RTVar.errPrintWriter().Flush()

        if RT0.booleanCast (RT0.get (mm, RTVar.ArglistsKeyword)) then
            let vm =
                RTMap.assoc (
                    (var :> IMeta).meta (),
                    RTVar.ArglistsKeyword,
                    RTSeq.second (mm.valAt (RTVar.ArglistsKeyword))
                )

            var.setMeta (vm :?> IPersistentMap)

        // TODO: Source-info stuff

        let mm =
            if isNull docString then
                mm
            else
                RTMap.assoc (mm, RTVar.DocKeyword, docString) :?> IPersistentMap

        let mm = Parser.ElideMeta(mm)

        let exprCtx = { cenv with Pctx = Expression }

        let meta =
            if isNull mm || (mm :> Counted).count () = 0 then
                None
            else
                Some <| Parser.Analyze(exprCtx, mm)

        let init = Parser.Analyze(exprCtx, RTSeq.third (form), var.Symbol.Name)
        let initProvided = RT0.count (form) = 3

        Expr.Def(
            Env = cenv,
            Form = form,
            Var = var,
            Init = init,
            InitProvided = initProvided,
            IsDynamic = isDynamic,
            ShadowsCoreMapping = shadowsCoreMapping,
            Meta = meta,
            SourceInfo = None
        ) // TODO: source info})


    /////////////////////////////////
    //
    //  FnExprParser
    //
    ////////////////////////////////

    // This naming convention drawn from the Java code.
    // Can this be deferred until it is time time to emit?
    static member ComputeFnNames(cenv: CompilerEnv, form: ISeq, name: string) : string * string =
        let enclosingMethod = cenv.Method

        let baseName =
            match enclosingMethod with
            | Some m -> m.Name
            | None -> Munger.Munge(RTVar.getCurrentNamespace().Name.Name + "$")

        let newName =
            match RTSeq.second (form) with
            | :? Symbol as sym -> $"{sym.Name}__{RT0.nextID ()}"
            | _ when isNull name -> $"fn__{RT0.nextID ()}"
            | _ when enclosingMethod.IsNone -> name
            | _ -> $"{name}__{RT0.nextID ()}"

        let simpleName = Munger.Munge(newName).Replace(".", "_DOT_")

        let finalName = baseName + simpleName

        // TODO: The original code has a comment that indicates the second value used to be finalName.Replace('.','/')
        // I'm not sure why I took it out.  I'm going to put it back in to see what happens.
        // BTW, noe that simpleName had all dots replace, but baseName might still have dots?
        (finalName, finalName.Replace('.', '/'))

    static member FnExprParser(cenv: CompilerEnv, form: ISeq, name: string) : Expr =

        let origForm = form

        let enclosingMethod = cenv.Method

        let onceOnly =
            match RT0.meta (form.first ()) with
            | null -> false
            | _ as m -> RT0.booleanCast (RT0.get (m, RTVar.OnceOnlyKeyword))

        //arglist might be preceded by symbol naming this fn
        let nm, form =
            match RTSeq.second (form) with
            | :? Symbol as sym -> sym, (RTSeq.cons (RTVar.FnSym, RTSeq.next (form)))
            | _ -> null, form

        let name, internalName = Parser.ComputeFnNames(cenv, form, name)

        let register = ObjXRegister(cenv.ObjXRegister)

        let internals =
            ObjXInternals(
                Name = name,
                InternalName = internalName,
                ThisName = (if isNull nm then null else nm.Name),
                Tag = TypeUtils.TagOf(form),
                OnceOnly = onceOnly,
                HasEnclosingMethod = enclosingMethod.IsSome
            )

        let fnExpr =
            Expr.Obj(
                Env = cenv,
                Form = origForm,
                Type = ObjXType.Fn,
                Internals = internals,
                Register = register,
                SourceInfo = None
            ) // TODO: source info  -- original has a SpanMap field

        let newCenv =
            { cenv with
                ObjXRegister = Some register
                NoRecur = false }

        let retTag = RT0.get (RT0.meta (form), RTVar.RettagKeyword)

        // Normalize body
        // Desired form is (fn ([args] body...) ...)
        // Mignt have (fn [args] body...) -- if so, convert to desired form
        let form =
            match RTSeq.second (form) with
            | :? IPersistentVector -> RTSeq.list (RTVar.FnSym, RTSeq.next (form))
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
        let mutable variadicMethod: ObjMethod option = None
        let mutable usesThis = false

        let rec loop (s: ISeq) =
            match s with
            | null -> ()
            | _ ->
                let m: ObjMethod =
                    Parser.FnMethodParser(newCenv, s.first (), fnExpr, internals, register, retTag)

                if m.IsVariadic then
                    if variadicMethod.IsSome then
                        raise <| ParseException("Can't have more than one variadic overload")

                    variadicMethod <- Some m
                elif methods.ContainsKey(m.NumParams) then
                    raise <| ParseException("Can't have two overloads with the same arity")
                else
                    methods.Add(m.NumParams, m)

                usesThis <- usesThis || m.UsesThis
                loop (s.next ())

        loop (RTSeq.next (form))

        match variadicMethod with
        | Some vm when methods.Count > 0 && methods.Keys.Max() >= vm.NumParams ->
            raise
            <| ParseException("Can't have fixed arity methods with more params than the variadic method.")
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
            | Some vm -> methods.Values |> Seq.append [ vm ]
            | None -> methods.Values

        internals.Methods <- allMethods.ToList()
        internals.VariadicMethod <- variadicMethod

        fnExpr


    static member FnMethodParser
        (
            cenv: CompilerEnv,
            form: obj,
            objx: Expr,
            objxInternals: ObjXInternals,
            objxRegister: ObjXRegister,
            retTag: obj
        ) =
        // ([args] body ... )

        let parameters = RTSeq.first (form) :?> IPersistentVector
        let body = RTSeq.next (form)

        let method = ObjMethod(ObjXType.Fn, objx, objxInternals, objxRegister, cenv.Method) // TODO: source info



        // TODO: Original code does prim interface calculation based on the params.

        let symRetTag =
            match retTag with
            | :? Symbol as sym -> sym
            | :? String as str -> Symbol.intern (null, str)
            | _ -> null

        // TODO: original code sets symRetTag to null if not symRetTag.name = "long" or "double"
        //       Need to figure this out.  Classic mode?

        method.RetType <-
            TypeUtils.TagType(
                cenv,
                match TypeUtils.TagOf(cenv, parameters) with
                | null -> symRetTag
                | _ as tag -> tag
            )

        // TODO: original code has check for primitive here.  Rests to typeof<Object> if not primitive

        // register 'this' as local 0
        let thisName =
            match objxInternals.ThisName with
            | null -> $"fn__{RT0.nextID ()}"
            | _ as n -> n

        let mutable newEnv, _ =
            { cenv with Method = Some method }
                .RegisterLocalThis(Symbol.intern (null, thisName), null, None)

        let validateParam (param: obj) =
            match param with
            | :? Symbol as sym ->
                if not <| isNull sym.Namespace then
                    raise <| ParseException("Can't use qualified name as paramter: {sym}")

                sym
            | _ -> raise <| ParseException("fn params must be Symbols")

        let mutable paramState = ParamParseState.Required
        let paramCount = parameters.count ()
        let argTypes = ResizeArray<Type>()

        for i = 0 to paramCount - 1 do
            let param = validateParam (parameters.nth (i))

            if RTVar.AmpersandSym.Equals(param) then
                if paramState = ParamParseState.Required then
                    paramState <- ParamParseState.Rest
                else
                    raise <| ParseException("Invalid parameter list")
            else
                // TODO: original code does primitive type analysis here
                let paramTag = TypeUtils.TagOf(param)

                if paramState = ParamParseState.Rest && not <| isNull paramTag then
                    raise <| ParseException("& arg cannot have type hint")

                // original code checks for primtive type signature on method + variadic

                let paramType =
                    if paramState = ParamParseState.Rest then
                        typeof<ISeq>
                    else
                        TypeUtils.TagType(cenv, paramTag)

                argTypes.Add(paramType)

                let env, b =
                    newEnv.RegisterLocal(
                        param,
                        (if paramState = Rest then RTVar.ISeqSym else paramTag),
                        None,
                        paramType,
                        true
                    )

                newEnv <- env
                method.ArgLocals.Add(b)

                match paramState with
                | ParamParseState.Required -> method.ReqParams.Add(b)
                | ParamParseState.Rest ->
                    method.RestParam <- Some b
                    paramState <- Done
                | _ -> raise <| ParseException("Unexpected parameter")

        if method.ReqParams.Count > Parser.MaxPositionalArity then
            raise
            <| ParseException("Can't specify more than {Parser.MaxPositionalArity} positional arguments")

        let bodyEnv =
            { newEnv with
                Pctx = Return
                LoopLocals = method.ArgLocals }

        method.Body <- Parser.BodyExprParser(bodyEnv, body)

        method


    /////////////////////////////////
    //
    //  TryExprParser
    //
    ////////////////////////////////

    static member TryExprParser(cenv: CompilerEnv, form: obj) : Expr =
        let form = form :?> ISeq


        if cenv.Pctx <> ParserContext.Return then
            // I'm not sure why we do this.
            Parser.Analyze(
                cenv,
                RTSeq.list (RTSeq.list (RTVar.FnOnceSym, PersistentVector.Empty, form)),
                $"try__{RT0.nextID ()}"
            )
        else

            // (try try-expr* catch-expr* finally-expr?)
            // catch-expr: (catch class sym expr*)
            // finally-expr: (finally expr*)

            let body = ResizeArray<obj>()
            let catches = ResizeArray<CatchClause>()
            let mutable bodyExpr: Expr option = None
            let mutable finallyExpr: Expr option = None
            let mutable caught = false


            let catchEnv =
                { cenv with
                    Pctx = Expression
                    IsAssignContext = false
                    InCatchFinally = true }

            let rec loop (fs: ISeq) =
                let f = fs.first ()

                let op =
                    match f with
                    | :? ISeq as fseq -> fseq.first ()
                    | _ -> null

                if
                    not <| Util.equals (op, RTVar.CatchSym)
                    && not <| Util.equals (op, RTVar.FinallySym)
                then
                    if caught then
                        raise <| ParseException("Only catch or finally can follow catch")

                    body.Add(f)
                else
                    // We have either a catch or finally.  Process accordingly

                    if bodyExpr.IsNone then
                        // We are on our first catch or finally.
                        // All body forms are collected, so build the expression for the body
                        let bodyEnv = { cenv with NoRecur = true }
                        bodyExpr <- Some(Parser.BodyExprParser(bodyEnv, RT0.seq (body)))


                    if Util.equals (op, RTVar.CatchSym) then
                        let second = RTSeq.second (f)
                        let t = TypeUtils.MaybeType(catchEnv, second, false)

                        if isNull t then
                            raise <| ParseException($"Unable to resolve classname: {RTSeq.second (f)}")

                        match RTSeq.third (f) with
                        | :? Symbol as sym ->
                            if not <| isNull sym.Namespace then
                                raise <| ParseException($"Can't bind qualified name: {sym}")

                            // Everything is looking good, let's parse the catch clause
                            let clauseTag =
                                match second with
                                | :? Symbol as sym -> sym
                                | _ -> null

                            let boundEnv, lb =
                                catchEnv.RegisterLocal(sym, clauseTag, None, typeof<Object>, false)

                            let handler =
                                Parser.BodyExprParser(boundEnv, RTSeq.next (RTSeq.next (RTSeq.next (f))))

                            catches.Add(
                                { CaughtType = t
                                  LocalBinding = lb
                                  Handler = handler }
                            )

                        | _ as third -> raise <| ParseException($"Bad binding form, expected Symbol, got: {third}")

                    else
                        // finally clause
                        if not <| isNull (fs.next ()) then
                            raise
                            <| InvalidOperationException("finally clause must be last in try expression")

                        let finallyEnv =
                            { cenv with
                                Pctx = Statement
                                IsAssignContext = false
                                InCatchFinally = true }

                        finallyExpr <- Some(Parser.BodyExprParser(finallyEnv, RTSeq.next (f)))

                loop (fs.next ())

            loop (form.next ())

            if bodyExpr.IsNone then

                // the only way this happens if there were catch or finally clauses.
                // We can return just the body expression itself.
                Parser.BodyExprParser(cenv, RT0.seq (body))

            else
                Expr.Try(Env = cenv, Form = form, TryExpr = bodyExpr.Value, Catches = catches, Finally = finallyExpr) // TODO: source info)


    /////////////////////////////////
    //
    //  InvokeExprParser
    //
    ////////////////////////////////


    // This one has a particularly nasty set of options.
    // We have (func arg1 arg2 ... )
    // Case 1:  func analyzes as an Expr.Var
    // Case 1a:  func is #'instance?
    //              and the form has length three , so (instance? arg1 arg2)
    //              and arg1 analyzes a literal with a Type value
    //           Then we output an Expr.InstanceOf  -- that's what this expression type is for.
    // Case 1b:  Direct linking is on (and in the orginal code:  not in an eval context)
    //           and the var is not dynamic, has not been marked as ^:redef or ^:declared
    //           and we _can_ parse is as an Expr.StaticInvoke  && original code has extra conditions not compiling and not compling deftype and with an internal assembly -- we're not there yet)
    //           Then we output the Expr.StaticInvoke
    // Case 1c:  Otherwise, (original code: we're not in an Eval Context -- if we are in an eval context, the code will be the default case)
    //           And we can find a PrimInteface matchiing the signature of the args:
    //           Then we turn the form into  (.invokePrim  .... ) with a bunch of metainfo manipulation.
    //           We'll ignore this option for now.
    // Case 1d:  Otherwise, we'll parse it as a normal invoke -- default case.
    // Case 2:  func analyzes as an Expr.Keyword  (Expr.Literal with a Keyword value
    //          and the form is (kw arg)
    //          and we have keyword callsites (objxregister is some)
    //          Then we output an Expr.KeywordInvoke
    // Case 3:  func analyzes as a StaticFieldExpr or StaticPropertyExpr
    //          Then we use it.  (this is the case the preserves the bug in the original code  CLJ-2806)  -- maybe we can ignore this?
    // Case 4:  func analyzes as an Expr.QualifiedMethod
    //          then return the result of ToHostExpr
    // Case 5:  Default case -- just parse it as an invoke.
    //          analyze the args, and make an Expr.Invoke

    static member MaybeParseVarInvoke(cenv: CompilerEnv, fexpr: Expr, form: ISeq, v: Var) : Expr option =

        let analyzeMaybeInstanceQ () : Expr option =
            match fexpr with
            | Expr.Var(Env = e; Form = f; Var = v; Tag = t) when v.Equals(RTVar.InstanceVar) && RT0.count (form) = 3 ->
                let sexpr = Parser.Analyze({ cenv with Pctx = Expression }, RTSeq.second (form))

                match sexpr with
                | Expr.Literal(Value = v) when (v :? Type) ->
                    let texpr = Parser.Analyze({ cenv with Pctx = Expression }, RTSeq.third (form))

                    Some
                    <| Expr.InstanceOf(Env = cenv, Form = form, Expr = texpr, Type = (v :?> Type), SourceInfo = None) // TODO: source info
                | _ -> None
            | _ -> None

        let analyzeMaybeStaticInvoke () : Expr option =
            if
                RT0.booleanCast (Parser.GetCompilerOption(RTVar.DirectLinkingKeyword))
                && (* && cenv.Pctx <> ParserContext.Eval *) not v.isDynamic
                && not <| RT0.booleanCast (RT0.get (RT0.meta (), RTVar.RedefKeyword, false))
                && not <| RT0.booleanCast (RT0.get (RT0.meta (), RTVar.DeclaredKeyword, false))
            then

                let formTag = TypeUtils.TagOf(form)
                let arity = RT0.count (form.next ())
                let sigTag = TypeUtils.SigTag(arity, v)
                let vTag = RT0.get (RT0.meta (), RTVar.TagKeyword)

                let tagToUse: obj =
                    if not <| isNull formTag then formTag
                    elif not <| isNull vTag then vTag
                    else sigTag

                match Parser.ParseStaticInvokeExpr(cenv, form, v, RTSeq.next (form), tagToUse) with
                | Some _ as se -> se
                | None -> None

            else
                None


        match analyzeMaybeInstanceQ () with
        | Some _ as se -> se // Case 1a
        | None ->
            match analyzeMaybeStaticInvoke () with
            | Some _ as se -> se // case 1b
            | None -> None


    static member InvokeExprParser(cenv: CompilerEnv, form: ISeq) : Expr =

        let cenv = cenv.WithParserContext(Expression)

        // (func arg1 arg2 ...)

        let fexpr = Parser.Analyze(cenv, RTSeq.first (form))

        let result =
            match fexpr with
            | Expr.Var(Env = e; Form = f; Var = v; Tag = t) -> Parser.MaybeParseVarInvoke(cenv, fexpr, form, v)
            | Expr.Literal(Type = KeywordType; Value = kw) as kwExpr when
                RT0.count (form) = 2 && cenv.ObjXRegister.IsSome  
                ->
                let target = Parser.Analyze(cenv, RTSeq.second (form))
                let siteIndex = cenv.RegisterKeywordCallSite(kw :?> Keyword)

                Some
                <| Expr.KeywordInvoke(
                    Env = cenv,
                    Form = form,
                    KwExpr = kwExpr,
                    Target = target,
                    Tag = TypeUtils.TagOf(form),
                    SiteIndex = siteIndex,
                    SourceInfo = None
                ) // TODO: source info)
            | Expr.InteropCall(Type = HostExprType.FieldOrPropertyExpr; IsStatic = true) as sfpExpr -> Some <| sfpExpr
            | Expr.QualifiedMethod(_) as qmExpr ->
                Some <| Parser.ToHostExpr(cenv, qmExpr, TypeUtils.TagOf(form), form.next ())
            | _ -> None

        match result with
        | Some e -> e
        | None ->

            let args = ResizeArray<Expr>()

            let rec loop (s: ISeq) =
                if isNull s then
                    ()
                else
                    args.Add(Parser.Analyze(cenv, s.first ()))
                    loop (s.next ())

            loop (RT0.seq (form.next ()))

            // TODO: The constructor would work on protocol details here.
            // Move to later pass?
            Expr.Invoke(
                Env = cenv,
                Form = form,
                Fexpr = fexpr,
                Args = args,
                Tag = TypeUtils.TagOf(form),
                SourceInfo = None
            )


    static member ParseStaticInvokeExpr(cenv: CompilerEnv, form: obj, v: Var, args: ISeq, tag: obj) : Expr option =

        if not <| v.isBound || isNull <| v.get () then
            None
        else
            let target = v.get().GetType()
            let argCount = RT0.count (args)
            let mutable method: MethodInfo = null

            let isValidMethod (m: MethodInfo) =
                let pInfos = m.GetParameters()

                argCount = pInfos.Length
                || argCount > pInfos.Length
                   && pInfos.Length > 0
                   && pInfos.[pInfos.Length - 1].ParameterType = typeof<ISeq>


            let maybeMatch =
                target.GetMethods()
                |> Array.tryFind (fun m -> m.IsStatic && m.Name = "invokeStatic" && isValidMethod (m))

            match maybeMatch with
            | None -> None
            | Some m ->
                let pInfos = m.GetParameters()

                let isVariadic =
                    if argCount = pInfos.Length then
                        argCount > 0 && pInfos.[pInfos.Length - 1].ParameterType = typeof<ISeq>
                    else
                        true

                let argExprs = ResizeArray<Expr>()

                let rec loop (s: ISeq) =
                    if isNull s then
                        ()
                    else
                        argExprs.Add(Parser.Analyze({ cenv with Pctx = Expression }, s.first ()))
                        loop (s.next ())

                loop (RT0.seq (args))

                Some
                <| Expr.StaticInvoke(
                    Env = cenv,
                    Form = form,
                    Target = target,
                    Method = m,
                    RetType = m.ReturnType,
                    Args = argExprs,
                    IsVariadic = isVariadic,
                    Tag = tag
                )

    static member ToHostExpr(cenv: CompilerEnv, qmExpr: Expr, tag: Symbol, args: ISeq) =
        // TODO: source info

        // we have the form (qmfexpr ...args...)
        // We need to decide what the pieces are in ...args...

        match qmExpr with
        | Expr.QualifiedMethod(
            Env = qmenv
            Form = form
            MethodType = methodType
            HintedSig = hintedSig
            MethodSymbol = methodSymbol
            MethodName = methodName
            Kind = kind
            TagClass = tagClass
            SourceInfo = sourceInfo) ->

            let instance, args =
                match kind with
                | QMMethodKind.Instance ->
                    let instance =
                        Parser.Analyze(cenv.WithParserContext(Expression), RTSeq.first (args))

                    Some instance, RTSeq.next (args)
                | _ -> None, args


            // We handle zero-arity calls separately, similarly to how HostExpr handles them.
            // Well, except here we have enough type information to fill in more details.
            // Constructors not included here.
            // We are trying to discriminate field access, property access, and method calls on zero arguments.
            //
            // One special case here:  Suppose we have a zero-arity _generic_method call, with type-args provided.
            // THis will look like:   (Type/StaticMethod (type-args type1 ..))  or (Type/InstanceMEthod instance-expression (type-args type1 ..))
            // We check for the arg count before removing the type-args, so these will be handled by the non-zero-arity code.
            // That is okay -- because this is generic, it can't be a field or property access, so we can treat it as a method call.

            let genericTypeArgs, args =
                match RTSeq.first (args) with
                | :? ISeq as firstArg ->
                    match RTSeq.first (firstArg) with
                    | :? Symbol as sym when sym.Equals(RTVar.TypeArgsSym) ->
                        // we have type-args supplied for a generic method call
                        // (. target methddname (type-args type1 ... ) arg1 ...)
                        TypeUtils.CreateTypeArgList(cenv, RTSeq.next (firstArg)), args.next ()
                    | _ -> TypeUtils.EmptyTypeList, args
                | _ -> TypeUtils.EmptyTypeList, args

            // Now we have a potential conflict.  What if we have a hinted signature on the QME?
            // Who wins the type-arg battle?
            // If the QME has a nonempty generic type args list, we us it in preference.

            let genericTypeArgs =
                match hintedSig with
                | Some hsig ->
                    match hsig.GenericTypeArgs with
                    | Some gta -> gta
                    | None -> genericTypeArgs
                | None -> genericTypeArgs

            let hasGenericTypeArgs = genericTypeArgs.Count > 0

            let isZeroArityCall = RT0.count (args) = 0 && kind <> QMMethodKind.Ctor

            if isZeroArityCall then
                // we know this is not a constructor call.
                let isStatic = (kind = QMMethodKind.Static)

                let memberInfo =
                    if not hasGenericTypeArgs then
                        Reflector.GetFieldOrPropertyInfo(methodType, methodName, isStatic)
                    else
                        null

                let memberInfo, hostExprType =
                    if isNull memberInfo then
                        Reflector.GetArityZeroMethod(methodType, methodName, genericTypeArgs, isStatic) :> MemberInfo,
                        (if isStatic then HostExprType.MethodExpr else HostExprType.InstanceZeroArityCallExpr)
                    else
                        memberInfo, HostExprType.FieldOrPropertyExpr

                match memberInfo with
                | null ->
                    let typeArgsStr =
                        if hasGenericTypeArgs then
                            $" and generic type args {QMHelpers.GenerateGenericTypeArgString(genericTypeArgs)}"
                        else
                            ""

                    let instOrStaticStr = if isStatic then "static" else "instance"

                    raise
                    <| MissingMemberException(
                        $"No {instOrStaticStr} field, property or method taking 0 args{typeArgsStr} named {methodName} found for {methodType.Name}"
                    )
                | _ ->
                    Expr.InteropCall(
                        Env = cenv,
                        Form = form,
                        Type = hostExprType,
                        IsStatic = isStatic,
                        Tag = tag,
                        Target = instance,
                        TargetType = methodType,
                        MemberName = methodName,
                        TInfo = memberInfo,
                        Args = null,
                        TypeArgs = genericTypeArgs,
                        SourceInfo = None
                    ) // TODO: source info


            else
                // TODO: there are some real differences in the constructors in the original code.
                // Need to figure out if it matters in the new world order.
                // Look at contructors for InstanceMethodExpr, StaticMethodExpr, InstanceFieldExpr, InstancePropertyExpr, etc.
                let method =
                    match hintedSig with
                    | Some hsig -> QMHelpers.ResolveHintedMethod(methodType, methodName, kind, hsig)
                    | None -> null

                match kind with
                | QMMethodKind.Ctor ->
                    Expr.New(
                        Env = cenv,
                        Form = form,
                        Constructor = method,
                        Args = Parser.ParseArgs(cenv, args),
                        Type = methodType,
                        IsNoArgValueTypeCtor = false,
                        SourceInfo = None
                    ) // TODO: source info)

                | _ ->
                    let isStatic = (kind = QMMethodKind.Static)

                    let hostArgs = Parser.ParseArgs(cenv, args)

                    Expr.InteropCall(
                        Env = cenv,
                        Form = form,
                        Type = HostExprType.MethodExpr,
                        IsStatic = isStatic,
                        Tag = tag,
                        Target = instance,
                        TargetType = methodType,
                        MemberName = Munger.Munge(methodName),
                        TInfo = method,
                        Args = hostArgs,
                        TypeArgs = genericTypeArgs,
                        SourceInfo = None
                    ) // TODO: source info

        | _ -> raise <| ArgumentException("Expected QualifiedMethod expression")


    /////////////////////////////////
    //
    //  HostExprParser
    //
    ////////////////////////////////

    static member HostExprParser(cenv: CompilerEnv, form: ISeq) : Expr =
        // TODO: Source info

        let tag = TypeUtils.TagOf(form)

        // form is one of:
        //  (. x fieldname-sym)
        //  (. x 0-ary-methodname-sym)
        //  (. x propertyname-sym)
        //  (. x methodname-sym args+)
        //  (. x (methodname-sym args?))
        //
        //  args might have a first element of the form (type-args t1 ...) to supply types for generic method calls

        // Parse into canonical form:
        // Target + memberName + args
        //
        //  (. x fieldname-sym)             Target = x member-name = fieldname-sym, args = null
        //  (. x 0-ary-methodname-sym)      Target = x member-name = 0-ary-method, args = null
        //  (. x propertyname-sym)          Target = x member-name = propertyname-sym, args = null
        //  (. x methodname-sym args+)      Target = x member-name = methodname-sym, args = args+
        //  (. x (methodname-sym args?))    Target = x member-name = methodname-sym, args = args?  -- note: in this case, we explicity cannot be a field or property

        if RT0.length (form) < 3 then
            raise
            <| ParseException(
                "Malformed member expression, expecting (. target member ... ) or  (. target (member ...))"
            )

        let target = RTSeq.second (form)

        let methodSym, args, methodRequired =
            match RTSeq.third (form) with
            | :? Symbol as sym -> sym, RTSeq.next (RTSeq.next (RTSeq.next (form))), false
            | :? ISeq as seq when RT0.length (form) = 3 ->
                match RTSeq.first (seq) with
                | :? Symbol as sym -> sym, RTSeq.next (seq), true
                | _ ->
                    raise
                    <| ParseException(
                        "Malformed member expression, expecting (. target member-name args... )  or (. target (member-name args...), where member-name is a Symbol"
                    )
            | _ ->
                raise
                <| ParseException(
                    "Malformed member expression, expecting (. target member-name args... )  or (. target (member-name args...)"
                )

        // determine static or instance be examinung the target
        // static target must be symbol, either fully.qualified.Typename or Typename that has been imported
        // If target does not resolve to a type, then it must be an instance call -- parse it.

        let staticType = TypeUtils.MaybeType(cenv, target, false)

        let instance =
            if isNull staticType then
                Some <| Parser.Analyze({ cenv with Pctx = Expression }, RTSeq.second (form))
            else
                None

        // staticType not null => static method call, instance set to null.
        // staticType null, instance is not null, set to an expression yielding the instance to make the call on.

        // If there is a type-args form, it must be the first argument.
        // Pull it out if it's there.

        let typeArgs, args =
            match RTSeq.first (args) with
            | :? ISeq as firstArg ->
                match RTSeq.first (firstArg) with
                | :? Symbol as sym when sym.Equals(RTVar.TypeArgsSym) ->
                    // we have type-args supplied for a generic method call
                    // (. target methddname (type-args type1 ... ) arg1 ...)
                    TypeUtils.CreateTypeArgList(cenv, RTSeq.next (firstArg)), args.next ()
                | _ -> TypeUtils.EmptyTypeList, args
            | _ -> TypeUtils.EmptyTypeList, args

        let isZeroArityCall = RT0.length (args) = 0 && not methodRequired

        if isZeroArityCall then
            Parser.ParseZeroArityCall(cenv, form, staticType, instance, methodSym, typeArgs, tag)
        else
            let methodName = Munger.Munge(methodSym.Name)
            let hostArgs = Parser.ParseArgs(cenv, args)

            Expr.InteropCall(
                Env = cenv,
                Form = form,
                Type = MethodExpr,
                IsStatic = (not <| isNull staticType),
                Tag = tag,
                Target = instance,
                TargetType = staticType,
                MemberName = methodName,
                TInfo = null,
                Args = hostArgs,
                TypeArgs = typeArgs,
                SourceInfo = None
            ) // TODO: source info

    static member ParseZeroArityCall
        (
            cenv: CompilerEnv,
            form: obj,
            staticType: Type,
            instance: Expr option,
            methodSym: Symbol,
            typeArgs: ResizeArray<Type>,
            tag: Symbol
        ) : Expr =

        // TODO: (in original) Figure out if we want to handle the -propname otherwise.
        let memberSym, isPropName =
            if methodSym.Name.[0] = '-' then
                Symbol.intern (methodSym.Name.Substring(1)), true
            else
                methodSym, false

        let memberName = Munger.Munge(memberSym.Name)

        // The JVM version does not have to worry about Properties.  It captures 0-arity methods under fields.
        // We have to put in special checks here for this.
        // Also, when reflection is required, we have to capture 0-arity methods under the calls that
        //   are generated by StaticFieldExpr and InstanceFieldExpr.

        let isStatic = not <| isNull staticType

        Expr.InteropCall(
            Env = cenv,
            Form = form,
            Type = FieldOrPropertyExpr,
            IsStatic = isStatic,
            Tag = tag,
            Target = instance,
            TargetType = staticType,
            MemberName = memberName,
            TInfo = null,
            Args = null,
            TypeArgs = typeArgs,
            SourceInfo = None
        ) // TODO: source info





    (*
            // TODO:  We defer all this calculation for a later pass.
            // The problem is that this code need the type of the instance.
            // We may need to defer this to the type inference pass.

            // At this point, we know only static vs instance


                    if (staticType != null)
                    {
                        if ( ! hasTypeArgs && (finfo = Reflector.GetField(staticType, memberName, true)) != null)
                            return new StaticFieldExpr(source, spanMap, tag, staticType, memberName, finfo);
                        if ( ! hasTypeArgs && (pinfo = Reflector.GetProperty(staticType, memberName, true)) != null)
                            return new StaticPropertyExpr(source, spanMap, tag, staticType, memberName, pinfo);
                        if (!isPropName && Reflector.GetArityZeroMethod(staticType, memberName, typeArgs, true) != null)
                            return new StaticMethodExpr(source, spanMap, tag, staticType, memberName, typeArgs, new List<HostArg>(), tailPosition);

                        string typeArgsStr = hasTypeArgs ? $" and generic type args {typeArgs.GenerateGenericTypeArgsString()} " : "";
                        throw new MissingMemberException($"No field, property, or method taking 0 args{typeArgsStr} named {memberName} found for {staticType.Name}");
                    }
                    else if (instance != null && instance.HasClrType && instance.ClrType != null)
                    {
                        Type instanceType = instance.ClrType;
                        if (!hasTypeArgs && (finfo = Reflector.GetField(instanceType, memberName, false)) != null)
                            return new InstanceFieldExpr(source, spanMap, tag, instance, memberName, finfo);
                        if (!hasTypeArgs && (pinfo = Reflector.GetProperty(instanceType, memberName, false)) != null)
                            return new InstancePropertyExpr(source, spanMap, tag, instance, memberName, pinfo);
                        if (!isPropName && Reflector.GetArityZeroMethod(instanceType, memberName, typeArgs, false) != null)
                            return new InstanceMethodExpr(source, spanMap, tag, instance, instanceType, memberName, typeArgs, new List<HostArg>(), tailPosition);
                        if (pcon.IsAssignContext)
                            return new InstanceFieldExpr(source, spanMap, tag, instance, memberName, null); // same as InstancePropertyExpr when last arg is null
                        else
                            return new InstanceZeroArityCallExpr(source, spanMap, tag, instance, memberName);
                    }
                    else
                    {
                        //  t is null, so we know this is not a static call
                        //  If instance is null, we are screwed anyway.
                        //  If instance is not null, then we don't have a type.
                        //  So we must be in an instance call to a property, field, or 0-arity method.
                        //  The code generated by InstanceFieldExpr/InstancePropertyExpr with a null FieldInfo/PropertyInfo
                        //     will generate code to do a runtime call to a Reflector method that will check all three.
                        //return new InstanceFieldExpr(source, spanMap, tag, instance, fieldName, null); // same as InstancePropertyExpr when last arg is null
                        //return new InstanceZeroArityCallExpr(source, spanMap, tag, instance, fieldName); 
                        if (pcon.IsAssignContext)
                            return new InstanceFieldExpr(source, spanMap, tag, instance, memberName, null); // same as InstancePropertyExpr when last arg is null
                        else
                            return new InstanceZeroArityCallExpr(source, spanMap, tag, instance, memberName); 

                    }
                }
            *)


    // TODO: Source info needed,
    static member CreateStaticFieldOrPropertyExpr
        (
            cenv: CompilerEnv,
            form: obj,
            tag: Symbol,
            t: Type,
            memberName: string,
            info: MemberInfo
        ) : Expr =

        // TODO: copy all the constructor code
        Expr.InteropCall(
            Env = cenv,
            Form = form,
            Type = FieldOrPropertyExpr,
            IsStatic = true,
            Tag = tag,
            Target = None,
            TargetType = t,
            MemberName = memberName,
            TInfo = info,
            Args = null,
            TypeArgs = TypeUtils.EmptyTypeList,
            SourceInfo = None
        )


    /////////////////////////////////
    //
    //  more to come
    //
    ////////////////////////////////

    static member NewExprParser(cenv: CompilerEnv, form: ISeq) : Expr = Parser.NilExprInstance

    // Saving for later, when I figure out what I'm doing
    static member DefTypeParser () (cenv: CompilerEnv, form: ISeq) : Expr = Parser.NilExprInstance
    static member ReifyParser () (cenv: CompilerEnv, form: ISeq) : Expr = Parser.NilExprInstance
    static member CaseExprParser(cenv: CompilerEnv, form: ISeq) : Expr = Parser.NilExprInstance


    /////////////////////////////////
    //
    //  Symbol resolution & lookup
    //
    ////////////////////////////////

    static member Resolve(cenv: CompilerEnv, sym: Symbol) : obj =
        Parser.ResolveIn(cenv, RTVar.getCurrentNamespace (), sym, false)

    static member ResolveIn(cenv: CompilerEnv, ns: Namespace, sym: Symbol, allowPrivate: bool) : obj =
        if not <| isNull sym.Namespace then
            match RTReader.NamespaceFor(ns, sym) with
            | null ->
                match TypeUtils.MaybeArrayType(cenv, sym) with
                | null -> raise <| new InvalidOperationException($"No such namespace: {sym.Namespace}")
                | _ as t -> t
            | _ as ns ->
                match ns.findInternedVar (Symbol.intern (sym.Name)) with
                | null -> raise <| new InvalidOperationException($"No such var: {sym}")
                | _ as v when
                    v.Namespace <> RTVar.getCurrentNamespace ()
                    && not v.isPublic
                    && not allowPrivate
                    ->
                    raise <| new InvalidOperationException($"Var: {sym} is not public")
                | _ as v -> v
        elif
            sym.Name.IndexOf('.') > 0
            || sym.Name.Length > 0 && sym.Name[sym.Name.Length - 1] = ']'
        then
            RTType.ClassForNameE(sym.Name)
        elif sym.Equals(RTVar.NsSym) then
            RTVar.NsVar
        elif sym.Equals(RTVar.InNsSym) then
            RTVar.InNSVar
        //elif CompileStubSymVar / CompileStubClassVar  // TODO: Decide on stubs
        else
            match ns.getMapping (sym) with
            | null ->
                if RT0.booleanCast ((RTVar.AllowUnresolvedVarsVar :> IDeref).deref ()) then
                    sym
                else
                    raise
                    <| new InvalidOperationException($"Unable to resolve symbol: {sym} in this context")
            | _ as o -> o


    static member IsInline(cenv: CompilerEnv, op: obj, arity: int) : IFn =
        let v =
            match op with
            | :? Var as v -> v
            | :? Symbol as s ->
                match cenv.ReferenceLocal(s) with
                | Some _ -> Parser.LookupVar(cenv, s, false)
                | _ -> null
            | _ -> null

        match v with
        | null -> null
        | _ ->
            if
                not <| Object.ReferenceEquals(v.Namespace, RTVar.getCurrentNamespace ())
                && not v.isPublic
            then
                raise <| new InvalidOperationException($"Var: {v} is not public")

            match RT0.get ((v :> IMeta).meta (), RTVar.InlineKeyword) :?> IFn with
            | null -> null
            | _ as ret ->
                match RT0.get ((v :> IMeta).meta (), RTVar.InlineAritiesKeyword) with
                | null -> ret
                | :? IFn as aritiesPred ->
                    if RT0.booleanCast (aritiesPred.invoke (arity)) then
                        ret
                    else
                        null
                | _ -> ret // Probably this should be an error: :inline-arities value should be an IFn.  Original code does a cast that might fail


    static member IsMacro(cenv: CompilerEnv, op: obj) : Var =
        let v = 
            match op with
            | :? Var as v -> v
            | :? Symbol as s ->
                match cenv.ReferenceLocal(s) with
                | Some _ -> null  // if there is local reference, it is not a macro, so return null
                | _ -> Parser.LookupVar(cenv, s, false)
            | _ -> null

        if not <| isNull v && v.isMacro then
            if v.Namespace = RTVar.getCurrentNamespace () && not v.isPublic then
                raise <| new InvalidOperationException($"Var: {v} is not public")
            v
        else
            null

    static member LookupVar(cenv: CompilerEnv, sym: Symbol, internNew: bool) =
        Parser.LookupVar(cenv, sym, internNew, true)

    static member LookupVar(cenv: CompilerEnv, sym: Symbol, internNew: bool, registerMacro: bool) : Var =
        let var =
            // Note: ns-qualified vars in other namespaces must exist already
            match sym with
            | _ when not <| isNull sym.Namespace ->
                match RTReader.NamespaceFor(sym) with
                | null -> null
                | _ as ns ->
                    let name = Symbol.intern (sym.Name)

                    if internNew && Object.ReferenceEquals(ns, RTVar.getCurrentNamespace ()) then
                        RTVar.getCurrentNamespace().intern (name)
                    else
                        ns.findInternedVar (name)
            | _ when sym.Equals(RTVar.NsSym) -> RTVar.NsVar
            | _ when sym.Equals(RTVar.InNsSym) -> RTVar.InNSVar
            | _ ->
                match RTVar.getCurrentNamespace().getMapping (sym) with
                | null ->
                    // introduce a new var in the current ns
                    if internNew then
                        RTVar.getCurrentNamespace().intern (Symbol.intern (sym.Name))
                    else
                        null
                | :? Var as v -> v
                | _ as o ->
                    raise
                    <| new InvalidOperationException($"Expecting var, but {sym} is mapped to {o}")

        if not <| isNull var && (not var.isMacro || registerMacro) then
            cenv.RegisterVar(var)

        var


    /////////////////////////////////
    //
    //  Macroexpansion
    //
    ////////////////////////////////

    // TODO: Macrochecking via Spec
    static member CheckSpecs(v: Var, form: ISeq) = ()

    static member Macroexpand1(form: obj) =
        Parser.Macroexpand1(CompilerEnv.Create(Expression), form)

    static member Macroexpand(form: obj) =
        Parser.Macroexpand(CompilerEnv.Create(Expression), form)

    static member Macroexpand1(cenv: CompilerEnv, form: obj) =
        match form with
        | :? ISeq as s -> Parser.MacroexpandSeq1(cenv, s)
        | _ -> form

    static member Macroexpand(cenv: CompilerEnv, form: obj) =
        let exf = Parser.Macroexpand1(cenv, form)

        if Object.ReferenceEquals(exf, form) then
            form
        else
            Parser.Macroexpand(cenv, exf)

    static member private MacroexpandSeq1(cenv: CompilerEnv, form: ISeq) =
        let op = form.first ()

        if (LispReader.IsSpecial(op)) then
            form
        else
            match Parser.IsMacro(cenv, op) with
            | null -> Parser.MacroExpandNonSpecial(cenv, op, form)
            | _ as v -> Parser.MacroExpandSpecial(cenv, v, form)

    static member private MacroExpandSpecial(cenv: CompilerEnv, v: Var, form: ISeq) =
        Parser.CheckSpecs(v, form)

        try
            // Here is macro magic -- supply the &form and &env args in front
            let args = RTSeq.cons (form, RTSeq.cons (cenv.Locals, form.next ()))
            (v :> IFn).applyTo (args)
        with
        | :? ArityException as e ->
            // hide the 2 extra params for a macro
            // This simple test is used in the JVM:   if (e.Name.Equals(munge(v.ns.Name.Name) + "$" + munge(v.sym.Name)))
            // Does not work for us because have to append a __1234 to the type name for functions in order to avoid name collisiions in the eval assembly.
            // So we have to see if the name is of the form   namespace$name__xxxx  where the __xxxx can be repeated.
            let reducedName = Parser.RemoveFnSuffix(e.name)

            if reducedName.Equals($"{Munger.Munge(v.Namespace.Name.Name)}${Munger.Munge(v.Symbol.Name)}") then
                raise <| new ArityException(e.actual - 2, e.name)
            else
                reraise ()
        | :? CompilerException -> reraise ()
        | _ as e ->
            if
                e :? ArgumentException
                || e :? InvalidOperationException
                || (e :> obj) :? IExceptionInfo
            then
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
                if form.count () < 2 then
                    raise
                    <| ArgumentException("Malformed member expression, expecting (.member target ...) ")

                let method = Symbol.intern (sname.Substring(1))
                let mutable target = RTSeq.second (form)

                if not <| isNull (TypeUtils.MaybeType(cenv, target, false)) then
                    target <-
                        (RTSeq.list (IdentitySym, target) :?> IObj)
                            .withMeta (RTMap.map (RTVar.TagKeyword, ClassSym))

                Parser.MaybeTransferSourceInfo(
                    Parser.PreserveTag(form, RTSeq.listStar (RTVar.DotSym, target, method, form.next().next ())),
                    form
                )
            else
                // (x.substring 2 5) =>  (. x substring 2 5)
                // also (package.class.name ... ) (. package.class name ... )
                let index = sname.IndexOf('.')

                if index = sname.Length - 1 then
                    let target = Symbol.intern (sname.Substring(0, index))
                    Parser.MaybeTransferSourceInfo(RTSeq.listStar (RTVar.NewSym, target, form.next ()), form)
                else
                    form
        | _ -> form


    /////////////////////////////////
    //
    //  Helper methods
    //
    //////////////////////////////////

    static member OptionallyGenerateMetaInit(cenv: CompilerEnv, form: obj, expr: Expr) : Expr =
        match RT0.meta (form) with
        | null -> expr
        | _ as meta -> Expr.Meta(Env = cenv, Form = form, Target = expr, Meta = Parser.AnalyzeMap(cenv, meta))


    //public static Regex UnpackFnNameRE = new Regex("^(.+)/$([^_]+)(__[0-9]+)*$");
    static member val FnNameSuffixRE = new Regex("__[0-9]+$")

    static member RemoveFnSuffix(s: string) =
        let rec loop (s: string) =
            let m = Parser.FnNameSuffixRE.Match(s)

            if m.Success then
                loop (s.Substring(0, s.Length - m.Groups.[0].Length))
            else
                s

        loop s

    static member PreserveTag(src: ISeq, dst: obj) : obj =
        match TypeUtils.TagOf(src) with
        | null -> dst
        | _ as tag ->
            match dst with
            | :? IObj as iobj -> (dst :?> IObj).withMeta (RTMap.map (RTVar.TagKeyword, tag))
            | _ -> dst

    static member MaybeTransferSourceInfo(newForm: obj, oldForm: obj) : obj =
        match oldForm, newForm with
        | (:? IObj as oldObj), (:? IObj as newObj) ->
            match oldObj.meta () with
            | null -> newForm
            | _ as oldMeta ->
                let spanMap = oldMeta.valAt (RTVar.SourceSpanKeyword)
                let mutable newMeta = newObj.meta ()

                if isNull newMeta then
                    newMeta <- RTMap.map ()

                newMeta <- newMeta.assoc (RTVar.SourceSpanKeyword, spanMap)
                newObj.withMeta (newMeta)
        | _, _ -> newForm


    static member IsAssignableExpr(expr: Expr) : bool =
        match expr with
        | Expr.LocalBinding _
        | Expr.Var _ -> true
        | Expr.InteropCall(Type = hostType) when hostType = HostExprType.FieldOrPropertyExpr -> true
        | _ -> false

    static member ElideMeta(m: IPersistentMap) = m // TODO: Source-info

    static member RegisterVar(v: Var) = () // TODO: constants registration

    static member GetCompilerOption(kw: Keyword) = RT0.get (CompilerOptionsVar, kw)

    static member InitializeCompilerOptions() =

        let mutable compilerOptions: obj = null

        let nixPrefix = "CLOJURE_COMPILER_"
        let winPrefix = "clojure.compiler."

        let envVars = Environment.GetEnvironmentVariables()

        for de in envVars do
            let de = de :?> DictionaryEntry
            let name = de.Key :?> string
            let value = de.Value :?> string

            if name.StartsWith(nixPrefix) then
                // compiler options on *nix need to be of the form
                // CLOJURE_COMPILER_DIRECT_LINKING because most shells do not
                // support hyphens in variable names
                let optionName = name.Substring(nixPrefix.Length).Replace("_", "-").ToLower()

                compilerOptions <-
                    RTMap.assoc (compilerOptions, Keyword.intern (null, optionName), Parser.ReadString(value))
            elif name.StartsWith(winPrefix) then
                // compiler options on Windows need to be of the form
                // clojure.compiler.direct-linking because most shells do not
                // support hyphens in variable names
                let optionName = name.Substring(winPrefix.Length)

                compilerOptions <-
                    RTMap.assoc (compilerOptions, Keyword.intern (null, optionName), Parser.ReadString(value))

        CompilerOptionsVar <-
            Var
                .intern(
                    Namespace.findOrCreate (Symbol.intern ("clojure.core")),
                    Symbol.intern ("*compiler-options*"),
                    compilerOptions
                )
                .setDynamic ()

    static member ReadString(s: string) = Parser.ReadString(s, null)

    static member ReadString(s: string, opts: obj) =
        use r = new PushbackTextReader(new StringReader(s))
        LispReader.read (r, opts)



    static member ParseArgs(cenv: CompilerEnv, argSeq: ISeq) =
        let args = ResizeArray<HostArg>()

        let analyzeArg (arg: obj) : obj * ParameterType * LocalBinding option =
            match arg with
            | :? ISeq as s ->
                match RTSeq.first (s) with
                | :? Symbol as op when op.Equals(RTVar.ByRefSym) ->
                    if RT0.length (s) <> 2 then
                        raise <| ParseException("Wrong number of arguments to by-ref")

                    match RTSeq.second (s) with
                    | :? Symbol as localArg ->
                        match cenv.ReferenceLocal(localArg) with
                        | Some _ as lbOpt -> localArg, ParameterType.ByRef, lbOpt
                        | _ ->
                            raise
                            <| ArgumentException($"Argument to by-ref must be a local variable: {localArg}")
                    | _ as arg -> raise <| ArgumentException($"Argument to by-ref must be a Symbol: {arg}")
                | _ as v -> raise <| ArgumentException("Expected (by-ref arg), got: {v}")
            | _ -> arg, ParameterType.Standard, None

        let rec loop (s: ISeq) =
            match s with
            | null -> ()
            | _ ->
                let arg, paramType, lb = analyzeArg (s.first ())
                let expr = Parser.Analyze(cenv.WithParserContext(Expression), arg)

                args.Add(
                    { HostArg.ArgExpr = expr
                      ParamType = paramType
                      LocalBinding = lb }
                )

        loop (argSeq)
        args


    static member ParamTagsOf(sym: Symbol) =
        let paramTags = RT0.get (RT0.meta (sym), RTVar.ParamTagsKeyword)

        if not <| isNull paramTags && not <| paramTags :? IPersistentVector then
            raise <| ArgumentException($"param-tags of symbol {sym} should be a vector")

        paramTags :?> IPersistentVector

    static member CreateQualifiedMethodExpr(cenv: CompilerEnv, methodType: Type, sym: Symbol) =
        let tagClass =
            match TypeUtils.TagOf(sym) with
            | null -> typeof<AFn>
            | _ as tag -> TypeUtils.TagToType(cenv, tag)

        let hintedSig = SignatureHint.MaybeCreate(cenv, Parser.ParamTagsOf(sym))

        let kind, methodName =
            if sym.Name.StartsWith(".") then
                QMMethodKind.Instance, sym.Name.Substring(1)
            elif sym.Name.Equals("new") then
                QMMethodKind.Ctor, sym.Name
            else
                QMMethodKind.Static, sym.Name

        Expr.QualifiedMethod(
            Env = cenv,
            Form = sym,
            MethodType = methodType,
            HintedSig = hintedSig,
            MethodSymbol = sym,
            MethodName = methodName,
            Kind = kind,
            TagClass = tagClass,
            SourceInfo = None
        ) // TODO: source info)

