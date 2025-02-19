module Clojure.Collections.Util

open System
open System.Collections
open Clojure.Numerics

// This is a minimal set of operations from the original clojure.lang.Util class
// Just enough to get us started on implementing collections.


/// Compare for equality.  Handles nulls.
let equals (x : obj, y  : obj ) =
    LanguagePrimitives.PhysicalEquality x y || not (isNull x) && x.Equals(y)

/// Equality check for collections.  Defers to IPersistentCollection.equiv when possible.
let pcequiv (k1: obj, k2: obj) =
    match k1, k2 with
    | :? IPersistentCollection as pc1, _ -> pc1.equiv (k2)
    | _, (:? IPersistentCollection as pc2) -> pc2.equiv (k1)
    | _ -> k1.Equals(k2)

/// Compare for equivalence.  Handles nulls, numbers, collections, and other objects.
let equiv (k1: obj, k2: obj) =
    if LanguagePrimitives.PhysicalEquality k1 k2 then
        true
    elif isNull k1 then
        false
    elif Numbers.IsNumeric k1 && Numbers.IsNumeric k2 then
        Numbers.equal (k1, k2)
    elif k1 :? IPersistentCollection || k2 :? IPersistentCollection then
        pcequiv (k1, k2)
    else
        k1.Equals(k2)

// TODO: figure out if we really need this nameForType.

/// Get the name for a type. I think this is mostly to handle nested types properly.
let nameForType (t: Type) =
    //| null -> "<null>"  // prior version printed a message
    if t.IsNested then
        let fullName = t.FullName
        let index = fullName.LastIndexOf('.')
        fullName.Substring(index + 1)
    else
        t.Name

/// Compare two objects.  Handles nulls, numbers, and IComparables only.
let compare(k1:obj,k2:obj) =
    match k1, k2 with
    | _,_ when LanguagePrimitives.PhysicalEquality k1 k2 -> 0
    | _, null -> 1
    | null, _ -> -1
    | _,_ when Numbers.IsNumeric(k1) -> Numbers.compare(k1,k2)
    | _ -> (k1 :?> IComparable).CompareTo(k2)


/// Type of an equivalence predicate.
type EquivPred = (obj * obj) -> bool

/// Equivalence predicate for nulls.
let private equivNull(k1,k2) = isNull k2

/// Equivalence predicate using Object.Equals.
let private equivEquals(k1,k2) = k1.Equals(k2)

/// Equivalence predicate for numbers.
let private equivNumber(k1,k2) = Numbers.IsNumeric(k2) && Numbers.equal(k1,k2)

/// Equivalence predicate for collections.
let private equivColl(k1:obj,k2:obj) = 
    if k1 :? IPersistentCollection || k2 :? IPersistentCollection then
        pcequiv(k1,k2)
    else
        k1.Equals(k2)

/// Get an equivalence predicate for a key, depending on the type of the key.
let getEquivPred(k1:obj) =
    match k1 with
    | null -> equivNull
    | _ when Numbers.IsNumeric(k1) -> equivNumber
    | :? String -> equivEquals                      // Java/C# has also Symbol here, but we don't have symbol at this point. will fall through to same at default
    | :? ICollection
    | :? IDictionary -> equivColl
    | _ -> equivEquals

/// Get a hash code for an object.  Handles nulls, numbers, strings, and IHashEq objects.  Defaults to Object.GetHashCode.
let hasheq(o:obj) : int =
    match o with
    | null -> 0
    | :? IHashEq as he -> he.hasheq()
    | _ when Numbers.IsNumeric(o) -> Numbers.hasheq(o)
    | :? string as s -> Murmur3.HashString(s)
    | _ -> o.GetHashCode()