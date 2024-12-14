---
layout: post
title: STM, part one -- Background
date: 2024-12-15 06:00:00 -0500
categories: general
---

Wherein we look at software transactional memory (STM) as implemented in Clojure.

## Introduction

Clojure provides "shared use of mutable storage locations" by implementing software transactional memory (STM).  Transactions here provide the ACI in ACID: atomicity, consistency, and isolation. The Clojure implementation of STM uses multi-version concurrency control (MVCC) to provide a consistent view of the world to each transaction.  

As I was porting the Clojure STM implementation to F# as part of the ClojureCLR.Next project, I realized I no longer understood the Java/C# code in any way.  It took quite a while to reconstruct the rationale behind the code. Hence this post; future me will thank present me.

## Background reading

It helps to be familiar with the basic concepts of STM and MVCC.  A quick introduction the concepts in general are:

- [STM](https://en.wikipedia.org/wiki/Software_transactional_memory)
- [MVCC](https://en.wikipedia.org/wiki/Multiversion_concurrency_control)

For the rationale behind STM in Clojure, the place to go is:

- [Refs and Transactions](https://clojure.org/reference/refs).  

Working through the code, I could reconstruct most of the rationale for why things were coded the way they were.  Except for histories.  I searched the interwebs for clues, and eventually found [this comment](https://clojurians.slack.com/archives/C053AK3F9/p1614937230273100) on the Clojurian Slack, reminding me that there are these things called 'books'.  It was such a pleasure to rediscover: 

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

- [ref](https://clojure.github.io/clojure/clojure.core-api.html#clojure.core/ref) -- creates a `Ref` object.
- [dosync](https://clojure.github.io/clojure/clojure.core-api.html#clojure.core/dosync) -- evaluates code in a transaction
- [ensure](https://clojure.github.io/clojure/clojure.core-api.html#clojure.core/ensure) -- enrolls a ref in the current transaction
- [ref-set](https://clojure.github.io/clojure/clojure.core-api.html#clojure.core/ref-set) -- sets the value of a ref (in a transaction)
- [alter](https://clojure.github.io/clojure/clojure.core-api.html#clojure.core/alter) -- updates the value of a ref (in a transaction)
- [commute](https://clojure.github.io/clojure/clojure.core-api.html#clojure.core/commute) -- updates the value of a ref (in a transaction) both during the transaction, with a recomputation at the end of the transaction.

For now, we can ignore little extras such as validators and watchers.





