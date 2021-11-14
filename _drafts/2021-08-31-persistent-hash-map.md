---
layout: post
title: Making a hash of things
date: 2021-08-31 19:36:00 -0500
categories: general
---

# Making a hash of things

The most complex data structure in the Clojure catalog has to be the PersistentHashMap. This is Phil Bagwell's [Hash Array Mapped Trie (HAMT)](https://en.wikipedia.org/wiki/Hash_array_mapped_trie) as modified by Rich Hickey to be immutable and persistent.  (Bagwell's original paper is available [here](http://infoscience.epfl.ch/record/64398/files/idealhashtrees.pdf.)  I want to describe the ideas behind this data structure and code a simple implementation, leaving out complexities that come from various efficiency hacks.


 # Hashing

<<<<<<< HEAD:_drafts/2021-08-26-persistent-hash-map.md
 There are a variety of data structures that use hashing to provide O(1) or O(ln n) access to data.  A simple flavor is the _hash table_.  
 Assume we are mapping keys to values. 
 A hash code is computed from the key which in turn is used to compute an index into an array where the corresponding value will be found.  
 Because generally hash codes or indexes are not unique, two keys can _collide_, i.e., are mapped to the same location.  
 Some technique must be used to deal with collisions.   
 The theory on hash tables of this type is extensive; you can get started [here](https://en.wikipedia.org/wiki/Hash_table).
=======
 There are a variety of data structures that use hashing to provide O(1) or O(ln n) access to data.  A simple flavor is the _hash table_.  Assume we are mapping keys to values. A hash code is computed from the key which in turn is used to compute an index into an array
 where the corresponding value will be found.  Typically the index is computed by modding the hash code by the array length.  Because (generally) hash codes or indexes  are not unique, two keys can _collide_, i.e., are mapped to the same index.  Some technique must be developed to deal with collisions.   The theory on hash tables of this type is extensive; you can get started [here](https://en.wikipedia.org/wiki/Hash_table).
>>>>>>> 747e5b1c1bc960de3024da5082b93ca96fd99d88:_drafts/2021-08-31-persistent-hash-map.md


Another approach uses trees instead of arrays.  As an example, we could store key/value pairs in a binary tree.  Treating the hash code as a sequence of bits and mapping 1 to Left and 0 to Right, the hash code of an item describes a path through the tree.  One does not need to use all the bits, just enough to distinguish a given key from all the others.  Assuming 5-bit hashcodes, this picture illustrates how a given set of four keys would be distributed.

![Binary search tree](/assets/images/BinaryTree-1.png)

Again, one must deal with collisions.  

<<<<<<< HEAD:_drafts/2021-08-26-persistent-hash-map.md
Binary branching is not efficient.  One can end up with trees that are quite deep and that are fragmented in memory.  Depth correlates with the number of memory accesses.  Fragmentation makes  accesses are expensive.   A _hash array mapped trie/tree_ (HAMT) combines the array and tree notions.   Rather than a node branching in two directions and the path to choose based on a single bit,  in an HAMT the branching factor in a node is greater -- usually a small power of two -- and the branch to choose is based on several contiguous bits in the hash.  Which bits depend on the branching factor (power of two) and the level in the tree.  Using four as the branching factor, we might see a configuration such as the following:
=======
Binary branching is not efficient.  One can end up with trees that are quite deep and fragmented in memory.  Depth correlates with the number of memory accesses; fragmentation means the accesses are expensive.   A _hash array mapped trie/tree_ (HAMT) combines the array and tree notions.   Rather than a node branching in two directions and the path to choose based on a single bit,  in a HAMT the branching factor at a node is larger -- usually a small power of two -- and the branch to choose is based on several contiguous bits in the hash treated as an integer to yield an index.  Which bits are used depends on the branching factor (power of two) and the level in the tree.  Using four as the branching factor, we might see a configuration such as the following:
>>>>>>> 747e5b1c1bc960de3024da5082b93ca96fd99d88:_drafts/2021-08-31-persistent-hash-map.md

![HAMT example](/assets/images/HAMT-1.png)

As the branching factor and hence the size of the arrays in the nodes increases, there can be a significant amount of space wasted by empty array entries.  A solution is allocating array storage only for the occupied cells. In this scheme, given a hash code and thus the index for this level, we need to know if that index is present and we must map this index to an index in the compressed array.  This is done by the node having a bitmap  where one bits indicate occupied indexes.  There is a neat trick to map an index to the array cell involving calculating the number of one bits (sometimes known as the _population count_) in a masked section of the bitmap.  (See below.)

Here's a rough sketch.   Let's assume a branching factor of 32 (a common choice).  Five bits can be used to provide an index in the range `[0,31]`.  We begin with the rightmost five bits to compute the index to check.  At the next level, we take the second five bits, etc.  Say we are two levels down from the root, and supposed the hash for our key is 0xDD707.  (I'll ignore the zeros on the most significant end.)  Then we need to extract the third set of five bits.  From this picture

![HAMT example](/assets/images/HAMT-2.png)

we compute an index of 21.   If we were not working with compacted array storage, we would just look at `entries[21]` for the node in question and see if it represented a key/value pair, a link to another node one level down, or was empty.   However, if we are using compacted array storage, we must figure out where index 21 is mapped to.  Of course, it might be that index 21 is empty and hence not in the array.  We check that by seeing if bit 21 is set in the node's bitmap.  If not, then the key is not present.

Suppose the node in question has bitmap 0xD36FCB4.  It is set in bits 2, 4, 5, 7, 10, 11, 12, 13, 14, 15, 17, 18, 20, 21, 24, 27.  So index 21 is indeed occupied in this node's array.  
But mapped to what index in the node's array? Well, you can count how many bits prior to 21 are set.  That is 13 in this case.  So to find intended index 21 we look in index 13 in the compacted array.

![HAMT example](/assets/images/HAMT-3.png)

These calculations are fairly easy to capture in code.  First we must extract five bits from the appropriate place in the hash.  `mask` does that -- the shift will be five times the level in the tree.

```F#
let mask(hash,shift) = (hash >>> shift) &&& 0x01f
```

<<<<<<< HEAD:_drafts/2021-08-26-persistent-hash-map.md
We now need an integer with the appropriate bit set (in our example, 21):
=======
(In our example, we would pass in values for `shift` that are a multiple of 5, the multiple corresponding to the depth of the node in question in tree.)

We can get an integer with a 1 in the appropirate position by shifting.  
>>>>>>> 747e5b1c1bc960de3024da5082b93ca96fd99d88:_drafts/2021-08-31-persistent-hash-map.md

```F#
let bitPos(hash, shift) = 1 <<< mask(hash,shift)
```

<<<<<<< HEAD:_drafts/2021-08-26-persistent-hash-map.md
The text for whether our index is present in the compacted array is

```F#
let bit = bitPos(hash,shift)
if bitmap &&& bit <> 0 then ...
```

If the key is indeed, present, we need to know how many bits are set below it.  The trick is to take our bit and subtract one.  That will give us all ones in the positions prior to us (0 to 20)
If we `AND` that mask with the bitmap for the node and then count how many bits are set in the result, we will have our index.  This function will do the trick:
=======
and use this to determine if the key is represented in a node by looking at the node's bitmap:
>>>>>>> 747e5b1c1bc960de3024da5082b93ca96fd99d88:_drafts/2021-08-31-persistent-hash-map.md

```F#
let bitIndex(bitmap,bit) = bitCount(bitmap &&& (bit-1))
```

The function `bitCount` is sometimes referred to as the population count.
Here is an implementation:

```F#
    let bitCount(x) =
        let x = x-((x >>> 1) &&& 0x55555555);
        let x = (((x >>> 2) &&& 0x33333333) + (x &&& 0x33333333))
        let x = (((x >>> 4) + x) &&& 0x0f0f0f0f)
        (x * 0x01010101) >>> 24
```

<<<<<<< HEAD:_drafts/2021-08-26-persistent-hash-map.md
The code for `bitCount` can be found in a lot of places.  Some CPU architectures have a single instruction which computes this.  You may have access to a function that inlines to that instruction (e.g., `System.Numerics.BitOperations.PopCount`).  
=======
Here, the argument for `bit` would be an `int` with one bit set, such as from `bitPos`.

The code for `bitCount` can be found in a lot of places.  Some CPU architectures have a single instruction which computes this.  You may have access to a function that inlines to that instruction (e.g., `System.Numerics.BitOperations.PopCount`).  The secret here is that `bit-1` for an integer with single bit set gives an integer with all 1s before that bit.  And'ing that with the bitmap and taking the population count tells you how many bits are set before the bit of interest.  That gives an index.

An example!

Suppose our hashcode is 0xDD707.  (I'll ignore the zeros on the most significant end.)  Then:

![HAMT example](/assets/images/HAMT-2.png)

Now take a node with bitmap 0xD36FCB4.  It is set in bits 2, 4, 5, 7, 10, 11, 12, 13, 14, 15, 17, 18, 20, 21, 24, 27.  If these are mapped to indexes 0, 1, 2, etc, then bit 21 maps to index 13:

![HAMT example](/assets/images/HAMT-3.png)
>>>>>>> 747e5b1c1bc960de3024da5082b93ca96fd99d88:_drafts/2021-08-31-persistent-hash-map.md


## Adding persistence and immutablility

<<<<<<< HEAD:_drafts/2021-08-26-persistent-hash-map.md
There are plenty of tutorials available online with wonderful pictures and animations that illustrate the ideas behind persistent, immutable tree structures.  I refer you to them for nice visuals.  For right now, I'll provide some mediocre visuals to tell our story.  In immutable collections, operations that modify the collection, such as an insertion or a deletion do not modify the data structure.  Say we have a tree-shaped data structure and we are doing an insertion into the tree.  We will make a copy of the tree with the new item inserted, leaving the original tree intact.   We can do this reasonably efficienty  if we are clever enough to have our new tree share as much structure of the old tree as possible, the parts that don't need to change.  This is safe if the starting tree is immutable because the parts from the original tree are guaranteed not to change.  Consider the following binary tree.  Nodes are labeled with _id:datum_.
=======
There are plenty of tutorials available online with wonderful pictures and animations that illustrate the ideas behind persistent, immutable tree structures.  I refer you to them for nice visuals.  See below for some mediocre visuals.  The main idea is that we do not modify the any tree's structure.  If a pointer has to change, or the data in a node has to change, one makes a copy of the nodes from the changed node back up to the root, reproducing the structure of the part of the tree that does not change by linking to those parts from the new parts.  Consider the following binary tree.  Nodes are labeled with _id:datum_.
>>>>>>> 747e5b1c1bc960de3024da5082b93ca96fd99d88:_drafts/2021-08-31-persistent-hash-map.md

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
type [<AllowNullLiteral>] Seqable =
    abstract seq : unit -> ISeq

and [<AllowNullLiteral>] IPersistentCollection = 
    inherit Seqable
    abstract count : unit -> int
    abstract cons: obj -> IPersistentCollection
    abstract empty: unit -> IPersistentCollection
    abstract equiv: obj -> bool

[<AllowNullLiteral>]
type ILookup = 
    abstract valAt : key:obj -> obj
    abstract valAt : key: obj * notFound: obj -> obj

[<AllowNullLiteral>]
type IMapEntry =
    abstract key : unit -> obj
    abstract value : unit -> obj

[<AllowNullLiteral>]
type Associative = 
    inherit IPersistentCollection
    inherit ILookup
    abstract containsKey : key: obj -> bool
    abstract entryAt : key: obj -> IMapEntry
    abstract assoc : key: obj * value: obj -> Associative

 [<AllowNullLiteral>]
type Counted =
    abstract count : unit -> int   

[<AllowNullLiteral>]
type IPersistentMap =
    inherit Associative
    inherit Counted
    abstract assoc : key: obj * value: obj -> IPersistentMap
    abstract assocEx : key: obj * value: obj -> IPersistentMap
    abstract without : key: obj -> IPersistentMap
    abstract cons: o:obj -> IPersistentMap
    abstract count: unit -> int
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

<<<<<<< HEAD:_drafts/2021-08-26-persistent-hash-map.md
Other than cheating a little by deferring `IPersistentCollection.cons` to the yet-to-appear `IPersistentMap.cons`, the code is straightforward.  (As my math alter ego says, "It is obvious".)  We see here the first of our delegations to the node class: `getNodeSeq()`.
=======
A standard trick is to define `IPersistentCollection.cons` in terms of the yet-to-appear `IPersistentMap.cons`.  We see here the first of our delegations to the node type: `getNodeSeq()`.
>>>>>>> 747e5b1c1bc960de3024da5082b93ca96fd99d88:_drafts/2021-08-31-persistent-hash-map.md

Onward to the more map-specific interfaces.

```F#
   interface ILookup with
        member this.valAt(k) = (this:>ILookup).valAt(k,null)
        member this.valAt(k,nf) = 
            match this with
            | Empty -> nf
            | Rooted(Node=n) -> n.find2 0 (hash(k)) k nf 
```

<<<<<<< HEAD:_drafts/2021-08-26-persistent-hash-map.md
The one-arg `valAt` delegates to the two-arg version -- that's usual.  And another debt (`find2`) is imposed on our node class.
=======
The one-arg `valAt` pushes to the two-arg version -- that's usual. An `Empty` map always returns the not-found value. And another debt (`find2`) is imposed on our nodes.
>>>>>>> 747e5b1c1bc960de3024da5082b93ca96fd99d88:_drafts/2021-08-31-persistent-hash-map.md

```F#
    static member notFoundValue = obj()

    interface Associative with  
        member this.containsKey(k) =
            match this with
            | Empty -> false
            | Rooted(Node=n) -> (n.find2 0 (hash(k)) k SimpleHashMap.notFoundValue)  <> (upcast SimpleHashMap.notFoundValue)
        member this.entryAt(k) = 
            match this with
            | Empty -> null
            | Rooted(Node=n) -> 
                match n.find 0 (hash(k)) k with
                | None -> null
                | Some me -> me
        member this.assoc(k,v) = upcast (this:>IPersistentMap).assoc(k,v)
```

<<<<<<< HEAD:_drafts/2021-08-26-persistent-hash-map.md
We continue our reliance on `find2`, incur a new dependency (`find`) and defer `Associative.assoc` to `IPersistentMap.assoc`.  Regarding our two finder methods on the nodes, `find` returns an `IMapEntry option` to indicate whether or not a value was found for the key;  `find2` returns either the value for the key or the given _not found_ value; here we can test for 'not found' coming back to us to derive a true/false result for `containsKey`.

And on to the heart of hte matter, the `IPersistentMap` interface:
=======
We continue our reliance on `find2`, incur a new dependency (`find`), and defer `Associative.assoc` to `IPersistentMap.assoc`.  Regarding our two finder methods on the nodes, `find` returns an `IMapentry option` to indicate whether or not a value was found for the key;  `find2` returns either the value for the key or the given _not found_ value; here we can test for 'not found' coming back to derive a true/false result for `containsKey`.

On to the real action: the `IPersistentMap` interface.
>>>>>>> 747e5b1c1bc960de3024da5082b93ca96fd99d88:_drafts/2021-08-31-persistent-hash-map.md

```F#
    interface IPersistentMap with
        member this.count() = (this:>Counted).count()
        member this.assocEx(k,v) = 
            if (this:>Associative).containsKey(k) then raise <| InvalidOperationException("Key already present")
            (this:>IPersistentMap).assoc(k,v)

```

`count` was handled above.  `assocEx` throws an exception if the given key is already in the map.  If not, we defer to the regular `assoc`, coming up in a moment.


```F#
        member this.cons(o) = 
            match o with 
            | null -> upcast this
            | :? IMapEntry as e -> (this:>IPersistentMap).assoc(e.key(),e.value())
            | _ -> 
                let rec step (s:ISeq) (m:IPersistentMap) =
                    if isNull s then
                        m
                    else
                        let me = s.first() :?> IMapEntry
                        step (s.next()) (m.assoc(me.key(),me.value()))
                step (RT.seq(o)) this

```
TClojure defines the `cons` operation is defined a bit strangely on maps.  Consing `null` is a no-op.  Consing an `IMapEntry` does an `assoc` to add the key/value pair.  Otherwise, we need to be consing something that can be converted to an `ISeq`. We iterate through the seq;  all entries must be `IMapEntry`s  and we add all those key/value pairs.  I encoded the iteration as a recursion -- you could go with mutable variables and a `while` loop if you are so inclined.

The heart of the map is the `find`/`assoc`/`without` trio -- lookups, insertions, deletions.  We hit lookups above.  Here we go with the remaining two.

```F#
        member this.assoc(k,v) = 
            let addedLeaf = Box()
            let rootToUse = 
                match this with
                | Empty -> SHMNode.EmptyBitmapNode
                | Rooted(Node=n) -> n
            let newRoot = rootToUse.assoc 0 (hash(k)) k v addedLeaf
            if newRoot = rootToUse then
                upcast this
            else
                let count = (this:>Counted).count()
                let updatedCount = if addedLeaf.isSet then count+1 else count
                upcast Rooted(updatedCount,newRoot)

```

<<<<<<< HEAD:_drafts/2021-08-26-persistent-hash-map.md
As we did above, our `assoc` will defer the action down to a node.  If we are `Rooted`, we defer to the root node; if we are `Empty`, there is no node to defer to, so we defer to an empty instance of one of our node types.  The pattern here you will see agin:  if an action gives us back the same node as we started with, then nothing happened, and our map itself is the answer.  In other words, we were trying to add a key/value pair, that pairing was already pressent, so the map to return is ourself.   If a different node comes back, then the result of the assoc will be a new SimpleHashMap, rooted with the returned node.  The `addedLeaf:Box` that we pass in is a sentinel.  It goes in _unset_.  If it is 'set' during the operation, then a new entry was made in the map and we need to add one to our count.  (If we assoc a key/value pair and the key is present, the count of entries is unchanged;  if there is no entry for the key prior to the operation, then the count has gone up.)
=======
As we did above, our `assoc` will defer the action down to a node.  If we are `Rooted`, we defer to the root node; if we are `Empty`, there is no node to defer to, so we defer to an empty instance of one of our node types.  The pattern that results will recur constantly below:  if an action gives us back the same node as we started with, then nothing happened, and our map itself is the answer.  In other words, we were trying to add a key/value pair, that pairing was already present, so the map to return is ourself.   If a different node comes back, then the result of the assoc will be a new SimpleHashMap, rooted with the returned node.  The `addedLeaf:Box` that we pass in is a sentinel.  It goes in _unset_.  If it is 'set' during the operation, then a new entry was made in the map and we need to add one to our count.  (If we assoc a key/value pair and the key is present, the count of entries is unchanged;  if there is no entry for the key prior to the operation, then the count has gone up.)
>>>>>>> 747e5b1c1bc960de3024da5082b93ca96fd99d88:_drafts/2021-08-31-persistent-hash-map.md


```F#
        member this.without(k) = 
            match this with
            | Empty -> upcast this
            | Rooted(Count=c;Node=n) ->
                let newRoot = n.without 0 (hash(k)) k
                if newRoot = n then
                    upcast this
                elif c = 1 then
                    Empty
                else
                    upcast Rooted(c-1,newRoot)
```

The `without` method follows a similar pattern.  It is not an error to to remove a non-existent key, so `without` when we are `Empty` is a no-op.  If `Rooted`, we delegate the operation to the node.  If the node itselfcomes back, there was no change (key was not present) and this is a no-op.  Else, we have a new root;  the count definitely decreased.

And we are done.

<<<<<<< HEAD:_drafts/2021-08-26-persistent-hash-map.md
Except, of course for all the work we pushed down to the nodes in the tree.
=======
Well, at this level.  We've deferred a lot to the type `SHMNode` of the tree nodes.  Take a deep breath.

>>>>>>> 747e5b1c1bc960de3024da5082b93ca96fd99d88:_drafts/2021-08-31-persistent-hash-map.md
