﻿module ExprUtils

open Expecto
open Clojure.Compiler


let compareGenericLists (a: ResizeArray<'T>, b: ResizeArray<'T>) =
    if isNull a then
        Expect.isTrue (isNull b) "First list is null, second list is not"
    else
        Expect.isNotNull b "First list is not null, second list is"

        let comp =
            a
            |> Seq.zip b
            |> Seq.forall (fun (x, y) -> if isNull x then isNull y else x.Equals(y))

        Expect.isTrue comp "Expect same list"


let compareInteropCalls (a: Expr, b: Expr) =
    match a, b with
    | Expr.InteropCall(
        Env = aenv
        Form = aform
        Type = atype
        IsStatic = aisstatic
        Tag = atag
        Target = atarget
        TargetType = atargettype
        MemberName = amembername
        TInfo = atinfo
        Args = aargs
        TypeArgs = atypeargs
        SourceInfo = asourceinfo),
      Expr.InteropCall(
          Env = benv
          Form = bform
          Type = btype
          IsStatic = bisstatic
          Tag = btag
          Target = btarget
          TargetType = btargettype
          MemberName = bmembername
          TInfo = btinfo
          Args = bargs
          TypeArgs = btypeargs
          SourceInfo = bsourceinfo) ->
        Expect.equal aenv benv "Env should be equal"
        Expect.equal aform bform "Form should be equal"
        Expect.equal atype btype "Type should be equal"
        Expect.equal aisstatic bisstatic "IsStatic should be equal"
        Expect.equal atag btag "Tag should be equal"
        Expect.equal atarget btarget "Target should be equal"
        Expect.equal atargettype btargettype "TargetType should be equal"
        Expect.equal amembername bmembername "MemberName should be equal"
        Expect.equal atinfo btinfo "TInfo should be equal"
        compareGenericLists (aargs, bargs)
        compareGenericLists (atypeargs, btypeargs)
        Expect.equal asourceinfo bsourceinfo "SourceInfo should be equal"
    | _ -> failwith "Not an InteropCall"


let compareSignatureHints (a: ISignatureHint, b: ISignatureHint) =
    compareGenericLists (a.Args, b.Args)

    match a.GenericTypeArgs, b.GenericTypeArgs with
    | None, None -> Expect.isTrue true "Both are None"
    | None, _ -> Expect.isTrue false "First is None, second is not"
    | _, None -> Expect.isTrue false "First is not None, second is"
    | Some a, Some b -> compareGenericLists (a, b)
    

let compareSignatureHintOptions (a: ISignatureHint option, b: ISignatureHint option) =
    match a, b with
    | None, None -> Expect.isTrue true "Both are None"
    | None, _ -> Expect.isTrue false "First is None, second is not"
    | _, None -> Expect.isTrue false "First is not None, second is"
    | Some a, Some b -> compareSignatureHints (a, b)


let compareQualifiedMethods(a: Expr, b:Expr) =
    match a, b with
    | Expr.QualifiedMethod(
        Env = aenv
        Form = aform
        MethodType = atype
        HintedSig = asig
        TagClass = atag
        MethodName = aemethodname
        Kind = akind
        SourceInfo = asourceinfo),
      Expr.QualifiedMethod(
          Env = benv
          Form = bform
          HintedSig = bsig
          MethodType = btype
          TagClass = btag
          MethodName = bemethodname
          Kind = bkind
          SourceInfo = bsourceinfo) ->
        Expect.equal aenv benv "Env should be equal"
        Expect.equal aform bform "Form should be equal"
        Expect.equal atype btype "Type should be equal"
        Expect.equal atag btag "Tag should be equal"
        compareSignatureHintOptions (asig, bsig)
        Expect.equal aemethodname bemethodname "MethodName should be equal"
        Expect.equal akind bkind "Kind should be equal"
        Expect.equal asourceinfo bsourceinfo "SourceInfo should be equal"
    | _ -> failwith "Not a QualifiedMethod"