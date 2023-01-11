namespace Clojure.Collections.Simple

open Clojure.Collections


type SimpleStringSeq private (index : int, source : string) =

    static member create (source : string) : ISeq =
        match source.Length with
        | 0 -> null
        | _ -> upcast SimpleStringSeq(0,source)

    interface Seqable with
        member this.seq() = (this :> ISeq)


    interface IPersistentCollection with
        member _.count() = 
            if index < source.Length then source.Length-index else 0

        member this.cons(o) = upcast (this :> ISeq).cons (o)
        member _.empty() = upcast SimpleEmptySeq()

        member this.equiv(o) =
            match o with
            | :? Seqable as s -> Util.seqEquiv (this :> ISeq) (s.seq ())
            | _ -> false

    interface ISeq with
        member _.first() = upcast source[index]
        member this.next() =
            if index + 1 < source.Length then
                SimpleStringSeq(index+1,source)
             else
                null
            

        member this.more() =
            let s = (this :> ISeq).next()
            if isNull s then SimpleEmptySeq()
            else s


        member this.cons(o) = SimpleCons(o, (this :> ISeq)) :> ISeq