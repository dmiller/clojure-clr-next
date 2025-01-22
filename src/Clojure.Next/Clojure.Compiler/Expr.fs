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

type ConstantType = 
| NilType
| BoolType
| StringType
| EmptyType
| OtherType

type CompilerContext(ctx: ParserContext) =
    member this.Ctx = ctx

type ProtocolDetails = { ProtocolOn: Type; OnMethod: MethodInfo; SiteIndex: int}

type Expr = 
| AssignExpr of AssignDetails
| BodyExpr of BodyDetails
| CollectionExpr of CollectionDetails
| ConstantExpr of ConstantDetails
| DefExpr of DefDetails
| FnExpr of FnDetails
| IfExpr of IfDetails
| ImportExpr of ImportDetails
| InstanceOfExpr of InstanceOfDetails
| InvokeExpr of InvokeDetails
| KeywordExpr of KeywordDetails
| KeywordInvokeExpr of KeywordInvokeDetails
| LetExpr of LetDetails
| MetaExpr of MetaDetails
| MethodExpr of MethodDetails
| NewExpr of NewDetails
| NewInstanceExpr of NewInstanceDetails
| NumberExpr of NumberDetails
| QualifiedMethodExpr of QualifiedMethodDetails
| TryExpr of TryDetails
| UnresolvedVarExpr of UnresolvedVarDetails
| VarExpr of VarDetails
| UntypedExpr of UntypedExprDetails

and AssignDetails = { Ctx: CompilerContext; Target: Expr; Value: Expr }
and BodyDetails = { Ctx: CompilerContext; Exprs: Expr list }
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
and ConstantDetails = { Type: ConstantType; Value: obj }
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
and IfDetails = { Test: Expr; Then: Expr; Else: Expr; SourceInfo: SourceInfo }
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
and KeywordDetails = { Ctx: CompilerContext; Keyword: Keyword }
and KeywordInvokeDetails = {Ctx: CompilerContext; KwExpr: Expr; Target: Expr; Tag: obj; SiteIndex: int; SourceInfo: SourceInfo }
and LetDetails = { Ctx: CompilerContext; Mode: LetExprMode; BindingInits: BindingInit list; Body: Expr; SourceInfo: SourceInfo }
and MetaDetails = { Ctx: CompilerContext; Target: Expr; Meta: Expr }
and MethodDetails = { Ctx: CompilerContext; MethodName: string; Args : HostArg list; }
and UntypedExprDetails = { Ctx: CompilerContext; Type: UntypedExprType; Target: Expr}
and NewDetails = { Ctx: CompilerContext; Args: HostArg list; Type: Type; IsNoArgValueTypeCtor: bool; SourceInfo: SourceInfo }
and NewInstanceDetails = { Ctx: CompilerContext (* help*) }  // maybe combindwith fn?
and NumberDetails = { Ctx: CompilerContext; Value: obj }
and QualifiedMethodDetails = { Ctx: CompilerContext; (* help *) SourceInfo: SourceInfo }
and RecurDetails = { Ctx: CompilerContext; Args: Expr list; LoopLocals: LocalBinding list; SourceInfo: SourceInfo }
and TryDetails = { Ctx: CompilerContext; TryExpr: Expr; Catches: CatchClause list; Finally: Expr }
and UnresolvedVarDetails = { Ctx: CompilerContext; Sym: Symbol }
and VarDetails = { Ctx: CompilerContext; Var: Var; Tag: obj }
and BindingInit = { Binding: LocalBinding; Init: Expr }
and LocalBinding = { Sym: Symbol; Tag: obj; Init: Expr; Name: string; IsArg: bool; IsByRef: bool; IsRecur: bool; IsThis: bool }
and CatchClause = { CaughtType: Type; LocalBinding: LocalBinding; Handler: Expr }
and HostArg = { ParamType: ParameterType; ArgExpr: Expr; LocalBinding: LocalBinding }

    

