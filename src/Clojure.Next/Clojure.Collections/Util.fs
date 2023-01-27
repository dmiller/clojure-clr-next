module Clojure.Collections.Util

open System
open System.Collections
open Clojure.Numerics

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
