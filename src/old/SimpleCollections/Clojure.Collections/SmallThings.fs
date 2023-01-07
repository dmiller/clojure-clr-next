namespace Clojure.Collections

open System


[<AbstractClass>]
[<AllowNullLiteral>]
type Obj(meta: IPersistentMap) =

    new() = Obj(null)

    interface IMeta with
        member _.meta() = meta

    interface IObj with
        member _.withMeta(m) =
            raise
            <| NotImplementedException("You must implement withMeta in derived classes")



// Needs to appear before the defintion of RT

[<Sealed>]
type Reduced(value) =

    interface IDeref with
        member _.deref() = value
