namespace Clojure.Collections

open System.Text

module Util =

    let checkEquals o1 o2 =
        obj.ReferenceEquals(o1, o2) || not (isNull o1) && o1.Equals(o2)

    let rec seqEquals (s1: ISeq) (s2: ISeq) =
        match s1, s2 with
        | null, null -> true
        | null, _ -> false
        | _, null -> false
        | _ -> checkEquals (s1.first ()) (s2.first ()) && seqEquals (s1.next ()) (s2.next ())

    let seqEquiv s1 s2 = seqEquals s1 s2

    let seqCount (s: ISeq) =
        let rec step (s: ISeq) cnt =
            if isNull s then cnt else step (s.next ()) (cnt + 1)

        step s 0

    let getHashCode (s: ISeq) =
        let combine hc x =
            31 * hc + if isNull x then 0 else x.GetHashCode()

        let rec step (s: ISeq) hc =
            if isNull s then
                hc
            else
                step (s.next ()) (combine hc (s.first ()))

        step s 1

    let rec seqToString (s: ISeq) =
        let sb = new StringBuilder()

        let rec appendItem (o: obj) =
            match o with
            | :? Seqable as s -> appendSeq (s.seq ())
            | _ -> sb.Append(o.ToString()) |> ignore

        and appendSeq (s: ISeq) =
            match s with
            | null -> ()
            | _ ->
                appendItem (s.first ())
                sb.Append(" ") |> ignore
                appendSeq (s.next ())

        sb.Append("(") |> ignore
        appendSeq s
        sb.Append(")") |> ignore
        sb.ToString()
