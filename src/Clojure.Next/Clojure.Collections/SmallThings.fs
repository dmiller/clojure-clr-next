namespace Clojure.Collections

open System

/// Abstract base class for objects that can have metadata.
/// method withMeta should be implemented in derived classes.
[<AbstractClass; AllowNullLiteral>]
type Obj(_meta: IPersistentMap) =

    new() = Obj(null)

    interface IMeta with
        member _.meta() = _meta

    interface IObj with
        member _.withMeta(m) =
            raise
            <| NotImplementedException("You must implement withMeta in derived classes")


/// Holds a reduced value.
[<Sealed>]
type Reduced(_value: obj) =

    interface IDeref with
        member _.deref() = _value
