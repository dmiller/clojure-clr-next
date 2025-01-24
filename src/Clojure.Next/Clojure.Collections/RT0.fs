namespace Clojure.Collections

open System
open System.Collections
open Clojure.Numerics
open System.Text.RegularExpressions
open System.Reflection
open System.Threading
open System.Linq

[<AbstractClass;Sealed>]
type RT0() =

    // Used for id generation
    static let mutable _id = 0;

    // This definition will serve for RT0.seq for the time being.
    // In RTSeq, we will install a new version
    static let mutable seqHolder : obj -> ISeq = RT0.introSeq

    static member val emptyObjectArray = Array.empty

    // This is the default seq function for RT0
    static member private introSeq  (o: obj) : ISeq =
        match o with
        | null -> null
        | :? Seqable as s -> s.seq ()
        | _ ->
            raise
            <| ArgumentException($"Don't know how to create ISeq from: {o.GetType().FullName}")


    static member seq (o: obj) : ISeq = seqHolder o

    // For use by RTSeq to install its own version of seq
    static member  setSeq (f: obj -> ISeq) = seqHolder <- f

    // TODO: Prime candidate for protocols
    static member  count (o: obj) : int =
        match o with
        | null -> 0
        | :? Counted as c -> c.count ()
        | :? IPersistentCollection as c ->
            let rec loop (s: ISeq) cnt =
                match s with
                | null -> cnt
                | :? Counted as c -> cnt + c.count ()
                | _ -> loop (s.next ()) (cnt + 1)

            loop (RT0.seq c) 0
        | :? String as s -> s.Length
        | :? Array as a -> a.GetLength(0)
        | :? ICollection as c -> c.Count
        | :? DictionaryEntry -> 2
        | _ when o.GetType().IsGenericType && o.GetType().Name = "KeyValuePair`2" -> 2
        | _ ->
            raise
            <| InvalidOperationException($"count not supported on this type: {Util.nameForType (o.GetType())}")



    static member  nthFrom (coll: obj, n: int) : obj =
        match coll with
        | null -> null
        | :? string as str -> str[n]
        | _ when coll.GetType().IsArray -> (coll :?> Array).GetValue(n)
        | :? IList as ilist -> ilist.[n]
        | :? JReMatcher as jrem -> jrem.group(n)
        | :? Match as matcher -> matcher.Groups.[n]
        | :? DictionaryEntry as de -> 
            match n with
            | 0 -> de.Key
            | 1 -> de.Value
            | _ -> raise <| ArgumentOutOfRangeException("n")
        | _ when coll.GetType().IsGenericType && coll.GetType().Name = "KeyValuePair`2" ->
            match n with
            | 0 -> coll.GetType().InvokeMember("Key", BindingFlags.GetProperty, null, coll, null)  // TODO: can we improve this
            | 1 -> coll.GetType().InvokeMember("Value", BindingFlags.GetProperty, null, coll, null)
            | _ -> raise <| ArgumentOutOfRangeException("n")
        | :? IMapEntry as me ->
            match n with
            | 0 -> me.key()
            | 1 -> me.value()
            | _ -> raise <| ArgumentOutOfRangeException("n")
        | :? Sequential ->
            let rec loop (i:int) (s:ISeq) =
                match s with
                | null -> raise <| ArgumentOutOfRangeException("n")
                | _ when i = n -> s.first()
                | _ -> loop (i + 1) (s.next())
            loop 0 (RT0.seq coll)
        | _ ->
            raise <| InvalidOperationException($"nth not supported on this type: {Util.nameForType (coll.GetType())}")



    static member  nthFromWithDefault (coll: obj, n: int, notFound: obj) : obj =
        match coll with
        | null -> notFound
        | _ when n < 0 -> notFound
        | :? string as str -> 
            if n < str.Length then str[n] else notFound
        | _ when coll.GetType().IsArray -> 
            let a = coll :?> Array
            if n < a.Length then a.GetValue(n) else notFound
        | :? JReMatcher as jrem -> 
            if jrem.isUnrealizedOrFailed then 
                notFound 
            else 
                let groups = jrem.groupCount()
                if ( groups > 0 && n <= groups) then 
                    jrem.group(n) 
                else 
                    notFound

        | :? Match as matcher -> 
            if n < matcher.Groups.Count then matcher.Groups.[n] else notFound
        | :? DictionaryEntry as de -> 
            match n with
            | 0 -> de.Key
            | 1 -> de.Value
            | _ -> raise <| ArgumentOutOfRangeException("n")
        | _ when coll.GetType().IsGenericType && coll.GetType().Name = "KeyValuePair`2" ->
            match n with
            | 0 -> coll.GetType().InvokeMember("Key", BindingFlags.GetProperty, null, coll, null)  // TODO: can we improve this
            | 1 -> coll.GetType().InvokeMember("Value", BindingFlags.GetProperty, null, coll, null)
            | _ -> raise <| ArgumentOutOfRangeException("n")
        | :? IMapEntry as me ->
            match n with
            | 0 -> me.key()
            | 1 -> me.value()
            | _ -> raise <| ArgumentOutOfRangeException("n")
        | :? Sequential -> 
            let rec loop (i: int) (s:ISeq) =
                match s with
                | null -> notFound
                | _ when i = n -> s.first()
                | _ -> loop (i + 1) (s.next())
            loop 0 (RT0.seq coll)
        | :? IList as ilist -> 
            if n < ilist.Count then ilist.[n] else notFound  // have to move this down to here.  Need LazySeqs to be handled first (above)
        | _ ->
            raise <| InvalidOperationException($"nth not supported on this type: {Util.nameForType (coll.GetType())}")



    static member  nth (coll: obj, n: int) =
        match coll with
        | :? Indexed as indexed -> indexed.nth(n)
        | _ -> RT0.nthFrom(coll, n)        // nthFrom(Util.Ret1(coll, coll = null), n)


    static member  nthWithDefault (coll: obj, n: int, notFound : obj) =
        match coll with
        | :? Indexed as indexed -> indexed.nth(n, notFound)
        | _ -> RT0.nthFromWithDefault(coll, n, notFound)        // nthFrom(Util.Ret1(coll, coll = null), n)

        (*



        *)

     // TODO: Prime candidate for protocols
    static member  private getFrom(coll: obj, key: obj) =
        match coll with
        | null -> null
        | :? IDictionary as m -> m[key]
        | :? IPersistentSet as set -> set.get(key)
        | _ when Numbers.IsNumeric(key) && (coll :? string || coll.GetType().IsArray) ->
            let n = Converters.convertToInt(key)
            if n >= 0 && n < RT0.count coll then RT0.nth(coll,n) else null
        | :? ITransientSet as tset -> tset.get(key)
        | _ -> null

     // TODO: Prime candidate for protocols
    static member  private getFromWithDefault(coll: obj, key: obj, notFound: obj) =
        match coll with
        | null -> notFound
        | :? IDictionary as m -> if m.Contains(key) then m[key] else notFound
        | :? IPersistentSet as set -> if set.contains(key) then set.get(key) else notFound
        | _ when Numbers.IsNumeric(key) && (coll :? string || coll.GetType().IsArray) ->
            let n = Converters.convertToInt(key)
            if n >= 0 && n < RT0.count coll then RT0.nth(coll, n) else notFound
        | :? ITransientSet as tset -> if tset.contains(key) then tset.get(key) else notFound
        | _ -> notFound

   
    static member  get(coll: obj, key: obj)  =
        match coll with
        | :? ILookup as look ->   look.valAt(key)
        | _ -> RT0.getFrom(coll, key)
     

    static member  getWithDefault(coll: obj, key: obj, notFound: obj) = 
        match coll with
        | :? ILookup as look ->   look.valAt(key, notFound)
        | _ -> RT0.getFromWithDefault(coll, key, notFound)

    static member meta (o: obj) : IPersistentMap =
        match o with
        | :? IMeta as m -> m.meta()
        | _ -> null


    static member  booleanCast(x:obj) : bool =
        match x with
        | :? bool as b -> b
        | _ -> not (isNull x)


    // Id generation

    static member nextID() = Interlocked.Increment(&_id)


    static member toArray(coll: obj) : obj array =
        match coll with
        | null -> RT0.emptyObjectArray
        | :? (obj array) as a -> a
        | :? string as s -> s.ToCharArray() |> Array.map box
        | :? Array as a -> 
            let len = a.Length
            let ret = Array.zeroCreate len
            for i = 0 to len - 1 do
                ret[i] <- a.GetValue(i)
            ret
        | :? IEnumerable as ie -> ie.Cast<obj>().ToArray()
        | _ -> 
            raise <| InvalidOperationException($"Unable to convert: {coll.GetType()} to Object[]")


    static member length(seq: ISeq) =
        let rec loop (s:ISeq) (cnt:int) =
            match s with
            | null -> cnt
            | _ -> loop (s.next()) (cnt + 1)
        loop seq 0