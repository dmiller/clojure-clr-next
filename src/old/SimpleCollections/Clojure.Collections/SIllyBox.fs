namespace Clojure.Collections


// ClojureJVM has a class named Box.
// It contains a Val (type Ojbect0 that can be get/set.
// THe only use of Box is in PersistentHashMap and PersistentHashTree.
// It is passed around in Assoc and Without calls to track whether a node gets added somewhere below in the tree.
// It is used in very restricted manner.
//   Box addedLeaf = new Box(null)
//   addedLeaf.Val = addedLeaf
//  if (addedLeaf.Val != null ) ...
// In other words, you are set or not, and setting is sticky.
// That's it.
//  I tried designing the code with multiple return values to deal with this, and it got too complicated, so I left the original design.
//  But decided to cut this down to the minimum.  Just a true/false value.
//  So the code snippets above become:
//    let addedLeaf = SillyBox()
//    addedLeaf.set()
//    if addedLeaf.isSet  ...

type SillyBox(init) =
    let mutable value: bool = init
    new() = SillyBox(false)

    member _.set() = value <- true
    member _.reset() = value <- false
    member _.isSet = value
    member _.isNotSet = not value
