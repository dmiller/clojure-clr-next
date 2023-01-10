namespace Clojure.Collections

type SimpleCons(head: obj, tail: ISeq) =

    interface ISeq with
        member _.first() = head
        member this.next() = (this :> ISeq).more().seq()

        member _.more() =
            if isNull tail then upcast SimpleEmptySeq() else tail

        member this.cons(o) = upcast SimpleCons(o, this)

    interface IPersistentCollection with
        member _.count() = 1 + Util.seqCount tail
        member this.cons(o) = upcast (this :> ISeq).cons(o)
        member _.empty() = upcast SimpleEmptySeq()

        member this.equiv(o) =
            match o with
            | :? Seqable as s -> Util.seqEquiv (this :> ISeq) (s.seq ())
            | _ -> false

    interface Seqable with
        member this.seq() = upcast this

    override this.Equals(o) =
        match o with
        | :? Seqable as s -> Util.seqEquals (this :> ISeq) (s.seq ())
        | _ -> false

    override this.GetHashCode() = Util.getHashCode this

    override this.ToString() = Util.seqToString this


and SimpleEmptySeq() =

    interface ISeq with
        member _.first() = null
        member _.next() = null
        member this.more() = upcast this
        member this.cons(o) = upcast SimpleCons(o, this)

    interface IPersistentCollection with
        member _.count() = 0
        member this.cons(o) = upcast (this :> ISeq).cons(o)
        member this.empty() = upcast this
        member this.equiv(o) = this.Equals(o)

    interface Seqable with
        member x.seq() = null

    override x.Equals(o) =
        match o with
        | :? Seqable as s -> s.seq () |> isNull
        | _ -> false

    override x.GetHashCode() = 1

    override x.ToString() = "()"


