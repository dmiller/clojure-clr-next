---
layout: post
title: Persistent vectors - Part 1
date: 2023-02-08 00:00:00 -0500
categories: general
---

This is the first of a series of posts on the design and implementation of `PersistentVector`, an immutable, persistent vector class supporting a transient mode for efficient batch operations.  We start with an overview of immutability and persistence.

... We will then look at asdfasd, then asdfasdfa.  I hope also to look at an alternative implementation that is more F#-idiomatic.  we will look at performance.
...

What we learn here also will be of great help in figuring out how `PersistentHashMap` is implemented.


## Persistence and immutability

There are any number of wonderful presentations on using tree-structured indexes to provide reasonably efficient access and structure sharing to deal with creating modified copies cheaply. A fair number deal with _hash array-mapped tries_ (HAMT), which we will cover when get to `PersistentHashMap`.  Vectors can be managed somewhat more simply.   A more focused search term for what we are doing here would be _persistent bit-partitioned vector trie_.  Definitely the most relevant to our focus here is a series of five posts by Jean Niklas L'orange:

- [Understanding Clojure's Persistent Vectors, pt. 1](https://hypirion.com/musings/understanding-persistent-vector-pt-1)
- [Understanding Clojure's Persistent Vectors, pt. 2](https://hypirion.com/musings/understanding-persistent-vector-pt-2)
- [Understanding Clojure's Persistent Vectors, pt. 3](https://hypirion.com/musings/understanding-persistent-vector-pt-3)
- [Understanding Clojure's Transients](https://hypirion.com/musings/understanding-clojure-transients)
- [Persistent Vector Performance Summarised](https://hypirion.com/musings/persistent-vector-performance-summarised)

These are directly about the data structure we are going to be looking at.  I see no need to duplicate his effort overall -- go read the articles.  I will, however, present a few key notions so we can get to the code quickly.

First, it is very useful that the vectors we are looking at are compact.  The elements of a vector of size `n` are indexed `0` to `n-1`.  There are no holes. This makes vectors simpler than hash trees -- HAMTs use a very clever optimization to deal with indexing holes efficiently.  Vectors don't need this trick -- everything can be packed tightly. And there will be additional simplifications that result from this packing, as we will see.

A multiway-branching tree provides access to our elements.  THe branching factor is fixed.  In our implementation, that factor is 32.  In the pictures below, I will use 4.  (Given the bit-twiddling techniques used, the branching factor usually will be a power of 2).  We pack everything to the left as far as possible and as close to the root as possible. And we keep the leaf nodes, where the actual data is kept, all at the same level.  This means that
at every level of the tree, all the nodes are full except for the rightmost oness
At the leaf level of the tree, each node will be full.  Leading to picture like this:

![PersistentVector node tree](/assets/images/PersistentVector-node-tree.png)

(The numbers appearing in the leaf nodes are their indexes.)

Given what I just said about leaf nodes being full, the leaf nodes will have number of elements that is multiple of four.  What if your vector has 26 elements?  The 'extra' two elements are stored in the _tail_, a short vector of size at most the branching factor to hold the overflow elements.  

The vector itself is an object that holds the count of elements, and indication of the tree depth, a pointer to the root of the tree, and a pointer to the tail.


![PersistentVector entire tree](/assets/images/PersistentVector-whole-tree.png)

Note that we can find an element given its index fairly easily.  The first step is to determine whether the desired element is in the tail.  (Determining that we'll go over below.) If so, you can just take the last two bits of its index and use that to access the tail array.  25 = binary(011001), the last two bits are 01, so access with a 1.  For an index not in the tail, we use pairs of bits to indicate our path down the tree.
19 = binary(010011) = 01 + 00 + 11 = 1 / 0 3.  Follow that path in the picture above.


## Let's code!

> Please note that the code here is a fairly direct translation of the C# code (which is very, very direct translation of the original Java code).  I'll talk about writing it more idiomatically as F# in a later post.

We need a node class.

```F#
[<AllowNullLiteral>] 
PVNode(edit: AtomicReference<Thread>, array: obj array) =

    member _.Edit = edit
    member _.Array = array

    new(edit) = PVNode(edit, (Array.zeroCreate 32))
```

Please, please, ignore that `edit` field.  I'm too lazy to edit it out of my copy-and-paste.  It won't be needed until we deal with transience.   For now it's just going to get copied around. Each node has an array in it. For an internal node, the array will hold the nodes at the next level.  For a leaf node, the array holds the actual values in the vector.

The vector itself is a header containing some bookkeeping and pointers to the root node (if it exists) and the tail (if it exists).

```F#
PersistentVector(meta: IPersistentMap, cnt: int, shift: int, root: PVNode, tail: obj array) =
    inherit APersistentVector()

    new(cnt, shift, root, tail) = PersistentVector(null, cnt, shift, root, tail)
```

Ignore `APersistentVector` for now.  It is analagous to `ASeq`, an abstract base class providing some common code (shared by `PersistentVector`, `AMapEntry`, and `MapEntry`).  Very straightforward.  I'll write a little about it later on.  You also can ignore the `meta: IPersistentMap` -- `PersistentVector` supports metadata.  (Another post for another day.)

`cnt` is the count of items in the vector.  `root` and `tail` are as mentioned.  `level` indicates how deep the tree is.  That could be computed from the count, but it is convenient to have it computed and at hand.  And it is not the depth itself, but rather an indication ths size of shift we have to do in our bit-twiddling operations to compute access paths. 

In the example above, with four-way branching, we needed two bits to determine the next entry to look at.  The depth of the tree is 3.  The first shift we need is 4 which is (tree_depth - 1) * 2.  As we move down the tree, we need to shift two less each time.  At the bottom, the needed shift is 0 and we can just mask off the last two bits directly.

In our actual code, using 32-way branching, the shift at the root is (tree_depth -1)*5;
that number is the one stored in the `shift` field in the vector object.


## Indexing

With this in mind, we can code up `nth`, the indexing accessor.    First, we have to decide if we are looking in the tree or the tail.  So, if the branching factor is 32, we really need to know the closest multiple of 32 less than our count.  I can think of three ways to compute this:

```F#
// modulus
let im1 = i - 1
im1 - (im1 % 32) 

// divide/multiply
(( i - 1) / 32) *  32
    
// act all shifty
((i-1) >>> 5) <<< 5
```

The `- 1` is there to handle exactly multiples of 32 properly.  Work it out.
The last one likely is the oddest.  Shift to the right to clear the bottom bits out (that is equivalent to dividing by 32), then shift left (equivalent to multiplying by 32).  Which one to use?  The shifting version.  It is generally accepted that shift operations are faster than div/mod operations (though some compilers are smart enough to deal with it).  I benchmarked it just to convince you.  See the endnote.  Thus we code

```F#
    member _.tailoff() =
        if cnt < 32 then 0 else ((cnt - 1) >>> 5) <<< 5
```
Read this as "the offset of the tail is N items".
If we are looking up an index `i <= this.tailoff()`, we are in the tree, not the tail.

At each level we shift right to get to the bits we are interested in and then mask them off.
Essentially:

```F#
node.Array[(i >>> shift) &&& 0x1f]
```

The following procedure will return the object array in the `PVNode` or the tail (also an object array).
It uses tail recursion to work its way down to the leaf level.  The shift quantity decreases by 5 at each level until there is no need to shift -- that is the leaf level.

```F#
    member this.arrayFor(i) =
        if 0 <= i && i < cnt then
            if i >= this.tailoff () then
                tail
            else
                let rec step (node: PVNode) shift =
                    if level <= 0 then
                        node.Array
                    else
                        let newNode = node.Array[(i >>> shift) &&& 0x1f] :?> PVNode
                        step newNode (shift - 5)

                step root shift  // this shift if the one in the PersistentVector itself
        else
            raise <| ArgumentOutOfRangeException("i")
```

Given the array where the index is located, the last five bits of the desired index tell you where to go in the array. Thus:

```F#
    interface Indexed with
        override this.nth(i) =
            let node = this.arrayFor (i)
            node[i &&& 0x1f]

        override this.nth(i, nf) =
            if 0 <= i && i < cnt then (this :> Indexed).nth (i) else nf
```

The `Indexed` interface has just these two methods.

```F#
[<AllowNullLiteral>]
type Indexed =
    inherit Counted
    abstract nth: i: int -> obj
    abstract nth: i: int * notFound: obj -> obj
```
  Note the for the one-parameter version, if you pass an index out of the range [0,cnt-1], the `arrayFor` call will throw an exception.  The two-parameter version checks for being out of bounds and returns the supplied 'not found' value instead.

## Adding an element

There are three important 'modification' methods.  The only way to add an item is via `cons`.  The interface `IPersistentVector` has its own version that is convenient because it returns an `IPersistentVector`, thus allowing chaining of calls.  Adding occurs at the high end only.   `IPersistentVector.assocN` allows the change of value at an index.  `IPersistentStack.pop` removes the 'topmost' element.   Thus, adding and removing are at one end only.


```F#
[<AllowNullLiteral>]
type IPersistentVector =
    inherit Associative
    inherit Sequential
    inherit IPersistentStack
    inherit Reversible
    inherit Indexed
    abstract length: unit -> int
    abstract assocN: i: int * value: obj -> IPersistentVector
    abstract cons: o: obj -> IPersistentVector
    abstract count: unit -> int

[<AllowNullLiteral>]
type IPersistentStack =
    inherit IPersistentCollection
    abstract peek: unit -> obj
    abstract pop: unit -> IPersistentStack
```

Of course, these operations don't modify anything.  They all return a new `PersistentVector` with the desired content.  This requires copying of intermediate nodes.  We'll cover `cons`.  `pop` just does the reverse.  `assocN` feels like a bit of both.

Take another look at the picture from earlier:

![PersistentVector entire tree](/assets/images/PersistentVector-whole-tree.png)

and imagine adding elements iteratively.   There are three situations we will encounter.

1. At the start, there is room in the tail.  We can create a new tail vector, and a new `PersistentVector` node with an incremented count, the same `root` and the new tail.  We can keep doing this until we have a full tail.  (It is okay for the tail to be full.)



![PersistentVector: room in tail](/assets/images/PersistentVector-room-in-tail.png)

2.  The tail is full, but there is room in the leaf row to add another leaf.  We'll have to figure out how to detect this, but essentially we create a new node using the current tail as its array, recreate the path up to the root, with copies to any nodes not on that path, and put the new element into a new tail array.


![PersistentVector: room in leaf row](/assets/images/PersistentVector-room-in-leaf-row.png)

3. There is no room in the leaf row.  We have to increase the depth of our tree by creating a new root node.
The existing tree becomes the leftmost child of the new root, and we establish a set of nodes leading down to the new leaf node.

![PersistentVector: root split](/assets/images/PersistentVector-split-root.png)

Our code for `cons` directly reflects these three cases:

```F#
   interface IPersistentVector with
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

The first test is for room in the tail.  We create a new tail and new `PersistentVector` with that tail.  The count is one greater, the shift and root are the same.

If the tail is full, we will need a new `PVNode` to put in the tree.  Its array can be the current tail.  (No need to copy -- everything is treated immutably.)  We then have to deter
mine if the root needs to split.  The magic incantation `(cnt >>> 5) > (1 <<< shift)` does the trick.

That expression stopped me cold when I first saw it.  Then I decided to just type it in and keep on coding.  Under the general theme of "no line of code unexamined", I knew I'd have to come back to it.  Here goes.

>I'll actually solve it generally.  Let `b` be our branching factor; `b` will always be a power of 2, say `b=2^k`.  In our pictures above, `b=2` and `k=2`; in our code, `b=32` and `k=5`. Let `d` be the depth of the tree.  Recall from above that `shift=(d-1)*k`.
>
>What is the capacity of a tree of depth `d`?  At level `j` (`j=1` at the root), there are `2^(j-1)` nodes.  Hence at the leaf level there will be `b^(d-1)` nodes.  Each node holds `b = 2^k` entries, so the total capacity when the tree has depth `d` is `b * b^(d-1)`.  
>
>There is not enough room in the leaves if `cnt > capacity`, which is to say `cnt > b * b^(d-1)`. Equivalently, `cnt/b > b^(d-1)`.  Substituting `2^k` for `b`, we have
>
```
   cnt/(2^k) > (2^k)^(d-1) = 2^(k*(d-1)) = 2^(shift)
```
>Dividing by a power of two can be done by shifting right by the exponent.  A power of two can be generated by left shifting `1` by the exponent.  And we arrive at the condition
>
```f#
    (cnt >>> k) > (1 <<< shift)
```
>in our case, with `k=5`.

Now (perhaps) I can sleep at night again.

The work to establish the new nodes required for immutability is done by `newPath` for the root-splitting case and `pushTail` for the 'room-in-the-leaves' case.






