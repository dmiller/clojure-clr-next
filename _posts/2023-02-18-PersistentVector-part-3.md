---
layout: post
title: Persistent vectors, Part 3 -- Transiency
date: 2023-02-18 12:00:00 -0500
categories: general
---

Clojure's vectors, hash-maps and hash-sets support _transients_.  We examine the implementation of transiency for `PersistentVector`.

## Background

You should read the reference article [_Transient Data Structures_](https://clojure.org/reference/transients) to get the rationale and overall idea of the role of transient data structures and interaction with them.  We will examine them at the implementation level rather than the Clojure API level.

In the context of `PersistentVector`, the functionality that we need to support is:

- Creating a transient version of an existing vector; this operation needs to be _O(1)_.
- No operation on the transient changes the existing object.
- The transient needs to support operations such as `assoc` and `conj` in an efficient manner.  These operations on a transient return another transient.  However, mutation of the transients structures is allowed.  Transients are not persistent.
- You can create a persistent data structure from the transient;  this operation needs to be _O(1)_ also.

The requirement for constant time operations for creating transients and converting them back to persistent objects means that there needs to be a great deal of structure sharing with the original (persistent, immutable) object.  Our mutating operations will have to be careful to do mutations only on copies of pieces of the data structure that they know they own exclusively.

## The interfaces

As always, we start with the interfaces that define the behavior we need to implement.
We start with

```F#
[<AllowNullLiteral>]
type IEditableCollection =
    abstract asTransient: unit -> ITransientCollection
```

`PersistentVector` will implement this interface to create a transient version of itself.

The remaining interfaces are implemented by the transient version.

```F#
[<AllowNullLiteral>]
type ITransientCollection =
    abstract conj: o: obj -> ITransientCollection
    abstract persistent: unit -> IPersistentCollection

[<AllowNullLiteral>]
type ITransientAssociative =
    inherit ITransientCollection
    inherit ILookup
    abstract assoc: key: obj * value: obj -> ITransientAssociative

[<AllowNullLiteral>]
type ITransientAssociative2 =
    inherit ITransientAssociative
    abstract containsKey: key: obj -> bool
    abstract entryAt: key: obj -> IMapEntry
    
[<AllowNullLiteral>]
type ITransientVector =
    inherit ITransientAssociative
    inherit Indexed
    abstract assocN: idx: int * value: obj -> ITransientVector
    abstract pop: unit -> ITransientVector
```

These are versions of `IPersistentCollection` and relatives that are specialized to a restricted set of operations and designed so that the operations all return transient objects.  Methods such as `conj` and `assoc` have their usual meanings.  Of note is `persistent` -- this is the method that returns a persistent version of the object when we desire to transition back to immutability+persistence.

## Implementation

A `PersistentVector` has a head node with a tail array and a pointer to the root of the index tree where the bulk of the elements in the array are stored.  We have a parallel for the persistence version.  Compare

```F#
type PersistentVector(meta: IPersistentMap, cnt: int, shift: int, root: PVNode, tail: obj array) =
    inherit APersistentVector()

    member internal _.Count = cnt
    member internal _.Shift = shift
    member internal _.Root = root
    member internal _.Tail = tail
``` 

to 

```F#
type TransientVector private (_cnt, _shift,_root,_tail) =
    inherit AFn()
        
    [<VolatileField>]
    let mutable cnt : int = _cnt

    [<VolatileField>]
    let mutable shift : int = _shift

    [<VolatileField>]
    let mutable root : PVNode = _root

    [<VolatileField>]
    let mutable tail : obj array = _tail
```

Where we had immutable fields for the count, etc., we now have mutable fields, allowing the head object's data to be updated as we perform operations.


We call the `IEditableCollection.asTransient()` method to create a transient copy of our `PersistentVector`:

```F#
    interface IEditableCollection with
        member this.asTransient() = TransientVector(this)
```

This invokes the `TransientVector` constructor

```F#
new(v:PersistentVector) = TransientVector(
    v.Count,
    v.Shift,
    TransientVector.editableRoot(v.Root),
    TransientVector.editableTail(v.Tail))
```

We need to make copies of the tail and the root node so that we may mutate them as needed without affecting our original `PersistentVector`.   Creating the tail is just a copy of small array:

```F#    
static member editableTail(tl : obj array) =
    let arr : obj array = Array.zeroCreate 32
    Array.Copy(tl,arr,tl.Length)
    arr
```

We need to have the same basic structure for indexing our transient version so that we can easily switch from persistent to transient and back to persistent without copying large parts of the tree.  This means using the same `PVNode` structure we had before.

And here is where the magic happens.  Our `PVNode` has a field in it the purpose of which is to identify whether the node can be mutated as part of the current operation, or instead needs to be copied.

Here is `PVnode` in its entirety:

```F#
PVNode(edit: AtomicBoolean, array: obj array) =

    member _.Edit = edit
    member _.Array = array

    new(edit) = PVNode(edit, (Array.zeroCreate 32))
```

The `Edit` field is used to contain a token serving two purposes:  to provide a value indicating being involved with the mutable operations of a particular transient root, and to indicate that transience has ended.  Though there a number of ways to accomplish this, a simple solution is an `AtomicBoolean`.  It will compare as equal only to itself.  If the `Edit` value of a node is equal to the `Edit` value of the root, then we can mutate it.  If not ... see below.

Thus, the code to create the root node of our new `TransientVector`: 

```F#
static member editableRoot(node:PVNode) =
    PVNode(AtomicBoolean(true),
           node.Array.Clone() :?> obj array)
```

As we perform mutating operations, we just need to check whether the node we are about to operate on is participating in this transient transaction.  If not, we work on a copy instead.  This is checked by `EnsureEditable`:

```F#
member this.ensureEditable(node:PVNode) =
    if node.Edit = root.Edit then
        node
    else
        PVNode(root.Edit,node.Array.Clone() :?> obj array)
```

Before we begin using one of our mutating methods, we also need to check that the root node indicates that transience is still being done, i.e., that we haven't called `persistent` yet.  That is done by calls to another `ensureEditable`.  This one checks the flag in the `PersistentBoolean`:

```F#
member this.ensureEditable() =
    if not <| root.Edit.Get() then
        raise <| InvalidOperationException("Transient used after persistent! call")
```




`TransientVector` ends up duplicating much of the code of `PersistentVector` for operations such as `conj`, the difference being that mutating operations are done where possible.  Consider `/cons`/`conj` specifically.  Here is the `PersistentVector` version:


```F#
        override this.cons(o) =

            if cnt - this.tailoff () < 32 then
                // room in the tail
                let newTail = Array.zeroCreate (tail.Length + 1)
                Array.Copy(tail, newTail, tail.Length)
                newTail[tail.Length] <- o
                PersistentVector(meta, cnt + 1, shift, root, newTail)
            else
                // tail is full, push into tree
                let tailNode = PVNode(root.Edit, tail)

                let newroot, newshift =
                    // overflow root?
                    if (cnt >>> 5) > (1 <<< shift) then
                        let newroot = PVNode(root.Edit)
                        newroot.Array[0] <- root
                        newroot.Array[1] <- PersistentVector.newPath (root.Edit, shift, tailNode)
                        newroot, shift + 5
                    else
                        this.pushTail (shift, root, tailNode), shift

                PersistentVector(meta, cnt + 1, newshift, newroot, [| o |])
```

And the `TransientVector` version:

```F#
       member this.conj(v) =

            this.ensureEditable()
            let n = cnt

            if n - this.tailoff() < 32 then 
                // room in tail?
                tail[n &&& 0x01f] <- v
                cnt <- n+1
                this
            else 
                // full tail, push into tree
                let tailNode = PVNode(root.Edit,tail)
                tail <-  Array.zeroCreate 32
                tail[0] <- v
                let newRoot, newShift = 
                    if (n >>> 5) > (1 <<< shift) then
                        let newRoot = PVNode(root.Edit)
                        newRoot.Array[0] <- root
                        newRoot.Array[1] <- TransientVector.newPath(root.Edit,shift,tailNode)
                        newRoot, shift+5
                    else
                        let newRoot = this.pushTail(shift, root, tailNode)
                        newRoot, shift
                root <- newRoot
                shift <- newShift
                cnt <- n+1
                this
```

We can see mutation on the tail array and on the `TransientVector`'s `root`, `shift` and `count`.

Finally, we can move back to a persistent structure easily.  Just create a head node and set your `AtomicBoolean` to a false value.  Then we cannot do any more mutation operations through the root node.  It is safe to use the `root` here.  (We do create a new tails that is no bigger than required.)

```F#
member this.persistent() =
    this.ensureEditable()
    root.Edit.Set(false) 
    let trimmedTail : obj array = Array.zeroCreate (cnt-this.tailoff())
    Array.Copy(tail,trimmedTail,trimmedTail.Length)
    PersistentVector(cnt,shift,root,trimmedTail)
```

Create similar modifictionsfor other operations such as `pop` and `assoc` and you are done.