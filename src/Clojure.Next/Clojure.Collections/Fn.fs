namespace Clojure.Collections

open System



// In ClojureJVM, this would also implement Callable and Runnable -- no exact equivalent here -- shoule we look at Func<>? ThreadDelegate?
[<AllowNullLiteral>]
type IFnArity =
    abstract hasArity: arity: int -> bool

type ArityException(actual0: int, name0: string, cause0: Exception) =
    inherit ArgumentException((sprintf "Wrong number of args(%i) passed to: %s" actual0 name0), cause0) // TODO: Should use Compiler.demunge(name) in sprintf
    member val name = name0
    member val actual = actual0
    new() = ArityException(-1, "<Unknown>", null)
    new(actual: int, name: string) = ArityException(actual, name, null)


[<AbstractClass; AllowNullLiteral>]
type AFn() =
    interface IFnArity with
        member _.hasArity(arity: int) : bool = false

    // This was in RT.  But only used in AFn and RestFn, so moving to here.
    static member boundedLength(list: ISeq, limit: int) : int =
        let rec loop (c: ISeq) i =
            if c <> null && i <= limit then
                loop (c.next ()) (i + 1)
            else
                i

        loop list 0


    // This was in RT.  Should be in Helpers.  TODO: Maybe split Helpers?  It is used in a few other places in the code
    static member seqLength(list: ISeq) : int =
        let rec loop (c: ISeq) i =
            if c <> null then loop (c.next ()) (i + 1) else i

        loop list 0


    // This was in RT.  But only used in RestFn, so moving to here.
    static member seqToArray<'a>(xs: ISeq) : 'a array =
        if xs = null then
            Array.zeroCreate (0)
        else
            let a = Array.zeroCreate<'a> (AFn.seqLength xs)

            let rec loop (s: ISeq) i =
                if s <> null then
                    a.[i] <- downcast s.first ()
                    loop (s.next ()) (i + 1)
                else
                    ()

            loop xs 0
            a

    member this.WrongArityException(reqArity: int) : ArityException =
        ArityException(reqArity, this.GetType().FullName)

    interface IFn with
        member this.invoke() = raise <| this.WrongArityException(0)
        member this.invoke(a1) = raise <| this.WrongArityException(1)
        member this.invoke(a1, a2) = raise <| this.WrongArityException(2)
        member this.invoke(a1, a2, a3) = raise <| this.WrongArityException(3)
        member this.invoke(a1, a2, a3, a4) = raise <| this.WrongArityException(4)
        member this.invoke(a1, a2, a3, a4, a5) = raise <| this.WrongArityException(5)
        member this.invoke(a1, a2, a3, a4, a5, a6) = raise <| this.WrongArityException(6)
        member this.invoke(a1, a2, a3, a4, a5, a6, a7) = raise <| this.WrongArityException(7)
        member this.invoke(a1, a2, a3, a4, a5, a6, a7, a8) = raise <| this.WrongArityException(8)
        member this.invoke(a1, a2, a3, a4, a5, a6, a7, a8, a9) = raise <| this.WrongArityException(9)
        member this.invoke(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10) = raise <| this.WrongArityException(10)
        member this.invoke(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11) = raise <| this.WrongArityException(11)
        member this.invoke(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12) = raise <| this.WrongArityException(12)

        member this.invoke(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13) =
            raise <| this.WrongArityException(13)

        member this.invoke(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13, a14) =
            raise <| this.WrongArityException(14)

        member this.invoke(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13, a14, a15) =
            raise <| this.WrongArityException(15)

        member this.invoke(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13, a14, a15, a16) =
            raise <| this.WrongArityException(16)

        member this.invoke(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13, a14, a15, a16, a17) =
            raise <| this.WrongArityException(17)

        member this.invoke(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13, a14, a15, a16, a17, a18) =
            raise <| this.WrongArityException(18)

        member this.invoke(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13, a14, a15, a16, a17, a18, a19) =
            raise <| this.WrongArityException(19)

        member this.invoke(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13, a14, a15, a16, a17, a18, a19, a20) =
            raise <| this.WrongArityException(20)

        member this.invoke
            (
                a1,
                a2,
                a3,
                a4,
                a5,
                a6,
                a7,
                a8,
                a9,
                a10,
                a11,
                a12,
                a13,
                a14,
                a15,
                a16,
                a17,
                a18,
                a19,
                a20,
                [<ParamArray>] args
            ) =
            raise <| this.WrongArityException(21)

    // TODO: Check to see if the original use of Util1.Ret is necessary.

    static member applyToHelper(fn: IFn, argList: ISeq) =

        let mutable al = argList

        let n () =
            al <- al.next ()
            al

        match AFn.boundedLength (argList, 20) with
        | 0 -> fn.invoke ()
        | 1 -> fn.invoke (al.first ())
        | 2 -> fn.invoke (al.first (), al.next().first ())
        | 3 -> fn.invoke (al.first (), n().first (), n().first ())
        | 4 -> fn.invoke (al.first (), n().first (), n().first (), n().first ())
        | 5 -> fn.invoke (al.first (), n().first (), n().first (), n().first (), n().first ())
        | 6 -> fn.invoke (al.first (), n().first (), n().first (), n().first (), n().first (), n().first ())
        | 7 ->
            fn.invoke (al.first (), n().first (), n().first (), n().first (), n().first (), n().first (), n().first ())
        | 8 ->
            fn.invoke (
                al.first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first ()
            )
        | 9 ->
            fn.invoke (
                al.first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first ()
            )
        | 10 ->
            fn.invoke (
                al.first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first ()
            )
        | 11 ->
            fn.invoke (
                al.first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first ()
            )
        | 12 ->
            fn.invoke (
                al.first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first ()
            )
        | 13 ->
            fn.invoke (
                al.first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first ()
            )
        | 14 ->
            fn.invoke (
                al.first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first ()
            )
        | 15 ->
            fn.invoke (
                al.first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first ()
            )
        | 16 ->
            fn.invoke (
                al.first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first ()
            )
        | 17 ->
            fn.invoke (
                al.first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first ()
            )
        | 18 ->
            fn.invoke (
                al.first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first ()
            )
        | 19 ->
            fn.invoke (
                al.first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first ()
            )
        | 20 ->
            fn.invoke (
                al.first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first ()
            )
        | _ ->
            fn.invoke (
                al.first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                n().first (),
                AFn.seqToArray (al.next ())
            )


    abstract applyTo: arglist: ISeq -> obj
    default this.applyTo arglist = AFn.applyToHelper (this, arglist)

// TODO: Do we need the implementation of IDynamicMetaObjectProvide.GetMetaObject?
