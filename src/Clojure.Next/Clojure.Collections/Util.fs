module Clojure.Collections.Util

open System
open System.Collections
open Clojure.Numerics


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
    | :? String as s -> Murmur3.HashInt(s.GetHashCode())
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

let equals (x, y) =
    obj.ReferenceEquals(x, y) || x <> null && x.Equals(y)


let pcequiv (k1: obj, k2: obj) =
    match k1, k2 with
    | :? IPersistentCollection as pc1, _ -> pc1.equiv (k2)
    | _, (:? IPersistentCollection as pc2) -> pc2.equiv (k1)
    | _ -> k1.Equals(k2)

let equiv (k1: obj, k2: obj) =
    if Object.ReferenceEquals(k1, k2) then
        true
    elif isNull k1 then
        false
    elif Numbers.IsNumeric k1 && Numbers.IsNumeric k2 then
        Numbers.equal (k1, k2)
    elif k1 :? IPersistentCollection || k2 :? IPersistentCollection then
        pcequiv (k1, k2)
    else
        k1.Equals(k2)


let nameForType (t: Type) =
    //| null -> "<null>"  // prior version printed a message
    if t.IsNested then
        let fullName = t.FullName
        let index = fullName.LastIndexOf('.')
        fullName.Substring(index + 1)
    else
        t.Name
