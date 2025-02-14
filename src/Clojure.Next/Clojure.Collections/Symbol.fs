namespace Clojure.Collections

open System
open Clojure.Numerics
open Clojure.Numerics.Hashing

[<Sealed; AllowNullLiteral>]
type Symbol private (_meta: IPersistentMap, _ns: string, _name: string) =
    inherit AFn()

    // cached hashcode value, lazy
    let mutable hasheq = 0

    // cached string representation, lazy
    let mutable _str: string = null

    private new(ns, name) = Symbol(null, ns, name)

    // accessors for the data
    member this.Namespace = _ns
    member this.Name = _name

    // Object overrides

    override this.Equals(obj) =
        match obj with
        | _ when Object.ReferenceEquals(this, obj) -> true
        | :? Symbol as sym -> Util.equals (_ns, sym.Namespace) && _name.Equals(sym.Name)
        | _ -> false

    override this.GetHashCode() =
        if hasheq = 0 then
            hasheq <- hashCombine (Murmur3.HashString(if isNull _ns then "" else _ns), Murmur3.HashString(_name))
        hasheq

    override this.ToString() =
        if isNull _str then
            _str <-
                match _ns with
                | null -> _name
                | _ -> $"{_ns}/{_name}"
        _str

    // factory methods
        
    /// Intern a symbol with the given name  and namespace-name
    static member intern(ns: string, name: string) = Symbol(null, ns, name)

    /// Intern a symbol with the given name (extracting the namespace if name is of the form ns/name)
    static member intern(nsname: string) =
        let i = nsname.IndexOf('/')

        if i = -1 || nsname.Equals("/") then
            Symbol(null, nsname)
        else
            Symbol(nsname.Substring(0, i), nsname.Substring(i + 1))

    // interface implementations

    interface IHashEq with
        member this.hasheq() = this.GetHashCode()

    interface IComparable with
        member this.CompareTo(obj) =
            match obj with
            | :? Symbol as sym ->
                let nsc = 
                    match _ns with
                    | null -> if isNull sym.Namespace then 0 else -1
                    | _ -> if isNull sym.Namespace then 1 else _ns.CompareTo(sym.Namespace)
                if nsc <> 0 then nsc else _name.CompareTo(sym.Name)
            | _ -> invalidArg "obj" "Must compare to non-null Symbol"

    interface IObj with
        override this.withMeta(m) =
            if Object.ReferenceEquals(_meta, m) then
                this
            else
                Symbol(m, _ns, _name)

    interface IMeta with
        override _.meta() = _meta

    interface Named with
        member _.getNamespace() = _ns
        member _.getName() = _name

    interface IFn with
        member this.invoke(arg1) = RT0.get (arg1, this)
        member this.invoke(arg1, arg2) = RT0.get (arg1, this, arg2)
