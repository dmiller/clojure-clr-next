namespace Clojure.Collections

open System
open System.Collections
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
type Indexed =
    inherit Counted
    abstract nth: i: int -> obj
    abstract nth: i: int * notFound: obj -> obj

[<AllowNullLiteral>]
type Reversible =
    abstract rseq: unit -> ISeq

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

type IPersistentSet =
    inherit IPersistentCollection
    inherit Counted
    abstract disjoin: key: obj -> IPersistentSet
    abstract contains: key: obj -> bool
    abstract get: key: obj -> obj

    abstract count: unit -> int // do we need this?

[<AllowNullLiteral>]
type IPersistentStack =
    inherit IPersistentCollection
    abstract peek: unit -> obj
    abstract pop: unit -> IPersistentStack

[<AllowNullLiteral>]
type IPersistentList =
    inherit Sequential
    inherit IPersistentStack

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
type IHashEq =
    abstract hasheq: unit -> int

[<AllowNullLiteral>]
type IMeta =
    abstract meta: unit -> IPersistentMap

[<AllowNullLiteral>]
type IObj =
    inherit IMeta
    abstract withMeta: meta: IPersistentMap -> IObj

type IDeref =
    abstract deref: unit -> obj


[<AllowNullLiteral>]
type IFn =
    abstract invoke: unit -> obj
    abstract invoke: arg1: obj -> obj
    abstract invoke: arg1: obj * arg2: obj -> obj
    abstract invoke: arg1: obj * arg2: obj * arg3: obj -> obj
    abstract invoke: arg1: obj * arg2: obj * arg3: obj * arg4: obj -> obj
    abstract invoke: arg1: obj * arg2: obj * arg3: obj * arg4: obj * arg5: obj -> obj
    abstract invoke: arg1: obj * arg2: obj * arg3: obj * arg4: obj * arg5: obj * arg6: obj -> obj
    abstract invoke: arg1: obj * arg2: obj * arg3: obj * arg4: obj * arg5: obj * arg6: obj * arg7: obj -> obj

    abstract invoke:
        arg1: obj * arg2: obj * arg3: obj * arg4: obj * arg5: obj * arg6: obj * arg7: obj * arg8: obj -> obj

    abstract invoke:
        arg1: obj * arg2: obj * arg3: obj * arg4: obj * arg5: obj * arg6: obj * arg7: obj * arg8: obj * arg9: obj -> obj

    abstract invoke:
        arg1: obj *
        arg2: obj *
        arg3: obj *
        arg4: obj *
        arg5: obj *
        arg6: obj *
        arg7: obj *
        arg8: obj *
        arg9: obj *
        arg10: obj ->
            obj

    abstract invoke:
        arg1: obj *
        arg2: obj *
        arg3: obj *
        arg4: obj *
        arg5: obj *
        arg6: obj *
        arg7: obj *
        arg8: obj *
        arg9: obj *
        arg10: obj *
        arg11: obj ->
            obj

    abstract invoke:
        arg1: obj *
        arg2: obj *
        arg3: obj *
        arg4: obj *
        arg5: obj *
        arg6: obj *
        arg7: obj *
        arg8: obj *
        arg9: obj *
        arg10: obj *
        arg11: obj *
        arg12: obj ->
            obj

    abstract invoke:
        arg1: obj *
        arg2: obj *
        arg3: obj *
        arg4: obj *
        arg5: obj *
        arg6: obj *
        arg7: obj *
        arg8: obj *
        arg9: obj *
        arg10: obj *
        arg11: obj *
        arg12: obj *
        arg13: obj ->
            obj

    abstract invoke:
        arg1: obj *
        arg2: obj *
        arg3: obj *
        arg4: obj *
        arg5: obj *
        arg6: obj *
        arg7: obj *
        arg8: obj *
        arg9: obj *
        arg10: obj *
        arg11: obj *
        arg12: obj *
        arg13: obj *
        arg14: obj ->
            obj

    abstract invoke:
        arg1: obj *
        arg2: obj *
        arg3: obj *
        arg4: obj *
        arg5: obj *
        arg6: obj *
        arg7: obj *
        arg8: obj *
        arg9: obj *
        arg10: obj *
        arg11: obj *
        arg12: obj *
        arg13: obj *
        arg14: obj *
        arg15: obj ->
            obj

    abstract invoke:
        arg1: obj *
        arg2: obj *
        arg3: obj *
        arg4: obj *
        arg5: obj *
        arg6: obj *
        arg7: obj *
        arg8: obj *
        arg9: obj *
        arg10: obj *
        arg11: obj *
        arg12: obj *
        arg13: obj *
        arg14: obj *
        arg15: obj *
        arg16: obj ->
            obj

    abstract invoke:
        arg1: obj *
        arg2: obj *
        arg3: obj *
        arg4: obj *
        arg5: obj *
        arg6: obj *
        arg7: obj *
        arg8: obj *
        arg9: obj *
        arg10: obj *
        arg11: obj *
        arg12: obj *
        arg13: obj *
        arg14: obj *
        arg15: obj *
        arg16: obj *
        arg17: obj ->
            obj

    abstract invoke:
        arg1: obj *
        arg2: obj *
        arg3: obj *
        arg4: obj *
        arg5: obj *
        arg6: obj *
        arg7: obj *
        arg8: obj *
        arg9: obj *
        arg10: obj *
        arg11: obj *
        arg12: obj *
        arg13: obj *
        arg14: obj *
        arg15: obj *
        arg16: obj *
        arg17: obj *
        arg18: obj ->
            obj

    abstract invoke:
        arg1: obj *
        arg2: obj *
        arg3: obj *
        arg4: obj *
        arg5: obj *
        arg6: obj *
        arg7: obj *
        arg8: obj *
        arg9: obj *
        arg10: obj *
        arg11: obj *
        arg12: obj *
        arg13: obj *
        arg14: obj *
        arg15: obj *
        arg16: obj *
        arg17: obj *
        arg18: obj *
        arg19: obj ->
            obj

    abstract invoke:
        arg1: obj *
        arg2: obj *
        arg3: obj *
        arg4: obj *
        arg5: obj *
        arg6: obj *
        arg7: obj *
        arg8: obj *
        arg9: obj *
        arg10: obj *
        arg11: obj *
        arg12: obj *
        arg13: obj *
        arg14: obj *
        arg15: obj *
        arg16: obj *
        arg17: obj *
        arg18: obj *
        arg19: obj *
        arg20: obj ->
            obj

    abstract invoke:
        arg1: obj *
        arg2: obj *
        arg3: obj *
        arg4: obj *
        arg5: obj *
        arg6: obj *
        arg7: obj *
        arg8: obj *
        arg9: obj *
        arg10: obj *
        arg11: obj *
        arg12: obj *
        arg13: obj *
        arg14: obj *
        arg15: obj *
        arg16: obj *
        arg17: obj *
        arg18: obj *
        arg19: obj *
        arg20: obj *
        [<ParamArray>] args: obj array ->
            obj



[<AllowNullLiteral>]
type IReduceInit =
    abstract reduce: IFn * obj -> obj

[<AllowNullLiteral>]
type IReduce =
    inherit IReduceInit
    abstract reduce: IFn -> obj

[<AllowNullLiteral>]
type IKVReduce =
    abstract kvreduce: f: IFn * inti: obj -> obj

[<AllowNullLiteral>]
type IPending =
    abstract isRealized: unit -> bool

[<AllowNullLiteral>]
type Named =
    abstract getNamespace: unit -> string
    abstract getName: unit -> string


type MapEquivalence =
    interface
    end

[<AllowNullLiteral>]
type IMapEnumerable =
    abstract keyEnumerator: unit -> IEnumerator
    abstract valEnumerator: unit -> IEnumerator


// The transients

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
type ITransientMap =
    inherit ITransientAssociative
    inherit Counted
    abstract assoc: key: obj * value: obj -> ITransientMap
    abstract without: key: obj -> ITransientMap
    abstract persistent: unit -> IPersistentMap

[<AllowNullLiteral>]
type ITransientSet =
    inherit ITransientCollection
    inherit Counted
    abstract disjoin: key: obj -> ITransientSet
    abstract contains: key: obj -> bool
    abstract get: key: obj -> obj

[<AllowNullLiteral>]
type ITransientVector =
    inherit ITransientAssociative
    inherit Indexed
    abstract assocN: idx: int * value: obj -> ITransientVector
    abstract pop: unit -> ITransientVector

[<AllowNullLiteral>]
type IEditableCollection =
    abstract asTransient: unit -> ITransientCollection

type IDrop =
    abstract drop: n: int -> Sequential
