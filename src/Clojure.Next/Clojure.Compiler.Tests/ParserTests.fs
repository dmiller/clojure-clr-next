module ParserTests


open Expecto

open System
open Clojure.IO
open Clojure.Compiler
open System.Collections.Generic
open System.Linq
open Clojure.Collections
open ExprUtils
open Clojure.Lib
open Clojure.Reflection
open System.Threading

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
                  (AST.Literal(LiteralExpr(env = cctx, form = form, value = form, literalType = NilType)))
                  "Should return a Literal with null value"

          testCase "Parses true"
          <| fun _ ->
              let form = ReadFromString "true"
              let cctx = CompilerEnv.Create(Expression)
              let ast = Parser.Analyze(cctx, form)

              Expect.equal
                  ast
                  (AST.Literal(LiteralExpr(env = cctx, form = form, value = form, literalType = BoolType)))
                  "Should return a Literal with null value"

          testCase "Parses false"
          <| fun _ ->
              let form = ReadFromString "false"
              let cctx = CompilerEnv.Create(Expression)
              let ast = Parser.Analyze(cctx, form)

              Expect.equal
                  ast
                  (AST.Literal(LiteralExpr(env = cctx, form = form, value = form, literalType = BoolType)))
                  "Should return a Literal with null value"


          testCase "Parses keyword"
          <| fun _ ->
              let form = ReadFromString ":kw"
              let cctx = CompilerEnv.Create(Expression)
              let ast = Parser.Analyze(cctx, form)

              Expect.equal
                  ast
                  (AST.Literal(LiteralExpr(env = cctx, form = form, value = form, literalType = KeywordType)))
                  "Should return a Literal with null value"

          testCase "Parses keyword and registers it"
          <| fun _ ->
              let form = ReadFromString ":kw"
              let register = ObjXRegister(None)
              let cctx = { CompilerEnv.Create(Expression) with ObjXRegister = Some register }
              let ast = Parser.Analyze(cctx, form)

              Expect.equal
                  ast
                  (AST.Literal(LiteralExpr(env = cctx, form = form, value = form, literalType = KeywordType)))
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
                  (AST.Literal(LiteralExpr(env = cctx, form = form1, value = form1, literalType = KeywordType)))
                  "Should return a Literal with null value"

              let ast = Parser.Analyze(cctx, form2)

              Expect.equal
                  ast
                  (AST.Literal(LiteralExpr(env = cctx, form = form2, value = form2, literalType = KeywordType)))
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
                  (AST.Literal(LiteralExpr(env = cctx, form = form1, value = form1, literalType = KeywordType)))
                  "Should return a Literal with null value"

              let ast = Parser.Analyze(cctx, form1)

              Expect.equal
                  ast
                  (AST.Literal(LiteralExpr(env = cctx, form = form1, value = form1, literalType = KeywordType)))
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
                  (AST.Literal(LiteralExpr(env = cctx, form = form, value = form, literalType = PrimNumericType)))
                  "Should return a numeric Literal"

          testCase "Parses a Double"
          <| fun _ ->
              let form = ReadFromString "42.1"
              let cctx = CompilerEnv.Create(Expression)
              let ast = Parser.Analyze(cctx, form)

              Expect.equal
                  ast
                  (AST.Literal(LiteralExpr(env = cctx, form = form, value = form, literalType = PrimNumericType)))
                  "Should return a numeric Literal"


          testCase "Parses an Int32"
          <| fun _ ->
              let form = 42
              let cctx = CompilerEnv.Create(Expression)
              let ast = Parser.Analyze(cctx, form)

              Expect.equal
                  ast
                  (AST.Literal(LiteralExpr(env = cctx, form = form, value = form, literalType = PrimNumericType)))
                  "Should return a numeric Literal"


          testCase "Parses other numeric primities as 'OtherType'"
          <| fun _ ->
              let form = 42u
              let cctx = CompilerEnv.Create(Expression)
              let ast = Parser.Analyze(cctx, form)

              Expect.equal
                  ast
                  (AST.Literal(LiteralExpr(env = cctx, form = form, value = form, literalType = OtherType)))
                  "Should return an 'OtherType' Literal"


          testCase "Parses a string"
          <| fun _ ->
              let form = ReadFromString "\"abc\""
              let cctx = CompilerEnv.Create(Expression)
              let ast = Parser.Analyze(cctx, form)

              Expect.equal
                  ast
                  (AST.Literal(LiteralExpr(env = cctx, form = form, value = form, literalType = StringType)))
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
                  (AST.Literal(LiteralExpr(env = cctx, form = form, value = form, literalType = OtherType)))
                  "Should return a Literal"

          testCase "Parses a constant set as a literal"
          <| fun _ ->
              let form = ReadFromString "#{1 2 3}"
              let cctx = CompilerEnv.Create(Expression)
              let ast = Parser.Analyze(cctx, form)

              Expect.equal
                  ast
                  (AST.Literal(LiteralExpr(env = cctx, form = form, value = form, literalType = OtherType)))
                  "Should return a Literal"


          testCase "Parses a constant map as a literal"
          <| fun _ ->
              let form = ReadFromString "#{:a 1 :b 2}"
              let cctx = CompilerEnv.Create(Expression)
              let ast = Parser.Analyze(cctx, form)

              Expect.equal
                  ast
                  (AST.Literal(LiteralExpr(env = cctx, form = form, value = form, literalType = OtherType)))
                  "Should return a Literal"

          // TODO: non-constant collections

          ]



let mutable namespaceCounter = 0

let nextNamespaceNum () =
    Interlocked.Increment(&namespaceCounter)

let abcSym = Symbol.intern ("abc")
let defSym = Symbol.intern ("def")
let pqrSym = Symbol.intern ("pqr")
let impSym = Symbol.intern ("importedType")
let macroSym = Symbol.intern ("macroV")
let constSym = Symbol.intern ("constV")
let privateSym = Symbol.intern ("private")
let ns2Sym = Symbol.intern ("ns2")

let createTestNameSpaces () =
    let ns1Name = $"ns1_{nextNamespaceNum ()}"
    let ns2Name = $"ns2_{nextNamespaceNum ()}"
    let ns1 = Namespace.findOrCreate (Symbol.intern (ns1Name))
    let ns2 = Namespace.findOrCreate (Symbol.intern (ns2Name))
    ns1.addAlias (ns2Sym, ns2)
    ns1.intern (abcSym) |> ignore
    ns1.intern (defSym) |> ignore
    ns1.reference (RTVar.InstanceVar.Name, RTVar.InstanceVar) |> ignore
    ns1.importClass (impSym, typeof<System.Text.StringBuilder>) |> ignore

    let mv = ns1.intern (macroSym)
    mv.setMacro ()

    let mc = ns1.intern (constSym)

    (mc :> IReference)
        .resetMeta ((mc :> IMeta).meta().assoc (Keyword.intern (null, "const"), true))
    |> ignore

    ns2.intern (pqrSym) |> ignore
    Var.internPrivate (ns2Name, "private") |> ignore
    ns1, ns2


[<Tests>]
let ResolveTests =
    testList
        "Resolve Tests"
        [ testCase "Test namespace test harness"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()

              let other = ns1.lookupAlias (ns2Sym)
              Expect.equal other ns2 "SHould find ns2 under alias"

              let abc = ns1.getMapping (abcSym)
              Expect.isNotNull abc "Should find abc in ns1"
              Expect.isTrue (abc :? Var) "should map to Var"

              let def = ns1.getMapping (defSym)
              Expect.isNotNull def "Should find def in ns1"
              Expect.isTrue (def :? Var) "should map to Var"

              let imp = ns1.getMapping (impSym)
              Expect.isNotNull imp "Should find importedType in ns1"
              Expect.isTrue (imp :? Type) "should map to a Type"

              let mac = ns1.getMapping (macroSym)
              Expect.isNotNull mac "Should find macroV in ns1"
              Expect.isTrue (mac :? Var) "should map to a Var)"
              Expect.isTrue ((mac :?> Var).isMacro) "should be a macro"

              let con = ns1.getMapping (constSym)
              Expect.isNotNull con "should find constV in ns1"
              Expect.isTrue (con :? Var) "should map to a Var"

              Expect.isTrue
                  (RT0.booleanCast ((con :?> IMeta).meta().valAt (Keyword.intern (null, "const"))))
                  "Should have :const true"

              let pqr = ns2.getMapping (pqrSym)
              Expect.isNotNull pqr "Should find pqr in ns2"
              Expect.isTrue (pqr :? Var) "Should map to a Var"

              let privateVar = ns2.getMapping (privateSym)
              Expect.isNotNull privateVar "Should find private var"
              Expect.isTrue (privateVar :? Var) "Should be a Var"
              Expect.isFalse ((privateVar :?> Var).isPublic) "Should not be public"

              let inst = ns1.getMapping (RTVar.InstanceVar.Name)
              Expect.isNotNull inst "Should find instance? in ns2"
              Expect.equal inst RTVar.InstanceVar "Should map to a Var(instance?)"

          testCase "Namespace alias, var not found => throws"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let sym = Symbol.intern (ns2Sym.Name, "fred1")
              let cctx = CompilerEnv.Create(Expression)

              Expect.throwsT<InvalidOperationException>
                  (fun _ -> Parser.ResolveIn(cctx, ns1, sym, true) |> ignore)
                  "Should throw with non-existent var in aliased ns"

          testCase "Namespace alias, private var found, not allow private => throws"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let sym = Symbol.intern (ns2Sym.Name, privateSym.Name)
              let cctx = CompilerEnv.Create(Expression)

              Expect.throwsT<InvalidOperationException>
                  (fun _ -> Parser.ResolveIn(cctx, ns1, sym, false) |> ignore)
                  "Should throw with private var in aliased ns, but private not allowed"

          testCase "Namespace alias, private var found, private allowed => return var"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let sym = Symbol.intern (ns2Sym.Name, privateSym.Name)
              let cctx = CompilerEnv.Create(Expression)
              let v = Parser.ResolveIn(cctx, ns1, sym, true)
              Expect.isNotNull v "Should find private var in aliased ns, and private allowed"
              Expect.equal (v :?> Var).Name privateSym "Should find private var in aliased ns, and private allowed"

          testCase "Namespace alias, public var found, private not allowed => return var"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let sym = Symbol.intern (ns2Sym.Name, pqrSym.Name)
              let cctx = CompilerEnv.Create(Expression)
              let v = Parser.ResolveIn(cctx, ns1, sym, false)
              Expect.isNotNull v "Should find private var in aliased ns, and private allowed"
              Expect.equal (v :?> Var).Name pqrSym "Should find private var in aliased ns, and private allowed"

          testCase "Looks like array type (Name/2), but Name not type => throws"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let sym = Symbol.intern ("Mustard", "2")
              let cctx = CompilerEnv.Create(Expression)

              Expect.throwsT<TypeNotFoundException>
                  (fun _ -> Parser.ResolveIn(cctx, ns1, sym, false) |> ignore)
                  "Should throw with looks like array type, but namespace is not a Type"

          testCase "Is array type (String/2) => return type"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let sym = Symbol.intern ("String", "2")
              let cctx = CompilerEnv.Create(Expression)
              let v = Parser.ResolveIn(cctx, ns1, sym, false)
              Expect.isNotNull v "Should find type"
              Expect.equal v typeof<String[][]> "Should find an array type"

          testCase "No namespace, name ends in ] => Look for array type"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let sym = Symbol.intern (null, "System.String[]")
              let cctx = CompilerEnv.Create(Expression)
              let v = Parser.ResolveIn(cctx, ns1, sym, false)
              Expect.isNotNull v "Should find type"
              Expect.equal v typeof<String[]> "Should find an array type"

          testCase "No namespace, name has . in it => Look for  type"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let sym = Symbol.intern (null, "System.Text.StringBuilder")
              let cctx = CompilerEnv.Create(Expression)
              let v = Parser.ResolveIn(cctx, ns1, sym, false)
              Expect.isNotNull v "Should find type"
              Expect.equal v typeof<System.Text.StringBuilder> "Should find the named type"

          testCase "No namespace, name has . in it  type missing => trhows"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let sym = Symbol.intern (null, "System.Wrong.StringBuilder")
              let cctx = CompilerEnv.Create(Expression)

              Expect.throwsT<TypeNotFoundException>
                  (fun _ -> Parser.ResolveIn(cctx, ns1, sym, false) |> ignore)
                  "Should throw with looks like array type, but namespace is not a Type"

          testCase "in-ns and ns are special cases"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)

              let v1 = Parser.ResolveIn(cctx, ns1, RTVar.NsSym, false)
              Expect.equal v1 RTVar.NsVar "Should find ns var"

              let v2 = Parser.ResolveIn(cctx, ns1, RTVar.InNsSym, false)
              Expect.equal v2 RTVar.InNSVar "Should find in-ns var"

          testCase "no namespace sym, not mapped, unresolved vars not allows => throws"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)

              Expect.throwsT<InvalidOperationException>
                  (fun _ -> Parser.ResolveIn(cctx, ns1, pqrSym, true) |> ignore)
                  "should throw on unmapped var with unresolved vars not allows"

          testCase "no namespace sym, not mapped, unresolved vars allowd => just return the sym"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)

              Var.pushThreadBindings (RTMap.map (RTVar.AllowUnresolvedVarsVar, true))

              try
                  let v1 = Parser.ResolveIn(cctx, ns1, pqrSym, true)
                  Expect.isNotNull v1 "Should find something"
                  Expect.equal v1 pqrSym "Should return the  symbol itself"
              finally
                  Var.popThreadBindings () |> ignore

          testCase "Return mapped symbol's value"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)

              let v1 = Parser.ResolveIn(cctx, ns1, abcSym, true)
              Expect.isNotNull v1 "Should find something"
              Expect.equal (v1.GetType()) typeof<Var> "Should return a var"

              let v2 = v1 :?> Var
              Expect.equal v2.Namespace ns1 "Namespace of var should be namespace we are looking in"
              Expect.equal v2.Name abcSym "Name of var should be the symbol we are looking for" ]

[<Tests>]
let LookupVarTests =
    testList
        "LookupVar Tests"
        [ testCase "Sym /w namespace, but doesn't map to any namespace => null"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)

              let sym = Symbol.intern ("ns3", "pqr")
              let v = Parser.LookupVar(cctx, sym, false)
              Expect.isNull v "Should not find var not in named namespace"

          testCase "Sym /w namespace, exists in that namespace => returns var"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)

              let sym = Symbol.intern (ns1.Name.Name, abcSym.Name)
              let v = Parser.LookupVar(cctx, sym, false)
              Expect.isNotNull v "Should find var not in named namespace"

          testCase "Sym /w namespace, does not exist in that namespace, internNew = false  => returns var"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)

              let sym = Symbol.intern (ns1.Name.Name, "fred2")
              let v = Parser.LookupVar(cctx, sym, false)
              Expect.isNull v "Should not find var not in named namespace"

          testCase
              "Sym /w namespace, does not exist in that namespace, namespace is current nameespace, internNew = true  => creates Var, returns var"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)

              let sym = Symbol.intern (ns1.Name.Name, "fred3")

              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try
                  let v = Parser.LookupVar(cctx, sym, true)
                  Expect.isNotNull v "Should find var not in named namespace"

                  Expect.isNotNull
                      (ns1.findInternedVar (Symbol.intern (null, "fred3")))
                      "Should have created var in current namespace"
              finally
                  Var.popThreadBindings () |> ignore

          testCase
              "Sym /w namespace, does not exist in that namespace, namespace is current nameespace, internNew = false  => null"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)

              let sym = Symbol.intern (ns1.Name.Name, "fred4")

              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try
                  let v = Parser.LookupVar(cctx, sym, false)
                  Expect.isNull v "Should not find var not in named namespace"
              finally
                  Var.popThreadBindings () |> ignore

          testCase "in-ns and ns are special cases"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)

              let v1 = Parser.LookupVar(cctx, RTVar.NsSym, false)
              Expect.equal v1 RTVar.NsVar "Should find ns var"

              let v2 = Parser.LookupVar(cctx, RTVar.InNsSym, false)
              Expect.equal v2 RTVar.InNSVar "Should find in-ns var"

          testCase "Sym w/o namespace, no mapping in current NS, internNew is false => null"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)

              let sym = Symbol.intern (null, "fred5")

              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try
                  let v = Parser.LookupVar(cctx, sym, false)
                  Expect.isNull v "Should not find var not in current namespace"
              finally
                  Var.popThreadBindings () |> ignore


          testCase "Sym w/o namespace, no mapping in current NS, internNew is true => should create Var and return it"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)

              let sym = Symbol.intern (null, "fred6")

              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try
                  let v = Parser.LookupVar(cctx, sym, true)
                  Expect.isNotNull v "Should find var not in current namespace"
                  Expect.isNotNull (ns1.findInternedVar (sym)) "Should have created var in current namespace"
              finally
                  Var.popThreadBindings () |> ignore


          testCase "Sym w/o namespace, maps to Var in current NS => returns Var"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)

              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try
                  let v = Parser.LookupVar(cctx, abcSym, true)
                  Expect.isNotNull v "Should find var not in current namespace"
              finally
                  Var.popThreadBindings () |> ignore

          testCase "Sym w/o namespace, maps to non-Var in current NS => throws"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)

              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try
                  Expect.throwsT<InvalidOperationException>
                      (fun _ -> Parser.LookupVar(cctx, impSym, true) |> ignore)
                      "Should throw with non-var in current ns"

              finally
                  Var.popThreadBindings () |> ignore

          testCase "Var found, non-macro  => registers it"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)
              let register = ObjXRegister(None)
              let cctx = { cctx with ObjXRegister = Some register }

              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try
                  let v = Parser.LookupVar(cctx, abcSym, true)
                  Expect.isTrue (register.Vars.containsKey (v)) "Should register var"

              finally
                  Var.popThreadBindings () |> ignore

          testCase "Var found, is macro, registerMacro = false  => not registered"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)
              let register = ObjXRegister(None)
              let cctx = { cctx with ObjXRegister = Some register }

              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try
                  let v = Parser.LookupVar(cctx, macroSym, true, false)
                  Expect.isFalse (register.Vars.containsKey (v)) "Should not register var"

              finally
                  Var.popThreadBindings () |> ignore

          testCase "Var found, is macro, registerMacro = true  => registers it"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)
              let register = ObjXRegister(None)
              let cctx = { cctx with ObjXRegister = Some register }

              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try
                  let v = Parser.LookupVar(cctx, macroSym, true, true)
                  Expect.isTrue (register.Vars.containsKey (v)) "Should register var"

              finally
                  Var.popThreadBindings () |> ignore



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
                  (AST.InteropCall(InteropCallExpr(
                      env = cctx,
                      form = form,
                      hostExprType = FieldOrPropertyExpr,
                      isStatic = true,
                      tag = null,
                      target = None,
                      targetType = typeof<Int64>,
                      memberName = "MaxValue",
                      tInfo = (typeof<Int64>.GetField ("MaxValue")),
                      args = ResizeArray<HostArg>(),
                      typeArgs = ResizeArray<Type>(),
                      sourceInfo = None
                  )))
              )

          testCase "Parses TypeName/FieldName with Fieldname not found as static QualifiedMethod"
          <| fun _ ->
              let form = ReadFromString "Int64/asdf"
              let cctx = CompilerEnv.Create(Expression)
              let ast = Parser.Analyze(cctx, form)

              Expect.equal
                  ast
                  (AST.QualifiedMethod(
                      Env = cctx,
                      Form = form,
                      MethodType = typeof<Int64>,
                      HintedSig = None,
                      MethodSymbol = Symbol.intern ("Int64/asdf"),
                      MethodName = "asdf",
                      Kind = Static,
                      TagClass = typeof<AFn>,
                      sourceInfo = None
                  ))
                  "Should find static QM"

          testCase "Parses TypeName/.FieldName with Fieldname not found as instance QualifiedMethod"
          <| fun _ ->
              let form = ReadFromString "Int64/.asdf"
              let cctx = CompilerEnv.Create(Expression)
              let ast = Parser.Analyze(cctx, form)

              Expect.equal
                  ast
                  (AST.QualifiedMethod(
                      Env = cctx,
                      Form = form,
                      MethodType = typeof<Int64>,
                      HintedSig = None,
                      MethodSymbol = Symbol.intern ("Int64/.asdf"),
                      MethodName = "asdf",
                      Kind = Instance,
                      TagClass = typeof<AFn>,
                      sourceInfo = None
                  ))
                  "Should find instance QM"

          testCase
              "Throws on ^NotAType TypeName/FieldName with Fieldname not found as static QualifiedMethod, with tag used for TagClass"
          <| fun _ ->
              let form = ReadFromString "^NotAType Int64/asdf"
              let cctx = CompilerEnv.Create(Expression)

              Expect.throwsT<CompilerException>
                  (fun _ -> Parser.Analyze(cctx, form) |> ignore)
                  "Should throw with non-type as :tag meta"


          testCase "Parses ^String TypeName/FieldName with Fieldname not found as static QualifiedMethod, with TagClass"
          <| fun _ ->
              let form = ReadFromString "^String Int64/asdf"
              let cctx = CompilerEnv.Create(Expression)
              let ast = Parser.Analyze(cctx, form)

              Expect.equal
                  ast
                  (AST.QualifiedMethod(
                      Env = cctx,
                      Form = form,
                      MethodType = typeof<Int64>,
                      HintedSig = None,
                      MethodSymbol = Symbol.intern ("Int64/asdf"),
                      MethodName = "asdf",
                      Kind = Static,
                      TagClass = typeof<String>,
                      sourceInfo = None
                  ))
                  "Should find static QM, with TagClass"

          testCase
              "Parses ^[String int] TypeName/FieldName with Fieldname not found as static QualifiedMethod, with HintedSig"
          <| fun _ ->
              let form = ReadFromString "^[String int] Int64/asdf"
              let cctx = CompilerEnv.Create(Expression)
              let ast = Parser.Analyze(cctx, form)

              compareQualifiedMethods (
                  ast,
                  (AST.QualifiedMethod(
                      Env = cctx,
                      Form = form,
                      MethodType = typeof<Int64>,
                      HintedSig =
                          (SignatureHint.MaybeCreate(cctx, PersistentVector.create (typeof<String>, typeof<Int32>))),
                      MethodSymbol = Symbol.intern ("Int64/asdf"),
                      MethodName = "asdf",
                      Kind = Static,
                      TagClass = typeof<AFn>,
                      sourceInfo = None
                  ))
              )

          testCase "Detects local bindings."
          <| fun _ ->

              let cctx = CompilerEnv.Create(Expression)

              let register = ObjXRegister(None)
              let internals = ObjXInternals()

              let objx =
                  AST.Obj(
                      Env = cctx,
                      Form = null,
                      Type = ObjXType.Fn,
                      Internals = internals,
                      Register = register,
                      sourceInfo = None
                  )

              let method = ObjMethod(ObjXType.Fn, objx, internals, register, None)

              let cctx =
                  { cctx with
                      Method = Some method
                      ObjXRegister = Some register }


              let cctx, lbThis = cctx.RegisterLocalThis(Symbol.intern ("this"), null, None)

              let cctx, lbAsdf =
                  cctx.RegisterLocal(Symbol.intern ("asdf"), null, None, null, false)

              let form0 = ReadFromString "this"
              let form1 = ReadFromString "asdf"

              let ast0 = Parser.Analyze(cctx, form0)

              Expect.equal
                  ast0
                  (AST.LocalBinding(LocalBindingExpr(env = cctx, form = form0, binding = lbThis, tag = null)))
                  "Should find binding for this"

              let ast1 = Parser.Analyze(cctx, form1)

              Expect.equal
                  ast1
                  (AST.LocalBinding(LocalBindingExpr(Env = cctx, Form = form1, Binding = lbAsdf, Tag = null)))
                  "Should find binding for asdf"

              let closes = register.Closes
              Expect.equal (closes.count ()) 0 "Should no closeovers"


          testCase "Detects local bindings, does close over if not in method locals."
          <| fun _ ->

              let cctx = CompilerEnv.Create(Expression)

              // Let's add a local binding to the environment, but before we add the method, so this binding will be non-local to the methld.
              let locals = cctx.Locals
              let pqrsSym = Symbol.intern ("pqrs")

              let lbPqrs =
                  { Sym = pqrsSym
                    Tag = null
                    Init = None
                    Name = pqrsSym.Name
                    IsArg = false
                    IsByRef = false
                    IsRecur = false
                    IsThis = false
                    Index = 20 }

              let newLocals = RTMap.assoc (locals, pqrsSym, lbPqrs) :?> IPersistentMap
              let cctx = { cctx with Locals = newLocals }
              
              // this test was designed before we had debugged FnExprParser, so we faked the environment.

              let register = ObjXRegister(None)
              let internals = ObjXInternals()

              let objx =
                  AST.Obj(
                      Env = cctx,
                      Form = null,
                      Type = ObjXType.Fn,
                      Internals = internals,
                      Register = register,
                      sourceInfo = None
                  )

              let method = ObjMethod(ObjXType.Fn, objx, internals, register, None)

              let cctx =
                  { cctx with
                      Method = Some method
                      ObjXRegister = Some register }

              let cctx, lbThis = cctx.RegisterLocalThis(Symbol.intern ("this"), null, None)

              let cctx, lbAsdf =
                  cctx.RegisterLocal(Symbol.intern ("asdf"), null, None, null, false)

              let form0 = ReadFromString "this"
              let form1 = ReadFromString "asdf"

              let ast0 = Parser.Analyze(cctx, form0)

              Expect.equal
                  ast0
                  (AST.LocalBinding(LocalBindingExpr(Env = cctx, Form = form0, Binding = lbThis, Tag = null)))
                  "Should find binding for this"

              let ast1 = Parser.Analyze(cctx, form1)

              Expect.equal
                  ast1
                  (AST.LocalBinding(LocalBindingExpr(Env = cctx, Form = form1, Binding = lbAsdf, Tag = null)))
                  "Should find binding for asdf"

              let ast2 = Parser.Analyze(cctx, pqrsSym)

              Expect.equal
                  ast2
                  (AST.LocalBinding(LocalBindingExpr(Env = cctx, Form = pqrsSym, Binding = lbPqrs, Tag = null)))
                  "Should find binding for pqrs"

              let closes = register.Closes
              Expect.equal (closes.count ()) 1 "Should be one closeover"
              Expect.isTrue (closes.containsKey (lbPqrs)) "Should contain pqrs closeover"
              Expect.equal (closes.valAt (lbPqrs)) lbPqrs "Should contain pqrs closeover"

          testCase "non-local, non-Type, resolves to Var, is macro => throws"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)


              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try

                  Expect.throwsT<CompilerException>
                      (fun _ -> Parser.Analyze(cctx, macroSym) |> ignore)
                      "Should throw with non-local, non-Type, resolves to Var, is macro"

              finally
                  Var.popThreadBindings () |> ignore

          testCase "non-local, non-Type, resolves to Var, is const =>  Analyzes 'V, returns Literal"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)


              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try

                  let constVar = ns1.findInternedVar (constSym)
                  let ast = Parser.Analyze(cctx, constSym)

                  Expect.equal
                      ast
                      (AST.Literal(LiteralExpr(env = cctx, form = constVar, value = constVar, literalType = OtherType)))
                      "Should return a Literal"

              finally
                  Var.popThreadBindings () |> ignore


          testCase "non-local, non-Type, resolves to Var, =>  returns AST.Var"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)


              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try

                  let abcVar = ns1.findInternedVar (abcSym)
                  let ast = Parser.Analyze(cctx, abcSym)

                  Expect.equal
                      ast
                      (AST.Var(Env = cctx, Form = abcSym, Var = abcVar, Tag = null))
                      "Should return an AST.Var"

              finally
                  Var.popThreadBindings () |> ignore


          testCase "non-local, non-Type, resolves to symbol (allow-unresolved = true) =>  AST.UnresolvedVar"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)


              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1, RTVar.AllowUnresolvedVarsVar, true))

              try

                  let sym = Symbol.intern (null, "fred7")
                  let ast = Parser.Analyze(cctx, sym)

                  Expect.equal
                      ast
                      (AST.UnresolvedVar(Env = cctx, Form = sym, Sym = sym))
                      "Should return an UnresolvedVar"

              finally
                  Var.popThreadBindings () |> ignore



          testCase "non-local, non-Type, resolves to symbol (allow-unresolved = false) =>  throws"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)


              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1, RTVar.AllowUnresolvedVarsVar, false))

              try

                  let sym = Symbol.intern (null, "fred8")

                  Expect.throwsT<CompilerException>
                      (fun _ -> Parser.Analyze(cctx, sym) |> ignore)
                      "Should throw with non-local, non-Type, resolves to symbol (allow-unresolved = false)"

              finally
                  Var.popThreadBindings () |> ignore

          ]


[<Tests>]
let BasicInvokeTests =
    testList
        "Basic Invoke Tests"
        [ testCase "(nil x y z) throws"
          <| fun _ ->
              let form = ReadFromString "(nil 7 8)"
              let cctx = CompilerEnv.Create(Expression)

              Expect.throwsT<CompilerException>
                  (fun _ -> Parser.Analyze(cctx, form) |> ignore)
                  "Should throw with nil as fexpr"

          testCase "(:keyword x)  (Just one arg + Register exists) => AST.KeywordInvoke, callsite registered"
          <| fun _ ->
              let kw = Keyword.intern (null, "kw")
              let form = ReadFromString "(:kw 7)"
              let cctx = CompilerEnv.Create(Expression)
              let register = ObjXRegister(None)
              let cctx = { cctx with ObjXRegister = Some register }

              let ast = Parser.Analyze(cctx, form)

              Expect.equal
                  ast
                  (AST.KeywordInvoke(KeywordInvokeExpr(
                      env = cctx,
                      form = form,
                      kwExpr = (AST.Literal(LiteralExpr(env = cctx, form = kw, value = kw, literalType = KeywordType)),
                      target = (AST.Literal(LiteralExpr(env=cctx, form=7L, value=7L, literalType=PrimNumericType))),
                      tag = null,
                      siteIndex = 0,
                      sourceInfo = None
                  )))
                  "Should return a KeywordInvoke"
          // Had to debug what was wrong with my test.  For reference.  (I had the wrong value for SiteIndex)
          //match ast with
          //| AST.KeywordInvoke(Env = kiEnv; Form = kiForm; KwExpr = kwExpr; Target = target; Tag = tag; SiteIndex = index; sourceInfo = si) as ki ->

          //      Expect.equal kiEnv cctx "Should have the expected env"
          //      Expect.equal kiForm form "Should have the expected form"
          //      Expect.isTrue (kwAST.IsLiteral) "KwExpr should be a Literal"
          //      Expect.equal kwExpr  (AST.Literal(Env = cctx, Form = kw, Value = kw, Type = KeywordType) ) "KwExpr should be a Literal"

          //      Expect.isTrue (target.IsLiteral) "Target should be a Literal"
          //      Expect.equal target  (AST.Literal(Env = cctx, Form = 7L, Value = 7L, Type = PrimNumericType)) "Target should be a Literal"

          //      Expect.isNull tag "Tag should be null"
          //      Expect.equal index 0 "SiteIndex should be 0"
          //      Expect.isNone si "SourceInfo should be None"
          //| _ -> failtest "Should be a KeywordInvoke"

          testCase "(:keyword x)  (Wrong number of args + Register exists) => Should not be KeywordInvoke"
          <| fun _ ->
              let kw = Keyword.intern (null, "kw")
              let form = ReadFromString "(:kw 7 8)"
              let cctx = CompilerEnv.Create(Expression)
              let register = ObjXRegister(None)
              let cctx = { cctx with ObjXRegister = Some register }

              let ast = Parser.Analyze(cctx, form)

              match ast with
              | AST.Invoke(invokeExpr) as invoke ->

                  Expect.equal invokeExpr.Env cctx "Should have the expected env"
                  Expect.equal invokeExpr.Form form "Should have the expected form"

                  Expect.equal
                      invokeExpr.Fexpr
                      (AST.Literal(LiteralExpr(env = cctx, form = kw, value = kw, literalType = KeywordType)))
                      "Fexpr should be a keyword Literal"

                  let expectedArgs = ResizeArray<AST>()
                  expectedArgs.Add(AST.Literal(LiteralExpr(env=cctx, form=7L, value=7L, literalType=PrimNumericType)))
                  expectedArgs.Add(AST.Literal(LiteralExpr(env=cctx, form=8L, value=8L, literalType=PrimNumericType)))
                  compareGenericLists (invokeExpr.Args, expectedArgs)

                  Expect.isNull invokeExpr.Tag "Tag should be null"
                  Expect.isNone invokeExpr.SourceInfo "SourceInfo should be None"
              | _ -> failtest "Should be an Invoke"


          testCase "(:keyword x)  (Just one arg + Register does not exist) => Should not be KeywordInvoke"
          <| fun _ ->
              let kw = Keyword.intern (null, "kw")
              let form = ReadFromString "(:kw 7)"
              let cctx = CompilerEnv.Create(Expression)

              let ast = Parser.Analyze(cctx, form)

              match ast with
              | AST.Invoke(invokeExpr) as invoke ->

                  Expect.equal invokeExpr.Env cctx "Should have the expected env"
                  Expect.equal invokeExpr.Form form "Should have the expected form"

                  Expect.equal
                      invokeExpr.Fexpr
                      (AST.Literal(Env = cctx, Form = kw, Value = kw, Type = KeywordType))
                      "Fexpr should be a keyword Literal"

                  let expectedArgs = ResizeArray<AST>()
                  expectedArgs.Add(AST.Literal(LiteralExpr(env=cctx, form=7L, value=7L, literalType=PrimNumericType)))
                  compareGenericLists (invokeExpr.Args, expectedArgs)

                  Expect.isNull invokeExpr.Tag "Tag should be null"
                  Expect.isNone invokeExpr.SourceInfo "SourceInfo should be None"
              | _ -> failtest "Should be an Invoke"


          testCase "(staticFieldOrProperty)   =>  same as staticFieldOrProperty -- almost deprecated, not recommended"
          <| fun _ ->
              let form = ReadFromString "(System.Int64/MaxValue)"
              let cctx = CompilerEnv.Create(Expression)
              let fexpr = ReadFromString "System.Int64/MaxValue"

              let ast = Parser.Analyze(cctx, form)
              let astFexpr = Parser.Analyze(cctx, fexpr)

              //Expect.equal ast astFexpr "Should be the same as fexpr"

              match ast, astFexpr with
              | AST.InteropCall(iExpr),
                AST.InteropCall(fExpr) ->
                  Expect.equal iExpr.Env cctx "Should have the expected env"
                  // Note: forms will not be equal
                  Expect.equal iExpr.HostExprType fExpr.HostExprType "Should be Int64"
                  Expect.equal iExpr.IsStatic fExpr.IsStatic "Should be static"
                  Expect.equal iExpr.Tag fExpr.Tag "Tag should be null"
                  Expect.equal iExpr.SourceInfo fExpr.SourceInfo "SourceInfo should be None"
                  Expect.equal iExpr.Target fExpr.Target "Target should be None"
                  Expect.equal iExpr.TargetType fExpr.TargetType "TargetType should be None"
                  Expect.equal iExpr.MemberName fExpr.MemberName "MemberName should be MaxValue"
                  Expect.equal iExpr.TInfo fExpr.TInfo "TInfo should be the field info for MaxValue"
                  compareGenericLists (iExpr.Args, fExpr.Args) 
                  compareGenericLists (iExpr.TypeArgs, fExpr.TypeArgs) 
              | _ -> failtest "Should be an InteropCall"


          testCase "(abc 7), abc bound to Var in namespace, not special   => basic invoke expr"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)
              let register = ObjXRegister(None)
              let cctx = { cctx with ObjXRegister = Some register }

              let form = ReadFromString "(abc 7)"
              let abcVar = ns1.findInternedVar (abcSym)

              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try
                  let ast = Parser.Analyze(cctx, form)

                  match ast with
                  | AST.Invoke(invokeExpr) as invoke ->

                      Expect.equal invokeExpr.Env cctx "Should have the expected env"
                      Expect.equal invokeExpr.Form form "Should have the expected form"

                      Expect.equal
                          invokeExpr.Fexpr
                          (AST.Var(Env = cctx, Form = abcSym, Var = abcVar, Tag = null))
                          "Fexpr should be an AST.Var"

                      let expectedArgs = ResizeArray<AST>()
                      expectedArgs.Add(AST.Literal(LiteralExpr(env=cctx, form=7L, value=7L, literalType=PrimNumericType)))
                      compareGenericLists (invokeExpr.Args, expectedArgs)

                      Expect.isNull invokeExpr.Tag "Tag should be null"
                      Expect.isNone invokeExpr.SourceInfo "SourceInfo should be None"
                  | _ -> failtest "Should be an Invoke"

              finally
                  Var.popThreadBindings () |> ignore


          testCase "(fred9 7), fred not bound to Var in namespace, not special   => trhows"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)
              let register = ObjXRegister(None)
              let cctx = { cctx with ObjXRegister = Some register }

              let form = ReadFromString "(fred9 7)"

              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try
                  Expect.throwsT<CompilerException> (fun _ -> Parser.Analyze(cctx, form) |> ignore) "should throw"

              finally
                  Var.popThreadBindings () |> ignore

          testCase "(instance? System.Int64 7), instance? mapped to Var in naemsspace   =>  AST.InstanceOf"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)
              let register = ObjXRegister(None)
              let cctx = { cctx with ObjXRegister = Some register }

              let form = ReadFromString "(instance? System.Int64 7)"

              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try
                  let ast = Parser.Analyze(cctx, form)

                  Expect.equal
                      ast
                      (AST.InstanceOf(InstanceOfExpr(
                          env = cctx,
                          form = form,
                          t = typeof<int64>,
                          expr = AST.Literal(LiteralExpr(env = cctx, form = 7L, value = 7L, literalType = PrimNumericType)),
                          sourceInfo = None
                      )))
                      "Should be an InstanceOf"
              finally
                  Var.popThreadBindings () |> ignore

          testCase
              "(instance? 8 7), instance? mapped to Var in naemsspace (first arg not a Type Literal)  =>  regular invoke"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)
              let register = ObjXRegister(None)
              let cctx = { cctx with ObjXRegister = Some register }

              let form = ReadFromString "(instance? 8 7)"

              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try
                  let ast = Parser.Analyze(cctx, form)

                  match ast with
                  | AST.Invoke(invokeExpr) as invoke ->

                      Expect.equal invokeExpr.Env cctx "Should have the expected env"
                      Expect.equal invokeExpr.Form form "Should have the expected form"

                      Expect.equal
                          invokeExpr.Fexpr
                          (AST.Var(Env = cctx, Form = RTVar.InstanceVar.Name, Var = RTVar.InstanceVar, Tag = null))
                          "Fexpr should be an AST.Var"

                      let expectedArgs = ResizeArray<AST>()
                      expectedArgs.Add(AST.Literal(LiteralExpr(env=cctx, form=8L, value=8L, literalType=PrimNumericType)))
                      expectedArgs.Add(AST.Literal(LiteralExpr(env=cctx, form=7L, value=7L, literalType=PrimNumericType)))
                      compareGenericLists (invokeExpr.Args, expectedArgs)

                      Expect.isNull invokeExpr.Tag "Tag should be null"
                      Expect.isNone invokeExpr.SourceInfo "SourceInfo should be None"
                  | _ -> failtest "Should be an Invoke"
              finally
                  Var.popThreadBindings () |> ignore

          testCase
              "(instance? System.Int64 7 8), instance? mapped to Var in naemsspace (first arg not a Type Literal)  =>  regular invoke"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)
              let register = ObjXRegister(None)
              let cctx = { cctx with ObjXRegister = Some register }

              let form = ReadFromString "(instance? System.Int64 8 7)"

              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try
                  let ast = Parser.Analyze(cctx, form)

                  match ast with
                  | AST.Invoke(invokeExpr) as invoke ->

                      Expect.equal invokeExpr.Env cctx "Should have the expected env"
                      Expect.equal invokeExpr.Form form "Should have the expected form"

                      Expect.equal
                          invokeExpr.Fexpr
                          (AST.Var(Env = cctx, Form = RTVar.InstanceVar.Name, Var = RTVar.InstanceVar, Tag = null))
                          "Fexpr should be an AST.Var"

                      let expectedArgs = ResizeArray<AST>()

                      expectedArgs.Add(
                          AST.Literal(
                              Env = cctx,
                              Form = Symbol.intern ("System.Int64"),
                              Value = typeof<int64>,
                              Type = OtherType
                          )
                      )

                      expectedArgs.Add(AST.Literal(LiteralExpr(env = cctx, form = 8L, value = 8L, literalType = PrimNumericType)))
                      expectedArgs.Add(AST.Literal(LiteralExpr(env = cctx, form = 7L, value = 7L, literalType = PrimNumericType)))
                      compareGenericLists (invokeExpr.Args, expectedArgs)

                      Expect.isNull invokeExpr.Tag "Tag should be null"
                      Expect.isNone invokeExpr.SourceInfo "SourceInfo should be None"
                  | _ -> failtest "Should be an Invoke"
              finally
                  Var.popThreadBindings () |> ignore

          // TODO: Tests for direct linking to Vars

          ]

// Class for testing QM resolution

type QMTest(_x: int64) =

    static member SF = 12 // static field
    member _.IF = 12 // instance field

    static member US0() = 12 // unique zero-arity static
    static member US1(x: int64) = 12 // unique single-arity static
    static member OS1(x: int64) = 12 // overloaded single-arity static
    static member OS1(x: string) = 12 // overloaded single-arity static

    member _.UI0() = 12 // unique zero-arity instance
    member _.UI1(x: int64) = 12 // unique single-arity instance
    member _.OI1(x: int64) = 12 // overloaded single-arity instance
    member _.OI1(x: string) = 12 // overloaded single-arity instance


[<Tests>]
let BasicQMTests =
    testList
        "Basic QM Tests"
        [ testCase "(|ParserTests+QMTest|/US0)  => zero-arity static interop call"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)
              let register = ObjXRegister(None)
              let cctx = { cctx with ObjXRegister = Some register }

              let form = ReadFromString "(|ParserTests+QMTest|/US0)"
              let ast = Parser.Analyze(cctx, form)

              compareInteropCalls (
                  ast,
                  (AST.InteropCall(InteropCall(
                      Env = cctx,
                      Form = (RTSeq.first (form)),
                      Type = MethodExpr,
                      IsStatic = true,
                      Tag = null,
                      Target = None,
                      TargetType = typeof<QMTest>,
                      MemberName = "US0",
                      TInfo = (typeof<QMTest>.GetMethod ("US0")),
                      Args = ResizeArray<HostArg>(),
                      TypeArgs = (ResizeArray<Type>()),
                      sourceInfo = None
                  )))
              )

          testCase "(|ParserTests+QMTest|/.UI0 7)  => zero-arity instance interop call"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)
              let register = ObjXRegister(None)
              let cctx = { cctx with ObjXRegister = Some register }

              let form = ReadFromString "(|ParserTests+QMTest|/.UI0 7)"
              let ast = Parser.Analyze(cctx, form)

              compareInteropCalls (
                  ast,
                  (AST.InteropCall(InteropCallExpr(
                      env = cctx,
                      form = (RTSeq.first (form)),
                      hostExprType = InstanceZeroArityCallExpr,
                      isStatic = false,
                      tag = null,
                      target = (Some <| AST.Literal(LiteralExpr(env=cctx, form=7L, value=7L, literalType=PrimNumericType))),
                      targetType = typeof<QMTest>,
                      memberName = "UI0",
                      tInfo = (typeof<QMTest>.GetMethod ("UI0")),
                      args = ResizeArray<HostArg>(),
                      typeArgs = (ResizeArray<Type>()),
                      sourceInfo = None
                  )))
              )

          testCase "(|ParserTests+QMTest|/Nope)  => zero-arity interop call throws (missing method)"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)
              let register = ObjXRegister(None)
              let cctx = { cctx with ObjXRegister = Some register }

              let form = ReadFromString "(|ParserTests+QMTest|/Nope)"

              Expect.throwsT<CompilerException>
                  (fun _ -> Parser.Analyze(cctx, form) |> ignore)
                  "Should throw with missing method"


          testCase "(|ParserTests+QMTest|/US1 7)  => positive-arity static interop call, method not found"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)
              let register = ObjXRegister(None)
              let cctx = { cctx with ObjXRegister = Some register }

              let form = ReadFromString "(|ParserTests+QMTest|/US1 7)"
              let ast = Parser.Analyze(cctx, form)

              let args = ResizeArray<HostArg>()

              args.Add(
                  { HostArg.ParamType = Standard
                    ArgExpr = AST.Literal(LiteralExpr(env = cctx, form = 7L, value = 7L, literalType = PrimNumericType))
                    LocalBinding = None }
              )

              compareInteropCalls (
                  ast,
                  (AST.InteropCall(InteropCallExpr(
                      env = cctx,
                      form = (RTSeq.first (form)),
                      hostExprType = MethodExpr,
                      isStatic = true,
                      tag = null,
                      target = None,
                      targetType = typeof<QMTest>,
                      memberName = "US1",
                      tInfo = null,
                      args = args,
                      typeArgs = (ResizeArray<Type>()),
                      sourceInfo = None
                  )))
              )


          testCase
              "(^[long] |ParserTests+QMTest|/US1 7)  => positive-arity static interop call, with params-tag, method found"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)
              let register = ObjXRegister(None)
              let cctx = { cctx with ObjXRegister = Some register }

              let form = ReadFromString "(^[long] |ParserTests+QMTest|/US1 7)"
              let ast = Parser.Analyze(cctx, form)

              let args = ResizeArray<HostArg>()

              args.Add(
                  { HostArg.ParamType = Standard
                    ArgExpr = AST.Literal(LiteralExpr(env = cctx, form = 7L, value = 7L, literalType = PrimNumericType))
                    LocalBinding = None }
              )

              compareInteropCalls (
                  ast,
                  (AST.InteropCall(InteropCallExpr(
                      env = cctx,
                      form = (RTSeq.first (form)),
                      hostExprType = MethodExpr,
                      isStatic = true,
                      tag = null,
                      target = None,
                      targetType = typeof<QMTest>,
                      memberName = "US1",
                      tInfo = (typeof<QMTest>.GetMethod ("US1")),
                      args = args,
                      typeArgs = (ResizeArray<Type>()),
                      sourceInfo = None
                  )))
              )


          testCase
              "(^[long] |ParserTests+QMTest|/.UI1 7)  => positive-arity instance interop call, with params-tag, method found"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)
              let register = ObjXRegister(None)
              let cctx = { cctx with ObjXRegister = Some register }

              let form = ReadFromString "(^[long] |ParserTests+QMTest|/.UI1 7 7)"
              let ast = Parser.Analyze(cctx, form)

              let args = ResizeArray<HostArg>()

              args.Add(
                  { HostArg.ParamType = Standard
                    ArgExpr = AST.Literal(LiteralExpr(env = cctx, form = 7L, value = 7L, literalType = PrimNumericType))
                    LocalBinding = None }
              )

              compareInteropCalls (
                  ast,
                  (AST.InteropCall(InteropCallExpr(
                      env = cctx,
                      form = (RTSeq.first (form)),
                      hostExprType = MethodExpr,
                      isStatic = false,
                      tag = null,
                      target = (Some <| AST.Literal(LiteralExpr(env = cctx, form = 7L, value = 7L, literalType = PrimNumericType))),
                      targetType = typeof<QMTest>,
                      memberName = "UI1",
                      tInfo = (typeof<QMTest>.GetMethod ("UI1")),
                      args = args,
                      typeArgs = (ResizeArray<Type>()),
                      sourceInfo = None
                  )))
              )

          testCase
              "(^[long] |ParserTests+QMTest|/OS1 7)  => positive-arity static interop call, with params-tag, overloaded method found"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)
              let register = ObjXRegister(None)
              let cctx = { cctx with ObjXRegister = Some register }

              let form = ReadFromString "(^[long] |ParserTests+QMTest|/OS1 7)"
              let ast = Parser.Analyze(cctx, form)

              let args = ResizeArray<HostArg>()

              args.Add(
                  { HostArg.ParamType = Standard
                    ArgExpr = AST.Literal(LiteralExpr(env = cctx, form = 7L, value = 7L, literalType = PrimNumericType))
                    LocalBinding = None }
              )

              let method = (typeof<QMTest>.GetMethod ("OS1", [| typeof<int64> |]))

              compareInteropCalls (
                  ast,
                  (AST.InteropCall(InteropCallExpr(
                      env = cctx,
                      form = (RTSeq.first (form)),
                      hostExprType = MethodExpr,
                      isStatic = true,
                      tag = null,
                      target = None,
                      targetType = typeof<QMTest>,
                      memberName = "OS1",
                      tInfo = method,
                      args = args,
                      typeArgs = (ResizeArray<Type>()),
                      sourceInfo = None
                  )))
              )

          testCase "(^[long] |ParserTests+QMTest|/new 7)  => constructor call (AST.New)"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)
              let register = ObjXRegister(None)
              let cctx = { cctx with ObjXRegister = Some register }

              let form = ReadFromString "(^[long] |ParserTests+QMTest|/new 7)"
              let ast = Parser.Analyze(cctx, form)

              let args = ResizeArray<HostArg>()

              args.Add(
                  { HostArg.ParamType = Standard
                    ArgExpr = AST.Literal(LiteralExpr(env = cctx, form = 7L, value = 7L, literalType = PrimNumericType))
                    LocalBinding = None }
              )

              let method = (typeof<QMTest>.GetMethod ("OS1", [| typeof<int64> |]))

              compareNewExprs (
                  ast,
                  (AST.New(
                      env = cctx,
                      form = (RTSeq.first (form)),
                      Type = typeof<QMTest>,
                      Constructor = typeof<QMTest>.GetConstructor ([| typeof<int64> |]),
                      args = args,
                      IsNoArgValueTypeCtor = false,
                      sourceInfo = None
                  ))
              ) ]


[<Tests>]
let SimpleSpecialOpTests =
    testList
        "Simple Special Op Tests"
        [ testCase "(monitor-enter x)  => Untyped:MonitorEnter"
          <| fun _ ->

              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)
              let register = ObjXRegister(None)
              let cctx = { cctx with ObjXRegister = Some register }
              let cctx = withLocals (cctx, [| "x"; "y" |])
              let cctx, method, register, internals = withMethod (cctx)

              let form = ReadFromString "(monitor-enter x)"
              let ast = Parser.Analyze(cctx, form)

              let target =
                  AST.LocalBinding(LocalBindingExpr(
                      env = cctx,
                      form = Symbol.intern ("x"),
                      Binding = (cctx.Locals.valAt (Symbol.intern ("x")) :?> LocalBinding),
                      tag = null
                  ))

              Expect.equal
                  ast
                  (AST.Untyped(env = cctx, form = form, Type = MonitorEnter, target = Some target))
                  "Should return a Untyped"

          testCase "(monitor-exit x)  => Untyped:MonitorExit"
          <| fun _ ->

              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)
              let register = ObjXRegister(None)
              let cctx = { cctx with ObjXRegister = Some register }
              let cctx = withLocals (cctx, [| "x"; "y" |])
              let cctx, method, register, internals = withMethod (cctx)

              let form = ReadFromString "(monitor-exit x)"
              let ast = Parser.Analyze(cctx, form)

              let target =
                  AST.LocalBinding(LocalBindingExpr(
                      env = cctx,
                      form = Symbol.intern ("x"),
                      Binding = (cctx.Locals.valAt (Symbol.intern ("x")) :?> LocalBinding),
                      tag = null
                  ))

              Expect.equal
                  ast
                  (AST.Untyped(env = cctx, form = form, Type = MonitorExit, target = Some target))
                  "Should return a Untyped"


          testCase "(throw x)  => Untyped:Throw"
          <| fun _ ->

              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)
              let register = ObjXRegister(None)
              let cctx = { cctx with ObjXRegister = Some register }
              let cctx = withLocals (cctx, [| "x"; "y" |])
              let cctx, method, register, internals = withMethod (cctx)

              let form = ReadFromString "(throw x)"
              let ast = Parser.Analyze(cctx, form)

              let target =
                  AST.LocalBinding(LocalBindingExpr(
                      env = cctx,
                      form = Symbol.intern ("x"),
                      Binding = (cctx.Locals.valAt (Symbol.intern ("x")) :?> LocalBinding),
                      tag = null
                  ))

              Expect.equal
                  ast
                  (AST.Untyped(env = cctx, form = form, Type = Throw, target = Some target))
                  "Should return a Untyped"


          testCase "(throw x y)  => error (wrong number of arguments)"
          <| fun _ ->

              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)
              let register = ObjXRegister(None)
              let cctx = { cctx with ObjXRegister = Some register }
              let cctx = withLocals (cctx, [| "x"; "y" |])
              let cctx, method, register, internals = withMethod (cctx)

              let form = ReadFromString "(throw x y)"

              Expect.throwsT<CompilerException>
                  (fun _ -> Parser.Analyze(cctx, form) |> ignore)
                  "Should throw with wrong number of arguments"

          testCase "(if x 7 8)  => AST.If"
          <| fun _ ->

              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)
              let register = ObjXRegister(None)
              let cctx = { cctx with ObjXRegister = Some register }
              let cctx = withLocals (cctx, [| "x"; "y" |])
              let cctx, method, register, internals = withMethod (cctx)

              let form = ReadFromString "(if x 7 8)"
              let ast = Parser.Analyze(cctx, form)

              let test =
                  AST.LocalBinding(LocalBindingExpr(
                      env = cctx,
                      form = Symbol.intern ("x"),
                      Binding = (cctx.Locals.valAt (Symbol.intern ("x")) :?> LocalBinding),
                      tag = null
                  ))

              Expect.equal
                  ast
                  (AST.If(IfExpr(
                      env = cctx,
                      form = form,
                      testExpr = test,
                      thenExpr = AST.Literal(LiteralExpr(env = cctx, form = 7L, value = 7L, literalType = PrimNumericType)),
                      elseExpr = AST.Literal(LiteralExpr(env = cctx, form = 8L, value = 8L, literalType = PrimNumericType)),
                      sourceInfo = None
                  )))
                  "Should return an AST.If"

          testCase "(if x 7)  => AST.If"
          <| fun _ ->

              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)
              let register = ObjXRegister(None)
              let cctx = { cctx with ObjXRegister = Some register }
              let cctx = withLocals (cctx, [| "x"; "y" |])
              let cctx, method, register, internals = withMethod (cctx)

              let form = ReadFromString "(if x 7)"
              let ast = Parser.Analyze(cctx, form)

              let test =
                  AST.LocalBinding(LocalBindingExpr(
                      env = cctx,
                      form = Symbol.intern ("x"),
                      Binding = (cctx.Locals.valAt (Symbol.intern ("x")) :?> LocalBinding),
                      tag = null
                  ))

              Expect.equal
                  ast
                  (AST.If(IfExpr(
                      env = cctx,
                      form = form,
                      testExpr = test,
                      thenExpr = AST.Literal(LiteralExpr(env = cctx, form = 7L, value = 7L, literalType = PrimNumericType)),
                      elseExpr = Parser.NilExprInstance,
                      sourceInfo = None
                  )))
                  "Should return an AST.If"

          testCase "(if x)  or (if x 7 8 9)   => throws"
          <| fun _ ->

              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)
              let register = ObjXRegister(None)
              let cctx = { cctx with ObjXRegister = Some register }
              let cctx = withLocals (cctx, [| "x"; "y" |])
              let cctx, method, register, internals = withMethod (cctx)

              let form = ReadFromString "(if x)"

              Expect.throwsT<CompilerException>
                  (fun _ -> Parser.Analyze(cctx, form) |> ignore)
                  "Should throw with wrong number of arguments"

              let form = ReadFromString "(if x 7 8 9)"

              Expect.throwsT<CompilerException>
                  (fun _ -> Parser.Analyze(cctx, form) |> ignore)
                  "Should throw with wrong number of arguments"

          testCase "#'abc => AST.TheVar expression"
          <| fun _ ->

              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)
              let register = ObjXRegister(None)
              let cctx = { cctx with ObjXRegister = Some register }

              let form = ReadFromString "#'abc"

              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try
                  let ast = Parser.Analyze(cctx, form)

                  Expect.equal
                      ast
                      (AST.TheVar(env = cctx, form = form, Var = ns1.findInternedVar (abcSym)))
                      "Should be a TheVar expression"

              finally
                  Var.popThreadBindings () |> ignore


          testCase "#'fred10 => throws on unbound"
          <| fun _ ->

              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)
              let register = ObjXRegister(None)
              let cctx = { cctx with ObjXRegister = Some register }

              let form = ReadFromString "#'fred10"

              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try
                  Expect.throwsT<CompilerException>
                      (fun _ -> Parser.Analyze(cctx, form) |> ignore)
                      "Should throw with unbound var"

              finally
                  Var.popThreadBindings () |> ignore


          testCase "#'(x) => throws on non-Symbol"
          <| fun _ ->

              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)
              let register = ObjXRegister(None)
              let cctx = { cctx with ObjXRegister = Some register }

              let form = ReadFromString "#'(x)"

              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try
                  Expect.throwsT<CompilerException>
                      (fun _ -> Parser.Analyze(cctx, form) |> ignore)
                      "Should throw on non-Symbol"

              finally
                  Var.popThreadBindings () |> ignore

          testCase "(do 7 8) => AST.Body expression"
          <| fun _ ->

              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)
              let register = ObjXRegister(None)
              let cctx = { cctx with ObjXRegister = Some register }

              let form = ReadFromString "(do 7 8)"

              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try
                  let ast = Parser.Analyze(cctx, form)

                  let bodyExprs = ResizeArray<AST>()

                  bodyExprs.Add(
                      AST.Literal(LiteralExpr(
                          env = cctx.WithParserContext(Statement),
                          form = 7L,
                          value = 7L,
                          literalType = PrimNumericType
                      ))
                  )

                  bodyExprs.Add(AST.Literal(LiteralExpr(env=cctx, form=8L, value=8L, literalType=PrimNumericType)))

                  compareBodies (ast, AST.Body(BodyExpr(env = cctx, form = RTSeq.next (form), exprs = bodyExprs)))

              finally
                  Var.popThreadBindings () |> ignore


          testCase "(set! abc 7) => set of Var"
          <| fun _ ->

              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)
              let register = ObjXRegister(None)
              let cctx = { cctx with ObjXRegister = Some register }

              let form = ReadFromString "(set! abc 7)"

              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try
                  let ast = Parser.Analyze(cctx, form)

                  Expect.equal
                      ast
                      (AST.Assign(AssignExpr(
                          env = cctx,
                          form = form,
                          target =
                              (AST.Var(
                                  env =
                                      { cctx with
                                          Pctx = Expression
                                          IsAssignContext = true },
                                  form = abcSym,
                                  Var = ns1.findInternedVar (abcSym),
                                  tag = null
                              )),
                          value =
                              (AST.Literal(LiteralExpr(
                                  env = cctx.WithParserContext(Expression),
                                  form = 7L,
                                  value = 7L,
                                  literalType = PrimNumericType
                              )))
                      )))
                      "Should return AST.Assign"

              finally
                  Var.popThreadBindings () |> ignore

          testCase "(set! x 7) => set of local"
          <| fun _ ->

              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)
              let register = ObjXRegister(None)
              let cctx = { cctx with ObjXRegister = Some register }
              let cctx = withLocals (cctx, [| "x"; "y" |])
              let cctx, method, register, internals = withMethod (cctx)

              let form = ReadFromString "(set! x 7)"

              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try
                  let ast = Parser.Analyze(cctx, form)

                  let expected =
                      (AST.Assign(AssignExpr(
                          env = cctx,
                          form = form,
                          target =
                              (AST.LocalBinding(LocalBindingExpr(
                                  env =
                                      { cctx with
                                          Pctx = Expression
                                          IsAssignContext = true },
                                  form = Symbol.intern "x",
                                  Binding = (cctx.Locals.valAt (Symbol.intern ("x")) :?> LocalBinding),
                                  tag = null
                              ))),
                          value =
                              (AST.Literal(LiteralExpr(
                                  env = cctx.WithParserContext(Expression),
                                  form = 7L,
                                  value = 7L,
                                  literalType = PrimNumericType
                              ))
                      ))))

                  Expect.equal ast expected "Should return AST.Assign"

              finally
                  Var.popThreadBindings () |> ignore

          testCase "(set! Int64/MaxValue  7) => set of FieldOrPropertyExpr"
          <| fun _ ->

              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)
              let register = ObjXRegister(None)
              let cctx = { cctx with ObjXRegister = Some register }
              let cctx = withLocals (cctx, [| "x"; "y" |])
              let cctx, method, register, internals = withMethod (cctx)

              let form = ReadFromString "(set! Int64/MaxValue 7)"

              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try
                  let ast = Parser.Analyze(cctx, form)

                  let expectedtarget =
                      AST.InteropCall(InteropCallExpr(
                          env =
                              { cctx with
                                  Pctx = Expression
                                  IsAssignContext = true },
                          form = Symbol.intern "Int64/MaxValue",
                          hostExprType = FieldOrPropertyExpr,
                          isStatic = true,
                          tag = null,
                          target = None,
                          targetType = typeof<Int64>,
                          memberName = "MaxValue",
                          tInfo = (typeof<Int64>.GetField ("MaxValue")),
                          args = ResizeArray<HostArg>(),
                          typeArgs = ResizeArray<Type>(),
                          sourceInfo = None
                      ))

                  let expectedValue =
                      AST.Literal(LiteralExpr(
                          env = cctx.WithParserContext(Expression),
                          form = 7L,
                          value = 7L,
                          literalType = PrimNumericType
                      ))

                  //let expected =
                  //    (AST.Assign(
                  //        Env = cctx,
                  //        Form = form,
                  //        Target = expectedTarget,
                  //        Value = expectedValue))

                  // Because interop calls are problematic to compare, we have to do this in pieces
                  match ast with
                  | AST.Assign(assignExpr) ->

                      Expect.equal assignExpr.Env cctx "Should have the expected env"
                      Expect.equal assignExpr.Form form "Should have the expected form"

                      compareInteropCalls (assignExpr.Target, expectedTarget)
                      Expect.equal assignExpr.Value expectedValue "Should have the expected value"

                  | _ -> failtest "Should be an Assign"


              finally
                  Var.popThreadBindings () |> ignore


          testCase "(set! 7 7) => set of non-assignable expression throws"
          <| fun _ ->

              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)
              let register = ObjXRegister(None)
              let cctx = { cctx with ObjXRegister = Some register }

              let form = ReadFromString "(set! 7 7)"

              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try
                  Expect.throwsT<CompilerException> (fun _ -> Parser.Analyze(cctx, form) |> ignore) "Should throw"


              finally
                  Var.popThreadBindings () |> ignore

          testCase """(clojure.core/import* "SomeType") => creates AST.Import"""
          <| fun _ ->

              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)
              let register = ObjXRegister(None)
              let cctx = { cctx with ObjXRegister = Some register }

              let form = ReadFromString """(clojure.core/import* "SomeType")"""

              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try
                  let ast = Parser.Analyze(cctx, form)
                  Expect.equal ast (AST.Import(ImportExpr(env = cctx, form = form, typename = "SomeType", sourceInfo = None))) "Should be an Import"
              finally
                  Var.popThreadBindings () |> ignore

          testCase """(clojure.core/import* 7) => Throws on non-string"""
          <| fun _ ->

              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)
              let register = ObjXRegister(None)
              let cctx = { cctx with ObjXRegister = Some register }

              let form = ReadFromString """(clojure.core/import* 7)"""

              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try
                  Expect.throwsT<CompilerException> (fun _ -> Parser.Analyze(cctx, form) |> ignore) "Should throw"
              finally
                  Var.popThreadBindings () |> ignore


          testCase "Don't quote me"
          <| fun _ ->

              // I don't think we need six or seven separate tests for this.
              let cctx = CompilerEnv.Create(Expression)


              let form = ReadFromString("'nil")
              let ast = Parser.Analyze(cctx, form)
              Expect.equal ast Parser.NilExprInstance "'nil is nil expr"

              let form = ReadFromString("'true")
              let ast = Parser.Analyze(cctx, form)
              Expect.equal ast Parser.TrueExprInstance "'true is true expr"

              let form = ReadFromString("'false")
              let ast = Parser.Analyze(cctx, form)
              Expect.equal ast Parser.FalseExprInstance "'false is false expr"

              let form = ReadFromString("'7")
              let ast = Parser.Analyze(cctx, form)
              Expect.equal ast (AST.Literal(LiteralExpr(env = cctx, form = 7L, value = 7L, literalType = PrimNumericType))) "'7 is 7"

              let form = ReadFromString("'\"abc\"")
              let ast = Parser.Analyze(cctx, form)

              Expect.equal
                  ast
                  (AST.Literal(LiteralExpr(env = cctx, form = "abc", value = "abc", literalType = StringType)))
                  "'\"abc\" is \"abc\""

              let form = ReadFromString("'[]")
              let ast = Parser.Analyze(cctx, form)

              Expect.equal
                  ast
                  (AST.Literal(LiteralExpr(env = cctx, form = RTSeq.second (form), value = RTSeq.second (form), literalType = EmptyType)))
                  "'[] is EmptyType]"

              let form = ReadFromString("'[1 2 3]")
              let ast = Parser.Analyze(cctx, form)

              Expect.equal
                  ast
                  (AST.Literal(LiteralExpr(env = cctx, form = RTSeq.second (form), value = RTSeq.second (form), literalType = OtherType)))
                  "'[1 2 3] is not EmptyType"

              let form = ReadFromString("' ^:kw [ 1 2 3]")
              let ast = Parser.Analyze(cctx, form)

              Expect.equal
                  ast
                  (AST.Literal(LiteralExpr(env = cctx, form = RTSeq.second (form), value = RTSeq.second (form), literalType = OtherType)))
                  "'collection with meta is not EmptyType"

              let form = ReadFromString("'x")
              let ast = Parser.Analyze(cctx, form)

              Expect.equal
                  ast
                  (AST.Literal(LiteralExpr(env = cctx, form = RTSeq.second (form), value = RTSeq.second (form), literalType = OtherType)))
                  "quote anything else is OtherType"

          ]

[<Tests>]
let LetTests =
    testList
        "Let Tests"
        [ testCase "let with bad binding forms"
          <| fun _ ->

              let cctx = CompilerEnv.Create(Expression)

              let form = ReadFromString "(let* x 7)"

              Expect.throwsT<CompilerException>
                  (fun _ -> Parser.Analyze(cctx, form) |> ignore)
                  "Thows - bad binding form"

              let form = ReadFromString "(let* [x y z] 7)"

              Expect.throwsT<CompilerException>
                  (fun _ -> Parser.Analyze(cctx, form) |> ignore)
                  "Thows - odd number of elements in binding form"

              let form = ReadFromString "(let* [x/y 7] 7)"

              Expect.throwsT<CompilerException>
                  (fun _ -> Parser.Analyze(cctx, form) |> ignore)
                  "Thows - qualified symbol for local"

              let form = ReadFromString "(let* [7 7] 7)"

              Expect.throwsT<CompilerException>
                  (fun _ -> Parser.Analyze(cctx, form) |> ignore)
                  "Thows - non-symbol for local"

          testCase "let with one binding, local ref in body"
          <| fun _ ->

              let cctx = CompilerEnv.Create(Expression)

              let form = ReadFromString "(let* [x 7] x)"
              let ast = Parser.Analyze(cctx, form)

              let xSym = Symbol.intern ("x")

              let expectedBindings = ResizeArray<BindingInit>()

              let firstCctx =
                  { cctx with
                      Pctx = Expression
                      IsAssignContext = false
                      LoopId = None }

              let firstBindingInit =
                  createBindingInit (
                      xSym,
                      AST.Literal(LiteralExpr(env=cctx, form=7L, value=7L, literalType=PrimNumericType)),
                      0,
                      false
                  )

              let firstBinding = firstBindingInit.Binding
              expectedBindings.Add(firstBindingInit)

              let secondCctx = { cctx with Locals = cctx.Locals.assoc (xSym, firstBinding) }

              let bodyCctx =
                  { secondCctx with
                      Pctx = Expression
                      IsRecurContext = false
                      LoopLocals = null }

              let expectedBodyForms = ResizeArray<AST>()
              expectedBodyForms.Add(AST.LocalBinding(LocalBindingExpr(Env = bodyCctx, Form = xSym, Binding = firstBinding, Tag = null)))

              let expectedBody =
                  AST.Body(BodyExpr(env = bodyCctx, form = RTSeq.next (RTSeq.next (form)), exprs = expectedBodyForms))

              match ast with
              | AST.Let(letExpr) ->
                  Expect.equal letExpr.Env cctx "Should have the expected env"
                  Expect.equal letExpr.Form form "Should have the expected form"
                  Expect.equal letExpr.Mode LetExprMode.Let "Should be a Let"
                  compareBodies (letExpr.Body, expectedBody)
                  compareGenericLists (letExpr.BindingInits, expectedBindings)
              | _ -> failtest "Should be a Let"

          testCase "let with two bindings, local ref in body"
          <| fun _ ->

              let cctx = CompilerEnv.Create(Expression)

              let form = ReadFromString "(let* [x 7 y 8] x)"
              let ast = Parser.Analyze(cctx, form)

              let xSym = Symbol.intern ("x")
              let ySym = Symbol.intern ("y")

              let expectedBindings = ResizeArray<BindingInit>()

              let firstCctx =
                  { cctx with
                      Pctx = Expression
                      IsAssignContext = false
                      LoopId = None }

              let firstBindingInit =
                  createBindingInit (
                      xSym,
                      AST.Literal(LiteralExpr(env = firstCctx, form = 7L, value = 7L, literalType = PrimNumericType)),
                      0,
                      false
                  )

              let firstBinding = firstBindingInit.Binding
              expectedBindings.Add(firstBindingInit)

              let secondCctx = { cctx with Locals = cctx.Locals.assoc (xSym, firstBinding) }

              let secondBindingInit =
                  createBindingInit (
                      ySym,
                      AST.Literal(LiteralExpr(env = secondCctx, form = 8L, value = 8L, literalType = PrimNumericType)),
                      1,
                      false
                  )

              let thirdCctx =
                  { secondCctx with Locals = secondCctx.Locals.assoc (ySym, secondBindingInit.Binding) }

              let bodyCctx =
                  { thirdCctx with
                      Pctx = Expression
                      IsRecurContext = false
                      LoopLocals = null }

              let expectedBodyForms = ResizeArray<AST>()
              expectedBodyForms.Add(AST.LocalBinding(LocalBindingExpr(Env = bodyCctx, Form = xSym, Binding = firstBinding, Tag = null)))

              let expectedBody =
                  AST.Body(BodyExpr(env = bodyCctx, form = RTSeq.next (RTSeq.next (form)), exprs = expectedBodyForms))

              match ast with
              | AST.Let(letExpr) ->
                  Expect.equal letExpr.Env cctx "Should have the expected env"
                  Expect.equal letExpr.Form form "Should have the expected form"
                  Expect.equal letExpr.Mode LetExprMode.Let "Should be a Let"
                  compareBodies (letExpr.Body, expectedBody)
                  compareGenericLists (letExpr.BindingInits, expectedBindings)
              | _ -> failtest "Should be a Let"

          testCase "let with two bindings, local ref in second init and body"
          <| fun _ ->

              let cctx = CompilerEnv.Create(Expression)

              let form = ReadFromString "(let* [x 7 y x] y)"
              let ast = Parser.Analyze(cctx, form)

              let xSym = Symbol.intern ("x")
              let ySym = Symbol.intern ("y")

              let expectedBindings = ResizeArray<BindingInit>()

              let firstCctx =
                  { cctx with
                      Pctx = Expression
                      IsAssignContext = false
                      LoopId = None }

              let firstBindingInit =
                  createBindingInit (
                      xSym,
                      AST.Literal(LiteralExpr(env = firstCctx, form = 7L, value = 7L, literalType = PrimNumericType)),
                      0,
                      false
                  )

              let firstBinding = firstBindingInit.Binding
              expectedBindings.Add(firstBindingInit)

              let secondCctx = { firstCctx with Locals = cctx.Locals.assoc (xSym, firstBinding) }

              let secondBindingInit =
                  createBindingInit (
                      ySym,
                      AST.LocalBinding(LocalBindingExpr(Env = secondCctx, Form = xSym, Binding = firstBinding, Tag = null)),
                      1,
                      false
                  )

              let secondBinding = secondBindingInit.Binding

              let thirdCctx =
                  { secondCctx with Locals = secondCctx.Locals.assoc (ySym, secondBindingInit.Binding) }

              let bodyCctx =
                  { thirdCctx with
                      Pctx = Expression
                      IsRecurContext = false
                      LoopLocals = null }

              let expectedBodyForms = ResizeArray<AST>()
              expectedBodyForms.Add(AST.LocalBinding(LocalBindingExpr(Env = bodyCctx, Form = ySym, Binding = secondBinding, Tag = null)))

              let expectedBody =
                  AST.Body(BodyExpr(env = bodyCctx, form = RTSeq.next (RTSeq.next (form)), exprs = expectedBodyForms))

              match ast with
              | AST.Let(letExpr) ->
                  Expect.equal letExpr.Env cctx "Should have the expected env"
                  Expect.equal letExpr.Form form "Should have the expected form"
                  Expect.equal letExpr.Mode LetExprMode.Let "Should be a Let"
                  compareBodies (letExpr.Body, expectedBody)
                  compareGenericLists (letExpr.BindingInits, expectedBindings)
              | _ -> failtest "Should be a Let"

          testCase "loop with two bindings, local ref in body -- check for loop id and loop locals"
          <| fun _ ->

              let cctx = CompilerEnv.Create(Expression)

              let form = ReadFromString "(loop* [x 7 y x] x)"
              let ast = Parser.Analyze(cctx, form)

              let xSym = Symbol.intern ("x")
              let ySym = Symbol.intern ("y")

              let expectedBindings = ResizeArray<BindingInit>()

              // Need to dig out the stupid loop id so we can do direct comparisons
              let loopId =
                  match ast with
                  | AST.Let(letExpr) -> letExpr.LoopId
                  | _ -> failtest "Should be a Let"


              let firstCctx =
                  { cctx with
                      Pctx = Expression
                      IsAssignContext = false
                      LoopId = loopId }

              let firstBindingInit =
                  createBindingInit (
                      xSym,
                      AST.Literal(LiteralExpr(env=cctx, form=7L, value=7L, literalType=PrimNumericType)),
                      0,
                      true
                  )

              let firstBinding = firstBindingInit.Binding
              expectedBindings.Add(firstBindingInit)

              let secondCctx = { firstCctx with Locals = cctx.Locals.assoc (xSym, firstBinding) }

              let secondBindingInit =
                  createBindingInit (
                      ySym,
                      AST.LocalBinding(LocalBindingExpr(Env = secondCctx, Form = xSym, Binding = firstBinding, Tag = null)),
                      1,
                      true
                  )

              let secondBinding = secondBindingInit.Binding

              let expectedLoopLocals = ResizeArray<LocalBinding>()
              expectedLoopLocals.Add(firstBinding)
              expectedLoopLocals.Add(secondBinding)

              match ast with
              | AST.Let(letExpr) ->
                  Expect.equal letExpr.Mode LetExprMode.Loop "Should be a loop"

                  match letExpr.Body with
                  | AST.Body(bodyExpr) ->
                      let benv = bodyExpr.Env
                      Expect.isTrue (benv.LoopId.IsSome) "Should have a loop id"
                      let loopLocals = benv.LoopLocals
                      Expect.isNotNull loopLocals "Should have loop locals"
                      Expect.equal (loopLocals.Count) 2 "Should have two loop locals"
                      compareGenericLists (loopLocals, expectedLoopLocals)
                  | _ -> failtest "Should be a Body"
              | _ -> failtest "Should be a Let"

          ]


[<Tests>]
let RecurTests =
    testList
        "Recur Tests"
        [ testCase "recur not in a loop -- throws"
          <| fun _ ->

              let cctx = CompilerEnv.Create(Expression)

              let form = ReadFromString "(let* [x 7] (recur 12))"

              Expect.throwsT<CompilerException>
                  (fun _ -> Parser.Analyze(cctx, form) |> ignore)
                  "Should throw when not in recur context"

          testCase "recur not in return context -- throws"
          <| fun _ ->

              let cctx = CompilerEnv.Create(Expression)

              let form = ReadFromString "(loop* [x 7] (recur 12) 7)"

              Expect.throwsT<CompilerException>
                  (fun _ -> Parser.Analyze(cctx, form) |> ignore)
                  "Should throw when not in recur context"

              let form = ReadFromString "(loop* [x 7] (if (recur 12) 7 8))"

              Expect.throwsT<CompilerException>
                  (fun _ -> Parser.Analyze(cctx, form) |> ignore)
                  "Should throw when not in recur context"


          testCase "recur with arg count mismatch -- throws"
          <| fun _ ->

              let cctx = CompilerEnv.Create(Expression)

              let form = ReadFromString "(loop* [x 7] (recur 12 14))"

              Expect.throwsT<CompilerException>
                  (fun _ -> Parser.Analyze(cctx, form) |> ignore)
                  "Should throw on arg count mismatch"

          testCase "recur across try -- throws"
          <| fun _ ->

              let cctx = CompilerEnv.Create(Expression)

              let form = ReadFromString "(loop* [x 7] (try (recur 12 14)))"

              Expect.throwsT<CompilerException>
                  (fun _ -> Parser.Analyze(cctx, form) |> ignore)
                  "Should throw on recur across try"


          testCase "recur works"
          <| fun _ ->

              let cctx = CompilerEnv.Create(Expression)

              let form = ReadFromString "(loop* [x 7] (recur 12))"
              let ast = Parser.Analyze(cctx, form)

              match ast with
              | AST.Let(letExpr) ->
                  match letExpr.Body with
                  | AST.Body(bodyExpr) ->
                      Expect.equal bodyExpr.Exprs.Count 1 "Should have one expression"

                      match bodyExpr.Exprs[0] with
                      | AST.Recur(LoopLocals = loopLocals; Args = args) ->
                          Expect.equal loopLocals.Count 1 "Should have one loop local"
                          Expect.equal args.Count 1 "Should have one arg"
                      | _ -> failtest "Should be a Recur"
                  | _ -> failtest "Should be a Body"
              | _ -> failtest "Should be a Let"


          ]



[<Tests>]
let TryTests =
    testList
        "Try Tests"
        [ testCase "try with no catch/finally => returns a body expression"
          <| fun _ ->

              let cctx = CompilerEnv.Create(Return)

              let form = ReadFromString "(try 7)"
              let ast = Parser.Analyze(cctx, form)

              Expect.isTrue (ast.IsBody) "Should be a body expression"

          testCase "try with no finally not last in body => throws"
          <| fun _ ->

              let cctx = CompilerEnv.Create(Return)

              let form = ReadFromString "(try 7 (finally 7) 7)"

              Expect.throwsT<CompilerException>
                  (fun _ -> Parser.Analyze(cctx, form) |> ignore)
                  "Should throw when finally not terminal"

          testCase "catch with bad exception type => throws"
          <| fun _ ->

              let cctx = CompilerEnv.Create(Return)

              let form = ReadFromString "(try 7 (catch Fred x 7))"

              Expect.throwsT<CompilerException>
                  (fun _ -> Parser.Analyze(cctx, form) |> ignore)
                  "Should throw when catch clause has non-type for exception"


          testCase "catch with bad local variable type => throws"
          <| fun _ ->

              let cctx = CompilerEnv.Create(Return)

              let form = ReadFromString "(try 7 (catch Exception x/y 7))"

              Expect.throwsT<CompilerException>
                  (fun _ -> Parser.Analyze(cctx, form) |> ignore)
                  "Should throw when catch clause has bad local var"

          testCase "valid try expression"
          <| fun _ ->

              let cctx = CompilerEnv.Create(Return)

              let form =
                  ReadFromString "(try 7 (catch ArgumentException x 7) (catch Exception y 12) (finally 7))"

              let ast = Parser.Analyze(cctx, form)

              match ast with
              | AST.Try(Env = env; TryExpr = tryExpr; Catches = catches; Finally = finallyExpr) ->
                  Expect.isTrue tryExpr.IsBody "Should be a body expression"
                  Expect.equal catches.Count 2 "Should have one catches"
                  Expect.isTrue finallyExpr.IsSome "Should have a finally expression"
              | _ -> failtest "Should be a Try"


          ]


[<Tests>]
let DefTests =
    testList
        "Def Tests"
        [ testCase "def without sufficient args or non-symbol as first arg => throws"
          <| fun _ ->

              let cctx = CompilerEnv.Create(Expression)

              let form = ReadFromString "(def)"

              Expect.throwsT<CompilerException>
                  (fun _ -> Parser.Analyze(cctx, form) |> ignore)
                  "Should throw when not enough args"

              let form = ReadFromString "(def x y z)"

              Expect.throwsT<CompilerException>
                  (fun _ -> Parser.Analyze(cctx, form) |> ignore)
                  "Should throw when too many args"

              let form = ReadFromString """(def x "abc" y z)"""

              Expect.throwsT<CompilerException>
                  (fun _ -> Parser.Analyze(cctx, form) |> ignore)
                  "Should throw when too many args"

              let form = ReadFromString """(def 7 12)"""

              Expect.throwsT<CompilerException>
                  (fun _ -> Parser.Analyze(cctx, form) |> ignore)
                  "Should throw when non-Symbol as first arg"

          testCase "def with bad first arg => throws"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)

              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try

                  let form = ReadFromString "(def ns2/not-there 7)"

                  Expect.throwsT<CompilerException>
                      (fun _ -> Parser.Analyze(cctx, form) |> ignore)
                      "try to def var in other namespace, var not found"

                  let form = ReadFromString "(def missing/not-there 7)"

                  Expect.throwsT<CompilerException>
                      (fun _ -> Parser.Analyze(cctx, form) |> ignore)
                      "try to def var, missing namespace"

              finally
                  Var.popThreadBindings () |> ignore


          testCase "basic def (no docstring, no init) works"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)

              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try

                  let form = ReadFromString "(def v)"
                  let ast = Parser.Analyze(cctx, form)
                  let vSym = Symbol.intern "v"
                  let vVar = ns1.findInternedVar (vSym)

                  Expect.equal
                      ast
                      (AST.Def(DefExpr(
                          env = cctx,
                          form = form,
                          var = vVar,
                          init = Parser.NilExprInstance,
                          initProvided = false,
                          isDynamic = false,
                          shadowsCoreMapping = false,
                          meta = None,
                          sourceInfo = None
                      )))
                      "Should be a def"

              finally
                  Var.popThreadBindings () |> ignore

          testCase "basic def with init (no docstring) works"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)

              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try

                  let form = ReadFromString "(def v 7)"
                  let ast = Parser.Analyze(cctx, form)
                  let vSym = Symbol.intern "v"
                  let vVar = ns1.findInternedVar (vSym)

                  Expect.equal
                      ast
                      (AST.Def(DefExpr(
                          env = cctx,
                          form = form,
                          var = vVar,
                          init = AST.Literal(LiteralExpr(env = cctx, form = 7L, value = 7L, literalType = PrimNumericType)),
                          initProvided = true,
                          isDynamic = false,
                          shadowsCoreMapping = false,
                          meta = None,
                          sourceInfo = None
                      )))
                      "Should be a def"

              finally
                  Var.popThreadBindings () |> ignore


          testCase "def : docstring in metadata"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)

              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try

                  let form = ReadFromString "(def ^{:doc \"something\"} v 7)"
                  let ast = Parser.Analyze(cctx, form)
                  let vSym = Symbol.intern "v"
                  let vVar = ns1.findInternedVar (vSym)
                  let metaMap = ReadFromString "{:doc \"something\"}"
                  let metaExpr = AST.Literal(LiteralExpr(env=cctx, form=metaMap, value=metaMap, literalType=OtherType))

                  Expect.equal
                      ast
                      (AST.Def(DefExpr(
                          env = cctx,
                          form = form,
                          var = vVar,
                          init = AST.Literal(LiteralExpr(env = cctx, form = 7L, value = 7L, literalType = PrimNumericType)),
                          initProvided = true,
                          isDynamic = false,
                          shadowsCoreMapping = false,
                          meta = Some(metaExpr),
                          sourceInfo = None
                      )))
                      "Should be a def"

              finally
                  Var.popThreadBindings () |> ignore

          testCase "def transfers :dynamic true to Var"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)

              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try

                  let form = ReadFromString "(def ^{:dynamic true} v 7)"
                  let ast = Parser.Analyze(cctx, form)
                  let vSym = Symbol.intern "v"
                  let vVar = ns1.findInternedVar (vSym)
                  let metaMap = ReadFromString "{:dynamic true}"
                  let metaExpr = AST.Literal(LiteralExpr(env=cctx, form=metaMap, value=metaMap, literalType=OtherType))

                  Expect.equal
                      ast
                      (AST.Def(DefExpr(
                          env = cctx,
                          form = form,
                          var = vVar,
                          init = AST.Literal(LiteralExpr(env = cctx, form = 7L, value = 7L, literalType = PrimNumericType)),
                          initProvided = true,
                          isDynamic = true,
                          shadowsCoreMapping = false,
                          meta = Some(metaExpr),
                          sourceInfo = None
                      )))
                      "Should be a def"
                  Expect.isTrue (RT0.booleanCast(vVar.isDynamic)) "Should be dynamic"

              finally
                  Var.popThreadBindings () |> ignore

          testCase "def transfers :arglists value to Var"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)

              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try

                  let form = ReadFromString "(def ^{:arglists '([x])} v 7)"
                  let ast = Parser.Analyze(cctx, form)
                  let vSym = Symbol.intern "v"
                  let vVar = ns1.findInternedVar (vSym)
                  let metaMapForm = ReadFromString "{:arglists '([x])}"
                  let metaMap = ReadFromString "{:arglists ([x])}" :?> IPersistentMap
                  let metaExpr = AST.Literal(LiteralExpr(env=cctx, form=metaMapForm, value=metaMap, literalType=OtherType))

                  Expect.equal
                      ast
                      (AST.Def(DefExpr(
                          env = cctx,
                          form = form,
                          var = vVar,
                          init = AST.Literal(LiteralExpr(env = cctx, form = 7L, value = 7L, literalType = PrimNumericType)),
                          initProvided = true,
                          isDynamic = false,
                          shadowsCoreMapping = false,
                          meta = Some(metaExpr),
                          sourceInfo = None
                      )))
                      "Should be a def"
                  let arglistskw = Keyword.intern "arglists"
                  Expect.equal ((vVar :> IMeta).meta().valAt(arglistskw)) (metaMap.valAt(arglistskw)) "Should have arglists"

              finally
                  Var.popThreadBindings () |> ignore

          testCase "def notes shadowsCoreMapping"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)

              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try

                  let pqrVar = ns2.findInternedVar pqrSym
                  let fredSym = Symbol.intern "fred11"
                  ns1.reference(fredSym, pqrVar) |> ignore

                  let form = ReadFromString "(def fred11 7)"
                  let ast = Parser.Analyze(cctx, form)
                  let fredVar = ns1.findInternedVar (fredSym)

                  Expect.equal
                      ast
                      (AST.Def(DefExpr(
                          env = cctx,
                          form = form,
                          var = fredVar,
                          init = AST.Literal(LiteralExpr(env=cctx, form=7L, value=7L, literalType=PrimNumericType)),
                          initProvided = true,
                          isDynamic = false,
                          shadowsCoreMapping = true,
                          meta = None,
                          sourceInfo = None
                      )))
                      "Should be a def"

              finally
                  Var.popThreadBindings () |> ignore

          // TODO: Test for printing warning for def of *dynamic*
          // TODO: Test for eliding metadata on def

          ]


[<Tests>]
let FnTests =
    testList
        "Fn Tests"
        [ testCase "(fn* [] 7) same as (fn* ([] 7)) -- normalize form"
          <| fun _ ->

              let cctx = CompilerEnv.Create(Expression)

              let form1 = ReadFromString "(fn* [] 7)"
              let ast1 = Parser.Analyze(cctx,form1)

              let form2 = ReadFromString "(fn* ([] 7))"
              let ast2 = Parser.Analyze(cctx,form2)
                
              compareObjExprs (ast1, ast2)

          testCase "parameter errors"
          <| fun _ ->

              let cctx = CompilerEnv.Create(Expression)

              let form = ReadFromString "(fn* ([x y] 7) ([x y] 8))"
              Expect.throwsT<CompilerException> (fun _ -> Parser.Analyze(cctx, form) |> ignore) "two methods with same arity => throws"

              let form = ReadFromString "(fn* ([x & y] 7) ([x & y] 8))"
              Expect.throwsT<CompilerException> (fun _ -> Parser.Analyze(cctx, form) |> ignore) "more than one variadic method => throws"

              let form = ReadFromString "(fn* ([x y z] 7) ([x & y] 8))"
              Expect.throwsT<CompilerException> (fun _ -> Parser.Analyze(cctx, form) |> ignore) "fixed with greater arity than variadic required => throws"

              let form = ReadFromString "(fn* ([x y & z w] 7) ([x y] 8))"
              Expect.throwsT<CompilerException> (fun _ -> Parser.Analyze(cctx, form) |> ignore) "more than one parameter after & => throws"

              let form = ReadFromString "(fn* ([x/y z] 7))"
              Expect.throwsT<CompilerException> (fun _ -> Parser.Analyze(cctx, form) |> ignore) "qualified sym as parameter => throws"               
             
              let form = ReadFromString "(fn* ([x 7] 7))"
              Expect.throwsT<CompilerException> (fun _ -> Parser.Analyze(cctx, form) |> ignore) "non-Symbol for parameter => throws"      

              let form = ReadFromString "(fn* ([x1 x2 x3 x4 x5 x6 x7 x8 x9 x10 x11 x12 x13 x14 x15 x16 x17 x18 x19 x20 x21 x22 x23 ] 7))"
              Expect.throwsT<CompilerException> (fun _ -> Parser.Analyze(cctx, form) |> ignore) "too many locals => throws"        

              let form = ReadFromString "(fn* ([x & ^int y] 7))"
              Expect.throwsT<CompilerException> (fun _ -> Parser.Analyze(cctx, form) |> ignore) "type hint on variadic param => throws"

          testCase "fn* basic data - with name, with :once, parameter reference in method"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let ns1Name = ns1.Name.Name
              let cctx = CompilerEnv.Create(Expression)

              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try 

                  let form = ReadFromString "(^:once fn* fred99 ([x] x))"
                  let ast = Parser.Analyze(cctx,form)

                  match ast with
                  | AST.Obj(Env = env; Form = form; Type = typ; Internals = internals; Register = register; sourceInfo = sourceInfo) ->
                      Expect.equal env cctx "Should have the expected env"
                      Expect.equal form form "Should have the expected form"
                      Expect.isTrue (internals.OnceOnly) "Should be once only"
                      Expect.isTrue (internals.Name.StartsWith($"{ns1Name}$fred99__"))  "Name should have proper prefix"
                      Expect.isTrue (internals.InternalName.StartsWith($"{ns1Name}$fred99__"))  "InternalName should have proper prefix"
                      Expect.isFalse (internals.HasEnclosingMethod) "Should not have enclosing method"
                      Expect.equal internals.Methods.Count 1 "Should have one method"
                      Expect.isTrue (internals.VariadicMethod.IsNone) "Should not have a variadic method"
                      Expect.isNull (internals.Tag) "Should not have a tag"
                      Expect.isTrue (register.Parent.IsNone) "Should not have a parent"
                      Expect.equal (register.Closes.count()) 0 "Should not have any Closes"
                      Expect.equal (register.Constants.count()) 0 "Should not have any Constants"
                      Expect.equal register.ConstantIds.Count 0 "Should not have any ConstantIds"
                      Expect.equal (register.Keywords.count()) 0 "Should not have any Keywords"
                      Expect.equal (register.Vars.count()) 0 "Should not have any Vars"
                      Expect.equal (register.KeywordCallsites.count()) 0 "Should not have any KeywordCallsites"
                      Expect.equal (register.ProtocolCallsites.count()) 0 "Should not have any ProtocolCallsites"
                      Expect.equal (register.VarCallsites.count()) 0 "Should not have any VarCallsites"
                      let method0 = internals.Methods[0]
                      Expect.equal method0.Type Fn "Should be of type Fn"
                      Expect.isTrue (Object.ReferenceEquals(method0.ObjxRegister, register)) "Should have the expected register"
                      Expect.isTrue (Object.ReferenceEquals(method0.ObjxInternals, internals)) "Should have the expected internals"
                      Expect.isFalse method0.UsesThis "Does not use this"
                      Expect.isTrue method0.RestParam.IsNone "Should not have a rest param"
                      Expect.equal method0.ReqParams.Count 1 "Should have one required param"
                      Expect.equal method0.ArgLocals.Count 1 "Should have one arg local"
                      Expect.equal method0.MethodName "invoke" "Should have the expected method name"
                      Expect.equal method0.RetType typeof<Object> "Should have Object return type"
                      Expect.isFalse method0.IsVariadic "Should not be variadic"
                      Expect.equal method0.NumParams 1 "Should have one param"
                      match method0.Body with
                      | AST.Body(bodyExpr) -> 
                        Expect.equal bodyExpr.Exprs.Count 1 "should have one form in the body"
                        let e0 = bodyExpr.Exprs[0]
                        Expect.isTrue e0.IsLocalBinding "should be a local binding"
                      | _ -> failtest "Expected body form to be a Body"
                  | _ -> failtest "Should be an Obj"
              
              finally 
                Var.popThreadBindings() |> ignore

          testCase "fn* basic data - without name, without :once, var reference in method"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let ns1Name = ns1.Name.Name
              let abcVar = ns1.findInternedVar abcSym

              let cctx = CompilerEnv.Create(Expression)

              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try
                  let form = ReadFromString "(fn* ([x] (abc 7)))"
                  let ast = Parser.Analyze(cctx,form)

                  match ast with
                  | AST.Obj(Env = env; Form = form; Type = typ; Internals = internals; Register = register; sourceInfo = sourceInfo) ->
                      Expect.isFalse (internals.OnceOnly) "Should be once only"
                      Expect.isTrue (internals.Name.StartsWith($"{ns1Name}$fn__"))  "Name should have proper prefix"
                      Expect.isTrue (internals.InternalName.StartsWith($"{ns1Name}$fn__"))  "InternalName should have proper prefix"
                      Expect.isFalse (internals.HasEnclosingMethod) "Should not have enclosing method"
                      Expect.equal (register.Closes.count()) 0 "Should not have any Closes"
                      Expect.equal (register.Constants.count()) 1 "Should have one Constant"
                      let const0 = register.Constants.nth(0)
                      Expect.equal const0 abcVar "Should be the expected Var"
                      Expect.equal register.ConstantIds.Count 1 "Should have one ConstantId"
                      Expect.equal (register.Keywords.count()) 0 "Should not have any Keywords"
                      Expect.equal (register.Vars.count()) 1 "Should not have any Vars"
                      let v0 = register.Vars.containsKey abcVar
                      Expect.equal (register.KeywordCallsites.count()) 0 "Should not have any KeywordCallsites"
                      Expect.equal (register.ProtocolCallsites.count()) 0 "Should not have any ProtocolCallsites"
                      Expect.equal (register.VarCallsites.count()) 0 "Should not have any VarCallsites"
                      let method0 = internals.Methods[0]
                      match method0.Body with
                      | AST.Body(bodyExpr) -> 
                        Expect.equal bodyExpr.Exprs.Count 1 "should have one form in the body"
                        let e0 = bodyExpr.Exprs[0]
                        Expect.isTrue e0.IsInvoke "should be a local binding"
                      | _ -> failtest "Expected body form to be a Body"
                  | _ -> failtest "Should be an Obj"
              finally
                Var.popThreadBindings() |> ignore


          testCase "fn* with name, uses this, has keyword reference"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let ns1Name = ns1.Name.Name
              let abcVar = ns1.findInternedVar abcSym

              let cctx = CompilerEnv.Create(Expression)


              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try

                  let form = ReadFromString "(fn* fred77 ([x] (abc fred77 :kw)))"
                  let ast = Parser.Analyze(cctx,form)

                  match ast with
                  | AST.Obj(Env = env; Form = form; Type = typ; Internals = internals; Register = register; sourceInfo = sourceInfo) ->

                      Expect.equal (register.Closes.count()) 0 "Should not have any Closes"
                      Expect.equal (register.Constants.count()) 2 "Should have two Constants"
                      Expect.equal register.ConstantIds.Count 2 "Should  have two ConstantIds"
                      Expect.equal (register.Keywords.count()) 1 "Should have one Keywords"
                      Expect.equal (register.Vars.count()) 1 "Should have one Vars"
                      Expect.equal (register.KeywordCallsites.count()) 0 "Should not have any KeywordCallsites"
                      Expect.equal (register.ProtocolCallsites.count()) 0 "Should not have any ProtocolCallsites"
                      Expect.equal (register.VarCallsites.count()) 0 "Should not have any VarCallsites"
                      let method0 = internals.Methods[0]
                      Expect.isTrue method0.UsesThis "Does use this"
                      match method0.Body with
                      | AST.Body(bodyExpr) -> 
                        Expect.equal bodyExpr.Exprs.Count 1 "should have one form in the body"
                        let e0 = bodyExpr.Exprs[0]
                        Expect.isTrue e0.IsInvoke "should be an invocation"
                      | _ -> failtest "Expected body form to be a Body"
                  | _ -> failtest "Should be an Obj"

              finally
                Var.popThreadBindings() |> ignore

          testCase "fn* has keyword callsite"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let ns1Name = ns1.Name.Name
              let abcVar = ns1.findInternedVar abcSym

              let cctx = CompilerEnv.Create(Expression)

              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try

                  let form = ReadFromString "(fn* ([x] (:kw x)))"
                  let ast = Parser.Analyze(cctx,form)

                  match ast with
                  | AST.Obj(Env = env; Form = form; Type = typ; Internals = internals; Register = register; sourceInfo = sourceInfo) ->

                      Expect.equal (register.Closes.count()) 0 "Should not have any Closes"
                      Expect.equal (register.Constants.count()) 1 "Should have one Constants"
                      Expect.equal register.ConstantIds.Count 1 "Should  have one ConstantIds"
                      Expect.equal (register.Keywords.count()) 1 "Should have one Keywords"
                      Expect.equal (register.Vars.count()) 0 "Should have no Vars"
                      Expect.equal (register.KeywordCallsites.count()) 1 "Should have one KeywordCallsites"
                      Expect.equal (register.ProtocolCallsites.count()) 0 "Should not have any ProtocolCallsites"
                      Expect.equal (register.VarCallsites.count()) 0 "Should not have any VarCallsites"
                      let method0 = internals.Methods[0]
                      match method0.Body with
                      | AST.Body(bodyExpr) -> 
                        Expect.equal bodyExpr.Exprs.Count 1 "should have one form in the body"
                        let e0 = bodyExpr.Exprs[0]
                        Expect.isTrue e0.IsKeywordInvoke "should be an KeywordInvoke"
                      | _ -> failtest "Expected body form to be a Body"
                  | _ -> failtest "Should be an Obj"

              finally
                Var.popThreadBindings() |> ignore

          testCase "fn* propagates :rettag, detects variadic "
          <| fun _ ->

              let cctx = CompilerEnv.Create(Expression)

              let form = ReadFromString "^{:rettag int :tag double} ( fn* ([x & y] x))"  // normally defn would move :tag to :rettag, but we can test both pieces here
              let ast = Parser.Analyze(cctx,form)

              match ast with
              | AST.Obj(Env = env; Form = form; Type = typ; Internals = internals; Register = register; sourceInfo = sourceInfo) ->
                  Expect.equal internals.Methods.Count 1 "Should have one method"
                  Expect.isTrue (internals.VariadicMethod.IsSome) "Should  have a variadic method"
                  Expect.isNotNull (internals.Tag) "Should have a tag"
                  Expect.equal internals.Tag (Symbol.intern("double")) "Should have double tag"
                  let method0 = internals.Methods[0]
                  Expect.equal method0.Type Fn "Should be of type Fn"
                  Expect.isFalse method0.UsesThis "Does not use this"
                  Expect.isTrue method0.RestParam.IsSome "Should not have a rest param"
                  Expect.equal method0.ReqParams.Count 1 "Should have one required param"
                  Expect.equal method0.ArgLocals.Count 2 "Should have two arg local"
                  Expect.equal method0.RetType typeof<int32> "Should have int32 return type"
                  Expect.isTrue method0.IsVariadic "Should be variadic"
                  Expect.equal method0.NumParams 2 "Should have two param"
              | _ -> failtest "Should be an Obj"


          testCase " :tag on parameter list overrides :rettag"
          <| fun _ ->

              let cctx = CompilerEnv.Create(Expression)

              let form = ReadFromString "^{:rettag int} ( fn* ( ^double [x & y] x))"  // normally defn would move :tag to :rettag, but we can test both pieces here
              let ast = Parser.Analyze(cctx,form)

              match ast with
              | AST.Obj(Env = env; Form = form; Type = typ; Internals = internals; Register = register; sourceInfo = sourceInfo) ->
                  Expect.isNull (internals.Tag) "Should not have a tag"
                  let method0 = internals.Methods[0]
                  Expect.equal method0.RetType typeof<double> "Should have int32 return type"
              | _ -> failtest "Should be an Obj"


          testCase "fn* has closed-over locals"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let ns1Name = ns1.Name.Name
              let abcVar = ns1.findInternedVar abcSym

              let cctx = CompilerEnv.Create(Expression)

              let pqrsSym = Symbol.intern ("pqrs")

              let lbPqrs =
                  { Sym = pqrsSym
                    Tag = null
                    Init = None
                    Name = pqrsSym.Name
                    IsArg = false
                    IsByRef = false
                    IsRecur = false
                    IsThis = false
                    Index = 20 }

              let newLocals = RTMap.assoc (cctx.Locals, pqrsSym, lbPqrs) :?> IPersistentMap
              let cctx = { cctx with Locals = newLocals }

              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try

                  let form = ReadFromString "(fn* ([x] (abc pqrs)))"
                  let ast = Parser.Analyze(cctx,form)

                  match ast with
                  | AST.Obj(Env = env; Form = form; Type = typ; Internals = internals; Register = register; sourceInfo = sourceInfo) ->

                      Expect.equal (register.Closes.count()) 1 "Should have one Closes"
                      let keys = RTMap.keys(register.Closes)
                      let key0 = RTSeq.first(keys) :?> LocalBinding
                      Expect.equal key0.Sym pqrsSym "Should have pqrs in closes"
                      Expect.equal (register.Constants.count()) 1 "Should have one Constants"
                      Expect.equal register.ConstantIds.Count 1 "Should  have one ConstantIds"
                      Expect.equal (register.Keywords.count()) 0 "Should have no Keywords"
                      Expect.equal (register.Vars.count()) 1 "Should have one Vars"
                      Expect.equal (register.KeywordCallsites.count()) 0 "Should have no KeywordCallsites"
                      Expect.equal (register.ProtocolCallsites.count()) 0 "Should not have any ProtocolCallsites"
                      Expect.equal (register.VarCallsites.count()) 0 "Should not have any VarCallsites"
                      let method0 = internals.Methods[0]
                      match method0.Body with
                      | AST.Body(bodyExpr) -> 
                        Expect.equal bodyExpr.Exprs.Count 1 "should have one form in the body"
                        let e0 = bodyExpr.Exprs[0]
                        Expect.isTrue e0.IsInvoke "should be an Invoke"
                      | _ -> failtest "Expected body form to be a Body"
                  | _ -> failtest "Should be an Obj"

              finally
                Var.popThreadBindings() |> ignore

          testCase "closed-overs should propagate to parent"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let ns1Name = ns1.Name.Name
              let abcVar = ns1.findInternedVar abcSym

              let cctx = CompilerEnv.Create(Expression)

              let pqrsSym = Symbol.intern ("pqrs")

              let lbPqrs =
                  { Sym = pqrsSym
                    Tag = null
                    Init = None
                    Name = pqrsSym.Name
                    IsArg = false
                    IsByRef = false
                    IsRecur = false
                    IsThis = false
                    Index = 20 }

              let newLocals = RTMap.assoc (cctx.Locals, pqrsSym, lbPqrs) :?> IPersistentMap
              let cctx = { cctx with Locals = newLocals }

              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try

                  let form = ReadFromString "(fn* ([x] (fn* [y] (abc pqrs))))"
                  let ast = Parser.Analyze(cctx,form)

                  match ast with
                  | AST.Obj(Env = env; Form = form; Type = typ; Internals = internals; Register = register; sourceInfo = sourceInfo) ->

                      Expect.equal (register.Closes.count()) 1 "Should have one Closes"
                      let keys = RTMap.keys(register.Closes)
                      let key0 = RTSeq.first(keys) :?> LocalBinding
                      Expect.equal key0.Sym pqrsSym "Should have pqrs in closes"
                      Expect.equal (register.Constants.count()) 0 "Should have no Constants"
                      Expect.equal register.ConstantIds.Count 0 "Should  have no ConstantIds"
                      Expect.equal (register.Keywords.count()) 0 "Should have no Keywords"
                      Expect.equal (register.Vars.count()) 0 "Should have no Vars"
                      Expect.equal (register.KeywordCallsites.count()) 0 "Should have no KeywordCallsites"
                      Expect.equal (register.ProtocolCallsites.count()) 0 "Should not have any ProtocolCallsites"
                      Expect.equal (register.VarCallsites.count()) 0 "Should not have any VarCallsites"
                      let method0 = internals.Methods[0]
                      match method0.Body with
                      | AST.Body(bodyExpr) -> 
                        Expect.equal bodyExpr.Exprs.Count 1 "should have one form in the body"
                        let e0 = bodyExpr.Exprs[0]
                        Expect.isTrue e0.IsObj "should be an Obj (fn)"
                      | _ -> failtest "Expected body form to be a Body"
                  | _ -> failtest "Should be an Obj"

              finally
                Var.popThreadBindings() |> ignore

          testCase "inner method should see closed-over from parent local"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let ns1Name = ns1.Name.Name
              let abcVar = ns1.findInternedVar abcSym

              let cctx = CompilerEnv.Create(Expression)

              let pqrsSym = Symbol.intern ("pqrs")

              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

              try

                  let form = ReadFromString "(fn* ([pqrs] (fn* [y] (abc pqrs))))"
                  let ast = Parser.Analyze(cctx,form)

                  match ast with
                  | AST.Obj(Env = env; Form = form; Type = typ; Internals = internals; Register = register; sourceInfo = sourceInfo) ->
                      Expect.equal (register.Closes.count()) 0 "Should have no Closes"
                      let method0 = internals.Methods[0]
                      match method0.Body with
                      | AST.Body(bodyExpr) -> 
                        Expect.equal bodyExpr.Exprs.Count 1 "should have one form in the body"
                        match bodyExpr.Exprs[0] with
                        | AST.Obj(Env = env; Form = form; Type = typ; Internals = internals; Register = register; sourceInfo = sourceInfo) ->
                            Expect.isTrue internals.HasEnclosingMethod "Inner has enclosing method"
                            Expect.equal (register.Closes.count()) 1 "Internal should have one close"
                            let keys = RTMap.keys(register.Closes)
                            let key0 = RTSeq.first(keys) :?> LocalBinding
                            Expect.equal key0.Sym pqrsSym "Should have pqrs in closes"
                        | _ -> failtest "Expected form to be an Fn (Obj)"
                      | _ -> failtest "Expected body form to be a Body"
                  | _ -> failtest "Should be an Obj"

              finally
                Var.popThreadBindings() |> ignore

          testCase "this test case was used in a blog post explaining symbol interpretation"
          <| fun _ ->    
            let ns1Name = "ns1"
            let ns2Name = "big.deal.namespace"

            let ns1 = Namespace.findOrCreate (Symbol.intern (ns1Name))
            let ns2 = Namespace.findOrCreate (Symbol.intern (ns2Name))

            let ns2Sym = Symbol.intern "ns2"
            ns1.addAlias(ns2Sym, ns2)
            
            let fSym = Symbol.intern "f"
            let gSym = Symbol.intern "g"
            let hSym = Symbol.intern "h"

            ns1.intern (fSym) |> ignore
            ns2.intern (gSym) |> ignore
            ns2.intern (hSym) |> ignore



            let stringSym = Symbol.intern "String"
            ns1.importClass (stringSym, typeof<System.String>) |> ignore

            Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))

            try

                let form = ReadFromString "(fn* [x] 
                                              (let* [y  7]
                                                (f (ns2/g Int64/MaxValue y) 
                                                   (String/.ToUpper x)
                                                   (big.deal.namespace/h System.Text.StringBuilder))))"
                let ast = Parser.Analyze(CompilerEnv.Create(Expression), form)
                Expect.isTrue ast.IsObj "Should have an AST"

                let tw = new System.IO.StringWriter()
                //ExprUtils.DebugPrint(tw, ast)
                let s = tw.ToString()
                Expect.isNotNull s "Should have a string"

            finally
                Var.popThreadBindings() |> ignore



        ]