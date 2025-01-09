namespace Clojure.Collections

open System
open System.Collections.Generic

type MethodImplCacheEntry = { Type: Type; Fn: IFn}

// A cache for type => IFn map.
[<Sealed>]
type MethodImplCache(_sym: Symbol, _protocol: IPersistentMap, _methodk : Keyword, _shift:int, _mask: int, _table : obj array, _map: IDictionary<Type,MethodImplCacheEntry>) = 

    let mutable _mre : MethodImplCacheEntry option = None

    member _.sym = _sym
    member _.protocol = _protocol
    member _.methodk = _methodk
    member _.table = _table
    member _.map = _map

    new(sym, protocol, methodk) = MethodImplCache(sym, protocol, methodk, 0, 0, RT0.emptyObjectArray, null)
    new(sym, protocol, methodk, map) = MethodImplCache(sym, protocol, methodk, 0, 0, null, map)

    member this.fnFor(t: Type) = 
        match _mre with
        | Some entry when entry.Type = t -> entry.Fn
        | _ -> this.findFnFor(t)

    member private _.findFnFor(t: Type) =
        match _map with
        | null -> 
            let idx = ((Util.hasheq(t) >>> _shift) &&& _mask) <<< 1
            if idx < _table.Length && Object.ReferenceEquals(t ,_table[idx]) then
                let e = _table.[idx + 1] :?> MethodImplCacheEntry
                _mre <- Some e
                e.Fn
            else
                null
        | _ -> 
            let ok, e = _map.TryGetValue(t)
            if ok then
                _mre <- Some e
                e.Fn
            else   
                _mre <- None
                null
