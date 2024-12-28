module NamespaceTests

open Expecto
open Clojure.Collections
open Clojure.Lib

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
    testList
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



            ]
