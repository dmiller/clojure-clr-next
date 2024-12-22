namespace Clojure.Lib

open System.Threading
open Clojure.Collections
open System
open System.Collections.Generic
open Clojure.Numerics
open System.Runtime.CompilerServices

// Holds values inside a Ref
[<Sealed>]
type internal RefVal private (v: obj, pt: int64) = 

    let mutable value : obj = v
    let mutable point : int64 = pt

    // these implement a doubly-linked circular list
    // to avoid self-reference check penalties, we use factories to get these initialized properly
    let mutable prior : RefVal = Unchecked.defaultof<RefVal>
    let mutable next : RefVal =  Unchecked.defaultof<RefVal>

    // create a list of one element
    static member  createSingleton(v, pt) = 
        let r = RefVal(v, pt)
        r.Prior <- r
        r.Next <- r
        r

    // Create a new RefVal and insert it after the given RefVal.
    member this.insertAfter(v, pt) = 
        let r = RefVal(v, pt)        
        r.Prior <- this
        r.Next <- this.Next
        this.Next <- r
        r.Next.Prior <- r
        r

    // some accessors
    member _.Value with get() = value
    member _.Point with get() = point

    member _.Prior 
        with get() = prior
        and private set(v) = prior <- v

    member _.Next 
        with get() = next
        and private set(v) = next <- v

    // Used by Ref to update the value in root node
    member this.SetValue(v, pt) =
        value <- v
        point <- pt

    // Set this node to be a list of one node (itself), discarding the rest of the list.
    member this.Trim() = 
        prior <- this
        next <- this

type LTState = 
    | Running = 1L
    | Committing = 2L
    | Retry = 3L
    | Killed = 4L
    | Committed = 5L
    | Stopped = 6L

exception RetryEx of string
exception AbortEx of string

// Encapsulates the status of a LockingTransaction
// Created anew each iteration of the (re)try code.
[<Sealed>]
type LTInfo(initState: LTState, startPoint: int64) = 

    // The actual value for the status is an LTState.
    // Stored here as an int64, which is the underlying represenation of an LTState
    //    so that we can Interlocked methods to modify.
    let mutable status : int64 = initState |> int64

    // Used to signal potential watchers (other) transactions that we have changed status
    //     so they can stop waiting.
    let latch = CountdownLatch(1)

    // Accessors

    member _.Latch = latch
    member _.StartPoint = startPoint

    member this.State
        with get() = Microsoft.FSharp.Core.LanguagePrimitives.EnumOfValue<int64, LTState>(status)
        and set(v:LTState) = this.set(v) |> ignore
    
    // We use Interlocked methods because of possible contention between transactions
    member this.compareAndSet(oldVal: LTState, newVal: LTState) =
        let origVal = Interlocked.CompareExchange(&status, newVal |> int64, oldVal |> int64)
        origVal = (oldVal |> int64)

    member this.set(newVal: LTState) =
        Interlocked.Exchange(&status, newVal |> int64)

    member this.isRunning =
        let s = Microsoft.FSharp.Core.LanguagePrimitives.EnumOfValue<int64, LTState>(Interlocked.Read(&status)) 
        s = LTState.Running || s = LTState.Committing   

// Pending call of a function on arguments.  Used by the commute operation.
type private CFn  = { fn: IFn; args: ISeq }

// Encapsulates a notification to be sent to watchers
type LTNotify = { ref: Ref; oldVal: obj; newVal: obj }

// Provides transaction semantics for Agents, Refs, etc.
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
    [<DefaultValue;ThreadStatic>]
    static val mutable private currentTransaction : LockingTransaction option
    
    // Get the transaction running on this thread (throw exception if no transaction). 
    static member getEx() =
        let transOpt = LockingTransaction.currentTransaction
        match transOpt with
        | None -> 
            raise <| InvalidOperationException("No transaction running")
        | Some t ->
            match t.Info with
            | None -> 
                raise <| InvalidOperationException("No transaction running")
            | Some info -> t

    // Get the transaction running on this thread (or None if no transaction).
    static member getRunning() =
        let transOpt = LockingTransaction.currentTransaction
        match transOpt with
        | None -> None
        | Some t ->
            match t.Info with
            | None -> None
            | Some info -> transOpt

    // Is there a transaction running on this thread?
    static member isRunning() = LockingTransaction.getRunning().IsSome

    // Point management

    // The current point.
    // Used to provide a total ordering on transactions for the purpose of determining preference on transactions when there are conflicts.
    // Transactions consume a point for init, for each retry, and on commit if writing.
    static member private lastPoint : AtomicLong  = AtomicLong()

    // The point at the start of the current retry (or first try).
    let mutable readPoint : int64 = 0L

    // The point at the start of the transaction.
    let mutable startPoint : int64 = 0L

    // Get a new read point value.
    member this.getReadPoint() =
        readPoint <- LockingTransaction.lastPoint.incrementAndGet()

    // Get a commit point value.
    static member getCommitPoint() =
        LockingTransaction.lastPoint.incrementAndGet()


    // The system ticks at the start of the transaction.
    // Used to initialize the LTInfo
    let mutable startTime : int64 = 0L

    // The state of the transaction.
    //  Encapsulated so things like Refs can look.
    let mutable info : LTInfo option = None

    member _.Info = info

    // Cached retry exception.
    let retryEx = RetryEx("")

    // Agent actions pending on this thread.
    let actions = ResizeArray<AgentAction>()

    // Ref assignments made in this transaction (both sets and commutes).
    let vals  = Dictionary<Ref,obj>()

    // Refs that have been set in this transaction.
    let sets = HashSet<Ref>()

    // Ref commutes that have been made in this transaction.
    let commutes = SortedDictionary<Ref, ResizeArray<CFn>>()

    // The set of Refs holding read locks.
    let ensures = HashSet<Ref>()

    // Actions

    // Stop this transaction.
    member private this.stop(state: LTState) =
        match info with
        | None -> ()
        | Some sinfo ->
            lock sinfo (fun () ->
                sinfo.State <- state
                sinfo.Latch.CountDown())
            info <- None
            vals.Clear()
            sets.Clear()
            commutes.Clear()
            // Java commented out: _actions.Clear()

    member private this.tryWriteLock(r: Ref) =
        try 
            if not (r.tryEnterWriteLock(LockWaitMsecs)) then
                raise retryEx
        with 
        | :? ThreadInterruptedException -> raise retryEx

    member private this.releaseIfEnsured(r: Ref) =
        if ensures.Contains(r) then
            ensures.Remove(r) |> ignore
            r.exitReadLock()

    member private this.blockAndBail(refinfo: LTInfo) =
        this.stop(LTState.Retry)
        try
            refinfo.Latch.Await(LockWaitMsecs) |> ignore
        with
        | :? ThreadInterruptedException -> ()
        raise retryEx

    member private this.barge(refinfo: LTInfo) =
        let mutable barged = false

        // if this transaction is older
        //   try to abort the other

        if this.bargeTimeElapsed && startPoint < refinfo.StartPoint then
            barged <- refinfo.compareAndSet(LTState.Running, LTState.Killed)
            if barged then
                refinfo.Latch.CountDown()
        barged

    member private this.lock(r : Ref) =

        // can't upgrade read lock, so release it.
        this.releaseIfEnsured(r)

        let mutable unlocked = true
        try
            this.tryWriteLock(r)
            unlocked <- false

            if r.currentPoint() > readPoint then
                raise retryEx

            let success() = 
                r.TxInfo <- info
                r.currentVal()

            match r.TxInfo with
            | Some (refinfo:LTInfo) when refinfo.isRunning && not <| obj.ReferenceEquals(refinfo, info) ->
                if not (this.barge(refinfo)) then
                    r.exitWriteLock()
                    unlocked <- true
                    this.blockAndBail(refinfo)
                else
                   success()
            | _ -> success()
        finally
            if not unlocked then
                r.exitWriteLock()


    // Kill this transaction.
    member this.abort() =
        this.stop(LTState.Killed)
        raise <| AbortEx("")

    // Determine if sufficient clock time has elapsed to barge another transaction.
    member private _.bargeTimeElapsed = (int64 Environment.TickCount) - startTime > BargeWaitTicks


    // Run a function in a transaction.
    // TODO: This can be called on something more general than  an IFn.
    // We can could define a delegate for this, probably use ThreadStartDelegate.
    // Should still have a version that takes IFn.
    // The Java original has a version that takes a Callable.
    // How would we generalize this?

    static member runInTransaction(fn:IFn) : obj =
        let transOpt = LockingTransaction.currentTransaction
        match transOpt with
        | None ->
            let newTrans = LockingTransaction()
            LockingTransaction.currentTransaction <- Some newTrans
            try
                newTrans.run(fn)
            finally
                LockingTransaction.currentTransaction <- None
        | Some t ->
            match t.Info with 
            | None -> t.run(fn)
            | _ ->  fn.invoke()


    // Determine if the exception wraps a RetryEx at some level.
    static member containsNestedRetryEx(ex: Exception)=
        let rec loop (e: Exception) =
            match e with
            | null -> false
            | :? RetryEx -> true
            | _ -> loop e.InnerException
        loop ex
 
    // Start a transaction and invoke a function.
    // TODO: Define an overload called on ThreadStartDelegate or something equivalent.

    member this.run(fn:IFn) : obj =
        let mutable finished = false
        let mutable ret = null
        let locked = ResizeArray<Ref>()
        let notify = ResizeArray<LTNotify>()
            
        let mutable i = 0

        while not finished && i < RetryLimit do 
            try
                try
                    this.getReadPoint()

                    if i = 0 then
                        startPoint <- readPoint
                        startTime <- int64 Environment.TickCount

                    let newLTInfo = LTInfo(LTState.Running, startPoint)

                    info <- Some <| newLTInfo
                    ret <- fn.invoke()

                    // make sure no one has killed us before this point,
                    // and can't from now on

                    if newLTInfo.compareAndSet(LTState.Running,LTState.Committing) then

                        for pair in commutes do
                            let r = pair.Key
                            if sets.Contains(r) then
                                ()
                            else
                                let wasEnsured = ensures.Contains(r)
                                // can't upgrade read lock, so release
                                this.releaseIfEnsured(r)
                                this.tryWriteLock(r)
                                locked.Add(r)

                                if wasEnsured && r.currentPoint() > readPoint then
                                    raise retryEx

                                match r.TxInfo with
                                | Some refinfo when refinfo <> newLTInfo && refinfo.isRunning ->
                                    if not (this.barge(refinfo)) then
                                        raise retryEx
                                | _ -> ()

                                let v = r.currentVal()
                                vals.[r] <- v
                                for f in pair.Value do
                                    vals.[r] <- f.fn.applyTo(RTSeq.cons(vals.[r], f.args))

                        for r in sets do
                            this.tryWriteLock(r)
                            locked.Add(r)

                        // validate and enqueue notifications
                        for pair in vals do
                            let r = pair.Key
                            r.validate(pair.Value)

                        // at this point, all values calced, all refs to be written locked
                        // no more client code to be called
                        let commitPoint = LockingTransaction.getCommitPoint()
                        for pair in vals do
                            let r = pair.Key
                            let oldval = r.currentVal()
                            let newval = pair.Value
                            r.setValue(newval, commitPoint)
                            if (r:>IRef).getWatches().count() > 0 then
                                notify.Add({ ref = r; oldVal = oldval; newVal = newval })

                        finished <- true
                        newLTInfo.set(LTState.Committed) |> ignore
                with
                    | :? RetryEx -> ()
                    | ex when not (LockingTransaction.containsNestedRetryEx(ex)) -> reraise()
            finally
                for k = locked.Count - 1 downto 0 do
                    locked.[k].exitWriteLock()
                locked.Clear()
                for r in ensures do
                    r.exitReadLock()
                ensures.Clear()
                this.stop(if finished then LTState.Committed else LTState.Retry)
                try
                    if finished then  // re-dispatch out of transaction
                        for n in notify do
                            n.ref.notifyWatches(n.oldVal, n.newVal)
                        for a in actions do
                            Agent.dispatchAction(a)
                finally
                    notify.Clear()
                    actions.Clear()
                    
                
            i <- i + 1

        if not finished then
            raise <| InvalidOperationException("Transaction failed after reaching retry limit")

        ret

        // Add an agent action sent during the transaction to a queue.
        member this.enqueue(action: AgentAction) = actions.Add(action)

        // Operations called by Ref to implement the primary Ref methods in clojure.core:  ref-set, alter, commute, @/deref
        // Each of these must check if the transaction is running and throw a RetryEx if not.
        // (Even though in the Ref, GetEx is called, which means we were running at the time, we might have gotten killed in the interim.

        // Get the value of a ref most recently set in this transaction (or prior to entering).
        member this.doGet(r: Ref) : obj =
            if info.Value.isRunning then
                if vals.ContainsKey(r) then
                    vals.[r]
                else
                    let valOpt = 
                        try
                            r.enterReadLock()

                            let rec loop (ver:RefVal) : obj option =
                                if ver.Point <= readPoint then
                                    Some ver.Value
                                elif Object.ReferenceEquals(ver.Prior, r.getRVals()) then
                                    None
                                else
                                    loop ver.Prior

                            loop (r.getRVals())

                        finally
                            r.exitReadLock()

                    match valOpt with
                    |None ->
                        // no version of val precedes the read point
                        r.addFault()
                        raise retryEx
                    |Some v -> v
            else 
                raise retryEx



        // Set the value of a ref inside the transaction.
        member this.doSet(r: Ref, v: obj) =
            if info.Value.isRunning then
                if commutes.ContainsKey(r) then
                    raise <| InvalidOperationException("Can't set after commute")
                if not (sets.Contains(r)) then
                    sets.Add(r) |> ignore
                    this.lock(r) |> ignore
                vals.[r] <- v
                v
            else
                raise retryEx

        // Touch a ref.  (Lock it.)
        member this.doEnsure(r: Ref) =
            if info.Value.isRunning then
                if ensures.Contains(r) then
                    ()
                else
                    r.enterReadLock()

                    // someone completed a write after our snapshot
                    if r.currentPoint() > readPoint then
                        r.exitReadLock()
                        raise retryEx
                    
                    match r.TxInfo with
                    | Some refinfo when refinfo.isRunning ->
                        r.exitReadLock()
                        if not <| Object.ReferenceEquals(refinfo,info) then
                            this.blockAndBail(refinfo)
                    | _ -> ensures.Add(r) |> ignore  // Note: in this case, the read lock is NOT released. Any ensured Ref is read-locked.

            else 
                raise retryEx

        // Post a commute on a ref in this transaction.        
        member this.doCommute(r:Ref, fn:IFn, args:ISeq) : obj =
            if not info.Value.isRunning then
                raise retryEx
            
            if not (vals.ContainsKey(r)) then
                let v = 
                    try
                        r.enterReadLock()
                        r.currentVal()
                    finally
                        r.exitReadLock()
                vals[r] <- v

            let mutable fns : ResizeArray<CFn> = null
            if not (commutes.TryGetValue(r, &fns)) then
                fns <- ResizeArray<CFn>()
                commutes[r] <- fns
            fns.Add({ fn = fn; args = args })
            let v = fn.applyTo(RTSeq.cons(vals[r], args))
            vals[r] <- v
            v

and [<AllowNullLiteral>] Ref(initVal: obj, meta: IPersistentMap) =
    inherit ARef(null)

    // Generates unique ids.
    static let ids : AtomicLong = AtomicLong()
    
    // An id uniquely identifying this reference.
    let id : int64 = ids.getAndIncrement()
    
    // Values at points in time for this reference.  
    // Initial value has timestamp 0.
    let mutable rvals : RefVal = RefVal.createSingleton(initVal, 0)

    // Number of faults for the reference.
    let faults = AtomicInteger()

    // Reader/writer lock for the reference.
    let lock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion)

    // Info on the transaction locking this ref.
    let mutable txInfo : LTInfo option = None

    // For implementing IDisposable
    let mutable disposed = false

    [<VolatileField>]
    let mutable minHistory = 0 

    [<VolatileField>]
    let mutable maxHistory = 10

    member _.Id = id
    member _.TxInfo
        with get () = txInfo
        and set(v) = txInfo <- v

    member internal _.getRVals() = rvals

    member this.setTxInfo(v) = txInfo <- Some v

    // Object overrides

    override _.ToString() = sprintf "<Ref %d>" id

    override this.Equals(o) = 
        match o with
        | _ when Object.ReferenceEquals(this, o) -> true
        | :? Ref as r -> id = r.Id
        | _ -> false

    override _.GetHashCode() = Murmur3.HashLong(id)

    interface IComparable<Ref> with
        member this.CompareTo(o) =
            match o with
            | null -> 1
            | _ -> id.CompareTo(o.Id)

    // History bounds manipulation

    member _.MinHistory with get() = minHistory    
    member _.MaxHistory with get() = maxHistory

    // the contract with clojure.core requires these to return the ref
    member this.SetMinHistory(v) = minHistory <- v; this
    member this.SetMaxHistory(v) = maxHistory <- v; this

    member private _.histCount() =
        let mutable count = 0
        let mutable tv = rvals.Next
        while not (LanguagePrimitives.PhysicalEquality tv rvals) do
            count <- count + 1
            tv <- tv.Next
        count

    member this.getHistoryCount() =
        try
            this.enterWriteLock()
            this.histCount()
        finally
            this.exitWriteLock()

    // Get rid of the history, keeping just the current value
    member this.trimHistory() =
        try
            this.enterWriteLock()
            rvals.Trim()
        finally
            this.exitWriteLock()

    // Convenience methods for locking, primarily for LockingTransaction to use.

    member _.enterReadLock() = lock.EnterReadLock()
    member _.exitReadLock() = lock.ExitReadLock()
    member _.enterWriteLock() = lock.EnterWriteLock()
    member _.exitWriteLock() = lock.ExitWriteLock()
    member _.tryEnterWriteLock(msecTimeout : int) = lock.TryEnterWriteLock(msecTimeout)


    // Add to the fault count.
    member _.addFault() = faults.incrementAndGet() |> ignore

    // Get properties of current (root) rval.
    member _.currentPoint() = rvals.Point
    member _.currentVal() = rvals.Value  

    interface IDeref with
        member this.deref() =
            match LockingTransaction.getRunning() with
            | None -> 
                try 
                    this.enterReadLock()
                    rvals.Value
                finally
                    this.exitReadLock()
            | Some t -> t.doGet(this)

    // Set the value
    member this.setValue(v: obj, commitPoint: int64) =
        let hcount = this.histCount()
        if (faults.get() > 0 && hcount < maxHistory) || hcount < minHistory then
            rvals <- rvals.insertAfter(v, commitPoint)
            faults.set(0) |> ignore
        else
            rvals <- rvals.Next
            rvals.SetValue(v, commitPoint)


    // Transaction operations

    // Set the value (must be in a transaction).
    member this.set(v: obj) = LockingTransaction.getEx().doSet(this, v)

    // Apply a commute to the reference. (Must be in a transaction.)
    member this.commute(fn: IFn, args: ISeq) = LockingTransaction.getEx().doCommute(this, fn, args)

    // Change to a computed value.
    member this.alter(fn: IFn, args: ISeq) = 
        let t = LockingTransaction.getEx()
        t.doSet(this, fn.applyTo(RTSeq.cons(t.doGet(this), args)))

    // Touch the reference.  (Add to the tracking list in the current transaction.)
    member this.touch() = LockingTransaction.getEx().doEnsure(this)

    // the long painful IFn implementation

    member this.fn() = (this:>IDeref).deref() :?> IFn

    interface IFn with
        member this.applyTo(args: ISeq) = AFn.applyToHelper(this, args)
        member this.invoke() = this.fn().invoke()
        member this.invoke(arg1) = this.fn().invoke(arg1)
        member this.invoke(arg1, arg2) = this.fn().invoke(arg1, arg2)
        member this.invoke(arg1, arg2, arg3) = this.fn().invoke(arg1, arg2, arg3)
        member this.invoke(arg1, arg2, arg3, arg4) = this.fn().invoke(arg1, arg2, arg3, arg4)
        member this.invoke(arg1, arg2, arg3, arg4, arg5) = this.fn().invoke(arg1, arg2, arg3, arg4, arg5)
        member this.invoke(arg1, arg2, arg3, arg4, arg5, arg6) = this.fn().invoke(arg1, arg2, arg3, arg4, arg5, arg6)
        member this.invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7) = this.fn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7)
        member this.invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8) = this.fn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8)
        member this.invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9) = this.fn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9)
        member this.invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10) = this.fn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10)
        member this.invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11) = this.fn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11)
        member this.invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12) = this.fn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12)
        member this.invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13) = this.fn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13)
        member this.invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14) = this.fn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14)
        member this.invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15) = this.fn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15)
        member this.invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16) = this.fn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16)
        member this.invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17) = this.fn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17)
        member this.invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18) = this.fn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18)
        member this.invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19) = this.fn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19)
        member this.invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19, arg20) = this.fn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19, arg20)
        member this.invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19, arg20, [<ParamArray>] args) = this.fn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19, arg20, args)


    member private _.Dispose(disposing: bool) =
        if not disposed then
            if disposing then
                if not (isNull lock) then
                    lock.Dispose()
            disposed <- true

    interface IDisposable with
        member this.Dispose() =
                this.Dispose(true);
                GC.SuppressFinalize(this)


// An encapulated message to an agent
and [<Sealed>] AgentAction(agent: Agent, fn: IFn, args: ISeq, solo: bool) =

    member this.Agent = agent

    // Send the message
    member this.execute() : unit =
        try
            if solo then
                let thread = new Thread(ParameterizedThreadStart(this.executeAction))
                thread.Start(null)
            else
                ThreadPool.QueueUserWorkItem(WaitCallback(this.executeAction)) |> ignore
        with
        | e -> 
            let handler : IFn = agent.getErrorHandler()
            if not <| isNull handler then
                try 
                    handler.invoke(e) |> ignore
                with
                | _ -> ()  // ignore errorHandler errors

    // Worker method to execute the action on a thread.
    member _.executeAction(state: obj) : unit =
        try
            Agent.Nested <- PersistentVector.EMPTY :> IPersistentVector

            let mutable error : Exception = null

            try 
                let oldval = agent.getState
                let newval = fn.applyTo(RTSeq.cons(oldval, args))
                agent.setState(newval) |> ignore
                agent.notifyWatches(oldval, newval)
            with
            | e -> error <- e
   
            if isNull error then
                Agent.releasePendingSends() |> ignore
            else
                Agent.Nested <- null   // allow errorHandler to send
                let handler : IFn = agent.getErrorHandler()
                if not <| isNull handler then
                    try                        
                        handler.invoke(agent :> obj, error :> obj) |> ignore
                    with
                    | _ -> ()  // ignore errorHandler errors
                if Object.ReferenceEquals(agent.getErrorMode(), Agent.continueKeyword) then
                    error <- null

            let mutable popped = false
            let mutable next : ActionQueue = ActionQueue.Empty

            while not popped do
                let prior = agent.ActionQueue.Get()
                next <- {q = prior.q.pop(); error = error}
                popped <- agent.ActionQueue.CompareAndSet(prior, next)

            if (isNull error && next.q.count() > 0) then
                ((next.q.peek()) :?> AgentAction).execute()

        finally
            Agent.Nested <- null

and ActionQueue = 
    { q : IPersistentStack; error: Exception }

    static member Empty : ActionQueue = {q = PersistentQueue.Empty; error = null}


// Represents an Agent.
// The Java implementation plays many more games with thread pools.  The CLR does not provide such support.
// TODO: think about the task library.  (I did the original CLR implementation under .NET 3.x, before the task library came out.)

and [<Sealed>] Agent(v: obj, meta: IPersistentMap) as this =
    inherit ARef(meta)

    // The current state of the agent.
    [<VolatileField>]
    let mutable _state : obj = v

    [<VolatileField>]
    let mutable errorMode = Agent.continueKeyword

    [<VolatileField>]
    let mutable errorHandler : IFn = null

    // Agent errors, a sequence of Exceptions.
    [<VolatileField>]
    let mutable errors : ISeq = null

    // A collection of agent actions enqueued during the current transaction.  Per thread.
    [<DefaultValue;ThreadStatic>]
    static val mutable private nested : IPersistentVector

    // A queue of pending actions.
    let aq = AtomicReference<ActionQueue>(ActionQueue.Empty)

    member _.ActionQueue : AtomicReference<ActionQueue> = aq

    do this.setState(v) |> ignore

    new(v) = Agent(v, null) 

    static member continueKeyword : Keyword = Keyword.intern(null, "continue")

    member _.getState = _state
    member _.getQueueCount = aq.Get().q.count();

    //member _.addError(e) = errors <- RTSeq.cons(e, errors)

    static member Nested
        with get() = Agent.nested
        and set(v) = Agent.nested <- v

    member this.setState(newState : obj) : bool =
        (this :> ARef).validate(newState)
        let ret = not <| Object.ReferenceEquals(_state,newState)
        _state <- newState
        ret


    member _.getError() = aq.Get().error 

    member _.getErrorMode() = errorMode
    member _.setErrorMode(kw: Keyword) = errorMode <- kw
    member _.getErrorHandler() = errorHandler
    member _.setErrorHandler(f: IFn) = errorHandler <- f

    [<MethodImpl(MethodImplOptions.Synchronized)>]
    member this.restart(newState: obj, clearActions: bool) : obj =

        if this.getError() = null then
            raise <| InvalidOperationException("Agent does not need a restart")
    
        (this :> ARef).validate(newState)
        _state <- newState

        if clearActions then
            aq.Set(ActionQueue.Empty)
        else
            // spin-lock to update
            let rec loop () =
                let prior = aq.Get()
                let restarted = aq.CompareAndSet(prior, {q = prior.q; error = null})
                if (not restarted )
                    then loop()
                else
                    prior
            let prior = loop()

            if prior.q.count() > 0 then
                ((prior.q.peek()) :?> AgentAction).execute()
        newState


    // Send a message to the agent.
    member this.dispatch(fn: IFn, args: ISeq, solo: bool) =
        let error = this.getError()
        if not <| isNull error then
            raise <| InvalidOperationException("Agent is failed, needs restart", error)
        let action = AgentAction(this, fn, args, solo)
        Agent.dispatchAction(action)
        this

    // Send an action
    // If there is transaction running on this thread, defer execution until the transaction ends (enqueue on the transaction).
    // If there is already an action running, enqueue it (
    // Otherwise, queue it for execution.
    static member dispatchAction(action: AgentAction) =
        let transOpt = LockingTransaction.getRunning()
        match transOpt with
        | Some trans -> trans.enqueue(action)
        | None -> 
            let nested = Agent.nested
            if not <| isNull nested then
                Agent.nested <- nested.cons(action)
            else
                action.Agent.enqueue(action)

    // Enqueue an action in the pending queue.
    // spin-locks to update the queue.
    member this.enqueue(action: AgentAction) =
        let rec loop () =
            let prior = aq.Get()
            let queued = aq.CompareAndSet(prior, {q = prior.q; error = null})
            if (not queued )
                then loop()
            else
                prior
        let prior = loop()
        if prior.q.count() = 0 && isNull prior.error then
            action.execute()

    interface IDeref with
        member this.deref() = _state

    // For compatibility
    // if we ever implement separate queueing, we'll need to do something here.
    // Comment in C# code: 
    //      Shutdown all threads executing.
    //      We need to work on this.</remarks>
    static member public shutdown() = ()

    // Enqueue nested actions
    static member releasePendingSends() : int = 
        let sends = Agent.nested
        if isNull sends then
            0
        else
            for i in 0 .. sends.count() - 1 do
                let a = sends.nth(i)  :?> AgentAction
                a.Agent.enqueue(a)
            Agent.nested <- PersistentVector.EMPTY
            sends.count()