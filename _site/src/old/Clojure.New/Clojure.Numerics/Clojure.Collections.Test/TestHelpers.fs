module TestHelpers

open Expecto
open System
open Clojure.Collections
open Clojure.Collections.Simple

// Some helpers for testing ISeqs

let verifyISeqContents (s: ISeq) (vals: obj list) =
    let avals = List.toArray vals

    let rec step (s: ISeq) (idx: int) =
        match s with
        | null -> idx
        | _ ->
            Expect.equal (s.first ()) avals.[idx] "Expected element"
            step (s.next ()) (idx + 1)

    let cnt = step s 0
    Expect.equal cnt avals.Length "Should have same number of elements"


let verifyISeqCons (s: ISeq) (newVal: obj) (vals: obj list) =
    let newSeq = s.cons (newVal)
    Expect.equal (newSeq.first ()) newVal "First value of cons"
    verifyISeqContents (newSeq.next ()) vals

let verifyISeqRestTypes (s: ISeq) (t: Type) =
    let rec step (s: ISeq) =
        match s.next () with
        | null -> ()
        | _ ->
            Expect.equal (s.next().GetType()) t "Next should have given type"
            step (s.next ())

    step s

let verifyIseqRestMaintainsMeta (s: ISeq) =
    let m = (s :?> IMeta).meta()

    let rec step (s: ISeq) =
        match s.next () with
        | null -> ()
        | _ ->
            Expect.isTrue (Object.ReferenceEquals((s.next () :?> IMeta).meta(), m)) "Next should have same meta"
            step (s.next ())

    step s


// Some Helpers for testing IObj

let metaForSimpleTests =
    let keys: obj list = [ "a"; "b" ]
    let vals: obj list = [ "AAA"; "BBB" ]
    SimpleMap(keys, vals)

let verifyWithMetaHasCorrectMeta (io: IObj) =
    let newIO = io.withMeta (metaForSimpleTests)
    Expect.isTrue (Object.ReferenceEquals(newIO.meta (), metaForSimpleTests)) "should have same meta"

let verifyWithMetaNoChange (io: IObj) =
    let io2 = io.withMeta (io.meta ())
    Expect.isTrue (Object.ReferenceEquals(io2, io)) "Expect same object back from withMeta if no change in meta"

let verifyNullMeta (io: IObj) =
    Expect.isNull (io.meta ()) "Meta expected to be null"

let verifyWithMetaReturnType (io: IObj) (t: Type) =
    let io2 = io.withMeta (metaForSimpleTests)
    Expect.equal (io2.GetType()) t "Expected type for withMeta"
