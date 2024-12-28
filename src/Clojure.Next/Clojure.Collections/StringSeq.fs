namespace Clojure.Collections

[<AllowNullLiteral>]
type StringSeq(meta: IPersistentMap, s: string, index: int) =
    inherit ASeq(meta)

    member this.S = s
    member this.I = index

    static member create(s: string) =
        if isNull s || s.Length = 0 then
            null
        else
            StringSeq(null, s, 0)

    interface ISeq with
        override _.first() = s[index]

        override _.next() =
            if index + 1 < s.Length then
                StringSeq(meta, s, index + 1)
            else
                null

    interface Counted with
        override _.count() =
            if index < s.Length then s.Length-index else 0

    interface IObj with
        override this.withMeta(m) = if m = meta then this else StringSeq(m,s,index)

    interface IndexedSeq with
        member _.index() = index

    interface IDrop with
        member _.drop(n) =
            let ii = index+n
            if ii < s.Length then
                StringSeq(meta,s,ii)
            else
                null

    interface IReduceInit with
        member _.reduce(f,start) =
            let rec loop (acc:obj) (i:int) =
                if i >= s.Length then
                    acc
                else
                   match f.invoke(acc,s[i]) with
                   | :? Reduced as red -> (red:>IDeref).deref()
                   | nextAcc -> loop nextAcc (i+1)
            loop start index
