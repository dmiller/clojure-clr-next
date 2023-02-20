module Clojure.Collections.RTSeq

// Some of the sequence functions from the original RT that need only PersistentList and Cons


    let cons (x: obj, coll: obj) : ISeq =
        match coll with
        | null -> upcast PersistentList(x)
        | :? ISeq as s -> upcast Cons(x, s)
        | _ -> upcast Cons(x, RT0.seq (coll))


    let meta (x:obj) = 
        match x with
        | :? IMeta as m -> m.meta()
        | _ -> null

    let conj(coll:IPersistentCollection, x:obj) : IPersistentCollection =
        match coll with
        | null -> PersistentList(x)
        | _ -> coll.cons(x)


    let next(x:obj) =
        let seq =
            match x with
            | :? ISeq as s -> s
            | _ -> RT0.seq(x)
        match seq with
        | null -> null
        | _ -> seq.next()

    let more(x:obj) =
        let seq =
            match x with
            | :? ISeq as s -> s
            | _ -> RT0.seq(x)
        match seq with
        | null -> null
        | _ -> seq.more()

    let first(x:obj) =
        let seq =
            match x with
            | :? ISeq as s -> s
            | _ -> RT0.seq(x)

        match seq with
        | null -> null
        | _ -> seq.first()


    let second(x:obj) = first(next(x))
    let third(x:obj) = first(next(next(x)))
    let fourth(x:obj) = first(next(next(next(x))))

    let peek(x:obj) = 
        match x with
        | null -> null
        | _ -> (x :?> IPersistentStack).peek()

    let pop(x:obj) = 
        match x with
        | null -> null
        | _ -> (x :?> IPersistentStack).pop()



