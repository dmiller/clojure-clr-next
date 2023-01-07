namespace Clojure.Collections

open System.Threading
open System


// Translated my old code to F#, but took the hint from Akka.Net that the Volatile's would suffice for Get/Set.

[<AllowNullLiteral>]
type AtomicReference<'T when 'T: not struct>(v) =

    let mutable value: 'T = v

    new() = AtomicReference(Unchecked.defaultof<'T>)

    member _.Get() = Volatile.Read(&value)
    member _.Value = Volatile.Read(&value)

    member _.CompareAndSet(expect, update) =
        let oldVal =
            Interlocked.CompareExchange(&value, update, expect)

        Object.ReferenceEquals(oldVal, expect)

    member this.GetAndSet(update) = Interlocked.Exchange(&value, update)
    member this.Set(update) = Volatile.Write(&value, update)
