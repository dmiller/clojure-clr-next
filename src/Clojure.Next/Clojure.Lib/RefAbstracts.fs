namespace Clojure.Lib

open Clojure.Collections
open System.Runtime.CompilerServices
open System


/// An abstract class providiing a basic implementation of IReference functionality.
[<AbstractClass; AllowNullLiteral>]
type AReference(meta: IPersistentMap) =

    // The JVM implementation does not make this class abstract, but we're never going to instantiate one of these directly.

    /// The metadata for the reference. (Settable)
    let mutable _meta = meta

    /// Create a reference with null metadata.
    new() = AReference(null)

    /// Get the metadata for the reference.
    member _.Meta = _meta

    interface IReference with
        [<MethodImpl(MethodImplOptions.Synchronized)>]
        member this.alterMeta(alter: IFn, args: ISeq) =
            _meta <- alter.applyTo (Cons(_meta, args)) :?> IPersistentMap
            _meta

        [<MethodImpl(MethodImplOptions.Synchronized)>]
        member this.resetMeta(m) =
            _meta <- m
            m

    interface IMeta with
        [<MethodImpl(MethodImplOptions.Synchronized)>]
        member this.meta() = _meta


/// An abstract class providing a basic implementation of IRef functionality.
[<AbstractClass; AllowNullLiteral>]
type ARef(meta: IPersistentMap) =
    inherit AReference(meta)

    /// The validator for values.
    [<VolatileField>]
    let mutable _validator: IFn = null

    /// The watchers on the reference.
    [<VolatileField>]
    let mutable _watches: IPersistentMap = PersistentHashMap.Empty

    /// Create a reference with null metadata.
    new() = ARef(null)

    interface IMeta with
        member this.meta() = this.Meta

    interface IDeref with
        member _.deref() =
            raise
            <| NotImplementedException("Derived classes must implement IDeref.deref()")


    /// Invoke an IFn on value to validate the value.
    /// The IFn can indicate a failed validation by returning false-y or throwing an exception.
    static member validate(vf: IFn, value: obj) =
        if isNull vf then
            ()
        else
            let ret =
                try
                    RT0.booleanCast (vf.invoke (value))
                with _ ->
                    raise <| InvalidOperationException("Invalid reference state")

            if not ret then
                raise <| InvalidOperationException("Invalid reference state")

    /// Validate a value with the current validator.
    member this.validate(value: obj) = ARef.validate (_validator, value)

    interface IRef with
        member this.setValidator(vf) =
            ARef.validate (vf, (this :> IDeref).deref ())
            _validator <- vf

        member _.getValidator() = _validator

        member this.addWatch(key, callback) =
            _watches <- _watches.assoc (key, callback)
            this :> IRef

        member this.removeWatch(key) =
            _watches <- _watches.without (key)
            this :> IRef

        member _.getWatches() = _watches

    // Some subclasses need to set the validator field without going through IRef.setValidator
    //   because the latter validates with the new validator function first.
    // See Var for an example of this.

    /// Set the validator without validating the current value.  (needed by some subclasses)
    member internal this.setValidatorInternal(vf) = _validator <- vf

    /// Notify all watches of a change in the reference.
    member this.notifyWatches(oldval, newval) =
        let ws = _watches

        if ws.count () > 0 then
            let rec loop (s: ISeq) =
                match s with
                | null -> ()
                | _ ->
                    let me = s.first () :?> IMapEntry
                    let fn = me.value () :?> IFn

                    if not (isNull fn) then
                        fn.invoke (me.key (), this, oldval, newval) |> ignore

                    loop (s.next ())

            loop ((ws :> Seqable).seq ())
