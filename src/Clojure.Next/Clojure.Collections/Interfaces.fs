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


// And everything else


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
type IndexedSeq =
    inherit ISeq
    inherit Sequential
    inherit Counted
    abstract index: unit -> int

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
    abstract count: unit -> int 

[<AllowNullLiteral>]
type IPersistentSet =
    inherit IPersistentCollection
    inherit Counted
    abstract disjoin: key: obj -> IPersistentSet
    abstract contains: key: obj -> bool
    abstract get: key: obj -> obj
    abstract count: unit -> int

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
type IMeta =
    abstract meta: unit -> IPersistentMap

[<AllowNullLiteral>]
type IObj =
    inherit IMeta
    abstract withMeta: meta: IPersistentMap -> IObj

[<AllowNullLiteral>]
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

    abstract applyTo : ISeq -> obj 

[<AllowNullLiteral>]
type IChunk =
    inherit Indexed
    abstract dropFirst: unit -> IChunk
    abstract reduce: f: IFn * start: obj -> obj

[<AllowNullLiteral>]
type IChunkedSeq =
    inherit ISeq
    inherit Sequential
    abstract chunkedFirst: unit -> IChunk
    abstract chunkedNext: unit -> ISeq
    abstract chunkedMore: unit -> ISeq

[<AllowNullLiteral>]
type IReduceInit =
    abstract reduce: IFn * obj -> obj

[<AllowNullLiteral>]
type IReduce =
    inherit IReduceInit
    abstract reduce: IFn -> obj

[<AllowNullLiteral>]
type IKVReduce =
    abstract kvreduce: f: IFn * init: obj -> obj

[<AllowNullLiteral>]
type IPending =
    abstract isRealized: unit -> bool

[<AllowNullLiteral>]
type Named =
    abstract getNamespace: unit -> string
    abstract getName: unit -> string

[<AllowNullLiteral>]
type MapEquivalence =
    interface
    end

[<AllowNullLiteral>]
type IMapEnumerable =
    abstract keyEnumerator: unit -> IEnumerator
    abstract valEnumerator: unit -> IEnumerator

[<AllowNullLiteral>]
type IMapEnumerableTyped<'t1,'t2> =
    abstract tkeyEnumerator: unit -> IEnumerator<'t1>
    abstract tvalEnumerator: unit -> IEnumerator<'t2>


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

[<AllowNullLiteral>]
type IDrop =
    abstract drop: n: int -> Sequential

[<AllowNullLiteral>]
type Sorted =
    abstract comparator: unit -> IComparer
    abstract entryKey: entry:obj -> obj
    abstract seq: ascending:bool -> ISeq
    abstract seqFrom: key:obj * ascending:bool -> ISeq



// References and related

// Represents an object with settable metadata.
[<AllowNullLiteral>]
type IReference =
    inherit IMeta
    abstract alterMeta: alter: IFn * args: ISeq -> IPersistentMap
    abstract resetMeta: m: IPersistentMap -> IPersistentMap

// Represents a reference to a value.
// IRef is the basic interface supported by Refs, Agents, Atoms, Vars, and other references to values.
// This interface supports getting/setting the validator for the value, and getting/setting watchers.
// Dereferencing is supplied in interface IDeref.
// This interface does not support changes to values. Changes are the responsibility of the implementations of this interface,
// and often have to be done in concert with LockingTransaction.
// The validator function will be applied to any new value before that value is applied.
// If the validator throws an exception or returns false, changing the reference to the new value is aborted.
// When setting a new validator, it must validate the current value.
// A reference can be watched by one or more Agents. The agent will be sent a message when the value changes.

[<AllowNullLiteral>]
type IRef =
    inherit IDeref
    abstract setValidator: vf: IFn -> unit
    abstract getValidator: unit -> IFn
    abstract getWatches: unit -> IPersistentMap
    abstract addWatch: key: obj * callback: IFn -> IRef
    abstract removeWatch: key: obj -> IRef


// Represents an object with a value that can be set.
// The only class that has this interface at present if Var.
// Hence the method 'doReset' that seets the 'root' value.

[<AllowNullLiteral>]
type Settable =
    abstract doSet: value: obj -> obj
    abstract doReset: value: obj -> obj




(*

*)