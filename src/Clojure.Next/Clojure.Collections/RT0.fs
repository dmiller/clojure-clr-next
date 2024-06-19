module Clojure.Collections.RT0

open System
open System.Collections
open Clojure.Numerics
open System.Text.RegularExpressions
open System.Reflection

let seq (o: obj) : ISeq =
    match o with
    | null -> null
    | :? Seqable as s -> s.seq ()
    | _ ->
        raise
        <| ArgumentException($"Don't know how to create ISeq from: {o.GetType().FullName}")

// we will want to get all of this eventually:

//  public static ISeq seq(object coll)
//{
//    if (coll is ASeq aseq)
//        return aseq;

//    return coll is LazySeq lseq ? lseq.seq() : seqFrom(coll);
//}

//// N.B. canSeq must be kept in sync with this!

//private static ISeq seqFrom(object coll)
//{
//    if (coll == null)
//        return null;

//    if (coll is Seqable seq)
//        return seq.seq();

//    if (coll.GetType().IsArray)
//        return ArraySeq.createFromObject(coll);

//    if (coll is string str)
//        return StringSeq.create(str);

//    if (coll is IEnumerable ie)  // java: Iterable  -- reordered clauses so others take precedence.
//        return chunkEnumeratorSeq(ie.GetEnumerator());            // chunkIteratorSeq

//    // The equivalent for Java:Map is IDictionary.  IDictionary is IEnumerable, so is handled above.
//    //else if(coll isntanceof Map)
//    //     return seq(((Map) coll).entrySet());
//    // Used to be in the java version:
//    //else if (coll is IEnumerator)  // java: Iterator
//    //    return EnumeratorSeq.create((IEnumerator)coll);

//    throw new ArgumentException("Don't know how to create ISeq from: " + coll.GetType().FullName);
//}


// TODO: Prime candidate for protocols
let count (o: obj) : int =
    match o with
    | null -> 0
    | :? Counted as c -> c.count ()
    | :? IPersistentCollection as c ->
        let rec loop (s: ISeq) cnt =
            match s with
            | null -> cnt
            | :? Counted as c -> cnt + c.count ()
            | _ -> loop (s.next ()) (cnt + 1)

        loop (seq c) 0
    | :? String as s -> s.Length
    | :? Array as a -> a.GetLength(0)
    | :? ICollection as c -> c.Count
    | :? DictionaryEntry -> 2
    | _ when o.GetType().IsGenericType && o.GetType().Name = "KeyValuePair`2" -> 2
    | _ ->
        raise
        <| InvalidOperationException($"count not supported on this type: {Util.nameForType (o.GetType())}")



let nthFrom (coll: obj, n: int) : obj =
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
        loop 0 (seq coll)
    | _ ->
        raise <| InvalidOperationException($"nth not supported on this type: {Util.nameForType (coll.GetType())}")



let nthFromWithDefault (coll: obj, n: int, notFound: obj) : obj =
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
        loop 0 (seq coll)
    | :? IList as ilist -> 
        if n < ilist.Count then ilist.[n] else notFound  // have to move this down to here.  Need LazySeqs to be handled first (above)
    | _ ->
        raise <| InvalidOperationException($"nth not supported on this type: {Util.nameForType (coll.GetType())}")



let nth (coll: obj, n: int) =
    match coll with
    | :? Indexed as indexed -> indexed.nth(n)
    | _ -> nthFrom(coll, n)        // nthFrom(Util.Ret1(coll, coll = null), n)


let nthWithDefault (coll: obj, n: int, notFound : obj) =
    match coll with
    | :? Indexed as indexed -> indexed.nth(n, notFound)
    | _ -> nthFromWithDefault(coll, n, notFound)        // nthFrom(Util.Ret1(coll, coll = null), n)

    (*



    *)

 // TODO: Prime candidate for protocols
let private getFrom(coll: obj, key: obj) =
    match coll with
    | null -> null
    | :? IDictionary as m -> m[key]
    | :? IPersistentSet as set -> set.get(key)
    | _ when Numbers.IsNumeric(key) && (coll :? string || coll.GetType().IsArray) ->
        let n = Converters.convertToInt(key)
        if n >= 0 && n < count coll then nth(coll,n) else null
    | :? ITransientSet as tset -> tset.get(key)
    | _ -> null

 // TODO: Prime candidate for protocols
let private getFromWithDefault(coll: obj, key: obj, notFound: obj) =
    match coll with
    | null -> notFound
    | :? IDictionary as m -> if m.Contains(key) then m[key] else notFound
    | :? IPersistentSet as set -> if set.contains(key) then set.get(key) else notFound
    | _ when Numbers.IsNumeric(key) && (coll :? string || coll.GetType().IsArray) ->
        let n = Converters.convertToInt(key)
        if n >= 0 && n < count coll then nth(coll, n) else notFound
    | :? ITransientSet as tset -> if tset.contains(key) then tset.get(key) else notFound
    | _ -> notFound

   
let get(coll: obj, key: obj)  =
    match coll with
    | :? ILookup as look ->   look.valAt(key)
    | _ -> getFrom(coll, key)
     

let getWithDefault(coll: obj, key: obj, notFound: obj) = 
    match coll with
    | :? ILookup as look ->   look.valAt(key, notFound)
    | _ -> getFromWithDefault(coll, key, notFound)

