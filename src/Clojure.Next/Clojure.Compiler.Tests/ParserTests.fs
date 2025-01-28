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
        "Basic constant Ttsts"
        [

          testCase "Parses an integer"
          <| fun _ ->
            let form = ReadFromString "42"
            let ast = Parser.Analyze(CompilerEnv.Create(Expression),form)
            Expect.isTrue (ast.IsLiteralExpr) "Should return a Literal"


          testCase "Parses a string"
          <| fun _ ->
            let form = ReadFromString "\"abc\""
            let ast = Parser.Analyze(CompilerEnv.Create(Expression),form)
            Expect.isTrue (ast.IsLiteralExpr) "Should return a Literal"

          testCase "Parses a vector"
          <| fun _ ->
            let form = ReadFromString "[1 2 3]"
            let cctx = CompilerEnv.Create(Expression)
            let ast = Parser.Analyze(cctx,form)
            Expect.equal ast (LiteralExpr({Value=form; Type=OtherType})) "Should return a Literal"


          ftestCase "Parses a list"
          <| fun _ ->
            let form = ReadFromString "(1 2 3)"
            let cctx = CompilerEnv.Create(Expression)
            let ast = Parser.Analyze(cctx,form)
            Expect.equal ast (LiteralExpr({Value=form; Type=OtherType})) "Should return a Literal"


        

        ]