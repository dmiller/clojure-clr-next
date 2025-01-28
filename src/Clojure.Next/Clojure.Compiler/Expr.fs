namespace Clojure.Compiler

open System
open System.Collections.Generic
open Clojure.Collections
open Clojure.Lib
open System.Reflection


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

type ProtocolDetails =
    { ProtocolOn: Type
      OnMethod: MethodInfo
      SiteIndex: int }

type Expr =
    | AssignExpr of AssignDetails
    | BodyExpr of BodyDetails
    | CollectionExpr of CollectionDetails // combines MapExpr, SetExpr, VectorExpr
    | LiteralExpr of LiteralDetails // Combines ConstExpr, NumberExpr, NilExpr, StringExpr, BoolExpr , KeywordExpr, EmptyExpr
    | DefExpr of DefDetails
    | FnExpr of FnDetails
    | HostExpr of HostExprDetails
    | IfExpr of IfDetails
    | ImportExpr of ImportDetails
    | InstanceOfExpr of InstanceOfDetails
    | InvokeExpr of InvokeDetails
    | KeywordInvokeExpr of KeywordInvokeDetails
    | LetExpr of LetDetails
    | LocalBindingExpr of LocalBindingDetails
    | MetaExpr of MetaDetails
    | MethodExpr of MethodDetails
    | NewExpr of NewDetails
    | NewInstanceExpr of NewInstanceDetails
    | QualifiedMethodExpr of QualifiedMethodDetails
    | RecurExpr of RecurDetails
    | TheVarExpr of TheVarDetails
    | TryExpr of TryDetails
    | UnresolvedVarExpr of UnresolvedVarDetails
    | VarExpr of VarDetails
    | UntypedExpr of UntypedExprDetails // combines MonitorEnterExpr, MonitorExitExpr, ThrowExpr

and AssignDetails =
    { Ctx: CompilerEnv
      Target: Expr
      Value: Expr }

and BodyDetails =
    { Ctx: CompilerEnv
      Exprs: ResizeArray<Expr> }

and CaseDetails =
    { Ctx: CompilerEnv
      Expr: Expr
      DefaultExpr: Expr
      Shift: int
      Mask: int
      Tests: SortedDictionary<int, Expr>
      Thens: SortedDictionary<int, Expr>
      SwitchType: Keyword
      TestType: Keyword
      SkipCheck: IPersistentSet
      ReturnType: Type }

and CollectionDetails = { Ctx: CompilerEnv; Value: obj }
and LiteralDetails = { Type: LiteralType; Value: obj }

and DefDetails =
    { Ctx: CompilerEnv
      Var: Var
      Init: Expr
      Meta: Expr option
      InitProvided: bool
      IsDynamic: bool
      ShadowsCoreMapping: bool
      SourceInfo: SourceInfo option}

and FnDetails =
    { Ctx: CompilerEnv (* help *)
      SourceInfo: SourceInfo }

and HostExprDetails =
    { Ctx: CompilerEnv
      Type: HostExprType
      Tag: Symbol
      Target: Expr option
      TargetType: Type
      MemberName: string
      TInfo: MemberInfo
      Args: ResizeArray<HostArg>
      IsTailPosition: bool
      SourceInfo: SourceInfo option }

and IfDetails =
    { Test: Expr
      Then: Expr
      Else: Expr
      SourceInfo: SourceInfo option }

and ImportDetails = { Ctx: CompilerEnv; Typename: string }

and InstanceOfDetails =
    { Ctx: CompilerEnv
      Expr: Expr
      Type: Type
      SourceInfo: SourceInfo }

and InvokeDetails =
    { Ctx: CompilerEnv
      Fexpr: Expr
      Args: ResizeArray<Expr>
      Tag: obj
      TailPosition: bool
      ProtocolDetails: ProtocolDetails option
      SourceInfo: SourceInfo }

and KeywordInvokeDetails =
    { Ctx: CompilerEnv
      KwExpr: Expr
      Target: Expr
      Tag: obj
      SiteIndex: int
      SourceInfo: SourceInfo }

and LetDetails =
    { Ctx: CompilerEnv
      Mode: LetExprMode
      BindingInits: ResizeArray<BindingInit>
      Body: Expr
      LoopId: int option
      SourceInfo: SourceInfo option}

and LocalBindingDetails =
    { Ctx: CompilerEnv
      Binding: LocalBinding
      Tag: Symbol }

and MetaDetails =
    { Ctx: CompilerEnv
      Target: Expr
      Meta: Expr }

and MethodDetails =
    { Ctx: CompilerEnv
      MethodName: string
      Args: ResizeArray<HostArg> }

and UntypedExprDetails =
    { Ctx: CompilerEnv
      Type: UntypedExprType
      Target: Expr option }

and NewDetails =
    { Ctx: CompilerEnv
      Args: ResizeArray<HostArg>
      Type: Type
      IsNoArgValueTypeCtor: bool
      SourceInfo: SourceInfo }

and NewInstanceDetails = { Ctx: CompilerEnv (* help*)  } // maybe combindwith fn?
and NumberDetails = { Ctx: CompilerEnv; Value: obj }

and QualifiedMethodDetails =
    { Ctx: CompilerEnv (* help *)
      SourceInfo: SourceInfo option }

and RecurDetails =
    { Ctx: CompilerEnv
      Args: ResizeArray<Expr>
      LoopLocals: ResizeArray<LocalBinding>
      SourceInfo: SourceInfo option }

and TheVarDetails = { Ctx: CompilerEnv; Var: Var }

and TryDetails =
    { Ctx: CompilerEnv
      TryExpr: Expr
      Catches: ResizeArray<CatchClause>
      Finally: Expr }

and UnresolvedVarDetails = { Ctx: CompilerEnv; Sym: Symbol }

and VarDetails =
    { Ctx: CompilerEnv; Var: Var; Tag: obj }

and BindingInit = { Binding: LocalBinding; Init: Expr }

and LocalBinding =
    { Sym: Symbol
      Tag: obj
      mutable Init: Expr option  // Needs to be mutable for LetFn -- we have to create all bindings, parse the inits, then go back and fill in the inits.
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
      LocalBinding: LocalBinding }

and ObjMethod() =
    member val UsesThis = false with get, set

and CompilerEnv =
    { Pctx: ParserContext
      Locals: IPersistentMap
      Method: ObjMethod option
      IsAssignContext: bool
      IsRecurContext: bool // TOOD: can we just check the loop id != None?
      LoopId: int option
      LoopLocals: ResizeArray<LocalBinding> }

    // Locals = Map from Symbol to LocalBinding

    static member Create(ctx: ParserContext) =
        { Pctx = ctx
          Locals = PersistentHashMap.Empty
          Method = None
          IsAssignContext = false
          IsRecurContext = false
          LoopId = None
          LoopLocals = null }

    member this.IsExpr = this.Pctx = ParserContext.Expression
    member this.IsStmt = this.Pctx = ParserContext.Statement
    member this.IsReturn = this.Pctx = ParserContext.Return

    member this.WithParserContext(ctx: ParserContext) = { this with Pctx = ctx }

    member this.GetLocalBinding(sym: Symbol) =
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
        // TODO: Implement ClosesOver
        ()

    member this.RegisterVar(v: Var) = () // TODO: Implement this



[<AbstractClass; Sealed>]
type ExprUtils private () =

    static member GetLiteralValue(e: Expr) =
        match e with
        | LiteralExpr l -> l.Value
        | _ -> failwith "Not a literal expression"
