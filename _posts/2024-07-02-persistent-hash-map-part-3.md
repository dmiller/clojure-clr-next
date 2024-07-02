---
layout: post
title: `PersistentHashMap`, part 2 -- The guts
date: 2024-07-02 00:00:00 -0500
categories: general
---

We take a look at the internal nodes that implement the core algorithms of the `PersistentHashMap` data structure.


## Background

Take a look at [The root]({{site.baseurl}}{% post_url 2024-07-02-persisent-hash-map-part-2 %}) for the context in which these node types occur.

## The `INode` interface

There is a tree structure sitting below the object representing the map.  The nodes in this tree are of three types: `ArrayNode`, `BitmapIndexedNode`, and `HashCollisionNode`.  These nodes implement the `INode` interface, which is defined as follows:

```F#
[<AllowNullLiteral>] 
type INode =

    abstract assoc: shift: int * hash: int * key: obj * value: obj * addedLeaf: BoolBox -> INode
    abstract without: shift: int * hash: int * key: obj -> INode
    abstract find: shift: int * hash: int * key: obj -> IMapEntry
    abstract find: shift: int * hash: int * key: obj * notFound: obj -> obj
    abstract getNodeSeq: unit -> ISeq

    abstract assoc: edit: AtomicBoolean * shift: int * hash: int * key: obj * value: obj * addedLeaf: BoolBox -> INode
    abstract without: edit: AtomicBoolean * shift: int * hash: int * key: obj * removedLeaf: BoolBox -> INode

    abstract kvReduce: fn: IFn * init: obj -> obj
    abstract fold: combinef: IFn * reducef: IFn * fjtask: IFn * fjfork: IFn * fjjoin: IFn -> obj
    abstract iterator: d: KVMangleFn<obj> -> IEnumerator
    abstract iteratorT: d: KVMangleFn<'T> -> IEnumerator<'T>
```

We will cover  `assoc`, `without`, and the second `find`.  (The first `find` is almost identical.)  These are the primary operations defining map behavior.

The overloads of `assoc` and `without` that take an `AtomicBoolean` first argument are used for the `transient` version of the map, which we will cover [later]({{site.baseurl}}{% post_url 2024-07-02-persisent-hash-map-part-4 %}).

   
## The `BitmapIndexedNode` node

The `BitmapIndexedNode` node is what we described in [the first]({{site.baseurl}}{% post_url 2024-07-02-persisent-hash-map-part-1 %}) of this series of posts.  It contains a compressed array of entries -- there are no empty slots.  A bitmap indicates which slots are occupied.  We us the `bitCount` technique to find the actual index for an entry that is present.  An entry can be a key-value pair or another node.  The array itself is double-sized. It contains alternating key/value pairs.  If your key maps to `i`, look at `array[2*i]`.  If that is null, then `array[2*i+1]` will contain a node -- you're going to need to go down to the next level.  If `array[2*i]` is not `null`, then it is an actual key.  and `array[2*i]` & `array[2*i+1]` are a key-value pair.  (This is why we manage `null` key presence in the root object. `null` in a key position indicates no key present.)

Looking at one of the `find` methods may help:

```F#
        member this.find(shift, hash, key, nf) =
            // Determine the index (bit-position) you should be looking at, based on the key and the level of this node.
            let bit = NodeOps.bitPos (hash, shift)

            if (bitmap &&& bit) = 0 then
                // the bitmap says there is no entry for this index, so your key is definitely not here
                nf
            else
                // There is an entry at this index.  Compute the index into the array for the entry.
                let idx = this.index (bit)

                // get the pair at the index
                let keyOrNull = array[2 * idx]
                let valOrNode = array[2 * idx + 1]

                if isNull keyOrNull then
                    // the key is null, so the associated 'value' is actually a node at the next level down.  Recurse.
                    // Note the +5 here -- we are using 2^5 = 32 as the branching factor, so the shift amount is in 5-bit increments.
                    (valOrNode :?> INode).find (shift + 5, hash, key, nf)
                elif Util.equiv (key, keyOrNull) then
                    // The key is not null, it is an actual key.  And it matches the key we are looking for.
                    // Return the value associated with the key.
                    valOrNode
                else
                    // They key is not null, it is not an actual key.  But it is not the key we are looking for.
                    nf
```

The `assoc` and `without` methods will have similar methods of traversal to find where a key should be.  Let's start with `assoc`.  We are given a key/value pair.  We need to find where the key should reside.  Once there, we need to either add the pair if the key is not present or replace the value if the key is present (assuming the new value differs from the current value).

One slight wrinkle is that if we need to add this key, the new `BitmapIndexedNode` that would result might be too full. (More on what that means later.)  In such a case, we switch from using a `BitmapIndexedNode` to an `ArrayNode`, to be discussed in detail in the next section.  For now, just hide your eyes when you walk past the code that builds the `ArrayNode`; it is not pretty. 

```F#
        // shift -- level in tree in in terms of number of bits to shift (multiple of 5)
        // hash - hash code of the key
        // key, value -- duh
        // addedLeaf -- a BoolBox that will be set to true if a leaf node is added
        member this.assoc(shift, hash, key, value, addedLeaf) =       

            // the same recursion pattern as before. Find the bit posiition for the hash code at this level
            // and the array index it maps to

            let bit = NodeOps.bitPos (hash, shift)
            let idx = this.index (bit)

            // let's see if we have an entry
            if bitmap &&& bit = 0 then

                // no entry at this index, meaning no existing value for this key  
                // We are going to be adding a key/value pair (rather than replacing a value).  

                // But first, we need to see if we are too full.
                // the bitCount of the bitmap tells us how many entries we have in this node in total
                let n = NodeOps.bitCount (bitmap)

                // if we are above a threshold size, we switch from a BitmapIndexedNode to an ArrayNode (see below)
                if n >= 16 then
                    // -------------------------------------------------------------

                    // all the code below (through the next line of dashes) is  what it takes 
                    // to transition from a BitmapIndexedNode to an ArrayNode 
                    
                    // This will be the new array of entries for the ArrayNode
                    let nodes: INode[] = Array.zeroCreate 32

                    // we know we don't have any entry for the new key/value pair.
                    // We will create a new node to hold that pair (at the next level down)
                    // We know exactly where to put it in the array.
                    let jdx = NodeOps.mask (hash, shift)

                    nodes[jdx] <-
                        (BitmapIndexedNode.Empty :> INode)
                            .assoc (shift + 5, hash, key, value, addedLeaf)

                    // Now we have to transfer the other entries from this BitmapIndexedNode to the new ArrayNode
                    // It is easiest to iterate across all 32 indexes, check each to see if there is an entry.
                    // i is the entry we are looking it, j is the index in the old array where i is located.
                    // j get incremented after we find an occupied entry.
                    let mutable j = 0

                    for i = 0 to 31 do
                        if ((bitmap >>> i) &&& 1) <> 0 then
                            // there is an entry
                            nodes[i] <-
                                if isNull array[j] then
                                    // the key is null, so the value is a node at the next level down -- transfer the node over.
                                    array[j + 1] :?> INode
                                else
                                    // the key is not null.  An ArrayNode does not contain key/value pairs. 
                                    // so we need a BitmapIndexNodec to hold the key/value pair at this entry.
                                    // We have to rehash the key, BTW.
                                    (BitmapIndexedNode.Empty :> INode)
                                        .assoc (shift + 5, NodeOps.hash (array[j]), array[j], array[j + 1], addedLeaf)

                            j <- j + 2

                    upcast ArrayNode(null, n + 1, nodes)

                    // -------------------------------------------------------------

                else
                    // we are not too full -- we can just add the key/value pair to the BitmapIndexedNode's array 
                    // (of course, we have to create a new array and a new  node)
                    // We know where the new key/value pair should go.  
                    // Because of the array compression, we have to make space in the middle to place them.
                
                    let newArray: obj[] = 2 * (n + 1) |> Array.zeroCreate
                    Array.Copy(array, 0, newArray, 0, 2 * idx)
                    newArray[2 * idx] <- key
                    addedLeaf.set ()
                    newArray[2 * idx + 1] <- value
                    Array.Copy(array, 2 * idx, newArray, 2 * (idx + 1), 2 * (n - idx))
                    // Note that the new node has a new entry, so we have to add to the bitmap to indicate its presence.
                    upcast BitmapIndexedNode(null, (bitmap ||| bit), newArray)
            else
                // Remember we are?
                // There IS an entry at the position of interest.
                // They question is whether we have a key/value pair here or a node at the next level down.
                let keyOrNull = array[2 * idx]
                let valOrNode = array[2 * idx + 1]

                if isNull keyOrNull then
                    // the key is null, so the value is a node at the next level down.  Recurse.
                    // See what comes back up.

                    let existingNode = (valOrNode :?> INode)
                    let n = existingNode.assoc (shift + 5, hash, key, value, addedLeaf)

                    // it is possible we get back what we started with -- the key/value pair is already in the map
                    // so our node does not change, either, and we can return 'this'
                    if LanguagePrimitives.PhysicalEquality n existingNode then
                        upcast this
                    else
                        // we got a new node back -- we have to replace the existing node with the new one 
                        // meaning we have to return a new version of ourself.
                        upcast BitmapIndexedNode(null, bitmap, NodeOps.cloneAndSet (array, 2 * idx + 1, upcast n))
                elif Util.equiv (key, keyOrNull) then
                    // the key is not null, and it matches the key we are looking for.
                    // either the value is the same -- no change --
                    // or we have to replace the value with the new value -- new node!
                    if LanguagePrimitives.PhysicalEquality value valOrNode then
                        upcast this
                    else
                        upcast BitmapIndexedNode(null, bitmap, NodeOps.cloneAndSet (array, 2 * idx + 1, value))
                else
                    // the key we are looking for definitely is not in the tree
                    // we are adding a new key/value pair -- this will send a signal back up to our caller
                    // Why is the only place you see addedLeaf.set()?
                    // Because in other places where we add a new pair, the work is done by call to assoc on a subnode.
                    // That call will set addedLeaf for us!
                    // In fact, this is where that happens ultimately.
                    addedLeaf.set ()

                    // This hides some complexity.  Recall that we are here because there is a key in our desired location,
                    // and it is not our key.  What we don't know is whether the existing key has the same hash as our key or not.
                    // We only know that it the same through the initial segment of bits we are are considering down to this level.
                    // PersistentHashMap.create node figures out what to do -- see code below.
                    upcast
                        BitmapIndexedNode(
                            null,
                            bitmap,
                            NodeOps.cloneAndSet2 (
                                array,
                                2 * idx,
                                null,
                                2 * idx + 1,
                                upcast PersistentHashMap.createNode (shift + 5, keyOrNull, valOrNode, hash, key, value)
                            )
                        )
```

The little detail of `PersistentHashMap.createNode` is that it will create a `BitmapIndexedNode` if the two keys have different hashes, and a `HashCollisionNode` (details below) if they have the same hash.  

```F#
    // key1: the key already in the map.
    // val1: its value
    // key2hash: the hash of the key we are adding
    // key2: the key we are adding
    // val2: its value
    static member internal createNode(shift: int, key1: obj, val1: obj, key2hash: int, key2: obj, val2: obj) : INode =
        // we have to recompute the hash for key1 -- we don't have it here.
        let key1hash = NodeOps.hash (key1)

        if key1hash = key2hash then
            // these two keys have the same hash -- all bits considered -- we have to create a HashCollisionNode
            upcast HashCollisionNode(null, key1hash, 2, [| key1; val1; key2; val2 |])
        else
            // the keys have different hashes -- we can create a BitmapIndexedNode to deal with them.
            // assoc works magic here -- it will figure out where to put the two keys in the new node.
            // we end up creating a node that we throw away -- the result of the first assoc call -- but it is tiny and very ephemeral.
            let box = BoolBox()
            let edit = AtomicBoolean()

            (BitmapIndexedNode.Empty :> INode)
                .assoc(edit, shift, key1hash, key1, val1, box)
                .assoc (edit, shift, key2hash, key2, val2, box)
```

That may be a bit long.  Here's a scorecard for the cases encountered in `assoc`:


| Entry present? |                  |                                   |                                                                         | Map count |
|----------------|------------------|-----------------------------------|-------------------------------------------------------------------------|:---------:|
| No entry       | >= 1/2 full      |                                   | Create an `ArrayNode` copying this node and inserting new K/V pair      | +1        |
| No entry       | < 1/2 full       |                                   | Create a `BitmapIndexedNode` copying this node and inserting a new K/V pair    | +1        |
| Has entry      | Entry is KV      | Key matches, value matches        | No-op | no change |
| Has entry      | Entry is KV      | Key matches, value does not match | Create a `BitmapIndexedNode` copying this node but key's value replaced with new value | no change |
| Has entry      | Entry is KV      | Key does not match                | Create a new node<br>(`HashCollisionNode` if the two keys hash the same, `BitmapIndexedNode` otherwise) <br>and insert where the existing KV is | +1 |
| Has entry      | Entry is SHMNode |                                   | Do the `assoc` on the subnode.<br>If same node comes back, then this is a no-op. <br>Else create a `BitmapIndexedNode` copying this one<br>with the new node replacing the existing one. | no change or +1 |


 Compared to `assoc`, `without` is a bit simpler.


 ```F#
        member this.without(shift, hash, key) =

            let bit = NodeOps.bitPos (hash, shift)

            if (bitmap &&& bit) = 0 then
                // no entry at this index -- the key is not in the map, so this is a no-op
                upcast this
            else
                // there is an entry, let's see if it a node or a key/value pair

                let idx = this.index (bit)
                let keyOrNull = array[2 * idx]
                let valOrNode = array[2 * idx + 1]

                if isNull keyOrNull then
                    // we have a node at this index -- recurse down a level
                    let existingNode = (valOrNode :?> INode)
                    let n = existingNode.without (shift + 5, hash, key)

                    if LanguagePrimitives.PhysicalEquality n existingNode then
                        // we got back what we started with -- the key is not in the map, so this is a no-op
                        upcast this
                    elif not (isNull n) then
                        // we got back a new node -- we have to replace the existing node with the new
                        upcast BitmapIndexedNode(null, bitmap, NodeOps.cloneAndSet (array, 2 * idx + 1, upcast n))
                    elif bitmap = bit then
                        // we got back null AND that subnode is the only entry in this node
                        // we are empty -- return null
                        null
                    else
                        // we got back null, but there are other entries in this node
                        // remove the entry (and mark the bitmap accordingly)
                        upcast BitmapIndexedNode(null, bitmap ^^^ bit, NodeOps.removePair (array, idx))
                elif Util.equiv (key, keyOrNull) then
                    // we had a key/value pair and the key matches the key we are looking for
                    if bitmap = bit then
                        // this key is the only entry.  removing it makes empty.
                        null
                    else
                        // there are other entries -- remove this one
                        upcast BitmapIndexedNode(null, bitmap ^^^ bit, NodeOps.removePair (array, idx))
                else
                    // there is a key here, but it is not the key we are looking for
                    // the key we are looking for is not here, so this is a no-op
                    upcast this
 ```

## The `ArrayNode` node type

The `ArrayNode` type is much simpler.  An `ArrayNode` contains a count and an array of `INode`s.  Thus, it does not contain any key/value pairs in the manner of `BitmapIndexedNode`.   Some entries in the array may be `null`.  The count tells us the number of non-`null` entries.

As you saw above, we switch from a `BitmapIndexedNode` to an `ArrayNode` when the number of entries exceeds a threshold -- that happens during an `assoc` operation, where we increase the number of entries.  We might switch back from an `ArrayNode` to a `BitmapIndexedNode` during a `without` operation, where we decrease the number of entries, if we drop back below the threshold.

The `find` method is straightforward.  If the entry at the index is `null`, then the key is not in the map.  If it is a node, then we continue the search at the next level.  

```F#
        member _.find(shift, hash, key, nf) =
            let idx = NodeOps.mask (hash, shift)
            let node = array[idx]

            match node with
            | null -> nf
            | _ -> node.find (shift + 5, hash, key, nf)
```

`assoc` similarly is much simpler:

```F#
        member this.assoc(shift, hash, key, value, addedLeaf) =

            // determine the index for the key at this level
            let idx = NodeOps.mask (hash, shift)
            let node = array[idx]

            if isNull node then
                // no entry, so the key is not in the map
                // create a new ArrayNode with the new key/value pair inserted in a BitmapIndexedNode at the next level down
                // note that the count is incremented -- we've added a new entry
                upcast
                    ArrayNode(
                        null,
                        count + 1,
                        NodeOps.cloneAndSet (
                            array,
                            idx,
                            (BitmapIndexedNode.Empty :> INode)
                                .assoc (shift + 5, hash, key, value, addedLeaf)
                        )
                    )
            else
                // there is an entry -- let that node do the assoc
                let n = node.assoc (shift + 5, hash, key, value, addedLeaf)

                // if the node coming back is the same as what we started with,
                // then the key is already in the map with the same value
                // this is a no-op
                if LanguagePrimitives.PhysicalEquality n node then
                    upcast this
                else
                    // the node coming back is different -- we have to replace the existing node with the new
                    // note that our count does not change -- we have the same number of entries, just a new one in one position.
                    // addedLeaf is set by the call to assoc on the subnode
                    upcast ArrayNode(null, count, NodeOps.cloneAndSet (array, idx, n))
```

`without` gets a little more complicated because of the possible transition back to a `BitmapIndexNode`.

```F#
        member this.without(shift, hash, key) =        
            let idx = NodeOps.mask (hash, shift)
            let node = array[idx]

            if isNull node then
                // not entry at the index -- the key is not in the map
                // this is a no-op -- return 'this'
                upcast this
            else
                // there is an entry -- let that node do the without
                let n = node.without (shift + 5, hash, key)

                // if we get back the node we started with, then the key is not in the map -- this is a no-op
                if LanguagePrimitives.PhysicalEquality n node then
                    upcast this

                // If we get back null from the subnode without, there are no more entries down that branch
                // We are going to remove a node from the array.
                // The question is: do we shrink back to a BitmapIndexedNode or stay as an ArrayNode?    
                elif isNull n then
       
                    if count <= 8 then 
                        // shrink to BitmapIndexedNode
                        this.pack (null, idx)
                    else
                        // stay as an array node
                        upcast ArrayNode(null, count - 1, NodeOps.cloneAndSet (array, idx, n))
                else
                    // we got back a new node -- we have to replace the existing node with the new
                    upcast ArrayNode(null, count, NodeOps.cloneAndSet (array, idx, n))
```
Similar to the transition from `BitmapIndexedNode` to `ArrayNode`, the `pack` method constructs a new `BitmapIndexedNode` from the entries in the `ArrayNode`, with the one entry at the index removed.  The `pack` method is defined as follows:



```F#
    member _.pack(edit: AtomicBoolean, idx) : INode =
        // we have one fewer entry (count-1), but a BitmapIndexedNode has a double-sized arrry (to hold key/value pairs) (2*(count-1))

        let newArray: obj[] = Array.zeroCreate <| 2 * (count - 1)
        let mutable j = 1
        let mutable bitmap = 0

        // move the non-null entries before the one we are removing
        // record them in the bitmap.
        for i = 0 to idx - 1 do
            if not (isNull array[i]) then
                newArray[j] <- upcast array[i]
                bitmap <- bitmap ||| (1 <<< i)
                j <- j + 2

        // move the non-null entries after the one we are removing
        // record them in the bitmap.
        for i = idx + 1 to array.Length - 1 do
            if not (isNull array[i]) then
                newArray[j] <- upcast array[i]
                bitmap <- bitmap ||| (1 <<< i)
                j <- j + 2

        // we now have what we need to create a new BitmapIndexedNode
        upcast BitmapIndexedNode(edit, bitmap, newArray)
```

## The `HashCollisionNode` node type

A `HashCollisionNode` appears when two or more keys with the same hash code.  This means equal as integers -- no shift/masking.  THis is the definition of _collision_ in hashing.
No matter how far down you might go in the tree, these two keys are going to be togther.  The 'HashCollisionNode` holds key/value pairs for a set of keys with identical hash codes.  If you have a decent hashing function and decent data, you may not see any of these.  (For testing, I create a class to use for keys that has a small number of possible hash codes, guaranteeing lots of collisions.)

A `HashCollisionNode` contains a count and an array of key-value pairs.  We do linear searching in the array to find the position of a key:

```F#
    member _.tryFindIndex(key: obj) : int option =
        let rec loop (i: int) =
            if i >= 2 * count then None
            elif Util.equiv (key, array[i]) then Some i
            else i + 2 |> loop

        loop 0
```

With this method, `find` is easy:

```F#
        member this.find(shift, h, key, nf) =
            match this.tryFindIndex (key) with
            | None -> nf
            | Some idx -> array[idx + 1]
```

`without` is also easy

```F#
        member this.without(shift, h, key) =
            match this.tryFindIndex (key) with
            | None -> upcast this   // the key is not present, so no change
            | Some idx ->
                // the key is present -- we have to remove it
                // If it is the only entry, then we are empty and return null
                if count = 1 then
                    null
                else
                    // we have to create a new HashCollisionNode with the key/value pair removed
                    upcast HashCollisionNode(null, h, count - 1, NodeOps.removePair (array, idx / 2))
```

`assoc` is a litte more complicated.  We are trying to insert a key/value pair and we've gotten down to `HashCollisionNode`.
This means only that the hash value of the new key matches the hash code of the keys in this `HashCollisionNode` through the initial segment of bits we are considering down to this level.  We have to check the hash code of the new key against the hash code in the `HashCollisionNode`.  If there is a match, then we have a real collision and we can insert into the `HashCollisionNode`.  Otherwise, this key does not belong here.  We create a `BitmapIndexedNode` to contain the `HashColliionNode` and the key-value pair -- in other words, we push the existing `HashCollisionNode` down a level in the tree.

```F#
        member this.assoc(shift, h, key, value, addedLeaf) =
            if h = hash then
                // the hash of the new key is a match -- we have a collision.
                match this.tryFindIndex (key) with
                | Some idx ->
                    // in fact, the new key is already here.  If the value is the same, then this is a no-op. 
                    // Otherwise, we have to replace the value.
                    if LanguagePrimitives.PhysicalEquality array[idx + 1] value then
                        upcast this
                    else
                        upcast HashCollisionNode(null, h, count, NodeOps.cloneAndSet (array, idx + 1, value))
                | None ->
                    // the new key is a collision, but no present already.
                    // we have to create a new HashCollisionNode with the new key/value pair added at the end.
                    let newArray: obj[] = 2 * (count + 1) |> Array.zeroCreate
                    Array.Copy(array, 0, newArray, 0, 2 * count)
                    newArray[2 * count] <- key
                    newArray[2 * count + 1] <- value
                    addedLeaf.set ()
                    upcast HashCollisionNode(edit, h, count + 1, newArray)
            else
                // there is no collision -- the new key does not belong here.
                // we create a new BitmapIndexedNode to hold the HashCollisionNode, 
                // then assoc the new key/value pair into it.
                (BitmapIndexedNode(null, NodeOps.bitPos (hash, shift), [| null; this |]) :> INode)
                    .assoc (shift, h, key, value, addedLeaf)
```

Take all the comments out and it's not _all_ that much code.

=========================================================




## Not bitter at all

After writing all this, I decided to benchmark my two versions of the code: (a) the translation of the C# code; and (b) the 'proper' F# code explained above.

In translating the C# version--itself a very direct translation of the Java code--I did try to write fairly idiomatic F# code.  Minor differences are things like the three node types being classes that implement a common interface.  The major differences lie in the internal structure of those nodes.  For example, in the C#-translation, the `BitmapNode`'s array does not contain a discriminated union of consisting of key-value pairs vs nodes.  Rather, it contains an array with 2*n elements.  When a hash segment translates to `i`, one checks `entries[2*i]`.  If it is `null`, then `entries[2*i+1]`
 will be the node to recurse to.   If it is not `null`', then it is a key and `entries[2*i+1]` is its paired value.  One then checks to see if is the key of interest and acts accordingly.

 Note that this method ascribes a special meaning to a `null` where a key might be.  Thus `null`s cannot be keys in the nodes themselves.  This is handled by special casing `null' key presence in the top-level code.

 I wondered what penalty the proper code would incur for the F# fanciness of discriminated unions, option types, etc.  So I wrote a benchmark using Benchmark.Net. (Yay!)  I chose to test `assoc`, figuring it would stand in also for `without` and `find`.  I tested by inserting 1000 and 10000 random integer values.

 The results were not happy.  `MakeSHMByAssoc` tests the code above; `MakePHMByAssoc` tests the C# translation;  `MakePHMByTransient` tests the use of transients in the translated C# code.  (Ignore the last one for now.)  Read and weep:


|             Method | count |           Mean |  Ratio |  Allocated |
|------------------- |------ |---------------:|-------:|-----------:|
|     MakeSHMByAssoc |  1000 |    45,228.8 us |  80.33 |  769.03 KB |
|     MakePHMByAssoc |  1000 |       570.7 us |   1.00 |  803.81 KB |
| MakePHMByTransient |  1000 |       399.6 us |   0.70 |  251.22 KB |
|                    |       |                |        |            |
|     MakeSHMByAssoc | 10000 | 3,320,067.3 us | 415.32 | 9129.06 KB |
|     MakePHMByAssoc | 10000 |     7,879.8 us |   1.00 | 9388.42 KB |
| MakePHMByTransient | 10000 |     3,765.6 us |   0.47 | 1644.79 KB |

I expected a hit.  I did not expect ratios like 80X and 415X in performance.  
I was also perplexed by the fact that it was allocating less memory.  
Surely a bunch of `isint` IL instructions versus `null` checks would not be so bad.

I went to bed.  

And when I woke up (not with a sudden insight, but with ... never mind).  
In my several moments of wakefulness, the answer came to me.  
In moving from the class-defined nodes to the discriminated union, the semantics of equality changed from reference to structural.  
Reference semantics is just doing a pointer check, which is appropriate here as we want to know if we got back exactly the same node as we had before, in code like this:

```F#
                let newNode =
                    node.assoc (shift + 5) hash key value addedLeaf

                if newNode = node then
                    this
                else
                    ArrayNode(count, cloneAndSet (nodes, idx, Some newNode))

```
With reference semantics, that little `=` is a nice call to `Object.ReferenceEquals` which becomes some really trivial IL/assembler.  
With structural semantics, that little `=` turns into a ferocious CPU cycle eater that traverse both data structures, including the arrays.   
The fix?  Just one little annotation:

```F#
[<ReferenceEquality>] SHMNode = ...
```

And we have tamed the beast:


|             Method | count |       Mean | Ratio |  Allocated |
|------------------- |------ |-----------:|------:|-----------:|
|     MakeSHMByAssoc |  1000 |   443.0 us |  0.78 |  773.03 KB |
|     MakePHMByAssoc |  1000 |   563.2 us |  1.00 |   805.4 KB |
| MakePHMByTransient |  1000 |   440.9 us |  0.78 |  252.03 KB |
|                    |       |            |       |            |
|     MakeSHMByAssoc | 10000 | 8,002.8 us |  0.99 | 9125.06 KB |
|     MakePHMByAssoc | 10000 | 8,106.0 us |  1.00 | 9387.96 KB |
| MakePHMByTransient | 10000 | 3,893.0 us |  0.48 | 1644.87 KB |

I am amazed that the 'proper' code beats the 'optimized' code.  I'm not sure how the F# compiler and runtime does it.

## Coda: Transients

The idea behind transient collections in Clojure is to allow efficient mass editing of a data structure such as a map efficiently using mutability, while protecting other threads from seeing those edits in an incomplete state.  To do so, access to the data structure is restrcited to a single thread.  Transitory mutability that cannot be seen still counts as immutability.  

Transients work best when doing a large number of operations all at once.  It requires a special transient type at the root.  It uses the same node types but requires those node types to implement special versions of key methods such as `assoc` and `without`.  For example, when inserting a key/value pair in a `BitmapNode`, rather than creating a new `BitmapNode` node, it can just modify the array of the existing node.

Adding this to the code above is non-trivial.  The arrays in the nodes are mutable, but one may have to change the array (if we need to increase or decrease the array size, e.g.), and things like the `bitmap` in a `BitmapNode` or the `count` in an `ArrayNode` will have to changed.  This requires mutable fields, something discriminated unions don't allow directly.   The obvious solutions are to use `ref` types in those fields or to move from the discriminated union for the node types back to classes/interface. 

I didn't think I'd have to contemplate this:  Given the closeness in performance, do I stick with the convoluted translation (Java -> C# -> F#) or go with the code above?  I think I have to deal with transients and the other cruft needed to finish the persistent hash-array mapped trie in the Clojure context in order to make that call.  

Stay tuned.


