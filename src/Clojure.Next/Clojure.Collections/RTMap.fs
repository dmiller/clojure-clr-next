namespace Clojure.Collections

open System

/// Runtime support for maps, sets, and vectors.
[<AbstractClass; Sealed>]
type RTMap() =

    /// Create a map from a sequence of key-value pairs in an array, checking for duplicates.
    static member map([<ParamArray>] init: obj array) : IPersistentMap =
        if isNull init || init.Length = 0 then
            PersistentArrayMap.Empty
        elif init.Length <= PersistentArrayMap.HashtableThreshold then
            PersistentArrayMap.createWithCheck init
        else
            PersistentHashMap.createWithCheck init

    /// Create a map from a sequence of key-value pairs in an array, without checking for duplicates.
    static member mapUniqueKeys([<ParamArray>] init: obj array) : IPersistentMap =
        if isNull init then
            PersistentArrayMap.Empty
        elif init.Length <= PersistentArrayMap.HashtableThreshold then
            PersistentArrayMap(init)
        else
            PersistentHashMap.create init

    /// Create a KeySeq from a collection
    static member keys(coll: obj) =
        match coll with
        | :? IPersistentMap as ipm -> KeySeq.createFromMap (ipm)
        | _ -> KeySeq.create (RTSeq.seq (coll))

    /// Createa set from a sequence of values in an array, checking for duplicates.
    static member set([<ParamArray>] init: obj array) : IPersistentSet = PersistentHashSet.createWithCheck init

    /// Create an IPersistentVector from an array, with the array being owned by the vector.
    static member vector([<ParamArray>] init: obj array) : IPersistentVector =
        LazilyPersistentVector.createOwning init

    /// Create a sub-vector (slice) of an IPersistentVector
    static member subvec(v: IPersistentVector, startIndex: int, endIndex: int) : IPersistentVector =
        if startIndex < 0 then
            raise <| new ArgumentOutOfRangeException("start", "cannot be less than zero")
        elif endIndex < startIndex then
            raise <| new ArgumentOutOfRangeException("end", "cannot be less than start")
        elif endIndex > v.count () then
            raise
            <| new ArgumentOutOfRangeException("end", "cannot be past the end of the vector")
        elif startIndex = endIndex then
            PersistentVector.Empty
        else
            IPVecSubVector(null, v, startIndex, endIndex)

    /// Add a new key/value pair to a (possibly null) Associative
    static member assoc(coll: obj, key: obj, value: obj) : Associative =
        match coll with
        | null -> PersistentArrayMap([| key; value |])
        | :? Associative as a -> a.assoc (key, value)
        | _ -> raise <| new ArgumentException("Cannot assoc on this type")

    /// Remove a key from a (possibly null) IPersistentMap
    static member dissoc(coll: obj, key: obj) =
        match coll with
        | null -> null
        | _ -> (coll :?> IPersistentMap).without (key)
