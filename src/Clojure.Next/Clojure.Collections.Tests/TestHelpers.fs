module TestHelpers

open Expecto
open System
open Clojure.Collections


// Some helpers for testing ISeqs

let verifyISeqContents (s: ISeq) (vals: obj list) =
    let avals = List.toArray vals

    let rec loop (s: ISeq) (idx: int) =
        match s with
        | null -> idx
        | _ ->
            Expect.equal (s.first ()) avals.[idx] "Expected element"
            loop (s.next ()) (idx + 1)

    let cnt = loop s 0
    Expect.equal cnt avals.Length "Should have same number of elements"

let compareISeqs (s1:ISeq) (s2:ISeq) =
    let rec loop (s1:ISeq) (s2:ISeq) =
        match s1, s2 with
        | null, null -> true
        | null, _ -> false
        | _, null -> true
        | _, _ when s1.first() = s2.first() -> loop (s1.next()) (s2.next())
        | _ -> false
    loop s1 s2


let verifyISeqCons (s: ISeq) (newVal: obj) (vals: obj list) =
    let newSeq = s.cons (newVal)
    Expect.equal (newSeq.first ()) newVal "First value of cons"
    verifyISeqContents (newSeq.next ()) vals

let verifyISeqRestTypes (s: ISeq) (t: Type) =
    let rec loop (s: ISeq) =
        match s.next () with
        | null -> ()
        | _ ->
            Expect.equal (s.next().GetType()) t "Next should have given type"
            loop (s.next ())

    loop s

let verifyIseqRestMaintainsMeta (s: ISeq) =
    let m = (s :?> IMeta).meta ()

    let rec loop (s: ISeq) =
        match s.next () with
        | null -> ()
        | _ ->
            Expect.isTrue (Object.ReferenceEquals((s.next () :?> IMeta).meta (), m)) "Next should have same meta"
            loop (s.next ())

    loop s


let takeEager (n: int) (s: ISeq) =
    let arr: obj array = Array.zeroCreate n

    let rec loop (i: int) (s: ISeq) =
        if isNull s || i >= n then
            i
        else
            arr[i] <- s.first ()
            loop (i + 1) (s.next ())

    let cnt = loop 0 s

    arr |> Seq.cast<obj> |> Seq.take cnt

// Some Helpers for testing IObj

// Defer these until we have maps available.

//let metaForSimpleTests =
//    let keys: obj list = [ "a"; "b" ]
//    let vals: obj list = [ "AAA"; "BBB" ]
//    SimpleMap(keys, vals)

//let verifyWithMetaHasCorrectMeta (io: IObj) =
//    let newIO = io.withMeta (metaForSimpleTests)
//    Expect.isTrue (Object.ReferenceEquals(newIO.meta (), metaForSimpleTests)) "should have same meta"

//let verifyWithMetaNoChange (io: IObj) =
//    let io2 = io.withMeta (io.meta ())
//    Expect.isTrue (Object.ReferenceEquals(io2, io)) "Expect same object back from withMeta if no change in meta"

//let verifyNullMeta (io: IObj) =
//    Expect.isNull (io.meta ()) "Meta expected to be null"

//let verifyWithMetaReturnType (io: IObj) (t: Type) =
//    let io2 = io.withMeta (metaForSimpleTests)
//    Expect.equal (io2.GetType()) t "Expected type for withMeta"
