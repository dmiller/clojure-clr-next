namespace Clojure.Collections

open System.Threading


///////////////////////////////////////////////////////////////////
//
//  Implements various classes from java.util.concurrent.atomic 
//
///////////////////////////////////////////////////////////////////


[<Sealed;AllowNullLiteral>]
type AtomicReference<'T when 'T: not struct>(r: 'T) =

    let mutable ref = r

    new() = AtomicReference(Unchecked.defaultof<'T>)

    override _.ToString() = ref.ToString()

    member this.Get() = ref

    member this.GetAndSet(update: 'T) = Interlocked.Exchange(&ref, update)

    
    member this.Set(update: 'T) = Interlocked.Exchange(&ref, update) |> ignore

    member this.CompareAndSet(expect: 'T, update: 'T) =
        let oldVal = Interlocked.CompareExchange(&ref, expect, update)
        LanguagePrimitives.PhysicalEquality oldVal expect



[<Sealed;AllowNullLiteral>]
type AtomicBoolean private(v:int) = 

    let mutable value : int  = v

    new() = AtomicBoolean(0)
    new(b:bool) = AtomicBoolean(AtomicBoolean.boolToInt(b))

    override _.ToString() = ref.ToString()

    static member boolToInt(v:bool) : int = if v then 1 else 0
    static member intToBool(v:int) : bool = v <> 0

    member this.Get() = AtomicBoolean.intToBool(value)

    member this.GetAndSet(update:bool) = 
        let oldVal = Interlocked.Exchange(&value, AtomicBoolean.boolToInt(update))
        AtomicBoolean.intToBool(oldVal)
    
    member this.Set(update: bool) = Interlocked.Exchange(&value, AtomicBoolean.boolToInt(update)) |> ignore

    member this.CompareAndSet(oldVal:bool, newVal:bool) =
        let ioldVal = AtomicBoolean.boolToInt(oldVal)
        let inewVal = AtomicBoolean.boolToInt(newVal)

        let origVal = Interlocked.CompareExchange(ref value, inewVal, ioldVal)
        origVal = ioldVal


// Implements the Java java.util.concurrent.atomic.AtomicLong class.  
[<Sealed;AllowNullLiteral>]
type AtomicLong(v: int64) =

    let mutable value = v

    new() = AtomicLong(0)

    override _.ToString() = value.ToString()

    member this.get() = value

    member this.getAndSet(update: int64) = Interlocked.Exchange(&value, update)

    
    member this.set(update: int64) = Interlocked.Exchange(&value, update)

    member this.compareAndSet(expect: int64, update: int64) =
        let oldVal = Interlocked.CompareExchange(&value, expect, update)
        oldVal  = expect

    member this.incrementAndGet() = Interlocked.Increment(&value)

    member this.getAndIncrement() = Interlocked.Increment(&value) - 1L

