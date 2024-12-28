namespace Clojure.Lib

open Clojure.Collections
open System.Runtime.CompilerServices
open System


// Provides a basic implementation of IReference functionality.
// The JVM implementation does not make this abstract, but we're never going to instantiate one of these directly.
[<AbstractClass;AllowNullLiteral>]
type AReference(m : IPersistentMap) =

    let mutable meta = m
    
    new() = AReference(null)

    member _.Meta = meta

    interface IReference with
        [<MethodImpl(MethodImplOptions.Synchronized)>]
        member this.alterMeta(alter : IFn, args : ISeq) =
            meta <- alter.applyTo(Cons(meta, args)) :?> IPersistentMap
            meta

        [<MethodImpl(MethodImplOptions.Synchronized)>]
        member this.resetMeta(m) =
            meta <- m
            m

    interface IMeta with
        [<MethodImpl(MethodImplOptions.Synchronized)>]
        member this.meta() = meta


// Provides a basic implementation of IRef functionality.
[<AbstractClass;AllowNullLiteral>]
type ARef(m) =
    inherit AReference(m)

    [<VolatileField>]
    let mutable validator : IFn  = null

    [<VolatileField>]
    let mutable watches : IPersistentMap = PersistentHashMap.Empty

    
    new() = ARef(null)

    interface IMeta with
        member this.meta() = this.Meta

    interface IDeref with
         member _.deref() =
            raise
            <| NotImplementedException("Derived classes must implement IDeref.deref()")

    // Invoke an IFn on value to validate the value.
    // The IFn can indicate a failed validation by returning false-y or throwing an exception.

    static member validate(vf : IFn, value: obj) =
        if isNull vf then
            ()
        else
            let ret =
                try
                    RT0.booleanCast(vf.invoke(value))
                with
                | _ -> raise <| InvalidOperationException("Invalid reference state")

            if not ret then
                raise <| InvalidOperationException("Invalid reference state")

    member this.validate(value: obj) = ARef.validate(validator, value)

    interface IRef with
        member this.setValidator(vf) =        
            ARef.validate(vf, (this:>IDeref).deref())
            validator <- vf

        member _.getValidator() = validator

        member this.addWatch(key, callback) =
            watches <- watches.assoc(key, callback)
            this :> IRef

        member this.removeWatch(key) =
            watches <- watches.without(key)
            this :> IRef

        member _.getWatches() = watches

    // Some subclasses need to set the validator field without going through IRef.setValidator
    //   because the latter validates with the new validator function first.
    // See Var for an example of this.
    member internal this.setValidatorInternal(vf) = validator <- vf

    member this.notifyWatches(oldval, newval) =
        let ws = watches
        if ws.count() > 0 then
            let rec loop (s: ISeq) =
                match s with
                | null -> ()
                | _ ->
                    let me = s.first() :?> IMapEntry
                    let fn = me.value() :?> IFn
                    if not (isNull fn) then
                        fn.invoke(me.key(), this, oldval, newval) |> ignore
                    loop (s.next())
            loop ((ws :> Seqable).seq())
