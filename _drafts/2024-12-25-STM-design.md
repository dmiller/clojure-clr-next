---
layout: post
title: STM in Clojure - Design
date: 2024-12-15 06:00:00 -0500
categories: general
---

Wherein we look at software transactional memory (STM) as implemented in Clojure.

- Part 1: STM in Clojure - Design  (this post)
- [Part 2: STM in Clojure - Code]({{site.baseurl}}{% post_url 2024-12-26-STM-code %})
- [Part 3: STM in Clojure - Testing]({{site.baseurl}}{% post_url 2024-12-27-STM-testing %})


## Introduction

Clojure provides "shared use of mutable storage locations" by implementing software transactional memory (STM).  Transactions here provide the ACI in ACID: atomicity, consistency, and isolation. The Clojure implementation of STM uses multi-version concurrency control (MVCC) to provide a consistent view of the world to each transaction and to the world at large.

As I was porting the Clojure STM implementation to F# as part of the ClojureCLR.Next project, I realized I no longer understood the Java/C# code.  It took quite a while to reconstruct the rationale behind the code. Hence this post; future me will thank present me.

## Background reading

It helps to be familiar with the basic concepts of STM and MVCC.  There are quick reads:

- [STM](https://en.wikipedia.org/wiki/Software_transactional_memory)
- [MVCC](https://en.wikipedia.org/wiki/Multiversion_concurrency_control)

The official reference on STM in Clojure is:

- [Refs and Transactions](https://clojure.org/reference/refs).  

[MVCC STM in Clojure](https://rhishikesh.com/posts/mvcc_stm/) has a nice write-up of how STM works in Clojure.

Working through the code, I could reconstruct most of the rationale for why things were coded the way they were.  Except for histories. The post cited above mentions histories but does not give a rationale for their existence.  I searched the interwebs for clues, and eventually found [this comment](https://clojurians.slack.com/archives/C053AK3F9/p1614937230273100) on the Clojurian Slack, reminding me that there are these things called 'books'.  It was such a pleasure to dig back into: 

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

We'll try to address some of these points in the design discussion below.

You should look at the API documentation for relevant functions.

- [dosync](https://clojure.github.io/clojure/clojure.core-api.html#clojure.core/dosync) -- evaluates code in a transaction
- [ref](https://clojure.github.io/clojure/clojure.core-api.html#clojure.core/ref) -- creates a `Ref` object.
- [deref](https://clojure.github.io/clojure/clojure.core-api.html#clojure.core/deref) -- returns the current value of a `Ref`.
- [ensure](https://clojure.github.io/clojure/clojure.core-api.html#clojure.core/ensure) -- enrolls a ref in the current transaction
- [ref-set](https://clojure.github.io/clojure/clojure.core-api.html#clojure.core/ref-set) -- sets the value of a ref (in a transaction)
- [alter](https://clojure.github.io/clojure/clojure.core-api.html#clojure.core/alter) -- updates the value of a ref (in a transaction)
- [commute](https://clojure.github.io/clojure/clojure.core-api.html#clojure.core/commute) -- updates the value of a ref (in a transaction) both during the transaction, with a recomputation at the end of the transaction.

I'm going to ignore some aspects of Refs and transations such as validators and watchers.  And I'm going to ignore agents.  Though they are important tools in using Refs, they are incidental to the main logic of the STM implementation.

## Design thinking

I don't want to derive all of MVCC from first principles, but a little thinking through scenarios will help motivate what we are going to see in the code.

The timeline of Ref values proceeds in discrete steps.  Any given point, in the world outside of any ongoing transaction, we see a consistent set of values across all the Refs that have been created.  There may be transactions running, but we do not see anything they are up to until they commit. When a transaction commits, it will update the Ref world atomically; from the point of view of anyone outside that transaction, the changes will appear to have happened all at once.  We will never see a state where only a subset of the changes to Ref values made by the transaction have been applied.

Internally to transactions and to the Refs themselves, timestamps are used to keep track of the order of events.  Timestamps are integers and are monotically increasing with time.  Transactions get assigned timestamps when they are created, when they are forced to retry execution after a conflict and when they commit.  They are used internally to determine the relative age of transactions.
In addition, the value currently assigned to a Ref will have a timestamp associated with it, in fact, the timestamp assigned to the commit action that set the value. For reasons we will go into later, a Ref can keep a history of values with older timestamps.  A snapshot of the "Ref world" would be a set of values in effect at a particular time.

Let's say we have Refs `r1`, `r2`, and `r3` with the following histories, indicated with the notation '<value, timestamp>':

```Clojure  
    r1: <v11, 0> <v12, 12> <v13, 17>
    r2: <v21, 0>                     <v22, 22>
    r3: <v31, 0>
```

A snapshot of the Ref world at timestamp 19 would be: r1 = v13, r2 = v21, r3 = v31.

In the normal world, outside of any transaction, we only see the most recent value for a ref.  Thus, right now in this world, if we used `@` (or `deref`) to get the values, we would see:

```Clojure
    @r1 => v13
    @r2 => v22
    @r3 => v31
```

Suppose now that transaction T1 commits with commit timestamp 29 and updates `r1` and `r3`.  The Ref world would now be:

```Clojure  
    r1: <v11, 0> <v12, 12> <v13, 17>            <v14, 29>
    r2: <v21, 0>                     <v22, 22>  
    r3: <v31, 0>                                <v32, 29>
```

Atomicity requires that either both changes are made or neither.  Consistency requires that no one ever sees the Ref world in an intermediate state where, say, `r1` has been updated but `r3` has not.  The easiest way to achieve this is to have T1 acquire locks on `r1` and `r3` before making any changes.  T1 will then update those refs before releasing the locks.  

You might have heard that MVCC doesn't involve locks.  It's bit more nuanced than that.  What MVCC avoids is coarse-grained locking and locks with non-trivial temporal extent. While T1 is running, before it gets to the point of committing, it is doing its various computations.  It is _not_ locking the refs it is updating, except briefly when it has to read a value or do a little bookkeeping.  Only at the point of committing does it lock the refs it is updating.  And there is no user code being run while those refs are locked.  The locks are held just long enough to update the data structures.

Isolation is achieved by making invisibile to the outside any changes made to a ref (via `ref-set`, `alter`, or `commute`) until the transaction commits.  While executing, the transaction _will_ see the changes it has made.  This is accomplished by having the transaction keep track of the 'in-transaction-value' of any ref it has updated. Thus, the `deref` operation operates differently inside a transaction than outside.

### Stepping on toes

Transactions should be coded in the expectation that the transaction's code may be executed an arbitray number of times.  A transaction will be forced to retry if the Ref world has changed underneath it in a way significant to the transaction, i.e., since the time it started and before committing, some other transaction has committed a change to a `Ref` of interest, then the transaction is no longer operating from a consistent snapshot of the Ref world.  The transaction will be aborted and restarted.  

If implemented in a naive manner, this mechanism could lead to inefficiency and unfairness.  Inefficiency comes about if we only discover a conflict at commit time. If we could discover a potential conflict as we are making in-transaction changes -- without violating isolation -- we could abort right away, forgoing any remaining computation, and restarting with hope for a better outcome.  Fairness is an issue if a transaction could be forced to retry indefinitely because it keeps getting preempted by other transactions.

Both of these issues are ameloriated by the same mechanism.  Consider two transactions.  Transaction T1 computes a (in-transaction) change to Ref R1.  At that time, it will put a little 'stamp' on R1 saying that it intends to change R1's value when T1 commits.  T2 comes along a bit later, but before T1 has committed, and wants to change (in-transaction) R1's value also.  T2 will see that R1 has T1's stamp on it.  At that point, T2 has two choices:  (a) force T1 to retry, allowing itself to continue; or (b) force itself to retry, allowing T1 to continue.   Option (a) is called 'barging' T1.  Option (b) in this code is called 'block-and-bail'.   There are several factors considered in the decision, but a primary one is the relatively priority of T1 versus T2, as determined by the timestamps of the transactions (older wins).

If T2 barges T1, it has to signal T1 in some way to force T1 to retry.  T2 can't just reach in to T1's thread and throw an exception or other hackery.   We use a clever trick to do this signaling efficiently. When a transaction is created, it creates a small 'transaction info' (`LTInfo`) object that contains its starting timestamp (the signifier of its 'age') and its status: running, committing, committed, etc.  This `LTInfo` object is used as the stamp T1 puts on R1.  When T2 needs to decide whether to barge T1, the `LTInfo` object tells it T1's age.  If T2 decides to barges T1, it does so by setting the status in T1's `LTInfo` object to 'retry'.  We arrange the transaction code so that any significant action -- setting refs or committing -- first checks to make sure the transaction is still running. If not, then it will retry.  So when T1 tries to do another `ref-set` or other Ref operation, or starts to commit, it will see that it has been barged and will retry.

This little trick also allows us to avoid cleaning up the stamps on Refs.  When we look at stamp, we check the status to see if the transaction that placed the stamp is still running.  If not, we ignore the stamp.  So there is no need to clean up the stamps.  They are tiny objects so the memory overhead is minimal.

### Histories

When a transaction is working and needs to `ref-set` or `alter` a Ref, and another transaction has committed a value since the transaction started, the transation must retry -- we cannot maintain a consistent state in the Ref world otherwise. `ensure` can be called on a Ref to enroll it in the transaction. The transaction will then be forced to retry if the ensured Ref is updated by another transaction.  

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

T1 will still see the world as it was at timestamp 24 if (a) we require Refs to keep a history and (b) code `deref` in a transaction to use the transaction's start point when looking up the value of a Ref. 

Because keeping every value across all time would be quite expensive, the size of the history is limited.  One can set the `:min-history` and `:max-history` when the Ref is created (defaulting to 0 and 10, respectively) or call `set-min-history` and `set-max-history` to change the values on the fly.  The size of the list will be dynamically adjusted to keep the history within the bounds.  (As we shall see.) 

Time to [look at the code]({{site.baseurl}}{% post_url 2024-12-26-STM-code %}).