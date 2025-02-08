module ParserTests


open Expecto

open System
open Clojure.IO
open Clojure.Compiler
open System.Collections.Generic
open System.Linq
open Clojure.Collections
open ExprUtils

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

          testCase "Parses TypeName/FieldName with Fieldname not found as static QualifiedMethod"
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

          testCase "Parses TypeName/.FieldName with Fieldname not found as instance QualifiedMethod"
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
                  "Should find instance QM"

          testCase "Throws on ^NotAType TypeName/FieldName with Fieldname not found as static QualifiedMethod, with tag used for TagClass"
          <| fun _ ->
              let form = ReadFromString "^NotAType Int64/asdf"
              let cctx = CompilerEnv.Create(Expression)

              Expect.throwsT<CompilerException> (fun _ -> Parser.Analyze(cctx, form) |> ignore) "Should throw with non-type as :tag meta"


          testCase "Parses ^String TypeName/FieldName with Fieldname not found as static QualifiedMethod, with TagClass"
          <| fun _ ->
              let form = ReadFromString "^String Int64/asdf"
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
                      TagClass = typeof<String>,
                      SourceInfo = None
                  ))
                  "Should find static QM, with TagClass"

          testCase "Parses ^[String int] TypeName/FieldName with Fieldname not found as static QualifiedMethod, with HintedSig"
          <| fun _ ->
              let form = ReadFromString "^[String int] Int64/asdf"
              let cctx = CompilerEnv.Create(Expression)
              let ast = Parser.Analyze(cctx, form)

              compareQualifiedMethods(
                  ast,
                  (Expr.QualifiedMethod(
                      Env = cctx,
                      Form = form,
                      MethodType = typeof<Int64>,
                      HintedSig = (SignatureHint.MaybeCreate(cctx, PersistentVector.create(typeof<String>, typeof<Int32>))),
                      MethodSymbol = Symbol.intern ("Int64/asdf"),
                      MethodName = "asdf",
                      Kind = Static,
                      TagClass = typeof<AFn>,
                      SourceInfo = None
                  )))



          testCase "Detects local bindings."
          <| fun _ ->

              let cctx = CompilerEnv.Create(Expression)

              let register = ObjXRegister(None)
              let internals = ObjXInternals()
              let objx = Expr.Obj(Env = cctx, Form = null, Type = ObjXType.Fn, Internals = internals, Register=register, SourceInfo = None)
              let method = ObjMethod(ObjXType.Fn, objx, internals, register, None )

              let cctx = {cctx with Method = Some method; ObjXRegister= Some register}


              let cctx, lbThis = cctx.RegisterLocalThis(Symbol.intern("this"), null, None)
              let cctx, lbAsdf = cctx.RegisterLocal(Symbol.intern("asdf"), null, None, null, false)       

              let form0  = ReadFromString "this"
              let form1 = ReadFromString "asdf"

              let ast0 = Parser.Analyze(cctx, form0)

              Expect.equal ast0 (Expr.LocalBinding(Env = cctx, Form = form0, Binding = lbThis, Tag=null)) "Should find binding for this"

              let ast1 = Parser.Analyze(cctx, form1)
              Expect.equal ast1 (Expr.LocalBinding(Env = cctx, Form = form1, Binding = lbAsdf, Tag=null)) "Should find binding for asdf"

              let closes = register.Closes
              Expect.equal (closes.count()) 0 "Should no closeovers"

          ftestCase "Detects local bindings, does close over if not in method locals."
          <| fun _ ->

              let cctx = CompilerEnv.Create(Expression)

              // Let's add a local binding to the environment, but before we add the method, so this binding will be non-local to the methld.
              let locals = cctx.Locals
              let pqrsSym = Symbol.intern("pqrs")
              let lbPqrs = {Sym = pqrsSym; Tag = null; Init = None; Name = pqrsSym.Name; IsArg = false; IsByRef = false; IsRecur = false; IsThis = false; Index =20}
              let newLocals = RTMap.assoc(locals, pqrsSym, lbPqrs) :?> IPersistentMap
              let cctx = {cctx with Locals = newLocals}

              let register = ObjXRegister(None)
              let internals = ObjXInternals()
              let objx = Expr.Obj(Env = cctx, Form = null, Type = ObjXType.Fn, Internals = internals, Register=register, SourceInfo = None)
              let method = ObjMethod(ObjXType.Fn, objx, internals, register, None )

              let cctx = {cctx with Method = Some method; ObjXRegister= Some register}

              let cctx, lbThis = cctx.RegisterLocalThis(Symbol.intern("this"), null, None)
              let cctx, lbAsdf = cctx.RegisterLocal(Symbol.intern("asdf"), null, None, null, false)       

              let form0  = ReadFromString "this"
              let form1 = ReadFromString "asdf"

              let ast0 = Parser.Analyze(cctx, form0)

              Expect.equal ast0 (Expr.LocalBinding(Env = cctx, Form = form0, Binding = lbThis, Tag=null)) "Should find binding for this"

              let ast1 = Parser.Analyze(cctx, form1)
              Expect.equal ast1 (Expr.LocalBinding(Env = cctx, Form = form1, Binding = lbAsdf, Tag=null)) "Should find binding for asdf"

              let ast2 = Parser.Analyze(cctx, pqrsSym)
              Expect.equal ast2 (Expr.LocalBinding(Env = cctx, Form = pqrsSym, Binding = lbPqrs, Tag=null)) "Should find binding for pqrs"

              let closes = register.Closes
              Expect.equal (closes.count()) 1 "Should be one closeover"
              Expect.isTrue (closes.containsKey(lbPqrs)) "Should contain pqrs closeover"
              Expect.equal (closes.valAt(lbPqrs)) lbPqrs "Should contain pqrs closeover"

              

          ]
