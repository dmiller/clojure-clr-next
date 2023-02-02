namespace Clojure.Collections

open System.Threading


[<Sealed>]
type AtomicReference<'T when 'T : not struct >(r:'T) =

    let mutable ref = r

    new() = AtomicReference(Unchecked.defaultof<'T>)

    member this.CompareAndSet(expect:'T,update:'T)  =
        let oldVal = Interlocked.CompareExchange( &ref , expect, update)
        obj.ReferenceEquals(oldVal,expect)

    member this.Get() = ref

    member this.GetAndSet(update:'T) = Interlocked.Exchange(&ref,update)

    override _.ToString() = ref.ToString()

