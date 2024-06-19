namespace Clojure.Collections


open System
open Clojure.Numerics
open System.Collections.Concurrent

[<Serializable; Sealed; AllowNullLiteral>]
type Keyword(baseSym: Symbol) =
    inherit AFn()

    let hasheq: int = (baseSym :> IHashEq).hasheq () + 0x9e3779b9

    [<NonSerialized>]
    let mutable cachedStr: string = null

    // Originally, ClojureJVM had this implementing IFn, while I had it based on AFn.
    // I changed it to match when updating to fix for CLJ-2350 (commit bd4c42d, 2021.09.14) in order to get consistency in arity error messages.
    // I'm changing it back for this implementation. Screw it.

    member _.Symbol = baseSym

    // Object overrides

    override this.Equals(obj) =
        match obj with
        | _ when Object.ReferenceEquals(this, obj) -> true
        | :? Keyword as k -> baseSym.Equals(k.Symbol)
        | _ -> false

    override _.GetHashCode() = hasheq

    override _.ToString() =
        if isNull cachedStr then
            cachedStr <- ":" + baseSym.ToString()

        cachedStr

    // Map from symbol to keyword to uniquify keywords.
    static member val private symKeyMap = new ConcurrentDictionary<Symbol, WeakReference>()

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

    static member intern(ns: string, name: string) =
        Keyword.intern (Symbol.intern (ns, name))

    static member intern(nsname: string) = Keyword.intern (Symbol.intern (nsname))

    interface Named with
        member _.getNamespace() = baseSym.Namespace
        member _.getName() = baseSym.Name

    // provide some versions to use internall without casting
    member _.Name = baseSym.Name
    member _.Namespace = baseSym.Namespace

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
            | _ -> RT0.getWithDefault (arg1, this, notFound)

    interface IComparable with
        member this.CompareTo(obj) =
            match obj with
            | :? Keyword as k -> (baseSym :> IComparable).CompareTo(k.Symbol)
            | _ -> invalidArg "obj" "Must compare to non-null Keyword"

    interface IHashEq with
        member this.hasheq() = hasheq

    static member find(sym: Symbol) =
        let (success, wr) = Keyword.symKeyMap.TryGetValue(sym)
        if success then wr.Target :?> Keyword else null

    static member find(ns: string, name: string) = Keyword.find (Symbol.intern (ns, name))
    static member find(nsname: string) = Keyword.find (Symbol.intern (nsname))

(*

// DO WE NEED THESE?

        #region Operator overloads

        public static bool operator ==(Keyword k1, Keyword k2)
        {
            if (ReferenceEquals(k1, k2))
                return true;

            if (k1 is null || k2 is null)
                return false;

            return k1.CompareTo(k2) == 0;
        }

        public static bool operator !=(Keyword k1, Keyword k2)
        {
            if (ReferenceEquals(k1, k2))
                return false;

            if (k1 is null || k2 is null)
                return true;

            return k1.CompareTo(k2) != 0;
        }

        public static bool operator <(Keyword k1, Keyword k2)
        {
            if (ReferenceEquals(k1, k2))
                return false;

            if (k1 is null)
                throw new ArgumentNullException(nameof(k1));

            return k1.CompareTo(k2) < 0;
        }

        public static bool operator >(Keyword k1, Keyword k2)
        {
            if (ReferenceEquals(k1, k2))
                return false;

            if (k1 is null)
                throw new ArgumentNullException(nameof(k1));

            return k1.CompareTo(k2) > 0;
        }

        #endregion

        #region ISerializable Members

        [System.Security.SecurityCritical]
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            // Instead of serializing the keyword,
            // serialize a KeywordSerializationHelper instead
            info.SetType(typeof(KeywordSerializationHelper));
            info.AddValue("_sym", _sym);
        }

        #endregion

        #region other
    }

    [Serializable]
    sealed class KeywordSerializationHelper : IObjectReference
    {

        #region Data

        readonly Symbol _sym;

        #endregion

        #region c-tors

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Standard API")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Standard API")]
        KeywordSerializationHelper(SerializationInfo info, StreamingContext context)
        {
            _sym = (Symbol)info.GetValue("_sym", typeof(Symbol));
        }

        #endregion

        #region IObjectReference Members

        public object GetRealObject(StreamingContext context)
        {
            return Keyword.intern(_sym);
        }

        #endregion
    }
}


*)
