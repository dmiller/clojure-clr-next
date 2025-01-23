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
            let ast = Compiler.Analyze(CompilerContext(Expression),form)
            Expect.equal (ast.GetType()) typeof<Expr> "Should return an Expr"

        ]