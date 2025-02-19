namespace Clojure.Collections

/// An ISeq iterating over the characters of a string.
[<AllowNullLiteral>]
type StringSeq(meta: IPersistentMap, _string: string, _index: int) =
    inherit ASeq(meta)


    static member create(s: string) =
        if isNull s || s.Length = 0 then
            null
        else
            StringSeq(null, s, 0)

    interface ISeq with
        override _.first() = _string[_index]

        override _.next() =
            if _index + 1 < _string.Length then
                StringSeq(meta, _string, _index + 1)
            else
                null

    interface Counted with
        override _.count() =
            if _index < _string.Length then
                _string.Length - _index
            else
                0

    interface IObj with
        override this.withMeta(m) =
            if m = meta then this else StringSeq(m, _string, _index)

    interface IndexedSeq with
        member _.index() = _index

    interface IDrop with
        member _.drop(n) =
            let ii = _index + n

            if ii < _string.Length then
                StringSeq(meta, _string, ii)
            else
                null

    interface IReduceInit with
        member _.reduce(f, start) =
            let rec loop (acc: obj) (i: int) =
                if i >= _string.Length then
                    acc
                else
                    match f.invoke (acc, _string[i]) with
                    | :? Reduced as red -> (red :> IDeref).deref ()
                    | nextAcc -> loop nextAcc (i + 1)

            loop start _index
