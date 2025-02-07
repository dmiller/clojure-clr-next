module ParserTests


open Expecto

open System
open Clojure.IO
open Clojure.Compiler

let CreatePushbackReaderFromString(s: string) =
    let sr = new System.IO.StringReader(s)
    new PushbackTextReader(sr)

let ReadFromString(s: string) =
    let r = CreatePushbackReaderFromString(s)
    LispReader.read(r,true,null,false)

let CreateLNPBRFromString(s: string) =
    let sr = new System.IO.StringReader(s)
    new LineNumberingTextReader(sr)

let ReadFromStringNumbering(s: string) =
    let r = CreateLNPBRFromString(s)
    LispReader.read(r,true,null,false)



[<Tests>]
let BasicParserTests =
    testList
        "Basic constant Tests"
        [

          // Special cases:  nil, true, false
          testCase  "Parses nil"
          <| fun _ ->
            let form = ReadFromString "nil"
            let cctx = CompilerEnv.Create(Expression)
            let ast = Parser.Analyze(cctx, form)
            Expect.equal ast (Expr.Literal(Env=cctx, Form = form, Value = form, Type = NilType)) "Should return a Literal with null value"

          testCase  "Parses true"
          <| fun _ ->
            let form = ReadFromString "true"
            let cctx = CompilerEnv.Create(Expression)
            let ast = Parser.Analyze(cctx, form)
            Expect.equal ast (Expr.Literal(Env=cctx, Form = form, Value = form, Type = BoolType)) "Should return a Literal with null value"

          testCase  "Parses false"
          <| fun _ ->
            let form = ReadFromString "false"
            let cctx = CompilerEnv.Create(Expression)
            let ast = Parser.Analyze(cctx, form)
            Expect.equal ast (Expr.Literal(Env=cctx, Form = form, Value = form, Type = BoolType)) "Should return a Literal with null value"


          testCase  "Parses keyword"
          <| fun _ ->
            let form = ReadFromString ":kw"
            let cctx = CompilerEnv.Create(Expression)
            let ast = Parser.Analyze(cctx, form)
            Expect.equal ast (Expr.Literal(Env=cctx, Form = form, Value = form, Type = KeywordType)) "Should return a Literal with null value"

          testCase  "Parses keyword and registers it"
          <| fun _ ->
            let form = ReadFromString ":kw"
            let register = ObjXRegister(None)
            let cctx = { CompilerEnv.Create(Expression) with ObjXRegister = Some register }
            let ast = Parser.Analyze(cctx, form)
            Expect.equal ast (Expr.Literal(Env=cctx, Form = form, Value = form, Type = KeywordType)) "Should return a Literal with null value"
            
            let kws = register.Keywords
            Expect.equal (kws.count()) 1 "Should be one keyword"
            Expect.isTrue (kws.containsKey form) "Should contain the keyword we read"

          testCase  "Parses two keywords and registers both"
          <| fun _ ->
            let form1 = ReadFromString ":a"
            let form2 = ReadFromString ":b"
            let register = ObjXRegister(None)
            let cctx = { CompilerEnv.Create(Expression) with ObjXRegister = Some register }
 
            let ast = Parser.Analyze(cctx, form1)
            Expect.equal ast (Expr.Literal(Env=cctx, Form = form1, Value = form1, Type = KeywordType)) "Should return a Literal with null value"
  
            let ast = Parser.Analyze(cctx, form2)
            Expect.equal ast (Expr.Literal(Env=cctx, Form = form2, Value = form2, Type = KeywordType)) "Should return a Literal with null value"


            let kws = register.Keywords
            Expect.equal (kws.count()) 2 "Should be two keywords"
            Expect.isTrue (kws.containsKey form1) "Should contain the first keyword we read"
            Expect.isTrue (kws.containsKey form2) "Should contain the second keyword we read"

          testCase  "Parsing same keyword twice registers once"
          <| fun _ ->
            let form1 = ReadFromString ":a"

            let register = ObjXRegister(None)
            let cctx = { CompilerEnv.Create(Expression) with ObjXRegister = Some register }
 
            let ast = Parser.Analyze(cctx, form1)
            Expect.equal ast (Expr.Literal(Env=cctx, Form = form1, Value = form1, Type = KeywordType)) "Should return a Literal with null value"
  
            let ast = Parser.Analyze(cctx, form1)
            Expect.equal ast (Expr.Literal(Env=cctx, Form = form1, Value = form1, Type = KeywordType)) "Should return a Literal with null value"


            let kws = register.Keywords
            Expect.equal (kws.count()) 1 "Should be two keywords"
            Expect.isTrue (kws.containsKey form1) "Should contain the first keyword we read"


          testCase "Parses an Int64"
          <| fun _ ->
            let form = ReadFromString "42"
            let cctx = CompilerEnv.Create(Expression)
            let ast = Parser.Analyze(cctx,form)
            Expect.equal ast (Expr.Literal(Env = cctx, Form = form, Value=form, Type=PrimNumericType)) "Should return a numeric Literal"

          testCase "Parses a Double"
          <| fun _ ->
            let form = ReadFromString "42.1"
            let cctx = CompilerEnv.Create(Expression)
            let ast = Parser.Analyze(cctx,form)
            Expect.equal ast (Expr.Literal(Env = cctx, Form = form, Value=form, Type=PrimNumericType)) "Should return a numeric Literal"


          testCase "Parses an Int32"
          <| fun _ ->
            let form = 42
            let cctx = CompilerEnv.Create(Expression)
            let ast = Parser.Analyze(cctx,form)
            Expect.equal ast (Expr.Literal(Env = cctx, Form = form, Value=form, Type=PrimNumericType)) "Should return a numeric Literal"


          testCase "Parses other numeric primities as 'OtherType'"
          <| fun _ ->
            let form = 42u
            let cctx = CompilerEnv.Create(Expression)
            let ast = Parser.Analyze(cctx,form)
   
            Expect.equal ast (Expr.Literal(Env = cctx, Form = form, Value=form, Type=OtherType)) "Should return an 'OtherType' Literal"


          testCase "Parses a string"
          <| fun _ ->
            let form = ReadFromString "\"abc\""
            let cctx = CompilerEnv.Create(Expression)
            let ast = Parser.Analyze(cctx,form)
            Expect.equal ast (Expr.Literal(Env = cctx, Form = form, Value=form, Type=StringType)) "Should return an string Literal"

          ftestCase "Parses a constant vector as a literal"
          <| fun _ ->
            let form = ReadFromString "[1 2 3]"
            let cctx = CompilerEnv.Create(Expression)
            let ast = Parser.Analyze(cctx,form)
            Expect.equal ast (Expr.Literal(Env = cctx, Form = form, Value=form, Type=OtherType)) "Should return a Literal"

          ftestCase "Parses a constant set as a literal"
          <| fun _ ->
            let form = ReadFromString "#{1 2 3}"
            let cctx = CompilerEnv.Create(Expression)
            let ast = Parser.Analyze(cctx,form)
            Expect.equal ast (Expr.Literal(Env = cctx, Form = form, Value=form, Type=OtherType)) "Should return a Literal"


          ftestCase "Parses a constant map as a literal"
          <| fun _ ->
            let form = ReadFromString "#{:a 1 :b 2}"
            let cctx = CompilerEnv.Create(Expression)
            let ast = Parser.Analyze(cctx,form)
            Expect.equal ast (Expr.Literal(Env = cctx, Form = form, Value=form, Type=OtherType)) "Should return a Literal"



        

        ]