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



let mutable namespaceCounter = 0

let nextNamespaceNum () =
    Interlocked.Increment(&namespaceCounter)

let abcSym = Symbol.intern ("abc")
let defSym = Symbol.intern ("def")
let pqrSym = Symbol.intern ("pqr")
let impSym = Symbol.intern ("importedType")
let macroSym = Symbol.intern("macroV")
let constSym = Symbol.intern("constV")
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
    ns1.importClass(impSym, typeof<System.Text.StringBuilder>) |> ignore

    let mv = ns1.intern(macroSym)
    mv.setMacro()

    let mc = ns1.intern(constSym)
    (mc :> IReference).resetMeta( (mc :> IMeta).meta().assoc(Keyword.intern (null, "const"),true)) |> ignore

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
              Expect.isTrue (RT0.booleanCast((con :?> IMeta).meta().valAt(Keyword.intern(null,"const")))) "Should have :const true"
              
              let pqr = ns2.getMapping(pqrSym)
              Expect.isNotNull pqr "Should find pqr in ns2"
              Expect.isTrue (pqr :? Var) "Should map to a Var"

              let privateVar = ns2.getMapping (privateSym)
              Expect.isNotNull privateVar "Should find private var"
              Expect.isTrue (privateVar :? Var) "Should be a Var"
              Expect.isFalse ((privateVar :?> Var).isPublic) "Should not be public"

          testCase "Namespace alias, var not found => throws"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let sym = Symbol.intern (ns2Sym.Name, "fred")
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

          testCase "no namespace sym, not mapped, unresolved vars allowd => just return the s"
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

              let sym = Symbol.intern (ns1.Name.Name, "fred")
              let v = Parser.LookupVar(cctx, sym, false)
              Expect.isNull v "Should not find var not in named namespace"

          testCase "Sym /w namespace, does not exist in that namespace, namespace is current nameespace, internNew = true  => creates Var, returns var"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)

              let sym = Symbol.intern (ns1.Name.Name, "fred")

              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))
               
              try 
                  let v = Parser.LookupVar(cctx, sym, true)
                  Expect.isNotNull v "Should find var not in named namespace"
                  Expect.isNotNull (ns1.findInternedVar(Symbol.intern(null,"fred"))) "Should have created var in current namespace"
              finally
                  Var.popThreadBindings () |> ignore

          testCase "Sym /w namespace, does not exist in that namespace, namespace is current nameespace, internNew = false  => null"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)

              let sym = Symbol.intern (ns1.Name.Name, "fred")

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

              let v1 = Parser.LookupVar(cctx,RTVar.NsSym, false)
              Expect.equal v1 RTVar.NsVar "Should find ns var"

              let v2 = Parser.LookupVar(cctx, RTVar.InNsSym, false)
              Expect.equal v2 RTVar.InNSVar "Should find in-ns var"

          testCase "Sym w/o namespace, no mapping in current NS, internNew is false => null"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)

              let sym = Symbol.intern (null, "fred")

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

              let sym = Symbol.intern (null, "fred")

              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))
               
              try 
                  let v = Parser.LookupVar(cctx, sym, true)
                  Expect.isNotNull v "Should find var not in current namespace"
                  Expect.isNotNull (ns1.findInternedVar(sym)) "Should have created var in current namespace"
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
                  Expect.throwsT<InvalidOperationException> (fun _ ->  Parser.LookupVar(cctx, impSym, true) |> ignore) "Should throw with non-var in current ns"

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
                  Expect.isTrue (register.Vars.containsKey(v)) "Should register var"

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
                  Expect.isFalse (register.Vars.containsKey(v)) "Should not register var"

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
                  Expect.isTrue (register.Vars.containsKey(v)) "Should register var"

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

          testCase
              "Parses ^[String int] TypeName/FieldName with Fieldname not found as static QualifiedMethod, with HintedSig"
          <| fun _ ->
              let form = ReadFromString "^[String int] Int64/asdf"
              let cctx = CompilerEnv.Create(Expression)
              let ast = Parser.Analyze(cctx, form)

              compareQualifiedMethods (
                  ast,
                  (Expr.QualifiedMethod(
                      Env = cctx,
                      Form = form,
                      MethodType = typeof<Int64>,
                      HintedSig =
                          (SignatureHint.MaybeCreate(cctx, PersistentVector.create (typeof<String>, typeof<Int32>))),
                      MethodSymbol = Symbol.intern ("Int64/asdf"),
                      MethodName = "asdf",
                      Kind = Static,
                      TagClass = typeof<AFn>,
                      SourceInfo = None
                  ))
              )

          testCase "Detects local bindings."
          <| fun _ ->

              let cctx = CompilerEnv.Create(Expression)

              let register = ObjXRegister(None)
              let internals = ObjXInternals()

              let objx =
                  Expr.Obj(
                      Env = cctx,
                      Form = null,
                      Type = ObjXType.Fn,
                      Internals = internals,
                      Register = register,
                      SourceInfo = None
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
                  (Expr.LocalBinding(Env = cctx, Form = form0, Binding = lbThis, Tag = null))
                  "Should find binding for this"

              let ast1 = Parser.Analyze(cctx, form1)

              Expect.equal
                  ast1
                  (Expr.LocalBinding(Env = cctx, Form = form1, Binding = lbAsdf, Tag = null))
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

              let register = ObjXRegister(None)
              let internals = ObjXInternals()

              let objx =
                  Expr.Obj(
                      Env = cctx,
                      Form = null,
                      Type = ObjXType.Fn,
                      Internals = internals,
                      Register = register,
                      SourceInfo = None
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
                  (Expr.LocalBinding(Env = cctx, Form = form0, Binding = lbThis, Tag = null))
                  "Should find binding for this"

              let ast1 = Parser.Analyze(cctx, form1)

              Expect.equal
                  ast1
                  (Expr.LocalBinding(Env = cctx, Form = form1, Binding = lbAsdf, Tag = null))
                  "Should find binding for asdf"

              let ast2 = Parser.Analyze(cctx, pqrsSym)

              Expect.equal
                  ast2
                  (Expr.LocalBinding(Env = cctx, Form = pqrsSym, Binding = lbPqrs, Tag = null))
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

                Expect.throwsT<CompilerException>(fun _ -> Parser.Analyze(cctx, macroSym) |> ignore) "Should throw with non-local, non-Type, resolves to Var, is macro"

              finally
                  Var.popThreadBindings () |> ignore

          testCase "non-local, non-Type, resolves to Var, is const =>  Analyzes 'V, returns Literal"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)


              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))
              try 

                let constVar = ns1.findInternedVar(constSym)
                let ast = Parser.Analyze(cctx, constSym)

                Expect.equal ast (Expr.Literal(Env = cctx, Form = constVar, Value = constVar, Type = OtherType)) "Should return a Literal"

              finally
                  Var.popThreadBindings () |> ignore


          testCase "non-local, non-Type, resolves to Var, =>  returns Expr.Var"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)


              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1))
              try 

                let abcVar = ns1.findInternedVar(abcSym)
                let ast = Parser.Analyze(cctx, abcSym)

                Expect.equal ast (Expr.Var(Env = cctx, Form = abcSym, Var = abcVar, Tag = null)) "Should return an Expr.Var"

              finally
                  Var.popThreadBindings () |> ignore


          testCase "non-local, non-Type, resolves to symbol (allow-unresolved = true) =>  Expr.UnresolvedVar"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)


              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1, RTVar.AllowUnresolvedVarsVar, true))
              try 

                let sym = Symbol.intern(null, "fred")
                let ast = Parser.Analyze(cctx, sym)

                Expect.equal ast (Expr.UnresolvedVar(Env = cctx, Form = sym, Sym = sym)) "Should return an UnresolvedVar"

              finally
                  Var.popThreadBindings () |> ignore



          testCase "non-local, non-Type, resolves to symbol (allow-unresolved = false) =>  throws"
          <| fun _ ->
              let ns1, ns2 = createTestNameSpaces ()
              let cctx = CompilerEnv.Create(Expression)


              Var.pushThreadBindings (RTMap.map (RTVar.CurrentNSVar, ns1, RTVar.AllowUnresolvedVarsVar, false))
              try 

                let sym = Symbol.intern(null, "fred")
                Expect.throwsT<CompilerException> (fun _ -> Parser.Analyze(cctx, sym) |> ignore) "Should throw with non-local, non-Type, resolves to symbol (allow-unresolved = false)"

              finally
                  Var.popThreadBindings () |> ignore

          ]
          
