namespace Clojure.Collections

open System
open System.Collections.Concurrent

type Symbol private (meta: IPersistentMap, ns: string, name: string) =
    inherit AFn()

    let mutable hasheq: int option = None

    [<NonSerialized>]
    let mutable toStringCached: string option = None

    private new(ns, name) = Symbol(null, ns, name)

    member x.Name = name
    member x.NS = ns


    static member intern(ns, name) = Symbol(ns, name)

    static member intern(nsname: string) =
        let i = nsname.IndexOf('/')

        if i = -1 || nsname.Equals("/")
        then Symbol(null, nsname)
        else Symbol(nsname.Substring(0, i), nsname.Substring(i + 1))

    // JVM comment: the create thunks preserve binary compatibility with code compiled
    // JVM comment: against earlier version of Clojure and can be removed (at some point).
    // So I'm leaving them out.
    static member create(ns, name): Symbol = Symbol.intern (ns, name)
    static member create(nsname): Symbol = Symbol.intern (nsname)

    override _.ToString() =
        match toStringCached with
        | Some s -> s
        | None ->
            let s =
                if ns = null then name else ns + "/" + name

            toStringCached <- Some s
            s

    override this.Equals(o: obj): bool =
        match o with
        | _ when Object.ReferenceEquals(this, o) -> true
        | :? Symbol as s -> Util.equals (ns, s.NS) && name.Equals(s.Name)
        | _ -> false

    override _.GetHashCode() =
        Util.hashCombine (name.GetHashCode(), Util.hash (ns))

    interface IHashEq with
        member _.hasheq() =
            match hasheq with
            | None ->
                let h =
                    Util.hashCombine (Murmur3.HashString(name), Util.hash (ns))

                hasheq <- Some h
                h
            | Some h -> h

    interface IMeta with
        member _.meta() = meta

    interface IObj with
        member this.withMeta(m: IPersistentMap) =
            if Object.ReferenceEquals(meta, m) then upcast this else upcast Symbol(m, ns, name)

    interface Named with
        member x.getNamespace() = ns
        member x.getName() = name

    interface IFn with
        member this.invoke(o) = RT.get (o, this)
        member this.invoke(o, notFound) = RT.get3 (o, this, notFound)

    interface IComparable<Symbol> with
        member this.CompareTo(s) =
            if this.Equals(s) then
                0
            elif isNull ns && not (isNull s.NS) then
                -1
            elif not (isNull ns) then
                if isNull s.NS then
                    1
                else
                    let nsc = ns.CompareTo(s.NS)

                    if nsc <> 0 then nsc else name.CompareTo(s.Name)
            else
                name.CompareTo(s.Name)

    interface IComparable with
        member this.CompareTo(o) =
            match o with
            | :? Symbol as s -> (this :> IComparable<Symbol>).CompareTo(s)
            | _ -> invalidArg "o" "must compare to a non-null Symbol"


//    // we also defined operators ==, !=, <, > : TODO: decide if there is any point to them.

//    // Not yet translated  - not clear if they are needed or should be elswhere

//    //let mutable toStringEscCached : string option = None

//    //       private static string NameMaybeEscaped(string s)
//    //       public string ToStringEscaped()


[<Sealed>]
[<AllowNullLiteral>]
type Keyword private (sym: Symbol) =
    inherit AFn()

    let hasheq = (sym :> IHashEq).hasheq() + 0x9e3779b9

    [<NonSerialized>]
    let mutable toStringCached: String option = None

    member x.Symbol = sym

    // map from symbols to keywords to uniquify keywords
    static member symKeyMap =
        ConcurrentDictionary<Symbol, WeakReference<Keyword>>()

    static member intern(s) =
        let useSym s =
            if (s :> IMeta).meta() |> isNull then s else downcast (s :> IObj).withMeta(null)

        let exists, wref = Keyword.symKeyMap.TryGetValue(s)

        if exists then
            let exists, existingKw = wref.TryGetTarget()

            if exists then
                existingKw
            else
                // WeakReference timed out.  Set a new Keyword in place
                // we don't have a timing problem here.  if someone managed to sneak another keyword in here, it won't really matter.
                let k = Keyword(useSym s)
                wref.SetTarget(k)
                k
        else
            let s1 = useSym s
            let k = Keyword(s1)

            Keyword.symKeyMap.GetOrAdd(s1, WeakReference<Keyword>(k))
            |> ignore
            // whether we were successful or not, okay to return the one we have in hand
            k

    static member intern(ns, name) =
        Keyword.intern (Symbol.intern (ns, name))

    static member intern(nsname) = Keyword.intern (Symbol.intern (nsname))

    override _.ToString() =
        match toStringCached with
        | Some s -> s
        | None ->
            let s = ":" + sym.ToString()
            toStringCached <- Some s
            s

    interface IEquatable<Keyword> with
        member this.Equals(k) =
            Object.ReferenceEquals(this, k)
            || sym.Equals(k.Symbol)

    override this.Equals(o) =
        match o with
        | :? Keyword as k -> (this :> IEquatable<Keyword>).Equals(k)
        | _ -> false

    override _.GetHashCode() = sym.GetHashCode() + 0x9e3779b9

    interface IHashEq with
        member _.hasheq() = hasheq

    // I prefer these for myself.
    member _.NS = sym.NS
    member _.Name = sym.Name

    interface Named with
        member _.getNamespace() = sym.NS
        member _.getName() = sym.Name

    interface IFn with
        // (:keyword arg) = (gat arg :keyword)
        member this.invoke(arg1) =
            match arg1 with
            | :? ILookup as ilu -> ilu.valAt (this)
            | _ -> RT.get (arg1, this)
        // (:keyword arg default) = (gat arg :keyword default)
        member this.invoke(arg1, notFound) =
            match arg1 with
            | :? ILookup as ilu -> ilu.valAt (this, notFound)
            | _ -> RT.get3 (arg1, this, notFound)

    interface IComparable<Keyword> with
        member _.CompareTo(k) =
            (sym :> IComparable<Symbol>).CompareTo(k.Symbol)

    interface IComparable with
        member this.CompareTo(o) =
            match o with
            | :? Keyword as k -> (this :> IComparable<Keyword>).CompareTo(k)
            | _ -> invalidArg "0" "Cannot campre to null or non-Keyword"

    // we had operator overloads for ==, !=, <, >
    // TODO: do we need?

    static member find(s) =
        let exists, wr = Keyword.symKeyMap.TryGetValue(s)

        if exists then
            let exists, kw = wr.TryGetTarget()
            if exists then kw else null
        else
            null

    static member find(ns, name) = Keyword.find (Symbol.intern (ns, name))
    static member find(nsname) = Keyword.find (Symbol.intern (nsname))

// not done yet
//       public void GetObjectData(SerializationInfo info, StreamingContext context)
//       sealed class KeywordSerializationHelper : IObjectReference
