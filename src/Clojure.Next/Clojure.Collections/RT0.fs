module Clojure.Collections.RT0

open System
open System.Collections

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
