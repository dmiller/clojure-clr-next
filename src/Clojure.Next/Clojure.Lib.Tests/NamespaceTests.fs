module NamespaceTests

open Expecto
open Clojure.Collections
open Clojure.Lib
open System

// Because these tests maniuplate global tests, we have to run them sequentially

let removeNamespaces () =
    let all = Namespace.All
    let coreNS = Namespace.findOrCreate (Symbol.intern("clojure.core"))

    let rec loop (s: ISeq) =
        match s with
        | null -> ()
        | _ ->
            let ns = s.first() :?> Namespace
            if ns <> coreNS then
                Namespace.remove ns.Name |> ignore
            loop (s.next())
    loop all

[<Tests>]
let BasicNamespaceTests =
    testSequenced <|  testList
        "Basic Namespace Tests"
        [

          testCase "findOrCreate creates a new namespace"
          <| fun _ ->
            removeNamespaces()
            let sym = Symbol.intern "abc"
            let ns = Namespace.findOrCreate sym
            Expect.isNotNull ns "We should find the namespace"
            Expect.equal ns.Name sym "The namespace name should be the same as the symbol name input to find"


          testCase "findOrCreate finds an existing namespace"
          <| fun _ ->
            removeNamespaces()
            let sym = Symbol.intern "abc"
            let ns1 = Namespace.findOrCreate sym
            let ns2 = Namespace.findOrCreate sym
            Expect.equal ns1 ns2 "The namespace should be the same when found twice"

          testCase "find returns null on non-existent namespace"
          <| fun _ ->
            removeNamespaces()
            let sym = Symbol.intern "abc"
            let ns1 = Namespace.find sym
            Expect.isNull ns1 "should return null when looking for non-existent namespace"


          testCase "find finds an existing namespace"
          <| fun _ ->
            removeNamespaces()
            let sym = Symbol.intern "abc"
            let ns1 = Namespace.findOrCreate sym
            let ns2 = Namespace.find sym
            Expect.equal ns1 ns2 "find should find created namespace"


          testCase "remove removes an existing namespace"
          <| fun _ ->
            removeNamespaces()
            let sym1 = Symbol.intern "abc"
            let sym2 = Symbol.intern "def"
            let ns1 = Namespace.findOrCreate sym1
            let ns2 = Namespace.findOrCreate sym2

            let ns3 = Namespace.remove(sym1)
            let ns4 = Namespace.find(sym1)
            let ns5 = Namespace.find(sym2)

            Expect.equal ns1 ns3 "remove returns the removed namespace"
            Expect.isNull ns4 "the removed namespace is not found"
            Expect.isNotNull ns5  "namespaces not removed still exist"

          testCase "remove on non-existent namespace returns null"
          <| fun _ ->
            removeNamespaces()
            let sym1 = Symbol.intern "abc"
            let ns1 = Namespace.remove(sym1)

            Expect.isNull ns1 "Removing non-existing namespace should return null"

          testCase "remove of clojure.core throws"
          <| fun _ ->
            removeNamespaces()
            let sym1 = Symbol.intern "clojure.core"
            
            Expect.throwsT<ArgumentException> (fun () -> Namespace.remove(sym1) |> ignore)  "remove clojure.core throws ArgumentException"


          testCase "interning symbol with namespace fails"
          <| fun _ ->
            removeNamespaces()
            let sym = Symbol.intern( "abc", "def")
            let ns = Namespace.findOrCreate(Symbol.intern("ghi"))
            Expect.throwsT<ArgumentException> (fun () -> ns.intern(sym) |> ignore)  "interning symbol with namespace fails"

          testCase "interning symbol creates Var"
          <| fun _ ->
            removeNamespaces()
            let sym = Symbol.intern("def")
            let ns = Namespace.findOrCreate(Symbol.intern("abc"))
            let v = ns.intern(sym)
            
            Expect.isNotNull v "interning symbol creates Var"
            Expect.equal (v.Namespace) ns "Var namespace is the same as the namespace it was interned in"
            Expect.equal (v.Name) sym "Var name is the same as the symbol it was interned with"

          testCase "interning symbol enters Var in map"
          <| fun _ ->
            removeNamespaces()
            let sym = Symbol.intern("def")
            let ns = Namespace.findOrCreate(Symbol.intern("abc"))
            let v = ns.intern(sym)
            let v2 = ns.findInternedVar(sym)

            Expect.equal v v2 "interning symbol enters Var in map"

          testCase "interning symbol agaom finds Var"
          <| fun _ ->
            removeNamespaces()
            let sym = Symbol.intern("def")
            let ns = Namespace.findOrCreate(Symbol.intern("abc"))
            let v1 = ns.intern(sym)
            let v2 = ns.intern(sym)

            Expect.equal v1 v2 "interning symbol again finds Var"

          testCase "referring symbol to var in other ns and then interning prints warning to *err*"
          <| fun _ ->
            removeNamespaces()

            // We need to capture the error stream
            let ms = new System.IO.MemoryStream()
            let tw = new System.IO.StreamWriter(ms)
            RTVar.ErrVar.bindRoot(tw)


            let sym1 = Symbol.intern("def")
            let sym2 = Symbol.intern("ghi")
            let ns1 = Namespace.findOrCreate(Symbol.intern("abc"))
            let ns2 = Namespace.findOrCreate(Symbol.intern("jkl"))

            let v1 = ns1.intern(sym1)
            ns2.refer(sym2, v1) |> ignore
            ns2.intern(sym2) |> ignore

            let s = System.Text.Encoding.UTF8.GetString(ms.ToArray())
            Expect.stringContains s "WARNING: ghi already refers to: #'abc/def in namespace: jkl, being replaced by: #'jkl/ghi" "should get warning"


          testCase "refer of symbol with namespace fails"
          <| fun _ ->
            removeNamespaces()

            let sym = Symbol.intern("def", "ghi")
            let ns1 = Namespace.findOrCreate(Symbol.intern("abc"))
            Expect.throwsT<ArgumentException> (fun () -> ns1.refer(sym, Var.create()) |> ignore)  "refer of symbol with namespace fails"

          testCase "refer enters Var"
          <| fun _ ->
            removeNamespaces()

            let ns = Namespace.findOrCreate(Symbol.intern("abc"))
            let sym = Symbol.intern("def")
            let v = Var.create()
            ns.refer(sym, v) |> ignore

            Expect.equal (ns.getMapping(sym)) v "refer enters Var"


          testCase "importClass on symbol with namespace fails"
          <| fun _ ->
            removeNamespaces()

            let ns = Namespace.findOrCreate(Symbol.intern("ghi"))
            let sym = Symbol.intern("abc", "def")

            Expect.throwsT<ArgumentException> (fun () -> ns.importClass(sym, typeof<Int32>) |> ignore)  "importClass on symbol with namespace fails"


          testCase "importClass enters type"
          <| fun _ ->
            removeNamespaces()

            let ns = Namespace.findOrCreate(Symbol.intern("ghi"))
            let sym = Symbol.intern("abc")
            ns.importClass(sym, typeof<Int32>) |> ignore

            Expect.equal (ns.getMapping(sym)) typeof<Int32> "importClass enters type"


          testCase "findInternedVar fails if non-Var value in map"
          <| fun _ ->
            removeNamespaces()

            let ns = Namespace.findOrCreate(Symbol.intern("ghi"))
            let sym = Symbol.intern("abc")
            ns.importClass(sym, typeof<Int32>) |> ignore

            let v = ns.findInternedVar(sym)

            Expect.isNull v "findInternedVar fails if non-Var value in map"


            ]