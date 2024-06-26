---
layout: post
title: `PersistentHashMap`, part 2 -- The guts
date: 2024-06-28 00:00:00 -0500
categories: general
---

We take a look at the internal nodes that implement the core algorithms of the `PersistentHashMap` data structure.


## Background

Take a look at [The root]({{site.baseurl}}{% post_url 2024-06-28-persisent-hash-map-part-2 %}) for what lies above what we deal with here.

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

We will cover the first four methods: `assoc`, `without`,  the first two `find` operations.

 `getNodeSeq` is used to get a sequence to iterate through the entries at the node in question and below.  This is straightforward; I'm not going bother with it here. Similarly for the `kvReduce`, `fold` and other operations.   The overloads of `assoc` and `without` that take an `AtomicBoolean` first argument are used for the `transient` version of the map, which we will cover [later]({{site.baseurl}}{% post_url 2024-06-28-persisent-hash-map-part-4 %}).

   
## The `BitmapIndexedNode` node

The `BitmapIndexedNode` node is what we described in the first of this series of post.  It contains a compressed array of entries -- there are no empty slots.  A bitmap indicates which slots are occupied.  We us the `bitCount` technique to find the actual index for an entry that is present.  An entry can be a key-value pair or another node.  The array itself is double-sized. It contains alternating key/value pairs.  If your key maps to `i`, look at `array[2*i]`.  If that is null, then `array[2*i+1]` will contain a node -- you're going to need to go down to the next level.  if `array[2*i]` is not `null`, then it is an actualy key. (This is why we can't put null keys down in the guts and have to hold them up at the main node.)  It may or may not be the key you ar looking for.  You have to check.  But no matter: if `array[2*i]` is not `null`, then `array[2*i]` & `array[2*i+1]` are a key-value pair.

Looking at one of the `find` methods may help:

```F#
        member this.find(shift, hash, key, nf) =
            // Determin the index (bit-position) you should be looking at, based on the key and the level of this node.
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
                    // They key i snot null, it is not an actual key.  But it is not the key we are looking for.
                    nf
```

The `assoc` and `without` methods will have similar methods of traversal.  The question is what you do when get to bottom.
Let's start with `assoc`.  It's going to make `without` look trivial (unfortunately).

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
                    // all the code below is just what it takes to transition from a BitmapIndexedNode to an ArrayNode.
                    
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

I'll leave `without` as an exercise.  Compared to `assoc`, it is a walk in the park.

## The `ArrayNode` node type



## The `HashCollisionNode` node type



null, then `array[2*i+1]` will contain the value associated with the key.  The `HashCollisionNode` is used when two or more keys have the same hash code.  It contains an array of key-value pairs.  The `ArrayNode` is used when the number of entries is small.  It contains an array of entries, some of which may be empty.  The number of entries is kept in a count field.




  The `HashCollisionNode` is used when two or more keys have the same hash code.  It contains an array of key-value pairs.  The `ArrayNode` is used when the number of entries is small.  It contains an array of entries, some of which may be empty.  The number of entries is kept in a count field.

(1) A `BitmapNode` is the kind of node described earlier.  It has an array of entries, each entry being either a key-value pair or another node.  There are no empty slots.  The node has a bitmap to help map the index computed from the hash to an index in the array.   (2) An `ArrayNode` has an array that contains nodes for the next level down in the tree.  Some of the entries may be blank.  (We use an option type for the array entries in order to distinguish occupied vs. not-occupied.)  It also holds a count of the occupied cells.  (3) A `CollisionNode' contains an array of key-value pairs.  A collision node will appear when we have two or more keys with the same hash code.  If one reaches a collision node in the course of searching for a key, one will be forced to do a linear search of the entries to see if the key appears.


















 ## The `assoc` method

    The `assoc` method is used to add a key-value pair to the map.  The first argument, `shift` indicates the level in the tree.  It refers to how much we have to shift the hash before masking off to get our index bits.  We will need to increase this every time we descend a level in the tree.  We also pass the hash code for the key, the key itself, the value to be added, and a `BoolBox` that is used to indicate whether a leaf node was added.  The `assoc` method returns a (possibly new) node that is the result of adding the key-value pair to the map.


   -------------------------------------------------------

   
   

We can capture these nodes types with a discriminated union and one helper type:

```F#
type BNodeEntry =
    | KeyValue of Key: obj * Value: obj
    | Node of Node: SHMNode

and SHMNode =
    | ArrayNode of Count: int * Nodes: (SHMNode option) []
    | BitmapNode of Bitmap: int * Entries: BNodeEntry []
    | CollisionNode of Hash: int * Count: int * KVs: MapEntry []

    ...
```

## Finding our way

Perhaps the easiest way to see how the pieces fit together is to look at the search operation.  Recall how this is coded at the root:

```F#
    interface ILookup with
        member this.valAt(k) = (this :> ILookup).valAt (k, null)

        member this.valAt(k, nf) =
            match this with
            | Empty -> nf
            | Rooted (Node = n) -> n.find2 0 (hash k) k nf
```

In `SHMNode` 

```F#
    member this.find2 (shift:int) (hash:int) (key:obj) (notFound:obj) : obj = 
        match this with  ...    
```

The `shift` argument will be a multiple of 5, the multiplier being the level in the tree.
We return the value associated with `key` if it is in the map; otherwise we return`notFound`.

At an `ArrayNode`, see if there is an entry in the index indicated by hash (for this level).  If that slot is empty, the key is not present and we can return 'notFound'.  If there is a entry, it is a node in the next level down and we continue the search there.

```F#
        | ArrayNode (Count = count; Nodes = nodes) ->
            let idx = mask (hash, shift)

            match nodes.[idx] with
            | None -> notFound
            | Some node -> node.find2 (shift + 5) hash key notFound
```

At a `BitmapNode`, we again check to see if there is an entry -- this uses the bitmap technique described earlier.  If no entry, we are done.  If there is an entry, it can be a key-value pair or another node.  if it is a key-value pair, we need to comparre the search key with the key in the key-value pair.   Match or no-match indicates our return value.   If we find a node, we continue our search down a level.

```F#
        | BitmapNode (Bitmap = bitmap; Entries = entries) ->
            match hashToIndex hash shift bitmap with
            | None -> notFound
            | Some idx ->
                match entries.[idx] with
                | KeyValue (Key = k; Value = v) -> if equiv (key, k) then v else notFound
                | Node (Node = node) -> node.find2 (shift + 5) hash key notFound
```

The function `hashToiIndex` codes the mapping from the hash index to the array index.  It returns an `int option` so we can tell if there is an entry or not.

```F#
    let hashToIndex (hash: int) (shift: int) (bitmap: int) : int option =
        let bit = bitPos (hash, shift)

        if bit &&& bitmap = 0 then
            None
        else
            bitIndex (bitmap, bit) |> Some
```

At a `CollisioNode`, we just conduct a linear search.

```F#
        | CollisionNode (Hash = hash; Count = count; KVs = kvs) ->
            match SHMNode.tryFindCNodeIndex (key, kvs) with
            | None -> notFound
            | Some idx -> (kvs.[idx] :> IMapEntry).value ()
```

where the linear search is coded by `tryFindCNodeIndex`:

```F#
    static member tryFindCNodeIndex(key: obj, kvs: MapEntry []) =
        kvs
        |> Array.tryFindIndex (fun kv -> equiv ((kv :> IMapEntry).key (), key))
```

The function `equiv` should encode the key equality comparison of your choice.

The method `find` is almost identical.  Instead of returning a default value if the key is not found, it returns an option with `None` indicating not-found.

## Insertion

In `SHMNode`, we have

```F#
    member this.assoc (shift:int) (hash:int) (key:obj) (value:obj) (addedLeaf:Box) : SHMNode = 
               match this with  ...
```

For an `ArrayNode`, we index into its array of items.  If nothing is there, then the key is not present, so we will insert it.  Being immutable, 'insert' means create a duplicate of this node with a new entry in the appropriate position in the array.  What kind of enty?  We create an empty `BitmapNode` and `assoc` our key/value into it.  If there is an entry, then it is an `SHMNode` one level down.  Defer the `assoc` to that level.  If we get back the same node we started with, then the `assoc` does nothing -- the key is already in the map with the given value -- and we can return our own node to indicate no change.   If we get back a different node, then we need to 'replace' the node that was there -- again, due to immutability, we will be making a copy.

```F#
        | ArrayNode (Count = count; Nodes = nodes) ->
            let idx = mask (hash, shift)

            match nodes.[idx] with
            | None ->
                let newNode =
                    SHMNode.EmptyBitmapNode.assoc (shift + 5) hash key value addedLeaf

                ArrayNode(count + 1, cloneAndSet (nodes, idx, Some newNode))
            | Some node ->
                let newNode =
                    node.assoc (shift + 5) hash key value addedLeaf

                if newNode = node then
                    this
                else
                    ArrayNode(count, cloneAndSet (nodes, idx, Some newNode))
```

The `cloneAndSet` method creates a copy of this node's entries with a new value in the indicated position:

```F#
    let cloneAndSet (arr: 'T [], i: int, a: 'T) : 'T [] =
        let clone: 'T [] = downcast arr.Clone()
        clone.[i] <- a
        clone
```

Doing `assoc` on a `BitmapNode` is a bit more complex, in fact the most complicated code in the implementation.  A table to ouline the logic:

| Entry present? |                  |                                   |                                                                         | Map count | Code snippet |
|----------------|------------------|-----------------------------------|-------------------------------------------------------------------------|:---------:|:------------:|
| No entry       | >= 1/2 full      |                                   | Create an `ArrayNode` copying this node and inserting new K/V pair      | +1        | A            |
| No entry       | < 1/2 full       |                                   | Create a `BitmapNode` copying this node and inserting a new K/V pair    | +1        | B            |
| Has entry      | Entry is KV      | Key matches, value matches        | No-op | no change | (inline) |
| Has entry      | Entry is KV      | Key matches, value does not match | Create a `BitmapNode` copying this node but key's value replaced with new value | no change | (inline) |
| Has entry      | Entry is KV      | Key does not match                | Create a new node<br>(`CollisionNode` if the two keys hash the same, `BitmapNode` otherwise) <br>and insert where the existing KV is | +1 | (inline) |
| Has entry      | Entry is SHMNode |                                   | Do the `assoc` on the subnode.<br>If same node comes back, then this is a no-op. <br>Else create a `BitmapNode` copying this one<br>with the new node replacing the existing one. | no change or +1 | (inline)

I present the code without additional commentary.   You really need to work through the mechanics.

```F#
        | BitmapNode (Bitmap = bitmap; Entries = entries) ->
            match hashToIndex hash shift bitmap with
            | None ->
                let n = bitCount (bitmap)

                if n >= 16 then
                    //[see code segment A below]
                else
                    //[see code segment B below]

           | Some idx ->
                let entry = entries.[idx]

                match entry with
                | KeyValue (Key = k; Value = v) ->
                    if equiv (key, k) then
                        if value = v then
                            this
                        else
                            BitmapNode(bitmap, cloneAndSet (entries, idx, KeyValue(key, value)))
                    else
                        addedLeaf.set ()

                        let newNode =
                            SHMNode.createNode (shift + 5) k v hash key value

                        BitmapNode(bitmap, cloneAndSet (entries, idx, Node(newNode)))
                | Node (Node = node) ->
                    let newNode =
                        node.assoc (shift + 5) hash key value addedLeaf

                    if newNode = node then
                        this
                    else
                        BitmapNode(bitmap, cloneAndSet (entries, idx, Node(newNode)))
```

```F#
    // Code segment A -- no entry for this key's hash here, but node is too full -- create ArrayNode
                    let nodes: SHMNode option [] = Array.zeroCreate 32

                    // create an entry for the new keya/value
                    let jdx = mask (hash, shift)

                    nodes.[jdx] <-
                        SHMNode.EmptyBitmapNode.assoc (shift + 5) hash key value addedLeaf
                        |> Some

                    // copy the entries from the exsiting BitmapNode to the new ArrayNode we are creating
                    let mutable j = 0

                    for i = 0 to 31 do
                        if ((bitmap >>> i) &&& 1) <> 0 then
                            nodes.[i] <-
                                match entries.[j] with
                                | KeyValue (Key = k; Value = v) ->
                                    SHMNode.EmptyBitmapNode.assoc (shift + 5) (getHash k) k v addedLeaf
                                    |> Some
                                | Node (Node = node) -> node |> Some

                            j <- j + 1

                    ArrayNode(n + 1, nodes)
```

```F#
    // Code segment B -- no entry for this key's hash here, the node is not too full -- create a BitmapNode with the new key/value added
                    let bit = bitPos (hash, shift)
                    let idx = bitIndex (bitmap, bit)
                    let newArray: BNodeEntry [] = Array.zeroCreate (n + 1)
                    Array.Copy(entries, 0, newArray, 0, idx)
                    newArray.[idx] <- KeyValue(key, value)
                    Array.Copy(entries, idx, newArray, idx + 1, n - idx)
                    addedLeaf.set ()
                    BitmapNode((bitmap ||| bit), newArray)
```

If we have reached a collision node, then there are keys with the same hash as our target, through the number of bits considered at this level.  Our target may have the same hash as those keys, in which case it should be added to the list.  (Rather, a new `CollisionNode` will be created with our key/value pair added.)  If the hash of our target key is different, then it is not colliding.  We can replace the `CollisionNode` with a `BitmapNode`, with the `CollisionNode` as its one entry, and then `assoc` our key/value into the new node.

```F#
        | CollisionNode (Hash = h; Count = count; KVs = kvs) ->
            if hash = h then
                match SHMNode.tryFindCNodeIndex (key, kvs) with
                | Some idx ->
                    let kv = kvs.[idx] :> IMapEntry

                    if kv.value () = value then
                        this
                    else
                        CollisionNode(hash, count, cloneAndSet (kvs, idx, MapEntry(key, value)))
                | None ->
                    let newArray: MapEntry [] = count + 1 |> Array.zeroCreate
                    Array.Copy(kvs, 0, newArray, 0, count)
                    newArray.[count] <- MapEntry(key, value)
                    addedLeaf.set ()
                    CollisionNode(hash, count + 1, newArray)
            else
                BitmapNode(
                    bitPos (hash, shift),
                    [| Node(this) |]
                )
                    .assoc
                    shift
                    h
                    key
                    value
                    addedLeaf
```

## Doing `without`

The `without` operation is somewhat easier.  

```F#
    member this.without (shift:int) (hash:int) (key:obj) : SHMNode option =     // Probably needs to return an option
        match this with
            ...
```

The return value is an `SHMNode option`  Across all three subtypes of `SHMNode`, a `None` return indicates that the `without` operation eliminated the branch represented by the node in question.  If `node.without(...)` returns `node` itself, the operation is a no-op; the key being removed is not in the tree.  If a different node comes back, then the key was present in the subtree and the returned node represents the pruned subtree.  In this case, the count for the map needs to decrease.


```F#
        | ArrayNode(Count=count; Nodes=nodes) -> 
            let idx = mask(hash,shift)
            match nodes.[idx] with
            | None -> this |> Some                                                       // key not present => no-op
            | Some node -> 
                match node.without (shift+5) hash key with
                | None ->                                                                // this branch got deleted
                    if count <= 8 then 
                        SHMNode.pack count nodes idx 
                        |> Some                             // we are small, convert back to using a BitmapNode
                    else 
                        ArrayNode(count-1,cloneAndSet(nodes,idx,None)) 
                        |> Some           // zero out the entry
                | Some newNode ->
                    if newNode = node then 
                        this  |> Some                                                    // key not present in subtree => no=op
                    else
                        ArrayNode(count-1,cloneAndSet(nodes,idx,Some newNode)) 
                        |> Some   // use the new node
```


```F#
        | BitmapNode(Bitmap=bitmap; Entries=entries) -> 
            match hashToIndex hash shift bitmap with
            | None -> this |> Some                                                      // key not present => no-op
            | Some idx ->
                let entry = entries.[idx]
                match entry with
                | KeyValue(Key=k; Value=v) -> 
                    if equiv(k,key) then
                        let bit = bitPos(hash,shift)                                     // key/value entry is for the target key
                        if bitmap = bit then                                             // only one entry, which is the one we are removing
                            None
                        else
                            BitmapNode(bitmap^^^bit,removeEntry(entries,idx)) 
                            |> Some    // create new node with the k/v entry removed
                    else this |> Some                                                    // key here not our target => no-op
                | Node(Node=node) -> 
                    match node.without (shift+5) hash key with
                    | None -> this |> Some                                               // key was only entry in the subtree, 
                    | Some n ->
                        if n = node then 
                            this |> Some                                                 // key was not in subtree => no-op
                        else 
                            BitmapNode(bitmap,cloneAndSet(entries,idx,Node(n))) 
                            |> Some  // key was removed from subtree, create node with new subtree
```

```F#
        | CollisionNode(Hash=h; Count=count; KVs=kvs) -> 
            match SHMNode.tryFindCNodeIndex(key,kvs) with   
            | None -> this |> Some                                                      // key not present => no-op
            | Some idx ->
                if count = 1 then                                                       
                    None                                                                // key present, only entry, node deleted
                else 
                    CollisionNode(h,count-1,removeEntry(kvs,idx)) 
                    |> Some               // key not present, create new node with entry removed
```

A few small debts were incurred above.  When an `ArrayNode` gets an entry removed (because a subtree is emptied by the operation), we have the opportunity to create a more efficient `BitmapNode`.  The size break here is 8.  The `pack` method is used to create a `BitmapNode` from an `ArrayNode`, with a specified entry to be left out.

```F#
    static member pack (count: int) (nodes: SHMNode option []) (idx: int) : SHMNode =
        let newArray: BNodeEntry [] = count - 1 |> Array.zeroCreate
        let mutable j = 0
        let mutable bitmap = 0

        for i = 0 to idx - 1 do
            match nodes.[i] with
            | None -> ()
            | Some n ->
                newArray.[j] <- Node n
                bitmap <- bitmap ||| 1 <<< i
                j <- j + 1

        for i = idx + 1 to nodes.Length - 1 do
            match nodes.[i] with
            | None -> ()
            | Some n ->
                newArray.[j] <- Node n
                bitmap <- bitmap ||| 1 <<< i
                j <- j + 1

        BitmapNode(bitmap, newArray)
```

We could probably do this with some fancy work with sequence functions, but I was feeling tired.

And we need to create a new array from an existing one, removing one item.

```F#
    let removeEntry (arr: 'T [], i: int) : 'T [] =
        let newArr: 'T [] = Array.zeroCreate <| arr.Length - 1
        Array.Copy(arr, 0, newArr, 0, i)
        Array.Copy(arr, (i + 1), newArr, i, newArr.Length - i)
        newArr
```

And we are done.

Well ..., except we have handled iterating across a map.

## Doing things in sequence

The root of the map defers creating an `ISeq` to the `SHMNode` at the root, via a call to `root.getNodeSeq()`.   We will write a different sequence type for each type of `SHMNode`.

```F#
    member this.getNodeSeq() = 
        match this with
        | ArrayNode(Count=count; Nodes=nodes) -> ArrayNodeSeq.create(nodes,0) 
        | BitmapNode(Bitmap=bitmap; Entries=entries) -> BitmapNodeSeq.create(entries,0)
        | CollisionNode(Hash=hash; Count=count; KVs=kvs) -> CollisionNodeSeq.create(kvs,0)
```

There is a variant of the sequence datatype for each of the node subtypes.  They are quite similar.  Each has an array to iterate through and an index indicating the index we have progressed to.  If the entry at the index is a key-value pair (as it can be for a CollisionNode or a BitmapNode), then calling `first` will return a `MapEntry` for that key/value pair.  If the entry at that index is a node (as it can be for an ArrayNode or a BitmapNode), then we need to run through the sequence of elements below that node.  So we will also need to keep track of the current subsequence.   When calling `next`, we need to advance to the next value, which means either a key-value entry or a node entry with a non-`null` sequence.

The code is simplest for `CollisionNode`s as they have only key-value pairs and no empty entries.  Recall that when we have no more values, `next` should return `null`.

```F#
CollisionNodeSeq(kvs: MapEntry [], idx: int) =
    inherit ASeq()

    static member create(kvs: MapEntry [], idx: int) : ISeq =
        if idx >= kvs.Length then
            null
        else
            CollisionNodeSeq(kvs, idx)

    interface ISeq with
        member _.first() = kvs.[idx]
        member _.next() = CollisionNodeSeq.create (kvs, idx + 1)
```


`ArrayNode`s are slightly complicated because they have `option` entries.  Any `None` entries must be skipped over when advancing to the `next` sequence.  We need also skip over any subnode that has a `null` sequence.

```F#
ArrayNodeSeq(nodes: (SHMNode option) [], idx: int, s: ISeq) =
    inherit ASeq()


    static member create(nodes: (SHMNode option) [], idx: int) : ISeq =
        if idx >= nodes.Length then
            null
        else
            match nodes.[idx] with
            | Some (node) ->
                match node.getNodeSeq () with
                | null -> ArrayNodeSeq.create (nodes, idx + 1)
                | s -> ArrayNodeSeq(nodes, idx, s)
            | None -> ArrayNodeSeq.create (nodes, idx + 1)

    interface ISeq with
        member _.first() = s.first ()

        member _.next() =
            match s.next () with
            | null -> ArrayNodeSeq.create (nodes, idx + 1)
            | s1 -> ArrayNodeSeq(nodes, idx, s1)
```

`BitmapNode`s need to deal with having both key-value pairs and nodes as entries.  However, they don't have to deal with `option`al entries.

```F#
BitmapNodeSeq(entries: BNodeEntry [], idx: int, seq: ISeq) =
    inherit ASeq()

    static member create(entries: BNodeEntry [], idx: int) : ISeq =
        if idx >= entries.Length then
            null
        else
            match entries.[idx] with
            | KeyValue (_, _) -> BitmapNodeSeq(entries, idx, null)
            | Node (Node = node) ->
                match node.getNodeSeq () with
                | null -> BitmapNodeSeq.create (entries, idx + 1)
                | s -> BitmapNodeSeq(entries, idx, s)

    interface ISeq with
        member _.first() =
            match entries.[idx] with
            | KeyValue (Key = k; Value = v) -> MapEntry(k, v)
            | Node (Node = _) -> seq.first ()

        member _.next() =
            match entries.[idx] with
            | KeyValue (_, _) -> BitmapNodeSeq.create (entries, idx + 1)
            | Node (_) ->
                match seq.next () with
                | null -> BitmapNodeSeq.create (entries, idx + 1)
                | s -> BitmapNodeSeq(entries, idx, s)
```

## Sigh

And now we really are done.

Except for the bitter recriminations.

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


