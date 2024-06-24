namespace Clojure.Lib

open Clojure.Collections
open System.Runtime.CompilerServices
open System
open System.Threading
open System.Collections.Generic


// Provides a basic implementation of IReference functionality.
// The JVM implementation does not make this abstract, but we're never going to instantiate one of these directly.
[<AbstractClass>]
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


// Provides a basic implementation of IReference functionality.
[<AbstractClass>]
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
            <| NotImplementedException("Derived classes must implement Associative.containsKey(key)")

    // Invoke an IFn on value to validate the value.
    // The IFn can indicate a failed validation by returning false-y or throwing an exception.

    static member Validate(vf : IFn, value: obj) =
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

    member this.Validate(value: obj) = ARef.Validate(validator, value)

    interface IRef with
        member this.setValidator(vf) =        
            ARef.Validate(vf, (this:>IDeref).deref())
            validator <- vf

        member _.getValidator() = validator

        member this.addWatch(key, callback) =
            watches <- watches.assoc(key, callback)
            this :> IRef

        member this.removeWatch(key) =
            watches <- watches.without(key)
            this :> IRef

        member _.getWatches() = watches


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



// Holds values inside a Ref
[<Sealed>]
type private TVal(v: obj, pt: int64) as this = 

    let mutable value : obj = v
    let mutable point : int64 = pt

    // these implement a doubly-linked circular list
    // the default constructor creates a self-linked node
    let mutable prior : TVal = this
    let mutable next : TVal = this

    new(v, pt, pr : TVal) as this = 
        TVal(v, pt)
        then
            this.Prior <- pr
            this.Next <- pr.Next
            pr.Next <- this
            this.Next.Prior <- this

    member _.Value 
        with get() = value
        and private set(v) = value <- v
    
    member _.Point = point
   
    member _.Next
        with get() = next
        and private set(v) = next <- v

    member _.Prior 
        with get() = prior
        and private set(v) = prior <- v

    member this.SetValue(v, pt) =
        value <- v
        point <- pt



type private LTState = 
    | Running = 0
    | Committing = 1
    | Retry = 2
    | Killed = 3
    | Committed = 4

exception RetryEx of string
exception AbortEx of string

type private LTInfo(initState: LTState, startPoint: int64) = 

    let mutable status : int64 = initState |> int64
    let latch = CountdownLatch(1)

    member _.Latch = latch
    member _.StartPoint = startPoint

    member _.Status
        with get() = status
        and set(v) = status <- v
    
    member this.compareAndSet(oldVal: LTState, newVal: LTState) =
        let origVal = Interlocked.CompareExchange(&status, newVal |> int64, oldVal |> int64)
        origVal = (oldVal |> int64)

    member this.set(newVal: LTState) =
        Interlocked.Exchange(&status, newVal |> int64) |> int64

    member this.isRunning =
        let s = enum<LTState> (Interlocked.Read(&status) |> int32) 
        s = LTState.Running || s = LTState.Committing   

// Pending call of a function on arguments.
type private CFn  = { fn: IFn; args: ISeq }


type LTNotify = { ref: Ref; oldVal: obj; newVal: obj }


// Provides transaction semantics for Agents, Refs, etc.
and LockingTransaction() =

    // The number of times to retry a transaction in case of a conflict.
    [<Literal>]
    let RetryLimit = 10000

    // How long to wait for a lock.
    [<Literal>]
    let LockWaitMsecs = 100

    // How old another transaction must be before we 'barge' it.
    // Java version has BARGE_WAIT_NANOS, set at 10*1_000_000.
    // If I'm thinking correctly tonight, that's 10 milliseconds.
    // Ticks here are 100 nanos, so we should have  10 * 1_000_000/100 = 100_000.
    [<Literal>]
    let BargeWaitTicks = 100_000L

    // The transaction running on the current thread.  (Thread-local.)
    [<DefaultValue;ThreadStatic>]
    static val mutable private currentTransaction : LockingTransaction option


    // The current point.

    // Used to provide a total ordering on transactions for the purpose of determining preference on transactions when there are conflicts.
    // Transactions consume a point for init, for each retry, and on commit if writing.
    static member private lastPoint : AtomicLong  = AtomicLong()

    // The state of the transaction.
    //  Encapsulated so things like Refs can look.
    let mutable info : LTInfo option = None

    // The point at the start of the current retry (or first try).
    let mutable readPoint : int64 = 0L

    // The point at the start of the transaction.
    let mutable startPoint : int64 = 0L

    // The system ticks at the start of the transaction.
    let mutable startTime : int64 = 0L

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

    // Point manipulation

    // Get a new read point value.
    member this.getReadPoint() =
        readPoint <- LockingTransaction.lastPoint.incrementAndGet()

    // Get a commit point value.
    static member getCommitPoint() =
        LockingTransaction.lastPoint.incrementAndGet()

    // Actions

    // Stop this transaction.
    member private this.stop(status: LTState) =
        match info with
        | Some i ->
            lock i (fun () ->
                i.Status <- status |> int64
                i.Latch.CountDown()
            )
            info <- None
            vals.Clear()
            sets.Clear()
            commutes.Clear()
            // Java commented out: _actions.Clear()
        | None -> ()

    member private this.tryWriteLock(r: Ref) =
        try 
            if not (r.TryEnterWriteLock(LockWaitMsecs)) then
                raise retryEx
        with 
        | :? ThreadInterruptedException -> raise retryEx

    member private this.releaseIfEnsured(r: Ref) =
        if ensures.Contains(r) then
            ensures.Remove(r) |> ignore
            r.ExitReadLock()

    member private this.blockAndBail(refinfo: LTInfo) =
        this.stop(LTState.Retry)
        try
            refinfo.Latch.Await(LockWaitMsecs) |> ignore
        with
        | :? ThreadInterruptedException -> ()
        raise retryEx

    member private this.lock(Ref r) =
        // can't upgrade read lock, so release it.
        this.releaseIfEnsured(r)

        let mutable unlocked = true
        try
            this.tryWriteLock(r)
            unlocked <- false

            if r.CurrentValPoint() > readPoint then
                raise retryEx

            let refinfo = r.TInfo

            // write lock conflict
            if refinfo.IsRunning && refinfo <> info then
                if not (this.barge(refinfo)) then
                    r.ExitWriteLock()
                    unlocked <- true
                    this.blockAndBail(refinfo)

            r.TInfo <- info
            r.TryGetVal()
        finally
            if not unlocked then
                r.ExitWriteLock()



(*

        object Lock(Ref r)
        {
            // can't upgrade read lock, so release it.
            ReleaseIfEnsured(r);

            bool unlocked = true;
            try
            {
                TryWriteLock(r);
                unlocked = false;

                if (r.CurrentValPoint() > _readPoint)
                    throw _retryex;

                Info refinfo = r.TInfo;

                // write lock conflict
                if (refinfo != null && refinfo != _info && refinfo.IsRunning)
                {
                    if (!Barge(refinfo))
                    {
                        r.ExitWriteLock();
                        unlocked = true;
                        return BlockAndBail(refinfo);
                    }
                }

                r.TInfo = _info;
                return r.TryGetVal();
            }
            finally
            {
                if (!unlocked)
                {
                    r.ExitWriteLock();
                }
            }
        }


        /// <summary>
        /// Kill this transaction.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "<Pending>")]
        void Abort()
        {
            Stop(KILLED);
            throw new AbortException();
        }

        /// <summary>
        /// Determine if sufficient clock time has elapsed to barge another transaction.
        /// </summary>
        /// <returns><value>true</value> if enough time has elapsed; <value>false</value> otherwise.</returns>
        private bool BargeTimeElapsed()
        {
            return Environment.TickCount - _startTime > BargeWaitTicks;
        }

        /// <summary>
        /// Try to barge a conflicting transaction.
        /// </summary>7
        /// <param name="refinfo">The info on the other transaction.</param>
        /// <returns><value>true</value> if we killed the other transaction; <value>false</value> otherwise.</returns>
        private bool Barge(Info refinfo)
        {
            bool barged = false;
            // if this transaction is older
            //   try to abort the other
            if (BargeTimeElapsed() && _startPoint < refinfo.StartPoint)
            {
                barged = refinfo.Status.compareAndSet(RUNNING, KILLED);
                if (barged)
                    refinfo.Latch.CountDown();
            }
            return barged;
        }

        /// <summary>
        /// Get the transaction running on this thread (throw exception if no transaction). 
        /// </summary>
        /// <returns>The running transaction.</returns>
        public static LockingTransaction GetEx()
        {
            LockingTransaction t = _transaction;
            if (t == null || t._info == null)
                throw new InvalidOperationException("No transaction running");
            return t;
        }

        /// <summary>
        /// Get the transaction running on this thread (or null if no transaction).
        /// </summary>
        /// <returns>The running transaction if there is one, else <value>null</value>.</returns>
        static internal LockingTransaction GetRunning()
        {
            LockingTransaction t = _transaction;
            if (t == null || t._info == null)
                return null;
            return t;
        }

        /// <summary>
        /// Is there a transaction running on this thread?
        /// </summary>
        /// <returns><value>true</value> if there is a transaction running on this thread; <value>false</value> otherwise.</returns>
        /// <remarks>Initial lowercase in name for core.clj compatibility.</remarks>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
        public static bool isRunning()
        {
            return GetRunning() != null;
        }

        /// <summary>
        /// Invoke a function in a transaction
        /// </summary>
        /// <param name="fn">The function to invoke.</param>
        /// <returns>The value computed by the function.</returns>
        /// <remarks>Initial lowercase in name for core.clj compatibility.</remarks>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
        public static object runInTransaction(IFn fn)
        {
            // TODO: This can be called on something more general than  an IFn.
            // We can could define a delegate for this, probably use ThreadStartDelegate.
            // Should still have a version that takes IFn.
            LockingTransaction t = _transaction;
            Object ret;

            if (t == null)
            {
                _transaction = t = new LockingTransaction();
                try
                {
                    ret = t.Run(fn);
                }
                finally
                {
                    _transaction = null;
                }
            }
            else
            {
                if (t._info != null)
                    ret = fn.invoke();
                else
                    ret = t.Run(fn);
            }

            return ret;
        }



        /// <summary>
        /// Start a transaction and invoke a function.
        /// </summary>
        /// <param name="fn">The function to invoke.</param>
        /// <returns>The value computed by the function.</returns>
        object Run(IFn fn)
        {
            // TODO: Define an overload called on ThreadStartDelegate or something equivalent.

            bool done = false;
            object ret = null;
            List<Ref> locked = new List<Ref>();
            List<Notify> notify = new List<Notify>();

            for (int i = 0; !done && i < RetryLimit; i++)
            {
                try
                {
                    GetReadPoint();
                    if (i == 0)
                    {
                        _startPoint = _readPoint;
                        _startTime = Environment.TickCount;
                    }

                    _info = new Info(RUNNING, _startPoint);
                    ret = fn.invoke();

                    // make sure no one has killed us before this point,
                    // and can't from now on
                    if (_info.Status.compareAndSet(RUNNING, COMMITTING))
                    {
                        foreach (KeyValuePair<Ref, List<CFn>> pair in _commutes)
                        {
                            Ref r = pair.Key;
                            if (_sets.Contains(r))
                                continue;

                            bool wasEnsured = _ensures.Contains(r);
                            // can't upgrade read lock, so release
                            ReleaseIfEnsured(r);
                            TryWriteLock(r);
                            locked.Add(r);

                            if (wasEnsured && r.CurrentValPoint() > _readPoint )
                                throw _retryex;

                            Info refinfo = r.TInfo;
                            if ( refinfo != null && refinfo != _info && refinfo.IsRunning)
                            {
                                if (!Barge(refinfo))
                                {
                                    throw _retryex;
                                }
                            }
                            object val = r.TryGetVal();
                            _vals[r] = val;
                            foreach (CFn f in pair.Value)
                                _vals[r] = f.Fn.applyTo(RT.cons(_vals[r], f.Args));
                        }
                        foreach (Ref r in _sets)
                        {
                            TryWriteLock(r);
                            locked.Add(r);
                        }
                        // validate and enqueue notifications
                        foreach (KeyValuePair<Ref, object> pair in _vals)
                        {
                            Ref r = pair.Key;
                            r.Validate(pair.Value);
                        }

                        // at this point, all values calced, all refs to be written locked
                        // no more client code to be called
                        long commitPoint = GetCommitPoint();
                        foreach (KeyValuePair<Ref, object> pair in _vals)
                        {
                            Ref r = pair.Key;
                            object oldval = r.TryGetVal();
                            object newval = pair.Value;
                          
                            r.SetValue(newval, commitPoint);
                            if (r.getWatches().count() > 0)
                                notify.Add(new Notify(r, oldval, newval));
                        }

                        done = true;
                        _info.Status.set(COMMITTED);
                    }
                }
                catch (RetryEx)
                {
                    // eat this so we retry rather than fall out
                }
                catch (Exception ex)
                {
                    if (ContainsNestedRetryEx(ex))
                    {
                        // Wrapped exception, eat it.
                    }
                    else
                    {
                        throw;
                    }
                }
                finally
                {
                    for (int k = locked.Count - 1; k >= 0; --k)
                    {
                        locked[k].ExitWriteLock();
                    }
                    locked.Clear();
                    foreach (Ref r in _ensures)
                        r.ExitReadLock();
                    _ensures.Clear();
                    Stop(done ? COMMITTED : RETRY);
                    try
                    {
                        if (done) // re-dispatch out of transaction
                        {
                            foreach (Notify n in notify)
                            {
                                n._ref.NotifyWatches(n._oldval, n._newval);
                            }
                            foreach (Agent.Action action in _actions)
                            {
                                Agent.DispatchAction(action);
                            }
                        }
                    }
                    finally
                    {
                        notify.Clear();
                        _actions.Clear();
                    }
                }
            }
            if (!done)
                throw new InvalidOperationException("Transaction failed after reaching retry limit");
            return ret;
        }

        /// <summary>
        /// Determine if the exception wraps a <see cref="RetryEx">RetryEx</see> at some level.
        /// </summary>
        /// <param name="ex">The exception to test.</param>
        /// <returns><value>true</value> if there is a nested  <see cref="RetryEx">RetryEx</see>; <value>false</value> otherwise.</returns>
        /// <remarks>Needed because sometimes our retry exceptions get wrapped.  You do not want to know how long it took to track down this problem.</remarks>
        private static bool ContainsNestedRetryEx(Exception ex)
        {
            for (Exception e = ex; e != null; e = e.InnerException)
                if (e is RetryEx)
                    return true;
            return false;
        }

        /// <summary>
        /// Add an agent action sent during the transaction to a queue.
        /// </summary>
        /// <param name="action">The action that was sent.</param>
        internal void Enqueue(Agent.Action action)
        {
            _actions.Add(action);
        }

        /// <summary>
        /// Get the value of a ref most recently set in this transaction (or prior to entering).
        /// </summary>
        /// <param name="r"></param>
        /// <param name="tvals"></param>
        /// <returns>The value.</returns>
        internal object DoGet(Ref r)
        {
            if (!_info.IsRunning)
                throw _retryex;
            if (_vals.ContainsKey(r))
            {
                return _vals[r];
            }
            try
            {
                r.EnterReadLock();
                if (r.TVals == null)
                    throw new InvalidOperationException(r.ToString() + " is not bound.");
                Ref.TVal ver = r.TVals;
                do
                {
                    if (ver.Point <= _readPoint)
                    {
                        return ver.Val;
                    }
                } while ((ver = ver.Prior) != r.TVals);
            }
            finally
            {
                r.ExitReadLock();
            }
            // no version of val precedes the read point
            r.AddFault();
            throw _retryex;
        }

        /// <summary>
        /// Set the value of a ref inside the transaction.
        /// </summary>
        /// <param name="r">The ref to set.</param>
        /// <param name="val">The value.</param>
        /// <returns>The value.</returns>
        internal object DoSet(Ref r, object val)
        {
            if (!_info.IsRunning)
                throw _retryex;
            if (_commutes.ContainsKey(r))
                throw new InvalidOperationException("Can't set after commute");
            if (!_sets.Contains(r))
            {
                _sets.Add(r);
                Lock(r);
            }
            _vals[r] = val;
            return val;
        }

        /// <summary>
        /// Touch a ref.  (Lock it.)
        /// </summary>
        /// <param name="r">The ref to touch.</param>
        internal void DoEnsure(Ref r)
        {
            if (!_info.IsRunning)
                throw _retryex;
            if (_ensures.Contains(r))
                return;

            r.EnterReadLock();

            // someone completed a write after our shapshot
            if (r.CurrentValPoint() > _readPoint)
            {
                r.ExitReadLock();
                throw _retryex;
            }

            Info refinfo = r.TInfo;

            // writer exists
            if (refinfo != null && refinfo.IsRunning)
            {
                r.ExitReadLock();
                if (refinfo != _info)  // not us, ensure is doomed
                    BlockAndBail(refinfo);
            }
            else
                _ensures.Add(r);
        }


        /// <summary>
        /// Post a commute on a ref in this transaction.
        /// </summary>
        /// <param name="r">The ref.</param>
        /// <param name="fn">The commuting function.</param>
        /// <param name="args">Additional arguments to the function.</param>
        /// <returns>The computed value.</returns>
        internal object DoCommute(Ref r, IFn fn, ISeq args)
        {
            if (!_info.IsRunning)
                throw _retryex;
            if (!_vals.ContainsKey(r))
            {
                object val = null;
                try
                {
                    r.EnterReadLock();
                    val = r.TryGetVal();
                }
                finally
                {
                    r.ExitReadLock();
                }
                _vals[r] = val;
            }
            if (!_commutes.TryGetValue(r, out List<CFn> fns))
                _commutes[r] = fns = new List<CFn>();
            fns.Add(new CFn(fn, args));
            object ret = fn.applyTo(RT.cons(_vals[r], args));
            _vals[r] = ret;

            return ret;
        }

        #endregion
    }
}


*)


and Ref() =
    inherit ARef(null)



(*

    public class Ref : ARef, IFn, IComparable<Ref>, IRef, IDisposable
    {


        #region Data

        /// <summary>
        /// Values at points in time for this reference.
        /// </summary>
        TVal _tvals;

        /// <summary>
        /// Values at points in time for this reference.
        /// </summary>
        internal TVal TVals
        {
            get { return _tvals; }
        }

        /// <summary>
        /// Number of faults for the reference.
        /// </summary>
        readonly AtomicInteger _faults;

        /// <summary>
        /// Reader/writer lock for the reference.
        /// </summary>
        readonly ReaderWriterLockSlim _lock;

        /// <summary>
        /// Info on the transaction locking this ref.
        /// </summary>
        LockingTransaction.Info _tinfo;

        /// <summary>
        /// Info on the transaction locking this ref.
        /// </summary>
        public LockingTransaction.Info TInfo
        {
            get { return _tinfo; }
            set { _tinfo = value; }
        }

        /// <summary>
        /// An id uniquely identifying this reference.
        /// </summary>
        readonly long _id;


        /// <summary>
        /// An id uniquely identifying this reference.
        /// </summary>
        public long Id
        {
            get { return _id; }
        }

        volatile int _minHistory = 0;
        public int MinHistory
        {
            get { return _minHistory; }
            set { _minHistory = value; }
        }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
        public Ref setMinHistory(int minHistory)
        {
            _minHistory = minHistory;
            return this;
        }

        volatile int _maxHistory = 10;

        public int MaxHistory
        {
            get { return _maxHistory; }
            set { _maxHistory = value; }
        }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
        public Ref setMaxHistory(int maxHistory)
        {
            _maxHistory = maxHistory;
            return this;
        }


        /// <summary>
        /// Used to generate unique ids.
        /// </summary>
        static readonly AtomicLong _ids = new AtomicLong();

        bool _disposed = false;

        #endregion

        #region C-tors & factory methods

        /// <summary>
        ///  Construct a ref with given initial value.
        /// </summary>
        /// <param name="initVal">The initial value.</param>
        public Ref(object initVal)
            : this(initVal, null)
        {
        }


        /// <summary>
        ///  Construct a ref with given initial value and metadata.
        /// </summary>
        /// <param name="initVal">The initial value.</param>
        /// <param name="meta">The metadat to attach.</param>
        public Ref(object initval, IPersistentMap meta)
            : base(meta)
        {
            _id = _ids.getAndIncrement();
            _faults = new AtomicInteger();
            _lock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
            _tvals = new TVal(initval, 0);
        }

        #endregion

        #region Debugging

        ///// <summary>
        ///// I was having a hard day.
        ///// </summary>
        ///// <returns></returns>
        //public string DebugStr()
        //{
        //    StringBuilder sb = new StringBuilder();
        //    sb.Append("<Ref ");
        //    sb.Append(Id);
        //    sb.Append(", ");
        //    if (_tinfo == null)
        //        sb.Append("NO");
        //    else
        //        sb.AppendFormat("{0} {1}", _tinfo.Status.get(), _tinfo.StartPoint);
        //    sb.Append(", ");
        //    if (_tvals == null)
        //        sb.Append("TVals: NO");
        //    else
        //    {
        //        sb.Append("TVals: ");
        //        TVal t = _tvals;
        //        do
        //        {
        //            sb.Append(t.Point);
        //            sb.Append(" ");
        //        } while ((t = t.Prior) != _tvals);
        //    }
        //    sb.Append(">");
        //    return sb.ToString();
        //}

        #endregion

        #region History counts

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
        public int getHistoryCount()
        {
            try
            {
                EnterWriteLock();
                return HistCount();
            }
            finally
            {
                ExitWriteLock();
            }
        }

        int HistCount()
        {
            if (_tvals == null)
                return 0;
            else
            {
                int count = 0;
                for (TVal tv = _tvals.Next; tv != _tvals; tv = tv.Next)
                    count++;
                return count;
            }
        }

        #endregion       

        #region IDeref Members

        /// <summary>
        /// Gets the (immutable) value the reference is holding.
        /// </summary>
        /// <returns>The value</returns>
        public override object deref()
        {
            LockingTransaction t = LockingTransaction.GetRunning();
            if (t == null)
            {
                object ret = currentVal();
                //Console.WriteLine("Thr {0}, {1}: No-trans get => {2}", Thread.CurrentThread.ManagedThreadId,DebugStr(), ret);
                return ret;
            }
            return t.DoGet(this);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
        object currentVal()
        {
            try
            {
                _lock.EnterReadLock();
                if (_tvals != null)
                    return _tvals.Val;
                throw new InvalidOperationException(String.Format("{0} is unbound.", ToString()));
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        #endregion

        #region  Interface for LockingTransaction

        /// <summary>
        /// Get the read lock.
        /// </summary>
        internal void EnterReadLock()
        {
            _lock.EnterReadLock();
        }

        /// <summary>
        /// Release the read lock.
        /// </summary>
        internal void ExitReadLock()
        {
            _lock.ExitReadLock();
        }

        /// <summary>
        /// Get the write lock.
        /// </summary>
        internal void EnterWriteLock()
        {
            _lock.EnterWriteLock();
        }


        /// <summary>
        /// Get the write lock.
        /// </summary>
        internal bool TryEnterWriteLock(int msecTimeout)
        {
            return _lock.TryEnterWriteLock(msecTimeout);
        }

        /// <summary>
        /// Release the write lock.
        /// </summary>
        internal void ExitWriteLock()
        {
            _lock.ExitWriteLock();
        }

        /// <summary>
        /// Add to the fault count.
        /// </summary>
        public void AddFault()
        {
            _faults.incrementAndGet();
        }

        /// <summary>
        /// Get the read/commit point associated with the current value.
        /// </summary>
        /// <returns></returns>
        public long CurrentValPoint()
        {
            return _tvals != null ? _tvals.Point : -1;
        }

        /// <summary>
        /// Try to get the value (else null).
        /// </summary>
        /// <returns>The value if it has been set; <value>null</value> otherwise.</returns>
        public object TryGetVal()
        {
            return _tvals?.Val;
        }

        /// <summary>
        /// Set the value.
        /// </summary>
        /// <param name="val">The new value.</param>
        /// <param name="commitPoint">The transaction's commit point.</param>
        internal void SetValue(object val, long commitPoint)
        {
            int hcount = HistCount();

            if (_tvals == null)
                _tvals = new TVal(val, commitPoint);
            else if ( (_faults.get() > 0 && hcount < _maxHistory) || hcount < _minHistory )
            {
                _tvals = new TVal(val, commitPoint, _tvals);
                _faults.set(0);
            }
            else
            {
                _tvals = _tvals.Next;
                _tvals.SetValue(val, commitPoint);
            }
        }

        #endregion

        #region Ref operations

        /// <summary>
        /// Set the value (must be in a transaction).
        /// </summary>
        /// <param name="val">The new value.</param>
        /// <returns>The new value.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
        public object set(object val)
        {
            return LockingTransaction.GetEx().DoSet(this, val);
        }

        /// <summary>
        /// Apply a commute to the reference. (Must be in a transaction.)
        /// </summary>
        /// <param name="fn">The function to apply to the current state and additional arguments.</param>
        /// <param name="args">Additional arguments.</param>
        /// <returns>The computed value.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
        public object commute(IFn fn, ISeq args)
        {
            return LockingTransaction.GetEx().DoCommute(this, fn, args);
        }

        /// <summary>
        /// Change to a computed value.
        /// </summary>
        /// <param name="fn">The function to apply to the current state and additional arguments.</param>
        /// <param name="args">Additional arguments.</param>
        /// <returns>The computed value.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
        public object alter(IFn fn, ISeq args)
        {
            LockingTransaction t = LockingTransaction.GetEx();
            return t.DoSet(this, fn.applyTo(RT.cons(t.DoGet(this), args)));
        }

        /// <summary>
        /// Touch the reference.  (Add to the tracking list in the current transaction.)
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
        public void touch()
        {
            LockingTransaction.GetEx().DoEnsure(this);
        }

        #endregion

        #region IFn Members


        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
        public IFn fn()
        {
            return (IFn)deref();
        }

        public object invoke()
        {
            return fn().invoke();
        }

        public object invoke(object arg1)
        {
            return fn().invoke(arg1);
        }

        public object invoke(object arg1, object arg2)
        {
            return fn().invoke(arg1, arg2);
        }

        public object invoke(object arg1, object arg2, object arg3)
        {
            return fn().invoke(arg1, arg2, arg3);
        }

        public object invoke(object arg1, object arg2, object arg3, object arg4)
        {
            return fn().invoke(arg1, arg2, arg3, arg4);
        }

        public object invoke(object arg1, object arg2, object arg3, object arg4, object arg5)
        {
            return fn().invoke(arg1, arg2, arg3, arg4, arg5);
        }

        public object invoke(object arg1, object arg2, object arg3, object arg4, object arg5, object arg6)
        {
            return fn().invoke(arg1, arg2, arg3, arg4, arg5, arg6);
        }

        public object invoke(object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7)
        {
            return fn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7);
        }

        public object invoke(object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7,
                             object arg8)
        {
            return fn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
        }

        public object invoke(object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7,
                             object arg8, object arg9)
        {
            return fn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
        }

        public object invoke(object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7,
                             object arg8, object arg9, object arg10)
        {
            return fn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
        }

        public object invoke(object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7,
                             object arg8, object arg9, object arg10, object arg11)
        {
            return fn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11);
        }

        public object invoke(object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7,
                             object arg8, object arg9, object arg10, object arg11, object arg12)
        {
            return fn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12);
        }

        public object invoke(object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7,
                             object arg8, object arg9, object arg10, object arg11, object arg12, object arg13)
        {
            return fn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13);
        }

        public object invoke(object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7,
                             object arg8, object arg9, object arg10, object arg11, object arg12, object arg13, object arg14)
        {
            return fn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14);
        }

        public object invoke(object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7,
                             object arg8, object arg9, object arg10, object arg11, object arg12, object arg13, object arg14,
                             object arg15)
        {
            return fn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15);
        }

        public object invoke(object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7,
                             object arg8, object arg9, object arg10, object arg11, object arg12, object arg13, object arg14,
                             object arg15, object arg16)
        {
            return fn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15,
                               arg16);
        }

        public object invoke(object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7,
                             object arg8, object arg9, object arg10, object arg11, object arg12, object arg13, object arg14,
                             object arg15, object arg16, object arg17)
        {
            return fn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15,
                               arg16, arg17);
        }

        public object invoke(object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7,
                             object arg8, object arg9, object arg10, object arg11, object arg12, object arg13, object arg14,
                             object arg15, object arg16, object arg17, object arg18)
        {
            return fn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15,
                               arg16, arg17, arg18);
        }

        public object invoke(object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7,
                             object arg8, object arg9, object arg10, object arg11, object arg12, object arg13, object arg14,
                             object arg15, object arg16, object arg17, object arg18, object arg19)
        {
            return fn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15,
                               arg16, arg17, arg18, arg19);
        }

        public object invoke(object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7,
                             object arg8, object arg9, object arg10, object arg11, object arg12, object arg13, object arg14,
                             object arg15, object arg16, object arg17, object arg18, object arg19, object arg20)
        {
            return fn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15,
                               arg16, arg17, arg18, arg19, arg20);
        }

        public object invoke(object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7,
                             object arg8, object arg9, object arg10, object arg11, object arg12, object arg13, object arg14,
                             object arg15, object arg16, object arg17, object arg18, object arg19, object arg20,
                             params object[] args)
        {
            return fn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15,
                               arg16, arg17, arg18, arg19, arg20, args);
        }

        public object applyTo(ISeq arglist)
        {
            return AFn.ApplyToHelper(this, arglist);
        }

        #endregion

        #region IComparable<Ref> Members

        /// <summary>
        /// Compare to another ref.
        /// </summary>
        /// <param name="other">The other ref.</param>
        /// <returns><value>true</value> if they are identical; <value>false</value> otherwise.</returns>
        public int CompareTo(Ref other)
        {
            if ( other is null)
                return 1;
    
            return _id.CompareTo(other._id);
        }

        #endregion

        #region object overrides

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            return obj is Ref r && _id == r._id;
        }

        public override int GetHashCode()
        {
            return Murmur3.HashLong(_id) ;    // _id.GetHashCode()
        }
        #endregion

        #region operator overrides

        public static bool operator ==(Ref x, Ref y)
        {
            if (ReferenceEquals(x, y))
                return true;

            if (x is null)
                return false;

            return x.CompareTo(y) == 0;
        }

        public static bool operator !=(Ref x, Ref y)
        {
            return !(x == y);
        }

        public static bool operator <(Ref x, Ref y)
        {
            if (ReferenceEquals(x, y))
                return false;

            if ( x is null)
                throw new ArgumentException("Cannot compare null","x");

            return x.CompareTo(y) < 0;
        }

        public static bool operator >(Ref x, Ref y)
        {
            if (ReferenceEquals(x, y))
                return false;

            if ( x is null)
                throw new ArgumentException("Cannot compare null","x");

            return x.CompareTo(y) > 0;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if ( _lock != null )
                        _lock.Dispose();
                }

                _disposed = true;
            }
        }

        #endregion
    }
}



*)

and AgentAction() =
    inherit Object()

and Agent() =
    inherit ARef()


    (*
    
    
    /**
 *   Copyright (c) Rich Hickey. All rights reserved.
 *   The use and distribution terms for this software are covered by the
 *   Eclipse Public License 1.0 (http://opensource.org/licenses/eclipse-1.0.php)
 *   which can be found in the file epl-v10.html at the root of this distribution.
 *   By using this software in any fashion, you are agreeing to be bound by
 * 	 the terms of this license.
 *   You must not remove this notice, or any other, from this software.
 **/

/**
 *   Author: David Miller
 **/

using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace clojure.lang
{
    /// <summary>
    /// Represents an Agent.
    /// </summary>
    /// <remarks>
    /// <para>See the Clojure documentation for more information.</para>
    /// <para>The Java implementation plays many more games with thread pools.  The CLR does not provide such support. We need to revisit this in CLR 4.  
    /// Until then: TODO: Implement our own thread pooling?</para>
    /// </remarks>
    public sealed class Agent : ARef
    {
        #region ActionQueue class

        class ActionQueue
        {
            public readonly IPersistentStack _q;
            public readonly Exception _error; // non-null indicates fail state
            static internal readonly ActionQueue EMPTY = new ActionQueue(PersistentQueue.EMPTY, null);

            public ActionQueue(IPersistentStack q, Exception error)
            {
                _q = q;
                _error = error;
            }
        }
        
        static readonly Keyword ContinueKeyword = Keyword.intern(null, "continue");
        //static readonly Keyword FailKeyword = Keyword.intern(null, "fail");

        #endregion

        #region Data

        /// <summary>
        /// The current state of the agent.
        /// </summary>
        private volatile object _state;

        /// <summary>
        /// The current state of the agent.
        /// </summary>
        public object State
        {
          get { return _state; }
        }

        /// <summary>
        /// A queue of pending actions.
        /// </summary>
        private readonly AtomicReference<ActionQueue> _aq = new AtomicReference<ActionQueue>(ActionQueue.EMPTY);

        /// <summary>
        /// Number of items in the queue.
        /// </summary>
        public int QueueCount
        {
            get
            {
                return _aq.Get()._q.count();
            }
        }

        /// <summary>
        /// Number of items in the queue.  For core.clj compatibility.
        /// </summary>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
        public int getQueueCount()
        {
            return QueueCount;
        }

        volatile Keyword _errorMode = ContinueKeyword;
        volatile IFn _errorHandler = null;

        ///// <summary>
        ///// Agent errors, a sequence of Exceptions.
        ///// </summary>
        //private volatile ISeq _errors = null;

        ///// <summary>
        ///// Agent errors, a sequence of Exceptions.
        ///// </summary>
        //public ISeq Errors
        //{
        //    get { return _errors; }
        //}


        ///// <summary>
        ///// Add an error.
        ///// </summary>
        ///// <param name="e">The exception to add.</param>
        //public void AddError(Exception e)
        //{
        //    _errors = RT.cons(e, _errors);
        //}


        /// <summary>
        /// A collection of agent actions enqueued during the current transaction.  Per thread.
        /// </summary>
        [ThreadStatic]
        private static IPersistentVector _nested;

        /// <summary>
        /// A collection of agent actions enqueued during the current transaction.  Per thread.
        /// </summary>
        public static IPersistentVector Nested
        {
            get { return _nested; }
            set { _nested = value; }
        }

        #endregion

        #region C-tors & factory methods

        /// <summary>
        /// Construct an agent with given state and null metadata.
        /// </summary>
        /// <param name="state">The initial state.</param>
        public Agent(object state)
            : this(state, null)
        {
        }

        /// <summary>
        /// Construct an agent with given state and metadata.
        /// </summary>
        /// <param name="state">The initial state.</param>
        /// <param name="meta">The metadata to attach.</param>
        public Agent(Object state, IPersistentMap meta)
            :base(meta)
        {
            SetState(state);
        }
        
        #endregion

        #region State manipulation
        
        /// <summary>
        /// Set the state.
        /// </summary>
        /// <param name="newState">The new state.</param>
        /// <returns><value>true</value> if the state changed; <value>false</value> otherwise.</returns>
        private bool SetState(object newState)
        {
            Validate(newState);
            bool ret = _state != newState;
            _state = newState;
            return ret;
        }

        #endregion

        #region Agent methods

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
        public Exception getError()
        {
            return _aq.Get()._error;
        }

        ///// <summary>
        ///// Clear the agent's errors.
        ///// </summary>
        ///// <remarks>Lowercase-name and  for core.clj compatibility.</remarks>
        //public void clearErrors()
        //{
        //    _errors = null;
        //}

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
        public void setErrorMode(Keyword k)
        {
            _errorMode = k;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
        public Keyword getErrorMode()
        {
            return _errorMode;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
        public void setErrorHandler(IFn f)
        {
            _errorHandler = f;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
        public IFn getErrorHandler()
        {
            return _errorHandler;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
        [MethodImpl(MethodImplOptions.Synchronized)]
        public object restart(object newState, bool clearActions)
        {
            if (getError() == null)
                throw new InvalidOperationException("Agent does not need a restart");

            Validate(newState);
            _state = newState;

            if (clearActions)
                _aq.Set(ActionQueue.EMPTY);
            else
            {
                bool restarted = false;
                ActionQueue prior = null;
                while (!restarted)
                {
                    prior = _aq.Get();
                    restarted = _aq.CompareAndSet(prior, new ActionQueue(prior._q, null));
                }

                if (prior._q.count() > 0)
                    ((Action)prior._q.peek()).execute();
            }

            return newState;
        }



        /// <summary>
        /// Send a message to the agent.
        /// </summary>
        /// <param name="fn">The function to be called on the current state and the supplied arguments.</param>
        /// <param name="args">The extra arguments to the function.</param>
        /// <param name="solo"><value>true</value> means execute on its own thread (send-off); 
        /// <value>false</value> means use a thread pool thread (send).</param>
        /// <returns>This agent.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
        public object dispatch(IFn fn, ISeq args, Boolean solo)
        {
            Exception error = getError();
            if (error != null)
                throw new InvalidOperationException("Agent is failed, needs restart", error);
            Action action = new Action(this,fn,args,solo);
            DispatchAction(action);

            return this;
        }

        /// <summary>
        /// Send an action (encapsulated message).
        /// </summary>
        /// <param name="action">The action to execute.</param>
        /// <remarks>
        /// <para>If there is a transaction running on this thread, 
        /// defer execution until the transaction ends 
        /// (enqueue the action on the transaction).</para>
        /// <para>If there is already an action running, enqueue it (nested).</para>
        /// <para>Otherwise, queue it for execution.</para>
        /// </remarks>
        internal static void DispatchAction(Action action)
        {
            LockingTransaction trans = LockingTransaction.GetRunning();
            if (trans != null)
                trans.Enqueue(action);
            else if (_nested != null)
                _nested = _nested.cons(action);
            else
                action.Agent.Enqueue(action);
        }

        /// <summary>
        /// Enqueue an action in the pending queue.
        /// </summary>
        /// <param name="action">The action to enqueue.</param>
        /// <remarks>Spin-locks to update the queue.</remarks>
        void Enqueue(Action action)
        {
            bool queued = false;
            ActionQueue prior = null;
            while (!queued)
            {
                prior = _aq.Get();
                queued = _aq.CompareAndSet(prior, new ActionQueue((IPersistentStack)prior._q.cons(action), prior._error));
            }

            if (prior._q.count() == 0 && prior._error == null )
                action.execute();
        }


        #endregion

        #region IDeref Members

        /// <summary>
        /// Gets the (immutable) value the reference is holding.
        /// </summary>
        /// <returns>The value</returns>
        public override object deref()
        {
            return _state;
        }

        #endregion

        #region core.clj compatability

        /// <summary>
        /// Shutdown all threads executing.
        /// </summary>
        /// <remarks>We need to work on this.</remarks>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
        public static void shutdown()
        {
            // JAVA: soloExecutor.shutdown();
            // JAVA: pooledExecutor.shutdown();

            // TODO: record active jobs and shut them down?
        }
        #endregion

        /// <summary>
        /// An encapsulated message.
        /// </summary>
        internal sealed class Action
        {
            #region Data

            /// <summary>
            /// The agent this message is for.
            /// </summary>
            readonly Agent _agent;

            /// <summary>
            /// The agent this message is for.
            /// </summary>
            public Agent Agent
            {
                get { return _agent; }
            } 

            /// <summary>
            /// The function to call to create the new state.
            /// </summary>
            readonly IFn _fn;

            /// <summary>
            /// The arguments to call (in addition to the current state).
            /// </summary>
            readonly ISeq _args;

            /// <summary>
            /// Should execute on its own thread (not a thread-pool thread).
            /// </summary>
            readonly bool _solo;

            #endregion

            #region Ctors

            /// <summary>
            /// Create an encapsulated message to an agent.
            /// </summary>
            /// <param name="agent">The agent the message is for.</param>
            /// <param name="fn">The function to compute the new value.</param>
            /// <param name="args">Additional arguments (in addition to the current state).</param>
            /// <param name="solo">Execute on its own thread?</param>
            public Action(Agent agent, IFn fn, ISeq args, bool solo)
            {
                _agent = agent;
                _fn = fn;
                _args = args;
                _solo = solo;
            }

            #endregion

            #region Executing the action

            /// <summary>
            /// Send the message.
            /// </summary>
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
            public void execute()
            {
                try
                {
                    if (_solo)
                    {
                        // TODO:  Reuse/cleanup these threads
                        Thread thread = new Thread(ExecuteAction);
                        //thread.Priority = ThreadPriority.Lowest;
                        thread.Start(null);
                    }
                    else
                        ThreadPool.QueueUserWorkItem(ExecuteAction);
                }
                catch (Exception error)
                {
                    if (_agent._errorHandler != null)
                    {
                        try
                        {
                            _agent._errorHandler.invoke(_agent, error);
                        }
                        catch (Exception)
                        {
                            // ignore _errorHandler errors
                        }
                    }
                }
            }

            /// <summary>
            /// Worker method to execute the action on a thread.
            /// </summary>
            /// <param name="state">(not used)</param>
            /// <remarks>corresponds to doRun in Java version</remarks>
            void ExecuteAction(object state)
            {
                try
                {
                    Agent.Nested = PersistentVector.EMPTY;

                    Exception error = null;

                    try
                    {
                        object oldval = _agent.State;
                        object newval = _fn.applyTo(RT.cons(_agent.State, _args));
                        _agent.SetState(newval);
                        _agent.NotifyWatches(oldval,newval);
                    }
                    catch (Exception e)
                    {
                        error = e;
                    }

                    if (error == null)
                        releasePendingSends();
                    else
                    {
                        Nested = null;  // allow errorHandler to send
                        if (_agent._errorHandler != null)
                        {
                            try
                            {
                                _agent._errorHandler.invoke(_agent, error);
                            }
                            catch (Exception)
                            {
                                // ignore error handler errors
                            }
                        }
                        if (_agent._errorMode == ContinueKeyword)
                            error = null;
                    }

                    bool popped = false;
                    ActionQueue next = null;
                    while (!popped)
                    {
                        ActionQueue prior = _agent._aq.Get();
                        next = new ActionQueue(prior._q.pop(), error);
                        popped = _agent._aq.CompareAndSet(prior, next);
                    }

                    if (error==null && next._q.count() > 0)
                        ((Action)next._q.peek()).execute();
                }
                finally
                {
                    Nested = null;
                }
            }

            #endregion
        }

        /// <summary>
        /// Enqueue nested actions.
        /// </summary>
        /// <returns></returns>
        /// <remarks>lowercase for core.clj compatibility</remarks>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
        public static int releasePendingSends()
        {
            IPersistentVector sends = Agent.Nested;
            if (sends == null)
                return 0;
            for (int i = 0; i < sends.count(); i++)
            {
                Action a = (Action)sends.valAt(i);
                a.Agent.Enqueue(a);
            }
            Nested = PersistentVector.EMPTY;
            return sends.count();
        }
    }
}

    *)