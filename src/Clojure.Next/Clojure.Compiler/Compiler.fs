namespace Clojure.Compiler

open Clojure.Collections
open Clojure.Numerics
open System



[<Sealed;AbstractClass>]
type Compiler private () =

    static member val NilExprInstance = ConstantExpr({Type=NilType; Value=null})
    static member val TrueExprInstance = ConstantExpr({Type=BoolType; Value=true})
    static member val FalseExprInstance = ConstantExpr({Type=BoolType; Value=false})

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
            | :? Keyword as kw -> KeywordExpr({Ctx = cctx; Keyword=kw})       // we'll have to walk to get keywords
            | _ when Numbers.IsNumeric(form) -> Compiler.AnalyzeNumber(cctx,form)
            | :? String as s -> ConstantExpr({Type=StringType; Value=String.Intern(s)})
            | :? IPersistentCollection as coll 
                    when not <| form :? IType && 
                         not <| form :? IRecord && 
                         coll.count() = 0  -> Compiler.OptionallyGenerateMetaInit(cctx, form, ConstantExpr({Type = EmptyType; Value = coll}))
            | :? ISeq -> Compiler.AnalyzeSeq(cctx, form)
            | :? IPersistentVector -> Compiler.AnalyzeVector(cctx, form)
            | :? IRecord -> ConstantExpr({Type=OtherType; Value=form})
            | :? IType -> ConstantExpr({Type=OtherType; Value=form})
            | :? IPersistentMap -> Compiler.AnalyzeMap(cctx, form)
            | :? IPersistentSet -> Compiler.AnalyzeSet(cctx, form)
            | _ -> ConstantExpr({Type=OtherType; Value=form})

        with 
        | :? CompilerException -> reraise()
        |  _ as e -> raise <| CompilerException(e)  // TODO: add source info


    static member AnalyzeSymbol(cctx: CompilerContext, sym: Symbol) : Expr =
        Compiler.NilExprInstance   // TODO

    static member AnalyzeNumber(cctx: CompilerContext, num: obj) : Expr =
        Compiler.NilExprInstance   // TODO

    static member AnalyzeSeq(cctx: CompilerContext, form: obj) : Expr =
        Compiler.NilExprInstance   // TODO

    static member AnalyzeVector(cctx: CompilerContext, form: obj) : Expr =
        Compiler.NilExprInstance   // TODO

    static member AnalyzeMap(cctx: CompilerContext, form: obj) : Expr =
        Compiler.NilExprInstance   // TODO

    static member AnalyzeSet(cctx: CompilerContext, form: obj) : Expr =
        Compiler.NilExprInstance   // TODO

    static member OptionallyGenerateMetaInit(cctx: CompilerContext, form: obj, expr: Expr) : Expr =
        match RT0.meta(form) with
        | null -> expr
        | _ as meta -> MetaExpr({Ctx = cctx; Target = expr; Meta = Compiler.AnalyzeMap(cctx, meta)})


