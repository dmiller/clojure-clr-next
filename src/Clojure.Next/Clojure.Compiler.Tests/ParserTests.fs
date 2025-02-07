module ParserTests


open Expecto

open System
open Clojure.IO
open Clojure.Compiler
open System.Collections.Generic
open System.Linq
open Clojure.Collections

let CreatePushbackReaderFromString (s: string) =
    let sr = new System.IO.StringReader(s)
    new PushbackTextReader(sr)

let ReadFromString (s: string) =
    let r = CreatePushbackReaderFromString(s)
    LispReader.read (r, true, null, false)

let CreateLNPBRFromString (s: string) =
    let sr = new System.IO.StringReader(s)
    new LineNumberingTextReader(sr)

let ReadFromStringNumbering (s: string) =
    let r = CreateLNPBRFromString(s)
    LispReader.read (r, true, null, false)



[<Tests>]
let BasicConstantTests =
    testList
        "Basic constant Tests"
        [

          // Special cases:  nil, true, false
          testCase "Parses nil"
          <| fun _ ->
              let form = ReadFromString "nil"
              let cctx = CompilerEnv.Create(Expression)
              let ast = Parser.Analyze(cctx, form)

              Expect.equal
                  ast
                  (Expr.Literal(Env = cctx, Form = form, Value = form, Type = NilType))
                  "Should return a Literal with null value"

          testCase "Parses true"
          <| fun _ ->
              let form = ReadFromString "true"
              let cctx = CompilerEnv.Create(Expression)
              let ast = Parser.Analyze(cctx, form)

              Expect.equal
                  ast
                  (Expr.Literal(Env = cctx, Form = form, Value = form, Type = BoolType))
                  "Should return a Literal with null value"

          testCase "Parses false"
          <| fun _ ->
              let form = ReadFromString "false"
              let cctx = CompilerEnv.Create(Expression)
              let ast = Parser.Analyze(cctx, form)

              Expect.equal
                  ast
                  (Expr.Literal(Env = cctx, Form = form, Value = form, Type = BoolType))
                  "Should return a Literal with null value"


          testCase "Parses keyword"
          <| fun _ ->
              let form = ReadFromString ":kw"
              let cctx = CompilerEnv.Create(Expression)
              let ast = Parser.Analyze(cctx, form)

              Expect.equal
                  ast
                  (Expr.Literal(Env = cctx, Form = form, Value = form, Type = KeywordType))
                  "Should return a Literal with null value"

          testCase "Parses keyword and registers it"
          <| fun _ ->
              let form = ReadFromString ":kw"
              let register = ObjXRegister(None)
              let cctx = { CompilerEnv.Create(Expression) with ObjXRegister = Some register }
              let ast = Parser.Analyze(cctx, form)

              Expect.equal
                  ast
                  (Expr.Literal(Env = cctx, Form = form, Value = form, Type = KeywordType))
                  "Should return a Literal with null value"

              let kws = register.Keywords
              Expect.equal (kws.count ()) 1 "Should be one keyword"
              Expect.isTrue (kws.containsKey form) "Should contain the keyword we read"

          testCase "Parses two keywords and registers both"
          <| fun _ ->
              let form1 = ReadFromString ":a"
              let form2 = ReadFromString ":b"
              let register = ObjXRegister(None)
              let cctx = { CompilerEnv.Create(Expression) with ObjXRegister = Some register }

              let ast = Parser.Analyze(cctx, form1)

              Expect.equal
                  ast
                  (Expr.Literal(Env = cctx, Form = form1, Value = form1, Type = KeywordType))
                  "Should return a Literal with null value"

              let ast = Parser.Analyze(cctx, form2)

              Expect.equal
                  ast
                  (Expr.Literal(Env = cctx, Form = form2, Value = form2, Type = KeywordType))
                  "Should return a Literal with null value"


              let kws = register.Keywords
              Expect.equal (kws.count ()) 2 "Should be two keywords"
              Expect.isTrue (kws.containsKey form1) "Should contain the first keyword we read"
              Expect.isTrue (kws.containsKey form2) "Should contain the second keyword we read"

          testCase "Parsing same keyword twice registers once"
          <| fun _ ->
              let form1 = ReadFromString ":a"

              let register = ObjXRegister(None)
              let cctx = { CompilerEnv.Create(Expression) with ObjXRegister = Some register }

              let ast = Parser.Analyze(cctx, form1)

              Expect.equal
                  ast
                  (Expr.Literal(Env = cctx, Form = form1, Value = form1, Type = KeywordType))
                  "Should return a Literal with null value"

              let ast = Parser.Analyze(cctx, form1)

              Expect.equal
                  ast
                  (Expr.Literal(Env = cctx, Form = form1, Value = form1, Type = KeywordType))
                  "Should return a Literal with null value"


              let kws = register.Keywords
              Expect.equal (kws.count ()) 1 "Should be two keywords"
              Expect.isTrue (kws.containsKey form1) "Should contain the first keyword we read"


          testCase "Parses an Int64"
          <| fun _ ->
              let form = ReadFromString "42"
              let cctx = CompilerEnv.Create(Expression)
              let ast = Parser.Analyze(cctx, form)

              Expect.equal
                  ast
                  (Expr.Literal(Env = cctx, Form = form, Value = form, Type = PrimNumericType))
                  "Should return a numeric Literal"

          testCase "Parses a Double"
          <| fun _ ->
              let form = ReadFromString "42.1"
              let cctx = CompilerEnv.Create(Expression)
              let ast = Parser.Analyze(cctx, form)

              Expect.equal
                  ast
                  (Expr.Literal(Env = cctx, Form = form, Value = form, Type = PrimNumericType))
                  "Should return a numeric Literal"


          testCase "Parses an Int32"
          <| fun _ ->
              let form = 42
              let cctx = CompilerEnv.Create(Expression)
              let ast = Parser.Analyze(cctx, form)

              Expect.equal
                  ast
                  (Expr.Literal(Env = cctx, Form = form, Value = form, Type = PrimNumericType))
                  "Should return a numeric Literal"


          testCase "Parses other numeric primities as 'OtherType'"
          <| fun _ ->
              let form = 42u
              let cctx = CompilerEnv.Create(Expression)
              let ast = Parser.Analyze(cctx, form)

              Expect.equal
                  ast
                  (Expr.Literal(Env = cctx, Form = form, Value = form, Type = OtherType))
                  "Should return an 'OtherType' Literal"


          testCase "Parses a string"
          <| fun _ ->
              let form = ReadFromString "\"abc\""
              let cctx = CompilerEnv.Create(Expression)
              let ast = Parser.Analyze(cctx, form)

              Expect.equal
                  ast
                  (Expr.Literal(Env = cctx, Form = form, Value = form, Type = StringType))
                  "Should return an string Literal"

          ]

[<Tests>]
let CollectionTests =
    testList
        "Collection Tests"
        [ testCase "Parses a constant vector as a literal"
          <| fun _ ->
              let form = ReadFromString "[1 2 3]"
              let cctx = CompilerEnv.Create(Expression)
              let ast = Parser.Analyze(cctx, form)

              Expect.equal
                  ast
                  (Expr.Literal(Env = cctx, Form = form, Value = form, Type = OtherType))
                  "Should return a Literal"

          testCase "Parses a constant set as a literal"
          <| fun _ ->
              let form = ReadFromString "#{1 2 3}"
              let cctx = CompilerEnv.Create(Expression)
              let ast = Parser.Analyze(cctx, form)

              Expect.equal
                  ast
                  (Expr.Literal(Env = cctx, Form = form, Value = form, Type = OtherType))
                  "Should return a Literal"


          testCase "Parses a constant map as a literal"
          <| fun _ ->
              let form = ReadFromString "#{:a 1 :b 2}"
              let cctx = CompilerEnv.Create(Expression)
              let ast = Parser.Analyze(cctx, form)

              Expect.equal
                  ast
                  (Expr.Literal(Env = cctx, Form = form, Value = form, Type = OtherType))
                  "Should return a Literal"

          ]

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


[<Tests>]
let SymbolTests =
    testList
        "Symbol Tests"
        [ testCase "Parses TypeName/FieldName with found Fieldname"
          <| fun _ ->
              let form = ReadFromString "Int64/MaxValue"
              let cctx = CompilerEnv.Create(Expression)
              let ast = Parser.Analyze(cctx, form)

              compareInteropCalls (
                  ast,
                  (Expr.InteropCall(
                      Env = cctx,
                      Form = form,
                      Type = FieldOrPropertyExpr,
                      IsStatic = true,
                      Tag = null,
                      Target = None,
                      TargetType = typeof<Int64>,
                      MemberName = "MaxValue",
                      TInfo = (typeof<Int64>.GetField ("MaxValue")),
                      Args = null,
                      TypeArgs = (ResizeArray<Type>()),
                      SourceInfo = None
                  ))
              )

          ftestCase "Parses TypeName/FieldName with Fieldname not found as static QualifiedMethod"
          <| fun _ ->
              let form = ReadFromString "Int64/asdf"
              let cctx = CompilerEnv.Create(Expression)
              let ast = Parser.Analyze(cctx, form)

              Expect.equal
                  ast
                  (Expr.QualifiedMethod(
                      Env = cctx,
                      Form = form,
                      MethodType = typeof<Int64>,
                      HintedSig = None,
                      MethodSymbol = Symbol.intern ("Int64/asdf"),
                      MethodName = "asdf",
                      Kind = Static,
                      TagClass = typeof<AFn>,
                      SourceInfo = None
                  ))
                  "Should find static QM"

          ftestCase "Parses TypeName/.FieldName with Fieldname not found as instance QualifiedMethod"
          <| fun _ ->
              let form = ReadFromString "Int64/.asdf"
              let cctx = CompilerEnv.Create(Expression)
              let ast = Parser.Analyze(cctx, form)

              Expect.equal
                  ast
                  (Expr.QualifiedMethod(
                      Env = cctx,
                      Form = form,
                      MethodType = typeof<Int64>,
                      HintedSig = None,
                      MethodSymbol = Symbol.intern ("Int64/.asdf"),
                      MethodName = "asdf",
                      Kind = Instance,
                      TagClass = typeof<AFn>,
                      SourceInfo = None
                  ))
                  "Should find static QM"

          // TODO: If tag on form, should be a tagged QM
          // TODO: if params-tag on form should supply signature hint 

          ]
