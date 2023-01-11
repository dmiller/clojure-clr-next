namespace Clojure.Collections.Simple

open Clojure.Collections


[<AllowNullLiteral>]
type SimpleIntRange(startVal: int, endVal: int) =

    interface ISeq with
        member _.first() = upcast startVal
        member this.next() = (this :> ISeq).more().seq ()

        member this.more() =
            if startVal = endVal then
                upcast SimpleEmptySeq()
            else
                upcast SimpleIntRange(startVal + 1, endVal)

        member this.cons(o) = SimpleCons(o, (this :> ISeq)) :> ISeq

    interface IPersistentCollection with
        member _.count() = endVal - startVal + 1
        member this.cons(o) = upcast (this :> ISeq).cons (o)
        member _.empty() = upcast SimpleEmptySeq()

        member this.equiv(o) =
            match o with
            | :? Seqable as s -> Util.seqEquiv (this :> ISeq) (s.seq ())
            | _ -> false

    interface Seqable with
        member this.seq() = (this :> ISeq)

    override this.Equals(o) =
        match o with
        | :? Seqable as s -> Util.seqEquals (this :> ISeq) (s.seq ())
        | _ -> false

    override this.GetHashCode() = Util.getHashCode this

    override this.ToString() = Util.seqToString this
