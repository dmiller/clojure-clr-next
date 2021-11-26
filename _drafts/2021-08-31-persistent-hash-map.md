---
layout: post
title: Making a hash of things
date: 2021-08-31 19:36:00 -0500
categories: general
---

# Making a hash of things

The most complex data structure in the Clojure catalog has to be the PersistentHashMap. This is Phil Bagwell's [Hash Array Mapped Trie (HAMT)](https://en.wikipedia.org/wiki/Hash_array_mapped_trie) as modified by Rich Hickey to be immutable and persistent.  (Bagwell's original paper is available [here](http://infoscience.epfl.ch/record/64398/files/idealhashtrees.pdf.)  I want to describe the ideas behind this data structure and code a simple implementation, leaving out complexities that come from various efficiency hacks.


 ## Hashing

 There are a variety of data structures that use hashing to provide O(1) or O(ln n) access to data.  A simple flavor is the _hash table_.  
 Assume we are mapping keys to values. 
 A hash code is computed from the key which in turn is used to compute an index into an array where the corresponding value will be found.  
 Because generally hash codes or indexes are not unique, two keys can _collide_, i.e., are mapped to the same location.  
 Some technique must be used to deal with collisions.   
 The theory on hash tables of this type is extensive; you can get started [here](https://en.wikipedia.org/wiki/Hash_table).


Another approach uses trees instead of arrays.  As an example, we could store key/value pairs in a binary tree.  Treating the hash code as a sequence of bits and mapping 1 to Left and 0 to Right, the hash code of an item describes a path through the tree.  One does not need to use all the bits, just enough to distinguish a given key from all the others.  Assuming 5-bit hashcodes, this picture illustrates how a given set of four keys would be distributed.

![Binary search tree](/assets/images/BinaryTree-1.png)

Again, one must deal with collisions.  

## Hash Array Mapped Trees

Binary branching is not efficient.  One can end up with trees that are quite deep and that are fragmented in memory.  Depth correlates with the number of memory accesses.  Fragmentation makes  accesses are expensive.   A _hash array mapped trie/tree_ (HAMT) combines the array and tree notions.   Rather than a node branching in two directions and the path to choose based on a single bit,  in an HAMT the branching factor in a node is greater -- usually a small power of two -- and the branch to choose is based on several contiguous bits in the hash.  Which bits depend on the branching factor (power of two) and the level in the tree.  Using four as the branching factor, we might see a configuration such as the following:

![HAMT example](/assets/images/HAMT-1.png)

As the branching factor and hence the size of the arrays in the nodes increases, there can be a significant amount of space wasted by empty array entries.  A solution is allocating array storage only for the occupied cells. In this scheme, given a hash code and thus the index for this level, we need to know if that index is present and we must map this index to an index in the compressed array.  This is done by the node having a bitmap  where one bits indicate occupied indexes.  There is a neat trick to map an index to the array cell involving calculating the number of one bits (sometimes known as the _population count_) in a masked section of the bitmap.  (See below.)

Here's a rough sketch.   Let's assume a branching factor of 32 (a common choice).  Five bits can be used to provide an index in the range `[0,31]`.  We begin with the rightmost five bits to compute the index to check.  At the next level, we take the second five bits, etc.  Say we are two levels down from the root, and supposed the hash for our key is 0xDD707.  (I'll ignore the zeros on the most significant end.)  Then we need to extract the third set of five bits.  From this picture

![HAMT example](/assets/images/HAMT-2.png)

we compute an index of 21.   If we were not working with compacted array storage, we would just look at `entries[21]` for the node in question and see if it represented a key/value pair, a link to another node one level down, or was empty.   However, if we are using compacted array storage, we must figure out where index 21 is mapped to.  Of course, it might be that index 21 is empty and hence not in the array.  We check that by seeing if bit 21 is set in the node's bitmap.  If not, then the key is not present.

Suppose the node in question has bitmap 0xD36FCB4.  It is set in bits 2, 4, 5, 7, 10, 11, 12, 13, 14, 15, 17, 18, 20, 21, 24, 27.  So index 21 is indeed occupied in this node's array.  
But mapped to what index in the node's array? Well, you can count how many bits prior to 21 are set.  That is 13 in this case.  So to find intended index 21 we look in index 13 in the compacted array.

![HAMT example](/assets/images/HAMT-3.png)

## Top-level code

These calculations are fairly easy to capture in code.  First we must extract five bits from the appropriate place in the hash.  `mask` does that -- the shift will be five times the level in the tree.

```F#
    let mask (hash, shift) = (hash >>> shift) &&& 0x01f
```

We now need an integer with the appropriate bit set (in our example, 21):

```F#
    let bitPos (hash, shift) = 1 <<< mask (hash, shift)
```

The text for whether our index is present in the compacted array is

```F#
        let bit = bitPos (hash, shift)

        if bit &&& bitmap = 0 then
```

If the key is indeed, present, we need to know how many bits are set below it.  The trick is to take our bit and subtract one.  That will give us all ones in the positions prior to us (0 to 20)
If we `AND` that mask with the bitmap for the node and then count how many bits are set in the result, we will have our index.  This function will do the trick:

```F#
    let bitIndex (bitmap, bit) = bitCount (bitmap &&& (bit - 1))
```

The function `bitCount` is sometimes referred to as the population count.
Here is an implementation:

```F#
    let bitCount (x) =
        let x = x - ((x >>> 1) &&& 0x55555555)
        let x = (((x >>> 2) &&& 0x33333333) + (x &&& 0x33333333))
        let x = (((x >>> 4) + x) &&& 0x0f0f0f0f)
        (x * 0x01010101) >>> 24
```

The code for `bitCount` can be found in a lot of places.  Some CPU architectures have a single instruction which computes this.  You may have access to a function that inlines to that instruction (e.g., `System.Numerics.BitOperations.PopCount`).  


## Adding persistence and immutablility

There are plenty of tutorials available online with wonderful pictures and animations that illustrate the ideas behind persistent, immutable tree structures.  I refer you to them for nice visuals.  For right now, I'll provide some mediocre visuals to tell our story.  In immutable collections, operations that modify the collection, such as an insertion or a deletion do not modify the data structure.  Say we have a tree-shaped data structure and we are doing an insertion into the tree.  We will make a copy of the tree with the new item inserted, leaving the original tree intact.   We can do this reasonably efficienty  if we are clever enough to have our new tree share as much structure of the old tree as possible, the parts that don't need to change.  This is safe if the starting tree is immutable because the parts from the original tree are guaranteed not to change.  Consider the following binary tree.  Nodes are labeled with _id:datum_.

![Original tree](/assets/images/PersistentTree-1.png)

If we want to modify the data of node 6 to be Q, we must make copies of all nodes from 6 back to the root (thus, nodes 4 and 1).  They point to the nodes in the original tree when possible and to the new nodes where required to create the correct structure with a minimum of duplication:

![New tree](/assets/images/PersistentTree-2.png)

 For clarity, here is the new tree standing alone.

![New tree, alone](/assets/images/PersistentTree-3.png)

 The original tree still exists, unmodified.  Copying and resuse are the secrets to immutability, persistence, and efficiency.

 And now we can code.

 ## At the root

 We will develop our map class to meet the requirements of the Clojure interfaces of relevance:

 ```F#
[<AllowNullLiteral>]
type Seqable =
    abstract seq: unit -> ISeq

and [<AllowNullLiteral>] IPersistentCollection =
    inherit Seqable
    abstract count: unit -> int
    abstract cons: obj -> IPersistentCollection
    abstract empty: unit -> IPersistentCollection
    abstract equiv: obj -> bool

and [<AllowNullLiteral>] ISeq =
    inherit IPersistentCollection
    abstract first: unit -> obj
    abstract next: unit -> ISeq
    abstract more: unit -> ISeq
    abstract cons: obj -> ISeq

[<AllowNullLiteral>]
type ILookup =
    abstract valAt: key: obj -> obj
    abstract valAt: key: obj * notFound: obj -> obj

[<AllowNullLiteral>]
type IMapEntry =
    abstract key: unit -> obj
    abstract value: unit -> obj

[<AllowNullLiteral>]
type Associative =
    inherit IPersistentCollection
    inherit ILookup
    abstract containsKey: key: obj -> bool
    abstract entryAt: key: obj -> IMapEntry
    abstract assoc: key: obj * value: obj -> Associative

[<AllowNullLiteral>]
type Sequential =
    interface
    end

[<AllowNullLiteral>]
type Counted =
    abstract count: unit -> int

[<AllowNullLiteral>]
type IPersistentMap =
    inherit Associative
    inherit IEnumerable<IMapEntry> // do we really need this?
    inherit Counted
    abstract assoc: key: obj * value: obj -> IPersistentMap
    abstract assocEx: key: obj * value: obj -> IPersistentMap
    abstract without: key: obj -> IPersistentMap
    abstract cons: o: obj -> IPersistentMap
 ```

A `SimpleHashMap` is either empty or contains at least one key/value entry.  If it contains any entries, they will be in a tree designed along the lines discussed above; the nodes of that tree will be of type `SHMNode`.  For convenience, it will also cache the count of entries.  Hence,

```F#
type SimpleHashMap =
    | Empty
    | Rooted of Count: int * Node: SHMNode
```

Most of the implementations of the interface methods are very simple (if `Empty`) or defer to one of a few special operations supported by the tree nodes (if `Rooted`).

```F#
    interface Counted with
        member this.count() =
            match this with
            | Empty -> 0
            | Rooted(Count=c) -> c

    interface Seqable with
        member this.seq() = 
            match this with
            | Empty -> null
            | Rooted(Node=n) -> n.getNodeSeq()

    interface IPersistentCollection with
        member this.count() = (this:>Counted).count()
        member this.cons(o) = upcast (this:>IPersistentMap).cons(o)
        member this.empty() = upcast Empty
        member this.equiv(o) = ... maybe more on this later ...
``` 

Other than cheating a little by deferring `IPersistentCollection.cons` to the yet-to-appear `IPersistentMap.cons`, the code is straightforward.  (As my math alter ego says, "It is obvious".)  We see here the first of our delegations to the node class: `getNodeSeq()`.

Onward to the more map-specific interfaces.

```F#
    interface ILookup with
        member this.valAt(k) = (this :> ILookup).valAt (k, null)

        member this.valAt(k, nf) =
            match this with
            | Empty -> nf
            | Rooted (Node = n) -> n.find2 0 (hash k) k nf
```

The one-arg `valAt` delegates to the two-arg version -- that's usual.  And another debt (`find2`) is imposed on our node class.

```F#
    static member notFoundValue = obj()

    interface Associative with
        member this.containsKey(k) =
            match this with
            | Empty -> false
            | Rooted (Node = n) ->
                (n.find2 0 (hash k) k SimpleHashMap.notFoundValue)
                <> (upcast SimpleHashMap.notFoundValue)

        member this.entryAt(k) =
            match this with
            | Empty -> null
            | Rooted (Node = n) ->
                match n.find 0 (hash k) k with
                | None -> null
                | Some me -> me

        member this.assoc(k, v) =
            upcast (this :> IPersistentMap).assoc (k, v)
```

We continue our reliance on `find2`, incur a new dependency (`find`) and defer `Associative.assoc` to `IPersistentMap.assoc`.  Regarding our two finder methods on the nodes, `find` returns an `IMapEntry option` to indicate whether or not a value was found for the key;  `find2` returns either the value for the key or the given _not found_ value; here we can test for 'not found' coming back to us to derive a true/false result for `containsKey`.

And on to the heart of hte matter, the `IPersistentMap` interface:

```F#
    interface IPersistentMap with
        member this.count() = (this :> Counted).count ()

        member this.assocEx(k, v) =
            if (this :> Associative).containsKey (k) then
                raise
                <| InvalidOperationException("Key already present")

            (this :> IPersistentMap).assoc (k, v)
```

`count` was handled above.  `assocEx` throws an exception if the given key is already in the map.  If not, we defer to the regular `assoc`, coming up in a moment.


```F#
        member this.cons(o) =
            match o with
            | null -> upcast this
            | :? IMapEntry as e ->
                (this :> IPersistentMap)
                    .assoc (e.key (), e.value ())
            | _ ->
                let rec step (s: ISeq) (m: IPersistentMap) =
                    if isNull s then
                        m
                    else
                        let me = s.first () :?> IMapEntry
                        step (s.next ()) (m.assoc (me.key (), me.value ()))

                step (RT.seq (o)) this

```
Clojure defines the `cons` operation a bit strangely on maps.  Consing `null` is a no-op.  Consing an `IMapEntry` does an `assoc` to add the key/value pair.  Otherwise, we need to be consing something that can be converted to an `ISeq`. We iterate through the seq;  all entries must be `IMapEntry`s  and we add all those key/value pairs.  I encoded the iteration as a recursion -- you could go with mutable variables and a `while` loop if you are so inclined.  (My actual code also has cases for `DictionaryEntry`, `KeyValuePair<K,V>`, and `IPersisentVector` (if it has exactly two elements) -- they don't add to this discussion.)

The heart of the map is the `find`/`assoc`/`without` trio -- lookups, insertions, deletions.  We hit lookups above.  Here we go with the remaining two.

```F#
        member this.assoc(k, v) =
            let addedLeaf = Box()

            let rootToUse =
                match this with
                | Empty -> SHMNode.EmptyBitmapNode
                | Rooted (Node = n) -> n

            let newRoot = rootToUse.assoc 0 (hash k) k v addedLeaf

            if newRoot = rootToUse then
                upcast this
            else
                let count = (this :> Counted).count ()

                let updatedCount =
                    if addedLeaf.isSet then
                        count + 1
                    else
                        count

                upcast Rooted(updatedCount, newRoot)

```

As we did above, our `assoc` will defer the action down to a node.  If we are `Rooted`, we defer to the root node; if we are `Empty`, there is no node to defer to, so we defer to an empty instance of one of our node types.  The pattern here you will see agin:  if an action gives us back the same node as we started with, then nothing happened, and our map itself is the answer.  In other words, we were trying to add a key/value pair, that pairing was already pressent, so the map to return is ourself.   If a different node comes back, then the result of the assoc will be a new SimpleHashMap, rooted with the returned node.  The `addedLeaf:Box` that we pass in is a sentinel.  It goes in _unset_.  If it is 'set' during the operation, then a new entry was made in the map and we need to add one to our count.  (If we assoc a key/value pair and the key is present, the count of entries is unchanged;  if there is no entry for the key prior to the operation, then the count has gone up.)


```F#
        member this.without(k) =
            match this with
            | Empty -> upcast this
            | Rooted (Count = c; Node = n) ->
                match n.without 0 (hash k) k with
                | None -> Empty
                | Some newRoot ->
                    if newRoot = n then upcast this
                    elif c = 1 then upcast Empty
                    else upcast Rooted(c - 1, newRoot)
```

The `without` method follows a similar pattern.  It is not an error to to remove a non-existent key, so `without` when we are `Empty` is a no-op.  If `Rooted`, we delegate the operation to the node.  If the node itselfcomes back, there was no change (key was not present) and this is a no-op.  Else, we have a new root;  the count definitely decreased.

And we are done.

Except, of course for all the work we pushed down to the nodes in the tree.

Buckle up.

## The heart of the matter

Now to deal into the nodes below the root that make up the tree itself. 

There are three node types:  (1) A `BitmapNode` is the kind of node described earlier.  It has an array of entries, each entry being either a key-value pair or another node.  There are no empty slots.  The node has a bitmap to help map the index computed from the hash to an index in the array.   (2) An `ArrayNode` has an array that contains nodes for the next level down in the tree.  Some of the entries may be blank.  (We use an option type for the array entries in order to distinguish occupied vs. not-occupied.)  It also holds a count of the occupied cells.  (3) A `CollisionNode' contains an array of key-value pairs.  A collision node will appear when we have two or more keys with the same hash code.  If one reaches a collision node in the course of searching for a key, one will be forced to do a linear search of the entries to see if the key appears.

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

| Entry present? |  |  |  | Map count | Code snippet |
|---|---|---|---|:---:|:---:|
| No entry | >= 1/2 full |  | Create an `ArrayNode` copying this node and inserting new K/V pair | +1 | A |
| No entry  | < 1/2 full |  | Create a `BitmapNode` copying this node and inserting a new K/V pair | +1 | B |
| Has entry | Entry is KV | Key matches, value matches | No-op | no change | (inline) |
| Has entry | Entry is KV | Key matches, value does not match | Create a `BitmapNode` copying this node but key's value replaced with new value | no change | (inline)
| Has entry | Entry is KV | Key does not match | Create a new node <br>(`CollisionNode` if the two keys hash the same, `BitmapNode` otherwise) <br>and insert where the existing KV is | +1 | (inline) |
| Has entry | Entry is SHMNode |  | Do the `assoc` on the subnode.  <br>If same node comes back, then this is a no-op.  <br>Else create a `BitmapNode` copying this one <br>with the new node replacing the existing one. | no change or +1 | (inline)

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
