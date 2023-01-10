namespace Clojure.Collections

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
