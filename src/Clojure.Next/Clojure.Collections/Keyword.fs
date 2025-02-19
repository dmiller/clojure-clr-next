namespace Clojure.Collections


open System
open Clojure.Numerics
open System.Collections.Concurrent

/// The keyword datatype.
/// Classic Lisp with Clojure flavor.
[<Sealed; AllowNullLiteral>]
type Keyword private (_baseSym: Symbol) =
    inherit AFn()

    let _hasheq: int = (_baseSym :> IHashEq).hasheq () + 0x9e3779b9

    [<NonSerialized>]
    let mutable _cachedStr: string = null

    // Originally, ClojureJVM had this implementing IFn, while I had it based on AFn.
    // I changed it to match when updating to fix for CLJ-2350 (commit bd4c42d, 2021.09.14) in order to get consistency in arity error messages.
    // I'm changing it back for this implementation. Screw it.

    member _.Symbol = _baseSym

    // Object overrides

    override this.Equals(obj) =
        match obj with
        | _ when Object.ReferenceEquals(this, obj) -> true
        | :? Keyword as k -> _baseSym.Equals(k.Symbol)
        | _ -> false

    override _.GetHashCode() = _hasheq

    override _.ToString() =
        if isNull _cachedStr then
            _cachedStr <- ":" + _baseSym.ToString()

        _cachedStr

    /// Map from symbol to keyword to uniquify keywords.
    static member val private symKeyMap = new ConcurrentDictionary<Symbol, WeakReference>()

    /// Intern a keyword with the given symbol.
    static member intern(sym: Symbol) =
        let generateSymForKey sym =
            if isNull ((sym :> IMeta).meta ()) then
                sym
            else
                (sym :> IObj).withMeta (null) :?> Symbol

        let rec loop () =
            let existingRef =
                let (success, existingRef) = Keyword.symKeyMap.TryGetValue(sym)

                if success then
                    existingRef
                else
                    let k = new Keyword(generateSymForKey sym)
                    let wr = new WeakReference(k)
                    Keyword.symKeyMap.GetOrAdd(sym, wr)

            if isNull existingRef.Target then
                // weak reference died in the interim
                // remove existing entry to avoid confusion (infinite loop) and retry
                Keyword.symKeyMap.TryRemove(sym) |> ignore
                loop ()
            else
                existingRef.Target :?> Keyword

        loop ()

    /// Intern a Keyword with the given namespace-name and name (strings).
    static member intern(ns: string, name: string) =
        Keyword.intern (Symbol.intern (ns, name))

    /// Intern a Keyword with the given name (extracting the namespace if name is of the form ns/name).
    static member intern(nsname: string) = Keyword.intern (Symbol.intern (nsname))

    interface Named with
        member _.getNamespace() = _baseSym.Namespace
        member _.getName() = _baseSym.Name

    /// Get the name of the keyword  (without casting to Named).
    member _.Name = _baseSym.Name

    /// Get the namespace of the keyword  (without casting to Named).
    member _.Namespace = _baseSym.Namespace

    interface IFn with
        // (:keyword arg)  => (get arg :keyword)
        member this.invoke(arg1) =
            match arg1 with
            | :? ILookup as ilu -> ilu.valAt (this)
            | _ -> RT0.get (arg1, this)

        // (:keyword arg default) => (get arg :keyword default)
        member this.invoke(arg1, notFound) =
            match arg1 with
            | :? ILookup as ilu -> ilu.valAt (this, notFound)
            | _ -> RT0.get (arg1, this, notFound)

    interface IComparable with
        member this.CompareTo(obj) =
            match obj with
            | :? Keyword as k -> (_baseSym :> IComparable).CompareTo(k.Symbol)
            | _ -> invalidArg "obj" "Must compare to non-null Keyword"

    interface IHashEq with
        member this.hasheq() = _hasheq

    /// Find a Keyword from a Symbol name.  Return null if not found.
    static member find(sym: Symbol) =
        let (success, wr) = Keyword.symKeyMap.TryGetValue(sym)
        if success then wr.Target :?> Keyword else null

    /// Find a Keyword from namespace and name (strings).  Return null if not found.
    static member find(ns: string, name: string) = Keyword.find (Symbol.intern (ns, name))

    /// Find a Keyword from a name, parsing out the namespace if of the form ns/name.  Return null if not found.
    static member find(nsname: string) = Keyword.find (Symbol.intern (nsname))
