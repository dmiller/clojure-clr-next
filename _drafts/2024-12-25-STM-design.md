---
layout: post
title: STM in Clojure - Design
date: 2024-12-15 06:00:00 -0500
categories: general
---

Wherein we look at software transactional memory (STM) as implemented in Clojure.

## Introduction

Clojure provides "shared use of mutable storage locations" by implementing software transactional memory (STM).  Transactions here provide the ACI in ACID: atomicity, consistency, and isolation. The Clojure implementation of STM uses multi-version concurrency control (MVCC) to provide a consistent view of the world to each transaction and to the world at large.

As I was porting the Clojure STM implementation to F# as part of the ClojureCLR.Next project, I realized I no longer understood the Java/C# code.  It took quite a while to reconstruct the rationale behind the code. Hence this post; future me will thank present me.

## Background reading

It helps to be familiar with the basic concepts of STM and MVCC.  A quick introduction the concepts in general are:

- [STM](https://en.wikipedia.org/wiki/Software_transactional_memory)
- [MVCC](https://en.wikipedia.org/wiki/Multiversion_concurrency_control)

For the rationale behind STM in Clojure, the place to go is:

- [Refs and Transactions](https://clojure.org/reference/refs).  

The best write-up I found on the implemntation of STM in Clojure [MVCC STM in Clojure](https://rhishikesh.com/posts/mvcc_stm/).

Working through the code, I could reconstruct most of the rationale for why things were coded the way they were.  Except for histories. The post mentioned above mentions histories but does not give a rationale for their existence.  I searched the interwebs for clues, and eventually found [this comment](https://clojurians.slack.com/archives/C053AK3F9/p1614937230273100) on the Clojurian Slack, reminding me that there are these things called 'books'.  It was such a pleasure to rediscover: 

- [Clojure Programming](https://www.oreilly.com/library/view/clojure-programming/978144931038) by Chas Emerick, Brian Carper, and Christophe Grand.  The section titled _Refs_ in _Chapter 4: Concurrency and Parallelism_ has an excellent description of the implementation of STM in Clojure.
- [The Joy of Clojure](https://www.manning.com/books/the-joy-of-clojure-second-edition) by Michael Fogus and Chris Houser.  The first two sections of _Chapter 10: Mutation and Concurrency_ are also excellent.

I an no longer doomed to repeating (my search for the meaning of) history. 


## Promises, promises

The following gurantees are made in [Refs and Transactions](https://clojure.org/reference/refs):

    All reads of Refs will see a consistent snapshot of the 'Ref world' as of the starting point of the transaction (its 'read point'). The transaction will see any changes it has made. This is called the in-transaction-value.

    All changes made to Refs during a transaction (via ref-set, alter or commute) will appear to occur at a single point in the 'Ref world' timeline (its 'write point').

    No changes will have been made by any other transactions to any Refs that have been ref-set / altered / ensured by this transaction.
    
    Changes may have been made by other transactions to any Refs that have been commuted by this transaction. That should be okay since the function applied by commute should be commutative.
    
    Readers and commuters will never block writers, commuters, or other readers.

    Writers will never block commuters, or readers.


    Next, you should look at the API documentation for relevant functions.

- [dosync](https://clojure.github.io/clojure/clojure.core-api.html#clojure.core/dosync) -- evaluates code in a transaction
- [ref](https://clojure.github.io/clojure/clojure.core-api.html#clojure.core/ref) -- creates a `Ref` object.
- [deref](https://clojure.github.io/clojure/clojure.core-api.html#clojure.core/deref) -- returns the current value of a `Ref`.
- [ensure](https://clojure.github.io/clojure/clojure.core-api.html#clojure.core/ensure) -- enrolls a ref in the current transaction
- [ref-set](https://clojure.github.io/clojure/clojure.core-api.html#clojure.core/ref-set) -- sets the value of a ref (in a transaction)
- [alter](https://clojure.github.io/clojure/clojure.core-api.html#clojure.core/alter) -- updates the value of a ref (in a transaction)
- [commute](https://clojure.github.io/clojure/clojure.core-api.html#clojure.core/commute) -- updates the value of a ref (in a transaction) both during the transaction, with a recomputation at the end of the transaction.

For now, we can ignore things such as validators and watchers.  And agents.  Though they are important tools in using refs, they are incidental to the main logic of the STM implementation.

## Design thinking

I don't want to derive all of MVCC from first principles, but a little thinking through scenarios will help motivate what we are going to see in the code.

The timeline of Ref values proceeds in discrete steps.  We will assign integers as timestamps; timestamps will be monontonically increasing.  The value currently assigned to a Ref will have a timestamp associated with it. In fact, for reasons we will go into later, a Ref can keep a history of values with older timestamps. Transactions in progress will also have timestamps associated with them.   A snapshot of the "Ref world" would be a set of values in effect at a particular time.

Let's say we have Refs `r1`, `r2`, and `r3` with the following histories, indicated with the notation '<value, timestamp>':

```Clojure  
    r1: <v11, 0> <v12, 12> <v13, 17>
    r2: <v21, 0>                     <v22, 22>
    r3: <v31, 0>
```

A snapshot of the Ref world at timestamp 19 would be: r1 = v13, r2 = v21, r3 = v31.

In the normal world, outside of any transaction, we only see the most value for a ref.  Thus, right now in this world, if we used `@` (or `deref`) to get the values, we would see:

```Clojure
    @r1 => v13
    @r2 => v22
    @r3 => v31
```

At a given point in time, a transaction may _commit_ and update the Ref world with the changes it has computed.  That commit will have a timestamp associated with it.
Suppose transaction T1 commits with commit timestamp 29 and updates `r1` and `r3`.  The Ref world would now be:

```Clojure  
    r1: <v11, 0> <v12, 12> <v13, 17>            <v14, 29>
    r2: <v21, 0>                     <v22, 22>  
    r3: <v31, 0>                                <v32, 29>
```

Atomicity requires that either both changes are made or neither.  Consistency requires that no one ever sees the Ref world in an intermediate state where, say, `r1` has been updated but `r3` has not.  The easiest way to achieve this is to have T1 acquire locks on `r1` and `r3` before making any changes.  T1 will then update those refs before releasing the locks.  

You might have heard that MVCC doesn't involve locks.  It's bit more nuanced than that.  What MVCC avoids is coarse-grained locking and locks with non-trivial temporal extent. While T1 is running, before it gets to the point of committing, it is doing its various computations.  It is _not_ locking the refs it is updating, except briefly when it has to read a value or do a little bookkeeping.  Only at the point of committing does it lock the refs it is updating.  And there is no user code being run while those refs are locked.  The locks are held just long enough to update the data structures.

Isolation is achieved by making invisibile to the outside any changes made to a ref (via `ref-set`, `alter`, or `commute`) until the transaction commits.  The transaction will see the changes it has made.  This is accomplished by having the transaction keep track of the 'in-transaction-value' of any ref it has updated. Thus, the `deref` operation operates differently inside a transaction than outside.

### Stepping on toes

Transactions should be coded in the expectation that the transaction's code may be executed an arbitray number of times.  A transaction will be forced to retry if the Ref world has changed underneath it, i.e., since the time it started and before committing, some other transaction has committed a change to a `Ref` of interest, then the transaction is no longer operating from a consistent snapshot of the Ref world.  The transaction will be aborted and restarted.  

If implemented in a naive manner, this mechanism could lead to inefficiency and unfairness.  Inefficiency comes about if we only discover a conflict at commit time. If we could discover a potential conflict as we are making in-transaction changes -- without violating isolation -- we could abort right away, forgoing any remaining computaiton, and restarting with hope for a better outcome.  Fairness is an issue if a transaction could be forced to retry indefinitely because it keeps getting preempted by other transactions.

Both of these issues are ameloriated by the same mechanism.  Consider two transactions.  Transaction T1 computes a (in-transaction) change to Ref R1.  At that time, it will put a little 'stamp' on R1 saying that it intends to change R1's value when T1 commits.  T2 comes along a bit later, but before T1 has committed, and want to change (in-transaction) R1's value also.  T2 will see that R1 has T1's stamp on it.  At that point, T2 has two choices:  (a) force T1 to retry, allowing itself to continue; or (b) force itself to retry, allowing T1 to continue.   Option (a) is called 'barging' T1.  Option (b) in this code is called 'block-and-bail'.   There are several factors considered in the decision, but a primary one is the relatively priority of T1 versus T2, as determined by the timestamps of the transactions (older wins).

Note thatn if T2 barges T1, we have to change T1's behavior in some way.  T2 can't just reach in to T1's thread and throw an exception or other hackery.   We use a clever trick to do this signaling efficiently. When a transaction is created, it creates a small 'transaction info' (TInfo) object that contains its starting timestamp (the signifier of its 'age') and its status: running, committing, committed, etc.  This TInfo object is used as the stamp T1 puts on R1.  When T2 needs to decide whether to barge T1, the TInfo object tells it T1's age.  If T2 decides to barges T1, it does so by setting the status in T1's TInfo object to 'retry'.  We arrange the transaction code so that any significant action -- setting refs or committing -- first checks to make sure the transaction is still running. If not, then it will retry.

This little trick also allows us to avoid cleaning up the stamps on Refs.  When we look at stamp, we check the status to see if the transaction that placed the stamp is still running.  If not, we ignore the stamp.  So there is no need to clean up the stamps.  They are tiny objects so the memomroy overhead is minimal.

### Histories

When a transaction is working and needs to `ref-set` or `alter` a Ref, and another transaction has committed a value since the transaction started, the transation must retry -- we cannot maintain a consistent state in the Ref world otherwise. `ensure` can be called on a Ref to enroll it in the transaction. The transaction will then be forced to retry if the Ref is updated by another transaction.  

What about a transaction that just wants to read a set of Refs at particular point in the timeline of the Ref world?  It would be nice if we could avoid forcing the transaction to retry if the Refs are updated.  We can do this by having the Ref keep a history of values.  The transaction can ask for the value of the Ref at the start time of the transaction.  It will see a consistent view of the world.  That is all that is required.

For example, consider the scenario outlined earlier.  The world looks like this:

```Clojure  
    r1: <v11, 0> <v12, 12> <v13, 17>
    r2: <v21, 0>                     <v22, 22>
    r3: <v31, 0>
```
A 'read only' transaction T1 starts with a timestamp of 24.  As it iterates through the refs, some other transaction comes along and updates the world to look like:


```Clojure  
    r1: <v11, 0> <v12, 12> <v13, 17>            <v14, 29>
    r2: <v21, 0>                     <v22, 22>  
    r3: <v31, 0>                                <v32, 29>
```

T1 will still see the world as it was at timestamp 24 if (a) require Refs to keep a history and (b) code `deref` in a transaction to use the transaction's start point when looking up the value of a Ref. 

Because keeping every value across all time would be quite expensive, the size of the history is limited.  One can set the `:min-history` and `:max-history` when the Ref is created (defaulting to 0 and 10, respectively) or call `set-min-history` and `set-max-history` to change the values on the fly.  The size of the list will be dynamically adjusted to keep the history within the bounds.  (As we shall see.) 


## Let's code

As suggested above, I'm going to leave out some details of the implementation that are not relevant to STM directly.
There are two primary classes to deliver: `Ref` and `LockingTransaction`.  In F#, these will be mutally recursive classes.

`Ref` is by far the simpler of the two.  
It primarily holds the history list of values, 
forwards certain operations to the transaction currently running (on its thread), 
and provides some basic operations to support `LockingTransaction`.

[The code below is presented in incorrect order for presentation.]

The history list is implemented using a doubly-linked circular list.  
The list is never empty; it always starts with an entry for the initial value given when the `Ref` is created.
The initial value will be associated with timestamp 0.
The 'root' of the list, the node that the `Ref` holds diretly, will always have the most recent value.

All accesses to this list must be protected by locking.  The conditions specified by our earlier guarantees matches up with the semantic of a `System.Threading.ReaderWriterLockSlim`.
It is incumbent on the `LockingTransaction` to acquire the lock in read mode when it is reading the value of a `Ref` 
and in write mode when it is updating the value of a `Ref`.  We will provide utility methods to make this easy.

Let's get started.  We define the class `Refval` to be a node in the double-linked circular list for holding values.
It must hold a value, a timestamp, and points to the next and previous nodes in the list.
The default constructor will create a node representing a list of one element, i.e., the next and previous pointers will point to the node itself.  


```F#
type RefVal(v: obj, pt: int64) as this
    // these initializations are sufficient to create a circular list of one element
    let mutable value : obj = v
    let mutable point : int64 = pt
    let mutable prior : RefVal = this
    let mutable next : RefVal = this

    // When we need to add a new node to the list, we need to know its value, timestamp, and its neighbors.    
    new(v, pt, pr : RefVal) as this = 
        RefVal(v, pt)
        then
            this.Prior <- pr
            this.Next <- pr.Next
            pr.Next <- this
            this.Next.Prior <- this

    // Ref will need some accessors to do iterations
    member _.Value with get() = value
    member _.Point with get() = point
    member _.Prior with get() = prior
    member _.Next with get() = next

    // We will need to update the value in the root node
    member _.SetValue(v: obj, pt: int64) = 
        value <- v
        point <- pt

    // There is one special operation to reset the root node so that the list has only this one element.
    member this.Trim() = 
        prior <- this
        next <- this
```

Now we can proceed with the `Ref` class.

```F#
[<AllowNullLiteral>]
type Ref(initVal: obj)
    // Initialize the root node with the initial value and timestamp 0
    let mutable refVals = RefVal(initVal, 0L)

    // We need a lock to protect access
    let lock = new System.Threading.ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion)

    // We need a field to hold the 'stamp' a transaction puts on the Ref. 
    // The details do not matter to us here.
    let mutable tinfo : LTInfo option = None

    // We need this history size bounds.    
    [<VolatileField>]
    let mutable minHistory = 0 

    [<VolatileField>]
    let mutable maxHistory = 10

    // This little goodie we'll need later. 
    // It counts the number of read faults that have occurred, used to decide when we need to grow the history list.
    let faults = AtomicInteger()
```

We're going to need some basic accessors for `LockingTransaction` and clojure.core to use.


```F#
    member _.TInfo
        with get () = tinfo
        and set(v) = tinfo <- v

    member _.MinHistory with get() = minHistory
    member _.MaxHistory with get() = maxHistory
    member this.SetMinHistory(v) = minHistory <- v; this
    member this.SetMaxHistory(v) = maxHistory <- v; this
```

Some little helpers for dealing with locking:

```F#
    member _.enterReadLock() = lock.EnterReadLock()
    member _.exitReadLock() = lock.ExitReadLock()
    member _.enterWriteLock() = lock.EnterWriteLock()
    member _.exitWriteLock() = lock.ExitWriteLock()
    member _.tryEnterWriteLock(msecTimeout : int) = lock.TryEnterWriteLock(msecTimeout)
```

We get to see a little locking action here.  `getHistoryCount` is needed to implement `get-history-count` in Clojure itself.
We need to acquire at least a read lock anytime we access `refVals`.

```F#
    // Count the entries in the values list
    // the caller should lock
    member private _.histCount() =
        let mutable count = 0
        let mutable tv = tvals.Next
        while LanguagePrimitives.PhysicalEquality tv tvals do
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
    member _.trimHistory() =
        try
            this.enterWriteLock()
            refVals.Trim()
        finally
            this.exitWriteLock()
```

A few more convenience methods. 

```F#
    // These need to be called with a lock acquired.
    member _.currentPoint() = refVals.Point
    member _.currentValue() = refVals.Value

    // Add to the fault count.
    member _.addFault() = faults.incrementAndGet() |> ignore
```

We have a few more public methods to implement to complete the interface.  `Ref` needs to implement interface `IDeref`.
Here is where we get our first interaction with the transaction. 
As mentioned previously, what `deref` looks at depends on whether it is called in a transaction scope or not.
`LockingTransaction.getRunning()` will return `None` if we are not in a transaction.  
Otherwise, it will return the transaction object.  
If we are running in a transaction scope, we have to ask the transaction to supply the value because there might be an in-transaction value for this `Ref`.
If not, we just access the current value.

```F#
    interface IDeref with
        member this.deref() =
            match LockingTransaction.getRunning() with
            | None -> 
                try 
                    this.enterReadLock()
                    refVals.Value
                finally
                    this.exitReadLock()
            | Some t -> t.doGet(this)
```

The operations `ref-set`, `alter`, and `commute` are all implemented in  `LockingTransaction`.
`LockingTransaction.getEx()` will return the transaction object if we are in a transaction scope and throw an exception if we are not.

```F#
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
```

The last operation for `Ref` is used by `LockingTransaction` to update the value of a `Ref` in the Ref world.
Here is where history comes into play.  We will expand the history list in two situations:  
(a) the current list is size is less than the minimum history size; 
and (b) we had a fault in getting the value of this `Ref` and we are less than the maximum history size.
The fault count incremented in the `LockedTransaction` code.  
We will reset the fault count to zero when we increase the list.
These two conditions are tested for in the `if` clause below.
If either condition is satisfied, we add a new `RefVal` node to the list.
If neither condition is met we replace the oldest value in the list with the new value and make it the new root node.
(The statement `refVals <- refVals.Next` is doing the heavy lifting here.  Draw a map.)

```F#
   // Set the value
    member this.setValue(v: obj, commitPoint: int64) =
        let hcount = this.histCount()
        if (faults.get() > 0 && hcount < maxHistory) || hcount < minHistory then
            refVals <- RefVal(v, commitPoint, refVals)
            faults.set(0) |> ignore
        else
            refVals <- refVals.Next
            refVals.SetValue(v, commitPoint)
```