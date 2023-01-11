namespace Clojure.Collections

open System.Collections.Generic

// The mutually recursive triple that underlies most of the following interfaces.

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
    inherit IEnumerable<IMapEntry>
    inherit Counted
    abstract assoc: key: obj * value: obj -> IPersistentMap
    abstract assocEx: key: obj * value: obj -> IPersistentMap
    abstract without: key: obj -> IPersistentMap
    abstract cons: o: obj -> IPersistentMap

    abstract count: unit -> int // do we need this?
