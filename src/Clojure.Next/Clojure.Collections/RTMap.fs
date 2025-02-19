namespace Clojure.Collections

open System

[<AbstractClass;Sealed>]
type RTMap() =

    static member map( [<ParamArray>] init: obj array) : IPersistentMap =
        if isNull init || init.Length = 0 then
            PersistentArrayMap.Empty
        elif init.Length <= PersistentArrayMap.hashtableThreshold then
            PersistentArrayMap.createWithCheck init
        else
            PersistentHashMap.createWithCheck init

    static member mapUniqueKeys( [<ParamArray>] init: obj array) : IPersistentMap =
        if isNull init then
            PersistentArrayMap.Empty
        elif init.Length <= PersistentArrayMap.hashtableThreshold then
            PersistentArrayMap(init)
        else
            PersistentHashMap.create init

    static member keys( coll: obj) =
        match coll with
        | :? IPersistentMap as ipm -> KeySeq.createFromMap(ipm)
        | _ -> KeySeq.create(RTSeq.seq(coll))

    static member set( [<ParamArray>] init: obj array) : IPersistentSet =
        PersistentHashSet.createWithCheck init

    static member vector ( [<ParamArray>] init: obj array) : IPersistentVector =
        LazilyPersistentVector.createOwning init

    static member subvec(v: IPersistentVector, startIndex: int, endIndex: int) : IPersistentVector =
        if startIndex < 0 then 
            raise <| new ArgumentOutOfRangeException("start", "cannot be less than zero")
        elif endIndex < startIndex then
            raise <| new ArgumentOutOfRangeException("end", "cannot be less than start")
        elif endIndex > v.count() then
            raise <| new ArgumentOutOfRangeException("end", "cannot be past the end of the vector")
        elif startIndex = endIndex then
            PersistentVector.Empty
        else
            IPVecSubVector(null,v, startIndex, endIndex)
            

    static member assoc(coll: obj, key: obj, value: obj) : Associative =
        match coll with
        | null -> PersistentArrayMap( [|key; value|])
        | :? Associative as a -> a.assoc(key, value)
        | _ -> 
            raise <| new ArgumentException("Cannot assoc on this type")


    static member dissoc(coll:obj, key:obj) =
        match coll with
        | null -> coll
        | _ -> (coll :?> IPersistentMap).without(key)

