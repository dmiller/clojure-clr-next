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

    let listStar1(arg1, rest) = cons(arg1, rest)
    let listStar2(arg1, arg2, rest) = cons(arg1, cons(arg2, rest))
    let listStar3(arg1, arg2, arg3, rest) = cons(arg1, cons(arg2, cons(arg3, rest)))
    let listStar4(arg1, arg2, arg3, arg4, rest) = cons(arg1, cons(arg2, cons(arg3, cons(arg4, rest))))
    let listStar5(arg1, arg2, arg3, arg4, arg5, rest) = cons(arg1, cons(arg2, cons(arg3, cons(arg4, cons(arg5, rest)))))

    let list0() = null
    let list1(arg1) = PersistentList(arg1)
    let list2(arg1, arg2) = listStar2(arg1,arg2,null)
    let list3(arg1, arg2, arg3) = listStar3(arg1,arg2,arg3,null)
    let list4(arg1, arg2, arg3, arg4) = listStar4(arg1,arg2,arg3,arg4,null)
    let list5(arg1, arg2, arg3, arg4, arg5) = listStar5(arg1,arg2,arg3,arg4,arg5,null)

    let assoc(coll:obj, key:obj, value: obj) =
        match coll with
        | null -> coll
        | _ -> (coll :?> IPersistentMap).assoc(key,value)

    let dissoc(coll:obj, key:obj) =
        match coll with
        | null -> coll
        | _ -> (coll :?> IPersistentMap).without(key)


(*


        public static ISeq list()
        {
            return null;
        }


        public static ISeq list(object arg1)
        {
            return new PersistentList(arg1);
        }


        public static ISeq list(object arg1, object arg2)
        {
            return listStar(arg1, arg2, null);
        }


        public static ISeq list(object arg1, object arg2, object arg3)
        {
            return listStar(arg1, arg2, arg3, null);
        }


        public static ISeq list(object arg1, object arg2, object arg3, object arg4)
        {
            return listStar(arg1, arg2, arg3, arg4, null);
        }


        public static ISeq list(object arg1, object arg2, object arg3, object arg4, object arg5)
        {
            return listStar(arg1, arg2, arg3, arg4, arg5, null);
        }




        public static ISeq listStar(object arg1, ISeq rest)
        {
            return cons(arg1, rest);
        }


        public static ISeq listStar(object arg1, object arg2, ISeq rest)
        {
            return cons(arg1, cons(arg2, rest));
        }


        public static ISeq listStar(object arg1, object arg2, object arg3, ISeq rest)
        {
            return cons(arg1, cons(arg2, cons(arg3, rest)));
        }


        public static ISeq listStar(object arg1, object arg2, object arg3, object arg4, ISeq rest)
        {
            return cons(arg1, cons(arg2, cons(arg3, cons(arg4, rest))));
        }


        public static ISeq listStar(object arg1, object arg2, object arg3, object arg4, object arg5, ISeq rest)
        {
            return cons(arg1, cons(arg2, cons(arg3, cons(arg4, cons(arg5, rest)))));
        }


*)

