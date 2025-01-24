namespace Clojure.Compiler

open System
open System.Collections.Generic
open Clojure.Collections
open Clojure.Lib
open System.Reflection


type SourceInfo = { Source: string; Line: int; Column: int;  EndLine: int; EndColumn: int }


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

type ProtocolDetails = { ProtocolOn: Type; OnMethod: MethodInfo; SiteIndex: int}

type Expr = 
| AssignExpr of AssignDetails
| BodyExpr of BodyDetails
| CollectionExpr of CollectionDetails   // combines MapExpr, SetExpr, VectorExpr
| LiteralExpr of LiteralDetails  // Combines ConstExpr, NumberExpr, NilExpr, StringExpr, BoolExpr , KeywordExpr, EmptyExpr
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
| TheVarExpr of TheVarDetails
| TryExpr of TryDetails
| UnresolvedVarExpr of UnresolvedVarDetails
| VarExpr of VarDetails
| UntypedExpr of UntypedExprDetails  // combines MonitorEnterExpr, MonitorExitExpr, ThrowExpr

and AssignDetails = { Ctx: CompilerContext; Target: Expr; Value: Expr }
and BodyDetails = { Ctx: CompilerContext; Exprs: List<Expr> }
and CaseDetails = { 
    Ctx: CompilerContext; 
    Expr: Expr; 
    DefaultExpr: Expr; 
    Shift: int; 
    Mask: int;
    Tests: SortedDictionary<int,Expr>;
    Thens : SortedDictionary<int,Expr>;
    SwitchType: Keyword;
    TestType: Keyword;
    SkipCheck: IPersistentSet
    ReturnType: Type}
and CollectionDetails = { Ctx: CompilerContext; Value: obj }
and LiteralDetails = { Type: LiteralType; Value: obj }
and DefDetails = { 
    Ctx: CompilerContext; 
    Var: Var; 
    Init: Expr; 
    Meta: Expr; 
    InitProvided: bool; 
    IsDynamic: bool; 
    ShadowsCoreMapping: bool; 
    SourceInfo: SourceInfo} 
and FnDetails = { Ctx: CompilerContext; (* help *) SourceInfo: SourceInfo }
and HostExprDetails = { 
    Ctx: CompilerContext; 
    Type: HostExprType;
    Tag: Symbol;
    Target: Expr option;
    TargetType: Type;
    MemberName: string;
    TInfo: MemberInfo;
    Args: ResizeArray<HostArg>;
    IsTailPosition: bool;    
    SourceInfo: SourceInfo option }
and IfDetails = { Test: Expr; Then: Expr; Else: Expr; SourceInfo: SourceInfo option}
and ImportDetails = { Ctx: CompilerContext; Typename: string  }
and InstanceOfDetails = { Ctx: CompilerContext; Expr: Expr; Type: Type; SourceInfo: SourceInfo }
and InvokeDetails = { 
    Ctx: CompilerContext; 
    Fexpr: Expr; 
    Args: Expr list; 
    Tag: obj; 
    TailPosition: bool;  
    ProtocolDetails: ProtocolDetails option; 
    SourceInfo: SourceInfo }
and KeywordInvokeDetails = {Ctx: CompilerContext; KwExpr: Expr; Target: Expr; Tag: obj; SiteIndex: int; SourceInfo: SourceInfo }
and LetDetails = { Ctx: CompilerContext; Mode: LetExprMode; BindingInits: BindingInit list; Body: Expr; SourceInfo: SourceInfo }
and LocalBindingDetails = { Ctx: CompilerContext; Binding: LocalBinding; Tag: Symbol }
and MetaDetails = { Ctx: CompilerContext; Target: Expr; Meta: Expr }
and MethodDetails = { Ctx: CompilerContext; MethodName: string; Args : HostArg list; }
and UntypedExprDetails = { Ctx: CompilerContext; Type: UntypedExprType; Target: Expr option}
and NewDetails = { Ctx: CompilerContext; Args: HostArg list; Type: Type; IsNoArgValueTypeCtor: bool; SourceInfo: SourceInfo }
and NewInstanceDetails = { Ctx: CompilerContext (* help*) }  // maybe combindwith fn?
and NumberDetails = { Ctx: CompilerContext; Value: obj }
and QualifiedMethodDetails = { Ctx: CompilerContext; (* help *) SourceInfo: SourceInfo option}
and RecurDetails = { Ctx: CompilerContext; Args: Expr list; LoopLocals: LocalBinding list; SourceInfo: SourceInfo }
and TheVarDetails = { Ctx: CompilerContext; Var: Var}
and TryDetails = { Ctx: CompilerContext; TryExpr: Expr; Catches: CatchClause list; Finally: Expr }
and UnresolvedVarDetails = { Ctx: CompilerContext; Sym: Symbol }
and VarDetails = { Ctx: CompilerContext; Var: Var; Tag: obj }
and BindingInit = { Binding: LocalBinding; Init: Expr }
and LocalBinding = { Sym: Symbol; Tag: obj; Init: Expr; Name: string; IsArg: bool; IsByRef: bool; IsRecur: bool; IsThis: bool; Index: int }
and CatchClause = { CaughtType: Type; LocalBinding: LocalBinding; Handler: Expr }
and HostArg = { ParamType: ParameterType; ArgExpr: Expr; LocalBinding: LocalBinding }

and ObjMethod() =
    member val UsesThis = false with get, set

and CompilerContext(_ctx: ParserContext, lbs : IPersistentMap, _method : ObjMethod option, _isAssignContext : bool ) =
    
    // Map from Symbol to LocalBinding
    let mutable _localBindings = lbs

    new(ctx: ParserContext) = new CompilerContext(ctx, PersistentHashMap.Empty, None, false)
        
    member this.WithParserContext(ctx: ParserContext) = new CompilerContext(ctx,_localBindings, _method, _isAssignContext) 
    member this.WithIsAssign(isAssign: bool)= new CompilerContext(_ctx,_localBindings, _method, isAssign) 

    member _.ParserContext = _ctx
    member _.LocalBindings = _localBindings
    member _.Method = _method

    member _.IsExpr = _ctx = ParserContext.Expression
    member _.IsStmt = _ctx = ParserContext.Statement
    member _.IsReturn = _ctx = ParserContext.Return
    member _.IsAssignContext = _isAssignContext

    member this.GetLocalBinding(sym: Symbol) = 
        match RT0.get(_localBindings,sym) with
        | :? LocalBinding as lb ->
            match _method with
            | Some m -> 
                if lb.Index = 0 then 
                    m.UsesThis <- true
                    this.ClosesOver(lb, m)

                Some lb
            | _ -> None
        | _ -> None

    member this.ContainsBindingForSym(sym: Symbol) = 
        not <| isNull (RT0.get(_localBindings,sym))


    member this.ClosesOver(b: LocalBinding, method: ObjMethod) = 
        // TODO: Implement ClosesOver
        ()

    member this.RegisterVar(v: Var) = () // TODO: Implement this
                
            



    
[<AbstractClass;Sealed>]
type ExprUtils private () =

    static member GetLiteralValue(e: Expr) = 
        match e with
        | LiteralExpr l -> l.Value
        | _ -> failwith "Not a literal expression"
