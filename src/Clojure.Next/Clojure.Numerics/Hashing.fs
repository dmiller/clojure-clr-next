module Clojure.Numerics.Hashing

open System
open System.Collections

// This code was all originally in clojure.lang.Util.
// Moved it here because it makes sense to have it in Clojure.Numerics
// along with Murmur3.  Also have moved the IHashEq interface here,
//  so it is not possible for it to be here.

let hash x =
    match x with
    | null -> 0
    | _ -> x.GetHashCode()

//a la boost
let hashCombine (seed: int, hash: int) =
    seed ^^^ (hash + 0x9e3779b9 + (seed <<< 6) + (seed >>> 2))


let hasheq (o: obj) : int =
    match o with
    | null -> 0
    | :? IHashEq as ihe -> ihe.hasheq ()
    | x when Numbers.IsNumeric(x) -> Numbers.hasheq (x)
    | :? String as s -> Murmur3.HashString(s)
    | _ -> o.GetHashCode()

let hashOrderedU (xs: IEnumerable) : uint =

    let mutable n: int = 0
    let mutable hash: uint = 1u

    for x in xs do
        hash <- 31u * hash + uint (hasheq (x))
        n <- n + 1

    Murmur3.finalizeCollHash hash n

let hashUnorderedU (xs: IEnumerable) : uint =

    let mutable n: int = 0
    let mutable hash: uint = 0u

    for x in xs do
        hash <- hash + uint (hasheq (x))
        n <- n + 1

    Murmur3.finalizeCollHash hash n


let hashOrdered (xs: IEnumerable) : int = hashOrderedU xs |> int
let hashUnordered (xs: IEnumerable) : int = hashUnorderedU xs |> int
