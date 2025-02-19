namespace Clojure.Collections

open System
open System.Collections
open Clojure.Numerics
open System.Text.RegularExpressions
open System.Reflection
open System.Threading
open System.Linq

/// A minimal set of runtime operations for the Clojure.Collections namespace
/// that can be defined before we have any implementation classes.
[<AbstractClass; Sealed>]
type RT0() =

    // Used for id generation
    static let mutable _id = 0


    //////////////
    //
    // RT0.Seq
    //
    //////////////

    // We have a bootstrapping problem.  Or rather a massive circular dependency problem.
    // We need a fairly generic function that creates an ISeq from an object.
    // Normall this would be just call Seqable.seq if that interface is supported.
    // However, ultimately, for performance, we will want to special case certain classes.
    // However, those classes won't be defined for a long time.
    // Our solution is to define RT0.seq to delegate to a function that we can change at a later point.
    // That later version is defined in RTSeq.  Any class defined before RTSeq will use the RT0 version.
    // Any class defined after RTSeq should use the RTSeq version directly.
    // This is a hack.  I'm open to better ideas.

    // Holds the current seq function
    static let mutable seqHolder: obj -> ISeq = RT0.introSeq

    /// The initial implementation of the seq function for RT0
    static member private introSeq(o: obj) : ISeq =
        match o with
        | null -> null
        | :? Seqable as s -> s.seq ()
        | _ ->
            raise
            <| ArgumentException($"Don't know how to create ISeq from: {o.GetType().FullName}")


    /// Calls the current implementation of the seq function.
    /// You should use RTSeq.seq instead of this function if you are defined after RTSeq.
    static member seq(o: obj) : ISeq = seqHolder o

    /// For use by RTSeq to install its own version of seq
    static member internal setSeq(f: obj -> ISeq) = seqHolder <- f

    //////////////

    /// Cached empty Object[]
    static member val emptyObjectArray = Array.empty

    // TODO: Prime candidate for protocols

    /// Yield a count of the elements in a collection.
    static member count(o: obj) : int =
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


    // TODO: Prime candidate for protocols

    /// Find the nth element of a collection.
    /// For collections not supporting interface Indexed.
    /// Throws is no such element.
    static member private nthFrom(coll: obj, n: int) : obj =
        match coll with
        | null -> null
        | :? string as str -> str[n]
        | _ when coll.GetType().IsArray -> (coll :?> Array).GetValue(n)
        | :? IList as ilist -> ilist.[n]
        | :? JReMatcher as jrem -> jrem.group (n)
        | :? Match as matcher -> matcher.Groups.[n]
        | :? DictionaryEntry as de ->
            match n with
            | 0 -> de.Key
            | 1 -> de.Value
            | _ -> raise <| ArgumentOutOfRangeException("n")
        | _ when coll.GetType().IsGenericType && coll.GetType().Name = "KeyValuePair`2" ->
            match n with
            | 0 -> coll.GetType().InvokeMember("Key", BindingFlags.GetProperty, null, coll, null) // TODO: can we improve this
            | 1 -> coll.GetType().InvokeMember("Value", BindingFlags.GetProperty, null, coll, null)
            | _ -> raise <| ArgumentOutOfRangeException("n")
        | :? IMapEntry as me ->
            match n with
            | 0 -> me.key ()
            | 1 -> me.value ()
            | _ -> raise <| ArgumentOutOfRangeException("n")
        | :? Sequential ->
            let rec loop (i: int) (s: ISeq) =
                match s with
                | null -> raise <| ArgumentOutOfRangeException("n")
                | _ when i = n -> s.first ()
                | _ -> loop (i + 1) (s.next ())

            loop 0 (RT0.seq coll)
        | _ ->
            raise
            <| InvalidOperationException($"nth not supported on this type: {Util.nameForType (coll.GetType())}")


    /// Find the nth element of a collection.
    /// For collections not supporting interface Indexed.
    /// Returns the supplied default value if no such element.
    static member private nthFrom(coll: obj, n: int, notFound: obj) : obj =
        match coll with
        | null -> notFound
        | _ when n < 0 -> notFound
        | :? string as str -> if n < str.Length then str[n] else notFound
        | _ when coll.GetType().IsArray ->
            let a = coll :?> Array
            if n < a.Length then a.GetValue(n) else notFound
        | :? JReMatcher as jrem ->
            if jrem.isUnrealizedOrFailed then
                notFound
            else
                let groups = jrem.groupCount ()

                if (groups > 0 && n <= groups) then
                    jrem.group (n)
                else
                    notFound

        | :? Match as matcher ->
            if n < matcher.Groups.Count then
                matcher.Groups.[n]
            else
                notFound
        | :? DictionaryEntry as de ->
            match n with
            | 0 -> de.Key
            | 1 -> de.Value
            | _ -> raise <| ArgumentOutOfRangeException("n")
        | _ when coll.GetType().IsGenericType && coll.GetType().Name = "KeyValuePair`2" ->
            match n with
            | 0 -> coll.GetType().InvokeMember("Key", BindingFlags.GetProperty, null, coll, null) // TODO: can we improve this
            | 1 -> coll.GetType().InvokeMember("Value", BindingFlags.GetProperty, null, coll, null)
            | _ -> raise <| ArgumentOutOfRangeException("n")
        | :? IMapEntry as me ->
            match n with
            | 0 -> me.key ()
            | 1 -> me.value ()
            | _ -> raise <| ArgumentOutOfRangeException("n")
        | :? Sequential ->
            let rec loop (i: int) (s: ISeq) =
                match s with
                | null -> notFound
                | _ when i = n -> s.first ()
                | _ -> loop (i + 1) (s.next ())

            loop 0 (RT0.seq coll)
        | :? IList as ilist -> if n < ilist.Count then ilist.[n] else notFound // have to move this down to here.  Need LazySeqs to be handled first (above)
        | _ ->
            raise
            <| InvalidOperationException($"nth not supported on this type: {Util.nameForType (coll.GetType())}")


    /// Retrieve the nth element of a collection.
    /// Throws an exception if no such element.
    static member nth(coll: obj, n: int) =
        match coll with
        | :? Indexed as indexed -> indexed.nth (n)
        | _ -> RT0.nthFrom (coll, n) // nthFrom(Util.Ret1(coll, coll = null), n)

    /// Retrieve the nth element of a collection.
    /// Returns the provided default value if no such element.
    static member nth(coll: obj, n: int, notFound: obj) =
        match coll with
        | :? Indexed as indexed -> indexed.nth (n, notFound)
        | _ -> RT0.nthFrom (coll, n, notFound) // nthFrom(Util.Ret1(coll, coll = null), n)


    // TODO: Prime candidate for protocols

    /// Retrieve the value associated with a key in a collection.
    /// For collections not supporting ILookup.
    /// Throws an exception if no such key.
    static member private getFrom(coll: obj, key: obj) =
        match coll with
        | null -> null
        | :? IDictionary as m -> m[key]
        | :? IPersistentSet as set -> set.get (key)
        | _ when Numbers.IsNumeric(key) && (coll :? string || coll.GetType().IsArray) ->
            let n = Converters.convertToInt (key)

            if n >= 0 && n < RT0.count coll then
                RT0.nth (coll, n)
            else
                null
        | :? ITransientSet as tset -> tset.get (key)
        | _ -> null

    // TODO: Prime candidate for protocols

    /// Retrieve the value associated with a key in a collection.
    /// For collections not supporting ILookup.
    /// Returns the provided default value if no such key.
    static member private getFrom(coll: obj, key: obj, notFound: obj) =
        match coll with
        | null -> notFound
        | :? IDictionary as m -> if m.Contains(key) then m[key] else notFound
        | :? IPersistentSet as set -> if set.contains (key) then set.get (key) else notFound
        | _ when Numbers.IsNumeric(key) && (coll :? string || coll.GetType().IsArray) ->
            let n = Converters.convertToInt (key)

            if n >= 0 && n < RT0.count coll then
                RT0.nth (coll, n)
            else
                notFound
        | :? ITransientSet as tset -> if tset.contains (key) then tset.get (key) else notFound
        | _ -> notFound


    /// Retrieve the value associated with a key in a collection.
    /// Throws an exception if no such key.
    static member get(coll: obj, key: obj) =
        match coll with
        | :? ILookup as look -> look.valAt (key)
        | _ -> RT0.getFrom (coll, key)

    /// Retrieve the value associated with a key in a collection.
    /// Returns the provided default value if no such key.
    static member get(coll: obj, key: obj, notFound: obj) =
        match coll with
        | :? ILookup as look -> look.valAt (key, notFound)
        | _ -> RT0.getFrom (coll, key, notFound)


    /// Retrieve the metadata associated with an object.
    /// Returns null if metadata not supported.
    static member meta(o: obj) : IPersistentMap =
        match o with
        | :? IMeta as m -> m.meta ()
        | _ -> null


    /// Cast a value to boolean.
    /// If boolean, returns it.  Otherwise nonNull is equivalent to true.
    /// The classic Lisp definition of 'truthy'.
    static member booleanCast(x: obj) : bool =
        match x with
        | :? bool as b -> b
        | _ -> not (isNull x)


    /// Generate a unique id.  Sequential. Thread-safe.
    static member nextID() = Interlocked.Increment(&_id)


    // TODO: Prime candidate for protocols

    /// Convert a collection to an array of objects.
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
            raise
            <| InvalidOperationException($"Unable to convert: {coll.GetType()} to Object[]")


    /// Compute the length of a sequence.
    /// In general, this is O(n) where n is the number of elements in the sequence.
    /// For general collections, consider RTSeq.count instead.
    static member length(seq: ISeq) =
        let rec loop (s: ISeq) (cnt: int) =
            match s with
            | null -> cnt
            | _ -> loop (s.next ()) (cnt + 1)

        loop seq 0
