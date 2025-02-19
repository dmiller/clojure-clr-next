namespace Clojure.Collections

open System.Threading


///////////////////////////////////////////////////////////////////
//
//  Implements various classes from java.util.concurrent.atomic
//
///////////////////////////////////////////////////////////////////

/// Provides atomic access to a cell holding a reference to an object (non-struct).
[<Sealed; AllowNullLiteral>]
type AtomicReference<'T when 'T: not struct>(ref: 'T) =

    let mutable _ref = ref

    /// Create an AtomicReference with a null reference.
    new() = AtomicReference(Unchecked.defaultof<'T>)

    override _.ToString() = _ref.ToString()

    /// Return the current value.
    member this.Get() = _ref

    /// Set the value to the provided argument and returns the original value.
    member this.GetAndSet(update: 'T) = Interlocked.Exchange(&_ref, update)

    // Set the value
    member this.Set(update: 'T) =
        Interlocked.Exchange(&_ref, update) |> ignore

    /// Compare and set the value if it is reference-equal to the expected value.
    member this.CompareAndSet(expect: 'T, update: 'T) =
        let oldVal = Interlocked.CompareExchange(&_ref, update, expect)
        LanguagePrimitives.PhysicalEquality oldVal expect


/// Provides atomic access to a cell holding a reference to a boolean value.
[<Sealed; AllowNullLiteral>]
type AtomicBoolean private (v: int) =

    // we store the value as integer so we can use methods from Interlocked.
    let mutable _val: int = v

    /// Create an AtomicBoolean initialted to false
    new() = AtomicBoolean(0)

    /// Create an AtomicBoolean initialized to the given value.
    new(b: bool) = AtomicBoolean(AtomicBoolean.boolToInt (b))

    override _.ToString() = ref.ToString()

    /// Convert from boolean to an integer
    static member private boolToInt(v: bool) : int = if v then 1 else 0

    /// Convert from an integer to a boolean
    static member private intToBool(v: int) : bool = v <> 0

    /// Get the value
    member this.Get() = AtomicBoolean.intToBool (_val)

    /// Set the value to the provided argument and returns the original value.
    member this.GetAndSet(update: bool) =
        let oldVal = Interlocked.Exchange(&_val, AtomicBoolean.boolToInt (update))
        AtomicBoolean.intToBool (oldVal)

    // Set the value
    member this.Set(update: bool) =
        Interlocked.Exchange(&_val, AtomicBoolean.boolToInt (update)) |> ignore

    /// Compare and set the value if it is equal to the expected value.
    member this.CompareAndSet(oldVal: bool, newVal: bool) =
        let ioldVal = AtomicBoolean.boolToInt (oldVal)
        let inewVal = AtomicBoolean.boolToInt (newVal)

        let origVal = Interlocked.CompareExchange(ref _val, inewVal, ioldVal)
        origVal = ioldVal

/// Provides atomic access to a cell holding an int64 value.
// Implements the Java java.util.concurrent.atomic.AtomicLong class.
[<Sealed; AllowNullLiteral>]
type AtomicLong(v: int64) =

    let mutable _val = v

    /// Create an AtomicLong initialized to 0
    new() = AtomicLong(0)

    override _.ToString() = _val.ToString()

    /// Get the value
    member this.get() = _val

    /// Set the value to the provided argument and returns the original value.
    member this.getAndSet(update: int64) = Interlocked.Exchange(&_val, update)

    // Set the value
    member this.set(update: int64) = Interlocked.Exchange(&_val, update)

    /// Compare and set the value if it is equal to the expected value.
    member this.compareAndSet(expect: int64, update: int64) =
        let oldVal = Interlocked.CompareExchange(&_val, expect, update)
        oldVal = expect

    /// Increment the current value by 1 and return the new value.
    member this.incrementAndGet() = Interlocked.Increment(&_val)

    /// Increment the current value by 1 and return the old value.
    member this.getAndIncrement() = Interlocked.Increment(&_val) - 1L

/// Provides atomic access to a cell holding an int32 value.
/// Implements the Java java.util.concurrent.atomic.AtomicInteger class.
[<Sealed; AllowNullLiteral>]
type AtomicInteger(v: int) =

    let mutable _val = v

    /// Create an AtomicInteger initialized to 0
    new() = AtomicInteger(0)

    override _.ToString() = _val.ToString()

    /// Get the value
    member this.get() = _val

    /// Set the value to the provided argument and returns the original value.
    member this.getAndSet(update: int) = Interlocked.Exchange(&_val, update)

    // Set the value
    member this.set(update: int) = Interlocked.Exchange(&_val, update)

    /// Compare and set the value if it is equal to the expected value.
    member this.compareAndSet(expect: int, update: int) =
        let oldVal = Interlocked.CompareExchange(&_val, expect, update)
        oldVal = expect

    /// Increment the current value by 1 and return the new value.
    member this.incrementAndGet() = Interlocked.Increment(&_val)

    /// Increment the current value by 1 and return the old value.
    member this.getAndIncrement() = Interlocked.Increment(&_val) - 1
