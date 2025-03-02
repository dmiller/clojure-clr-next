namespace Clojure.Lib

open System.Threading
open Clojure.Collections
open System
open System.Collections.Generic
open Clojure.Numerics
open System.Runtime.CompilerServices

/// Holds values inside a Ref (internal use only)
[<Sealed>]
type internal RefVal private (v: obj, pt: int64) =

    let mutable _value: obj = v
    let mutable _point: int64 = pt

    // these implement a doubly-linked circular list
    // to avoid self-reference check penalties, we use factories to get these initialized properly
    let mutable _prior: RefVal = Unchecked.defaultof<RefVal>
    let mutable _next: RefVal = Unchecked.defaultof<RefVal>

    /// create a list of one element
    static member createSingleton(v, pt) =
        let r = RefVal(v, pt)
        r.Prior <- r
        r.Next <- r
        r

    /// Create a new RefVal and insert it after the given RefVal.
    member this.insertAfter(v, pt) =
        let r = RefVal(v, pt)
        r.Prior <- this
        r.Next <- this.Next
        this.Next <- r
        r.Next.Prior <- r
        r

    // some accessors
    member _.Value = _value
    member _.Point = _point

    member _.Prior
        with get () = _prior
        and private set (v) = _prior <- v

    member _.Next
        with get () = _next
        and private set (v) = _next <- v

    /// Used by Ref to update the value in root node
    member this.SetValue(v, pt) =
        _value <- v
        _point <- pt

    /// Set this node to be a list of one node (itself), discarding the rest of the list.
    member this.Trim() =
        _prior <- this
        _next <- this

/// Locking transaction state
type LTState =
    | Running = 1L
    | Committing = 2L
    | Retry = 3L
    | Killed = 4L
    | Committed = 5L
    | Stopped = 6L

/// An exception thrown when a transaction retry is needed.
exception RetryEx of string

/// An exception thrown when a transaction aborts.
exception AbortEx of string

/// Encapsulates the status of a LockingTransaction
/// Created anew on each iteration of the (re)try code.
[<Sealed>]
type LTInfo(initState: LTState, startPoint: int64) =

    // The actual value for the status is an LTState.
    // Stored here as an int64, which is the underlying represenation of an LTState
    //    so that we can Interlocked methods to modify.
    let mutable _status: int64 = initState |> int64

    // Used to signal potential watchers (other) transactions that we have changed status
    //     so they can stop waiting.
    let _latch = CountdownLatch(1)

    // Accessors

    member _.Latch = _latch
    member _.StartPoint = startPoint

    member this.State
        with get () = Microsoft.FSharp.Core.LanguagePrimitives.EnumOfValue<int64, LTState>(_status)
        and set (v: LTState) = this.set (v) |> ignore

    // We use Interlocked methods because of possible contention between transactions
    member this.compareAndSet(oldVal: LTState, newVal: LTState) =
        let origVal =
            Interlocked.CompareExchange(&_status, newVal |> int64, oldVal |> int64)

        origVal = (oldVal |> int64)

    member this.set(newVal: LTState) =
        Interlocked.Exchange(&_status, newVal |> int64)

    member this.isRunning =
        let s =
            Microsoft.FSharp.Core.LanguagePrimitives.EnumOfValue<int64, LTState>(Interlocked.Read(&_status))

        s = LTState.Running || s = LTState.Committing

/// Pending call of a function on arguments.  Used by the commute operation.
type private CFn = { fn: IFn; args: ISeq }

/// Encapsulates a notification to be sent to watchers
type LTNotify = { ref: Ref; oldVal: obj; newVal: obj }

/// Provides transaction semantics for Agents, Refs, etc.
and [<Sealed>] LockingTransaction() =

    // The number of times to retry a transaction in case of conflicts.
    [<Literal>]
    let RetryLimit = 10000

    // How long to wait for a lock.
    [<Literal>]
    let LockWaitMsecs = 100

    // How old this transaction must be before it is allowed to barge other (newer) transactions.
    // Java version has BARGE_WAIT_NANOS, set at 10*1_000_000.
    // Ticks here are 100 nanos, so we should have  10 * 1_000_000/100 = 100_000.
    [<Literal>]
    let BargeWaitTicks = 100_000L

    // Transaction on the current thread

    // The transaction running on the current thread.  (Thread-local.)
    [<DefaultValue; ThreadStatic>]
    static val mutable private _currentTransaction: LockingTransaction option

    // Get the transaction running on this thread (throw exception if no transaction).
    static member getEx() =
        let transOpt = LockingTransaction._currentTransaction

        match transOpt with
        | None -> raise <| InvalidOperationException("No transaction running")
        | Some t ->
            match t.Info with
            | None -> raise <| InvalidOperationException("No transaction running")
            | Some info -> t

    // Get the transaction running on this thread (or None if no transaction).
    static member getRunning() =
        let transOpt = LockingTransaction._currentTransaction

        match transOpt with
        | None -> None
        | Some t ->
            match t.Info with
            | None -> None
            | Some info -> transOpt

    /// Is there a transaction running on this thread?
    static member isRunning() = LockingTransaction.getRunning().IsSome

    // Point management

    /// The current point.
    static member val private _lastPoint: AtomicLong = AtomicLong()
    // Used to provide a total ordering on transactions for the purpose of determining preference on transactions when there are conflicts.
    // Transactions consume a point for init, for each retry, and on commit if writing.
    

    /// The point at the start of the current retry (or first try).
    let mutable _readPoint: int64 = 0L

    /// The point at the start of the transaction.
    let mutable _startPoint: int64 = 0L

    /// Get a new read point value.
    member this.getReadPoint() =
        _readPoint <- LockingTransaction._lastPoint.incrementAndGet ()

    /// Get a commit point value.
    static member getCommitPoint() =
        LockingTransaction._lastPoint.incrementAndGet ()


    /// The system ticks at the start of the transaction.
    /// Used to initialize the LTInfo
    let mutable _startTime: int64 = 0L

    /// The state of the transaction.
    ///  Encapsulated so things like Refs can look.
    let mutable _info: LTInfo option = None

    member _.Info = _info

    /// Cached retry exception.
    let _retryEx = RetryEx("")

    /// Agent actions pending on this thread.
    let _actions = ResizeArray<AgentAction>()

    // Ref assignments made in this transaction (both sets and commutes).
    let _vals = Dictionary<Ref, obj>()

    // Refs that have been set in this transaction.
    let _sets = HashSet<Ref>()

    // Ref commutes that have been made in this transaction.
    let _commutes = SortedDictionary<Ref, ResizeArray<CFn>>()

    // The set of Refs holding read locks.
    let _ensures = HashSet<Ref>()

    // Actions

    /// Stop this transaction.
    member private this.stop(state: LTState) =
        match _info with
        | None -> ()
        | Some sinfo ->
            lock sinfo (fun () ->
                sinfo.State <- state
                sinfo.Latch.CountDown())

            _info <- None
            _vals.Clear()
            _sets.Clear()
            _commutes.Clear()
    // Java commented out: _actions.Clear()

    member private this.tryWriteLock(r: Ref) =
        try
            if not (r.tryEnterWriteLock (LockWaitMsecs)) then
                raise _retryEx
        with :? ThreadInterruptedException ->
            raise _retryEx

    member private this.releaseIfEnsured(r: Ref) =
        if _ensures.Contains(r) then
            _ensures.Remove(r) |> ignore
            r.exitReadLock ()

    member private this.blockAndBail(refinfo: LTInfo) =
        this.stop (LTState.Retry)

        try
            refinfo.Latch.Await(LockWaitMsecs) |> ignore
        with :? ThreadInterruptedException ->
            ()

        raise _retryEx

    member private this.barge(refinfo: LTInfo) =
        let mutable barged = false

        // if this transaction is older
        //   try to abort the other

        if this.bargeTimeElapsed && _startPoint < refinfo.StartPoint then
            barged <- refinfo.compareAndSet (LTState.Running, LTState.Killed)

            if barged then
                refinfo.Latch.CountDown()

        barged

    member private this.lock(r: Ref) =

        // can't upgrade read lock, so release it.
        this.releaseIfEnsured (r)

        let mutable unlocked = true

        try
            this.tryWriteLock (r)
            unlocked <- false

            if r.currentPoint () > _readPoint then
                raise _retryEx

            let success () =
                r.TxInfo <- _info
                r.currentVal ()

            match r.TxInfo with
            | Some(refinfo: LTInfo) when refinfo.isRunning && not <| obj.ReferenceEquals(refinfo, _info) ->
                if not (this.barge (refinfo)) then
                    r.exitWriteLock ()
                    unlocked <- true
                    this.blockAndBail (refinfo)
                else
                    success ()
            | _ -> success ()
        finally
            if not unlocked then
                r.exitWriteLock ()


    /// Kill this transaction.
    member this.abort() =
        this.stop (LTState.Killed)
        raise <| AbortEx("")

    /// Determine if sufficient clock time has elapsed to barge another transaction.
    member private _.bargeTimeElapsed =
        (int64 Environment.TickCount) - _startTime > BargeWaitTicks



    // TODO: This can be called on something more general than  an IFn.
    // We can could define a delegate for this, probably use ThreadStartDelegate.
    // Should still have a version that takes IFn.
    // The Java original has a version that takes a Callable.
    // How would we generalize this?

    /// Run a function in a transaction.
    static member runInTransaction(fn: IFn) : obj =
        let transOpt = LockingTransaction._currentTransaction

        match transOpt with
        | None ->
            let newTrans = LockingTransaction()
            LockingTransaction._currentTransaction <- Some newTrans

            try
                newTrans.run (fn)
            finally
                LockingTransaction._currentTransaction <- None
        | Some t ->
            match t.Info with
            | None -> t.run (fn)
            | _ -> fn.invoke ()


    /// Determine if the exception wraps a RetryEx at some level.
    static member private containsNestedRetryEx(ex: Exception) =
        let rec loop (e: Exception) =
            match e with
            | null -> false
            | :? RetryEx -> true
            | _ -> loop e.InnerException

        loop ex


    // TODO: Define an overload called on ThreadStartDelegate or something equivalent.

    /// Start a transaction and invoke a function.
    member this.run(fn: IFn) : obj =
        let mutable finished = false
        let mutable ret = null
        let locked = ResizeArray<Ref>()
        let notify = ResizeArray<LTNotify>()

        let mutable i = 0

        while not finished && i < RetryLimit do
            try
                try
                    this.getReadPoint ()

                    if i = 0 then
                        _startPoint <- _readPoint
                        _startTime <- int64 Environment.TickCount

                    let newLTInfo = LTInfo(LTState.Running, _startPoint)

                    _info <- Some <| newLTInfo
                    ret <- fn.invoke ()

                    // make sure no one has killed us before this point,
                    // and can't from now on

                    if newLTInfo.compareAndSet (LTState.Running, LTState.Committing) then

                        for pair in _commutes do
                            let r = pair.Key

                            if _sets.Contains(r) then
                                ()
                            else
                                let wasEnsured = _ensures.Contains(r)
                                // can't upgrade read lock, so release
                                this.releaseIfEnsured (r)
                                this.tryWriteLock (r)
                                locked.Add(r)

                                if wasEnsured && r.currentPoint () > _readPoint then
                                    raise _retryEx

                                match r.TxInfo with
                                | Some refinfo when refinfo <> newLTInfo && refinfo.isRunning ->
                                    if not (this.barge (refinfo)) then
                                        raise _retryEx
                                | _ -> ()

                                let v = r.currentVal ()
                                _vals.[r] <- v

                                for f in pair.Value do
                                    _vals.[r] <- f.fn.applyTo (RTSeq.cons (_vals.[r], f.args))

                        for r in _sets do
                            this.tryWriteLock (r)
                            locked.Add(r)

                        // validate and enqueue notifications
                        for pair in _vals do
                            let r = pair.Key
                            r.validate (pair.Value)

                        // at this point, all values calced, all refs to be written locked
                        // no more client code to be called
                        let commitPoint = LockingTransaction.getCommitPoint ()

                        for pair in _vals do
                            let r = pair.Key
                            let oldval = r.currentVal ()
                            let newval = pair.Value
                            r.setValue (newval, commitPoint)

                            if (r :> IRef).getWatches().count () > 0 then
                                notify.Add(
                                    { ref = r
                                      oldVal = oldval
                                      newVal = newval }
                                )

                        finished <- true
                        newLTInfo.set (LTState.Committed) |> ignore
                with
                | :? RetryEx -> ()
                | ex when not (LockingTransaction.containsNestedRetryEx (ex)) -> reraise ()
            finally
                for k = locked.Count - 1 downto 0 do
                    locked.[k].exitWriteLock ()

                locked.Clear()

                for r in _ensures do
                    r.exitReadLock ()

                _ensures.Clear()
                this.stop (if finished then LTState.Committed else LTState.Retry)

                try
                    if finished then // re-dispatch out of transaction
                        for n in notify do
                            n.ref.notifyWatches (n.oldVal, n.newVal)

                        for a in _actions do
                            Agent.dispatchAction (a)
                finally
                    notify.Clear()
                    _actions.Clear()


            i <- i + 1

        if not finished then
            raise
            <| InvalidOperationException("Transaction failed after reaching retry limit")

        ret

    /// Add an agent action sent during the transaction to a queue.
    member this.enqueue(action: AgentAction) = _actions.Add(action)

    // Operations called by Ref to implement the primary Ref methods in clojure.core:  ref-set, alter, commute, @/deref
    // Each of these must check if the transaction is running and throw a RetryEx if not.
    // (Even though in the Ref, GetEx is called, which means we were running at the time, we might have gotten killed in the interim.

    /// Get the value of a ref most recently set in this transaction (or prior to entering).
    member this.doGet(r: Ref) : obj =
        if _info.Value.isRunning then
            if _vals.ContainsKey(r) then
                _vals.[r]
            else
                let valOpt =
                    try
                        r.enterReadLock ()

                        let rec loop (ver: RefVal) : obj option =
                            if ver.Point <= _readPoint then
                                Some ver.Value
                            elif Object.ReferenceEquals(ver.Prior, r.getRVals ()) then
                                None
                            else
                                loop ver.Prior

                        loop (r.getRVals ())

                    finally
                        r.exitReadLock ()

                match valOpt with
                | None ->
                    // no version of val precedes the read point
                    r.addFault ()
                    raise _retryEx
                | Some v -> v
        else
            raise _retryEx



    /// Set the value of a ref inside the transaction.
    member this.doSet(r: Ref, v: obj) =
        if _info.Value.isRunning then
            if _commutes.ContainsKey(r) then
                raise <| InvalidOperationException("Can't set after commute")

            if not (_sets.Contains(r)) then
                _sets.Add(r) |> ignore
                this.lock (r) |> ignore

            _vals.[r] <- v
            v
        else
            raise _retryEx

    /// Touch a ref.  (Lock it.)
    member this.doEnsure(r: Ref) =
        if _info.Value.isRunning then
            if _ensures.Contains(r) then
                ()
            else
                r.enterReadLock ()

                // someone completed a write after our snapshot
                if r.currentPoint () > _readPoint then
                    r.exitReadLock ()
                    raise _retryEx

                match r.TxInfo with
                | Some refinfo when refinfo.isRunning ->
                    r.exitReadLock ()

                    if not <| Object.ReferenceEquals(refinfo, _info) then
                        this.blockAndBail (refinfo)
                | _ -> _ensures.Add(r) |> ignore // Note: in this case, the read lock is NOT released. Any ensured Ref is read-locked.

        else
            raise _retryEx

    /// Post a commute on a ref in this transaction.
    member this.doCommute(r: Ref, fn: IFn, args: ISeq) : obj =
        if not _info.Value.isRunning then
            raise _retryEx

        if not (_vals.ContainsKey(r)) then
            let v =
                try
                    r.enterReadLock ()
                    r.currentVal ()
                finally
                    r.exitReadLock ()

            _vals[r] <- v

        let mutable fns: ResizeArray<CFn> = null

        if not (_commutes.TryGetValue(r, &fns)) then
            fns <- ResizeArray<CFn>()
            _commutes[r] <- fns

        fns.Add({ fn = fn; args = args })
        let v = fn.applyTo (RTSeq.cons (_vals[r], args))
        _vals[r] <- v
        v

/// A storag cell that supports transactional change.
and [<AllowNullLiteral>] Ref(_initVal: obj, _meta: IPersistentMap) =
    inherit ARef(null)

    /// Generates unique ids.
    static let _ids: AtomicLong = AtomicLong()

    /// An id uniquely identifying this reference.
    let _id: int64 = _ids.getAndIncrement ()

    /// Values at points in time for this reference.
    /// Initial value has timestamp 0.
    let mutable _rvals: RefVal = RefVal.createSingleton (_initVal, 0)

    /// Number of faults for the reference.
    let _faults = AtomicInteger()

    // Reader/writer lock for the reference.
    let _lock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion)

    // Info on the transaction locking this ref.
    let mutable _txInfo: LTInfo option = None

    // For implementing IDisposable
    let mutable _disposed = false

    /// The minimum number of values to keep in the history.
    [<VolatileField>]
    let mutable _minHistory = 0

    /// The maximum number of values to keep in the history.
    [<VolatileField>]
    let mutable _maxHistory = 10

    /// The id of the reference.
    member _.Id = _id

    /// The transaction info for the reference.
    member _.TxInfo
        with get () = _txInfo
        and set (v) = _txInfo <- v

    member internal _.getRVals() = _rvals

    member private this.setTxInfo(v) = _txInfo <- Some v

    // Object overrides

    override _.ToString() = sprintf "<Ref %d>" _id

    override this.Equals(o) =
        match o with
        | _ when Object.ReferenceEquals(this, o) -> true
        | :? Ref as r -> _id = r.Id
        | _ -> false

    override _.GetHashCode() = Murmur3.HashLong(_id)

    interface IComparable<Ref> with
        member this.CompareTo(o) =
            match o with
            | null -> 1
            | _ -> _id.CompareTo(o.Id)

    // History bounds manipulation

    /// The minimum number of values to keep in the history.
    member _.MinHistory = _minHistory

    /// The maximum number of values to keep in the history.
    member _.MaxHistory = _maxHistory

    /// Set the minimum number of values to keep in the history.
    /// Returns the Ref (required by clojure.core)
    member this.SetMinHistory(v) =
        _minHistory <- v
        this

    /// Set the maximum number of values to keep in the history.
    /// Returns the Ref (required by clojure.core)
    member this.SetMaxHistory(v) =
        _maxHistory <- v
        this

    ///  The number of items in the history list (internal use -- needs to be wrapped with a lock)
    member private _.histCount() =
        let mutable count = 0
        let mutable tv = _rvals.Next

        while not (LanguagePrimitives.PhysicalEquality tv _rvals) do
            count <- count + 1
            tv <- tv.Next

        count

    /// The number of items in the history list.
    member this.getHistoryCount() =
        try
            this.enterWriteLock ()
            this.histCount ()
        finally
            this.exitWriteLock ()

    /// Get rid of the history, keeping just the current value
    member this.trimHistory() =
        try
            this.enterWriteLock ()
            _rvals.Trim()
        finally
            this.exitWriteLock ()

    // Convenience methods for locking, primarily for LockingTransaction to use.

    member _.enterReadLock() = _lock.EnterReadLock()
    member _.exitReadLock() = _lock.ExitReadLock()
    member _.enterWriteLock() = _lock.EnterWriteLock()
    member _.exitWriteLock() = _lock.ExitWriteLock()
    member _.tryEnterWriteLock(msecTimeout: int) = _lock.TryEnterWriteLock(msecTimeout)


    /// Add to the fault count.
    member _.addFault() = _faults.incrementAndGet () |> ignore

    /// The current commit point
    member _.currentPoint() = _rvals.Point

    /// The current value
    member _.currentVal() = _rvals.Value

    interface IDeref with
        member this.deref() =
            match LockingTransaction.getRunning () with
            | None ->
                try
                    this.enterReadLock ()
                    _rvals.Value
                finally
                    this.exitReadLock ()
            | Some t -> t.doGet (this)

    /// Set the value
    member this.setValue(v: obj, commitPoint: int64) =
        let hcount = this.histCount ()

        if (_faults.get () > 0 && hcount < _maxHistory) || hcount < _minHistory then
            _rvals <- _rvals.insertAfter (v, commitPoint)
            _faults.set (0) |> ignore
        else
            _rvals <- _rvals.Next
            _rvals.SetValue(v, commitPoint)


    // Transaction operations

    /// Set the value (must be in a transaction).
    member this.set(v: obj) =
        LockingTransaction.getEx().doSet (this, v)

    /// Apply a commute to the reference. (Must be in a transaction.)
    member this.commute(fn: IFn, args: ISeq) =
        LockingTransaction.getEx().doCommute (this, fn, args)

    /// Change to a computed value.
    member this.alter(fn: IFn, args: ISeq) =
        let t = LockingTransaction.getEx ()
        t.doSet (this, fn.applyTo (RTSeq.cons (t.doGet (this), args)))

    /// Touch the reference.  (Add to the tracking list in the current transaction.)
    member this.touch() =
        LockingTransaction.getEx().doEnsure (this)

    // the long painful IFn implementation

    member this.fn() = (this :> IDeref).deref () :?> IFn

    interface IFn with
        member this.applyTo(args: ISeq) = AFn.applyToHelper (this, args)
        member this.invoke() = this.fn().invoke ()
        member this.invoke(arg1) = this.fn().invoke (arg1)
        member this.invoke(arg1, arg2) = this.fn().invoke (arg1, arg2)
        member this.invoke(arg1, arg2, arg3) = this.fn().invoke (arg1, arg2, arg3)

        member this.invoke(arg1, arg2, arg3, arg4) =
            this.fn().invoke (arg1, arg2, arg3, arg4)

        member this.invoke(arg1, arg2, arg3, arg4, arg5) =
            this.fn().invoke (arg1, arg2, arg3, arg4, arg5)

        member this.invoke(arg1, arg2, arg3, arg4, arg5, arg6) =
            this.fn().invoke (arg1, arg2, arg3, arg4, arg5, arg6)

        member this.invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7) =
            this.fn().invoke (arg1, arg2, arg3, arg4, arg5, arg6, arg7)

        member this.invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8) =
            this.fn().invoke (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8)

        member this.invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9) =
            this.fn().invoke (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9)

        member this.invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10) =
            this.fn().invoke (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10)

        member this.invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11) =
            this
                .fn()
                .invoke (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11)

        member this.invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12) =
            this
                .fn()
                .invoke (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12)

        member this.invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13) =
            this
                .fn()
                .invoke (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13)

        member this.invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14) =
            this
                .fn()
                .invoke (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14)

        member this.invoke
            (
                arg1,
                arg2,
                arg3,
                arg4,
                arg5,
                arg6,
                arg7,
                arg8,
                arg9,
                arg10,
                arg11,
                arg12,
                arg13,
                arg14,
                arg15
            ) =
            this
                .fn()
                .invoke (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15)

        member this.invoke
            (
                arg1,
                arg2,
                arg3,
                arg4,
                arg5,
                arg6,
                arg7,
                arg8,
                arg9,
                arg10,
                arg11,
                arg12,
                arg13,
                arg14,
                arg15,
                arg16
            ) =
            this
                .fn()
                .invoke (
                    arg1,
                    arg2,
                    arg3,
                    arg4,
                    arg5,
                    arg6,
                    arg7,
                    arg8,
                    arg9,
                    arg10,
                    arg11,
                    arg12,
                    arg13,
                    arg14,
                    arg15,
                    arg16
                )

        member this.invoke
            (
                arg1,
                arg2,
                arg3,
                arg4,
                arg5,
                arg6,
                arg7,
                arg8,
                arg9,
                arg10,
                arg11,
                arg12,
                arg13,
                arg14,
                arg15,
                arg16,
                arg17
            ) =
            this
                .fn()
                .invoke (
                    arg1,
                    arg2,
                    arg3,
                    arg4,
                    arg5,
                    arg6,
                    arg7,
                    arg8,
                    arg9,
                    arg10,
                    arg11,
                    arg12,
                    arg13,
                    arg14,
                    arg15,
                    arg16,
                    arg17
                )

        member this.invoke
            (
                arg1,
                arg2,
                arg3,
                arg4,
                arg5,
                arg6,
                arg7,
                arg8,
                arg9,
                arg10,
                arg11,
                arg12,
                arg13,
                arg14,
                arg15,
                arg16,
                arg17,
                arg18
            ) =
            this
                .fn()
                .invoke (
                    arg1,
                    arg2,
                    arg3,
                    arg4,
                    arg5,
                    arg6,
                    arg7,
                    arg8,
                    arg9,
                    arg10,
                    arg11,
                    arg12,
                    arg13,
                    arg14,
                    arg15,
                    arg16,
                    arg17,
                    arg18
                )

        member this.invoke
            (
                arg1,
                arg2,
                arg3,
                arg4,
                arg5,
                arg6,
                arg7,
                arg8,
                arg9,
                arg10,
                arg11,
                arg12,
                arg13,
                arg14,
                arg15,
                arg16,
                arg17,
                arg18,
                arg19
            ) =
            this
                .fn()
                .invoke (
                    arg1,
                    arg2,
                    arg3,
                    arg4,
                    arg5,
                    arg6,
                    arg7,
                    arg8,
                    arg9,
                    arg10,
                    arg11,
                    arg12,
                    arg13,
                    arg14,
                    arg15,
                    arg16,
                    arg17,
                    arg18,
                    arg19
                )

        member this.invoke
            (
                arg1,
                arg2,
                arg3,
                arg4,
                arg5,
                arg6,
                arg7,
                arg8,
                arg9,
                arg10,
                arg11,
                arg12,
                arg13,
                arg14,
                arg15,
                arg16,
                arg17,
                arg18,
                arg19,
                arg20
            ) =
            this
                .fn()
                .invoke (
                    arg1,
                    arg2,
                    arg3,
                    arg4,
                    arg5,
                    arg6,
                    arg7,
                    arg8,
                    arg9,
                    arg10,
                    arg11,
                    arg12,
                    arg13,
                    arg14,
                    arg15,
                    arg16,
                    arg17,
                    arg18,
                    arg19,
                    arg20
                )

        member this.invoke
            (
                arg1,
                arg2,
                arg3,
                arg4,
                arg5,
                arg6,
                arg7,
                arg8,
                arg9,
                arg10,
                arg11,
                arg12,
                arg13,
                arg14,
                arg15,
                arg16,
                arg17,
                arg18,
                arg19,
                arg20,
                [<ParamArray>] args
            ) =
            this
                .fn()
                .invoke (
                    arg1,
                    arg2,
                    arg3,
                    arg4,
                    arg5,
                    arg6,
                    arg7,
                    arg8,
                    arg9,
                    arg10,
                    arg11,
                    arg12,
                    arg13,
                    arg14,
                    arg15,
                    arg16,
                    arg17,
                    arg18,
                    arg19,
                    arg20,
                    args
                )


    member private _.Dispose(disposing: bool) =
        if not _disposed then
            if disposing then
                if not (isNull _lock) then
                    _lock.Dispose()

            _disposed <- true

    interface IDisposable with
        member this.Dispose() =
            this.Dispose(true)
            GC.SuppressFinalize(this)


/// An encapulated message to an agent
and [<Sealed>] AgentAction(_agent: Agent, _fn: IFn, _args: ISeq, _solo: bool) =

    /// The agent to which the message is to be sent.
    member this.Agent = _agent

    /// Send the message
    member this.execute() : unit =
        try
            if _solo then
                let thread = new Thread(ParameterizedThreadStart(this.executeAction))
                thread.Start(null)
            else
                ThreadPool.QueueUserWorkItem(WaitCallback(this.executeAction)) |> ignore
        with e ->
            let handler: IFn = _agent.getErrorHandler ()

            if not <| isNull handler then
                try
                    handler.invoke (e) |> ignore
                with _ ->
                    () // ignore errorHandler errors

    /// Worker method to execute the action on a thread.
    member _.executeAction(state: obj) : unit =
        try
            Agent.Nested <- PersistentVector.Empty :> IPersistentVector

            let mutable error: Exception = null

            try
                let oldval = _agent.getState
                let newval = _fn.applyTo (RTSeq.cons (oldval, _args))
                _agent.setState (newval) |> ignore
                _agent.notifyWatches (oldval, newval)
            with e ->
                error <- e

            if isNull error then
                Agent.releasePendingSends () |> ignore
            else
                Agent.Nested <- null // allow errorHandler to send
                let handler: IFn = _agent.getErrorHandler ()

                if not <| isNull handler then
                    try
                        handler.invoke (_agent :> obj, error :> obj) |> ignore
                    with _ ->
                        () // ignore errorHandler errors

                if Object.ReferenceEquals(_agent.getErrorMode (), Agent.continueKeyword) then
                    error <- null

            let mutable popped = false
            let mutable next: ActionQueue = ActionQueue.Empty

            while not popped do
                let prior = _agent.ActionQueue.Get()

                next <-
                    { Queue = prior.Queue.pop ()
                      Error = error }

                popped <- _agent.ActionQueue.CompareAndSet(prior, next)

            if (isNull error && next.Queue.count () > 0) then
                ((next.Queue.peek ()) :?> AgentAction).execute ()

        finally
            Agent.Nested <- null

/// A queue of actions for an agent. (immutable)
and ActionQueue =
    { Queue: IPersistentStack
      Error: Exception }

    /// An empty ActionQueue.
    static member Empty: ActionQueue =
        { Queue = PersistentQueue.Empty
          Error = null }


/// Represents an Agent.
and [<Sealed>] Agent(v: obj, meta: IPersistentMap) as this =
    inherit ARef(meta)


    // The Java implementation plays many more games with thread pools.  The CLR does not provide such support.
    // TODO: think about the task library.  (I did the original CLR implementation under .NET 3.x, before the task library came out.)

    /// The current state of the agent.
    [<VolatileField>]
    let mutable _state: obj = v

    /// The error mode of the agent
    [<VolatileField>]
    let mutable _errorMode = Agent.continueKeyword

    /// The error handler for the agent.
    [<VolatileField>]
    let mutable _errorHandler: IFn = null

    /// Agent errors, a sequence of Exceptions.
    [<VolatileField>]
    let mutable _errors: ISeq = null

    /// A collection of agent actions enqueued during the current transaction.  Per thread.
    [<DefaultValue; ThreadStatic>]
    static val mutable private _nested: IPersistentVector

    /// A queue of pending actions.
    let _aq = AtomicReference<ActionQueue>(ActionQueue.Empty)

    /// The queue of pending actions for this agent.
    member _.ActionQueue: AtomicReference<ActionQueue> = _aq

    do this.setState (v) |> ignore

    /// Create an Agent with null metadata.
    new(v) = Agent(v, null)

    /// :continue
    static member val continueKeyword: Keyword = Keyword.intern (null, "continue")

    /// Get the current state of the agent.
    member _.getState = _state

    /// The number of actions in the queue.
    member _.getQueueCount = _aq.Get().Queue.count()

    //member _.addError(e) = errors <- RTSeq.cons(e, errors)

    static member Nested
        with get () = Agent._nested
        and set (v) = Agent._nested <- v

    // Set the value of the agent, with validation. Returns true if the value was changed.
    member this.setState(newState: obj) : bool =
        (this :> ARef).validate (newState)
        let ret = not <| Object.ReferenceEquals(_state, newState)
        _state <- newState
        ret


    member _.getError() = _aq.Get().Error

    member _.getErrorMode() = _errorMode
    member _.setErrorMode(kw: Keyword) = _errorMode <- kw
    member _.getErrorHandler() = _errorHandler
    member _.setErrorHandler(f: IFn) = _errorHandler <- f

    /// Restart the actions on an agent (in case of an error).
    [<MethodImpl(MethodImplOptions.Synchronized)>]
    member this.restart(newState: obj, clearActions: bool) : obj =

        if this.getError () = null then
            raise <| InvalidOperationException("Agent does not need a restart")

        (this :> ARef).validate (newState)
        _state <- newState

        if clearActions then
            _aq.Set(ActionQueue.Empty)
        else
            // spin-lock to update
            let rec loop () =
                let prior = _aq.Get()
                let restarted = _aq.CompareAndSet(prior, { Queue = prior.Queue; Error = null })
                if (not restarted) then loop () else prior

            let prior = loop ()

            if prior.Queue.count () > 0 then
                ((prior.Queue.peek ()) :?> AgentAction).execute ()

        newState


    /// Send a message to the agent.
    member this.dispatch(fn: IFn, args: ISeq, solo: bool) =
        let error = this.getError ()

        if not <| isNull error then
            raise <| InvalidOperationException("Agent is failed, needs restart", error)

        let action = AgentAction(this, fn, args, solo)
        Agent.dispatchAction (action)
        this

    /// Send an action
    /// If there is transaction running on this thread, defer execution until the transaction ends (enqueue on the transaction).
    /// If there is already an action running, enqueue it.
    /// Otherwise, queue it for execution.
    static member dispatchAction(action: AgentAction) =
        let transOpt = LockingTransaction.getRunning ()

        match transOpt with
        | Some trans -> trans.enqueue (action)
        | None ->
            let nested = Agent._nested

            if not <| isNull nested then
                Agent._nested <- nested.cons (action)
            else
                action.Agent.enqueue (action)

    /// Enqueue an action in the pending queue.
    /// spin-locks to update the queue.
    member this.enqueue(action: AgentAction) =
        let rec loop () =
            let prior = _aq.Get()
            let queued = _aq.CompareAndSet(prior, { Queue = prior.Queue; Error = null })
            if (not queued) then loop () else prior

        let prior = loop ()

        if prior.Queue.count () = 0 && isNull prior.Error then
            action.execute ()

    interface IDeref with
        member this.deref() = _state

    // For compatibility
    // if we ever implement separate queueing, we'll need to do something here.
    // Comment in C# code:
    //      Shutdown all threads executing.
    //      We need to work on this.</remarks>
    static member public shutdown() = ()

    /// Enqueue nested actions
    static member releasePendingSends() : int =
        let sends = Agent._nested

        if isNull sends then
            0
        else
            for i in 0 .. sends.count () - 1 do
                let a = sends.nth (i) :?> AgentAction
                a.Agent.enqueue (a)

            Agent._nested <- PersistentVector.Empty
            sends.count ()
