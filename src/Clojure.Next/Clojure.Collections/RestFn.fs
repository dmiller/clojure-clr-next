namespace Clojure.Collections

//open System
//open Clojure.Collections

//[<AbstractClass>]
//type RestFn() = 
//    inherit AFunction()

//    abstract getRequiredArity() : unit -> int

//    static member ontoArrayPrepend(arr : obj array, [<ParamArray>] args: obj array) =
//        let mutable ret : ISeq = ArraySeq.create(arr)
//        for i = args.Length downto 0 do
//            ret <- RTSeq.cons(args.[i],ret)
//        ret

//    abstract doInvoke : obj -> obj
//    default _.doInvoke(args:obj ) : obj = null
//    abstract _.doInvoke : obj * obj -> obj
//    default _.doInvoke(arg1: obj, args:obj ) : obj = null
//    abstract _.doInvoke : obj * obj * obj -> obj
//    default _.doInvoke(arg1: obj, arg2: obj, args:obj ) : obj = null
//    abstract _.doInvoke : obj * obj * obj * obj -> obj
//    default _.doInvoke(arg1: obj, arg2: obj, arg3: obj, args:obj ) : obj = null
//    abstract _.doInvoke : obj * obj * obj * obj * obj -> obj
//    default _.doInvoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, args:obj ) : obj = null
//    abstract _.doInvoke : obj * obj * obj * obj * obj * obj -> obj
//    default _.doInvoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, args:obj ) : obj = null
//    abstract _.doInvoke : obj * obj * obj * obj * obj * obj * obj -> obj
//    default _.doInvoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, args:obj ) : obj = null
//    abstract _.doInvoke : obj * obj * obj * obj * obj * obj * obj * obj -> obj
//    default _.doInvoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, args:obj ) : obj = null
//    abstract _.doInvoke : obj * obj * obj * obj * obj * obj * obj * obj * obj -> obj
//    default _.doInvoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj, args:obj ) : obj = null
//    abstract _.doInvoke : obj * obj * obj * obj * obj * obj * obj * obj * obj * obj -> obj
//    default _.doInvoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj, arg9: obj, args:obj ) : obj = null
//    abstract _.doInvoke : obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj -> obj
//    default _.doInvoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj, arg9: obj, arg10: obj, args:obj ) : obj = null
//    abstract _.doInvoke : obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj -> obj
//    default _.doInvoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj, arg9: obj, arg10: obj, arg11: obj, args:obj ) : obj = null
//    abstract _.doInvoke : obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj -> obj
//    default _.doInvoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj, arg9: obj, arg10: obj, arg11: obj, arg12: obj, args:obj ) : obj = null
//    abstract _.doInvoke : obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj -> obj
//    default _.doInvoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj, arg9: obj, arg10: obj, arg11: obj, arg12: obj, arg13: obj, args:obj ) : obj = null
//    abstract _.doInvoke : obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj -> obj
//    default _.doInvoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj, arg9: obj, arg10: obj, arg11: obj, arg12: obj, arg13: obj, arg14: obj, args:obj ) : obj = null
//    abstract _.doInvoke : obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj -> obj
//    default _.doInvoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj, arg9: obj, arg10: obj, arg11: obj, arg12: obj, arg13: obj, arg14: obj, arg15: obj, args:obj ) : obj = null
//    abstract _.doInvoke : obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj -> obj
//    default _.doInvoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj, arg9: obj, arg10: obj, arg11: obj, arg12: obj, arg13: obj, arg14: obj, arg15: obj, arg16: obj, args:obj ) : obj = null
//    abstract _.doInvoke : obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj -> obj
//    default _.doInvoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj, arg9: obj, arg10: obj, arg11: obj, arg12: obj, arg13: obj, arg14: obj, arg15: obj, arg16: obj, arg17: obj, args:obj ) : obj = null
//    abstract _.doInvoke : obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj -> obj
//    default _.doInvoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj, arg9: obj, arg10: obj, arg11: obj, arg12: obj, arg13: obj, arg14: obj, arg15: obj, arg16: obj, arg17: obj, arg18: obj, args:obj ) : obj = null
//    abstract _.doInvoke : obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj -> obj
//    default _.doInvoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj, arg9: obj, arg10: obj, arg11: obj, arg12: obj, arg13: obj, arg14: obj, arg15: obj, arg16: obj, arg17: obj, arg18: obj, arg19: obj, args:obj ) : obj = null
//    abstract _.doInvoke : obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj * obj -> obj
//    default _.doInvoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj, arg9: obj, arg10: obj, arg11: obj, arg12: obj, arg13: obj, arg14: obj, arg15: obj, arg16: obj, arg17: obj, arg18: obj, arg19: obj, arg20: obj, args:obj ) : obj = null

//    override x.applyTo (argList:ISeq) : obj =
//        let reqArity = x.getRequiredArity()
//        if AFn.boundedLength(argList,reqArity) <= reqArity
//        then AFn.ApplyToHelper(x,argList)
//        else
//            let mutable al = argList
//            let n() = al <- al.next(); al
//            match reqArity with
//            | 0 -> x.doInvoke( al )
//            | 1 -> x.doInvoke( arglist.first(), al.next() )
//            | 2 -> x.doInvoke( arglist.first(), nl().first(), al.next() )
//            | 3 -> x.doInvoke( arglist.first(), nl().first(), nl().first(), al.next() )
//            | 4 -> x.doInvoke( arglist.first(), nl().first(), nl().first(), nl().first(), al.next() )
//            | 5 -> x.doInvoke( arglist.first(), nl().first(), nl().first(), nl().first(), nl().first(), al.next() )
//            | 6 -> x.doInvoke( arglist.first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), al.next() )
//            | 7 -> x.doInvoke( arglist.first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), al.next() )
//            | 8 -> x.doInvoke( arglist.first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), al.next() )
//            | 9 -> x.doInvoke( arglist.first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), al.next() )
//            | 10 -> x.doInvoke( arglist.first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), al.next() )
//            | 11 -> x.doInvoke( arglist.first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), al.next() )
//            | 12 -> x.doInvoke( arglist.first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), al.next() )
//            | 13 -> x.doInvoke( arglist.first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), al.next() )
//            | 14 -> x.doInvoke( arglist.first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), al.next() )
//            | 15 -> x.doInvoke( arglist.first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), al.next() )
//            | 16 -> x.doInvoke( arglist.first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), al.next() )
//            | 17 -> x.doInvoke( arglist.first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), al.next() )
//            | 18 -> x.doInvoke( arglist.first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), al.next() )
//            | 19 -> x.doInvoke( arglist.first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), al.next() )
//            | 20 -> x.doInvoke( arglist.first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), nl().first(), al.next() )
//            | _ -> raise <| WrongArityException(-1)    
//    override x.invoke() = 
//        match x.getRequiredArity() with
//        | 0 -> doInvoke(null)
//        | _ -> raise <| WrongArityException(0)
//    override x.invoke(arg1: obj) = 
//        match x.getRequiredArity() with
//        | 0 -> doInvoke(ArraySeq.create(arg1))
//        | 1 -> doInvoke(arg1, null)
//        | _ -> raise <| WrongArityException(1)
//    override x.invoke(arg1: obj, arg2: obj) = 
//        match x.getRequiredArity() with
//        | 0 -> doInvoke(ArraySeq.create(arg1, arg2))
//        | 1 -> doInvoke(arg1, ArraySeq.create(arg2))
//        | 2 -> doInvoke(arg1, arg2, null)
//        | _ -> raise <| WrongArityException(2)
//    override x.invoke(arg1: obj, arg2: obj, arg3: obj) = 
//        match x.getRequiredArity() with
//        | 0 -> doInvoke(ArraySeq.create(arg1, arg2, arg3))
//        | 1 -> doInvoke(arg1, ArraySeq.create(arg2, arg3))
//        | 2 -> doInvoke(arg1, arg2, ArraySeq.create(arg3))
//        | 3 -> doInvoke(arg1, arg2, arg3, null)
//        | _ -> raise <| WrongArityException(3)
//    override x.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj) = 
//        match x.getRequiredArity() with
//        | 0 -> doInvoke(ArraySeq.create(arg1, arg2, arg3, arg4))
//        | 1 -> doInvoke(arg1, ArraySeq.create(arg2, arg3, arg4))
//        | 2 -> doInvoke(arg1, arg2, ArraySeq.create(arg3, arg4))
//        | 3 -> doInvoke(arg1, arg2, arg3, ArraySeq.create(arg4))
//        | 4 -> doInvoke(arg1, arg2, arg3, arg4, null)
//        | _ -> raise <| WrongArityException(4)
//    override x.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj) = 
//        match x.getRequiredArity() with
//        | 0 -> doInvoke(ArraySeq.create(arg1, arg2, arg3, arg4, arg5))
//        | 1 -> doInvoke(arg1, ArraySeq.create(arg2, arg3, arg4, arg5))
//        | 2 -> doInvoke(arg1, arg2, ArraySeq.create(arg3, arg4, arg5))
//        | 3 -> doInvoke(arg1, arg2, arg3, ArraySeq.create(arg4, arg5))
//        | 4 -> doInvoke(arg1, arg2, arg3, arg4, ArraySeq.create(arg5))
//        | 5 -> doInvoke(arg1, arg2, arg3, arg4, arg5, null)
//        | _ -> raise <| WrongArityException(5)
//    override x.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj) = 
//        match x.getRequiredArity() with
//        | 0 -> doInvoke(ArraySeq.create(arg1, arg2, arg3, arg4, arg5, arg6))
//        | 1 -> doInvoke(arg1, ArraySeq.create(arg2, arg3, arg4, arg5, arg6))
//        | 2 -> doInvoke(arg1, arg2, ArraySeq.create(arg3, arg4, arg5, arg6))
//        | 3 -> doInvoke(arg1, arg2, arg3, ArraySeq.create(arg4, arg5, arg6))
//        | 4 -> doInvoke(arg1, arg2, arg3, arg4, ArraySeq.create(arg5, arg6))
//        | 5 -> doInvoke(arg1, arg2, arg3, arg4, arg5, ArraySeq.create(arg6))
//        | 6 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, null)
//        | _ -> raise <| WrongArityException(6)
//    override x.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj) = 
//        match x.getRequiredArity() with
//        | 0 -> doInvoke(ArraySeq.create(arg1, arg2, arg3, arg4, arg5, arg6, arg7))
//        | 1 -> doInvoke(arg1, ArraySeq.create(arg2, arg3, arg4, arg5, arg6, arg7))
//        | 2 -> doInvoke(arg1, arg2, ArraySeq.create(arg3, arg4, arg5, arg6, arg7))
//        | 3 -> doInvoke(arg1, arg2, arg3, ArraySeq.create(arg4, arg5, arg6, arg7))
//        | 4 -> doInvoke(arg1, arg2, arg3, arg4, ArraySeq.create(arg5, arg6, arg7))
//        | 5 -> doInvoke(arg1, arg2, arg3, arg4, arg5, ArraySeq.create(arg6, arg7))
//        | 6 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, ArraySeq.create(arg7))
//        | 7 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, null)
//        | _ -> raise <| WrongArityException(7)
//    override x.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj) = 
//        match x.getRequiredArity() with
//        | 0 -> doInvoke(ArraySeq.create(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8))
//        | 1 -> doInvoke(arg1, ArraySeq.create(arg2, arg3, arg4, arg5, arg6, arg7, arg8))
//        | 2 -> doInvoke(arg1, arg2, ArraySeq.create(arg3, arg4, arg5, arg6, arg7, arg8))
//        | 3 -> doInvoke(arg1, arg2, arg3, ArraySeq.create(arg4, arg5, arg6, arg7, arg8))
//        | 4 -> doInvoke(arg1, arg2, arg3, arg4, ArraySeq.create(arg5, arg6, arg7, arg8))
//        | 5 -> doInvoke(arg1, arg2, arg3, arg4, arg5, ArraySeq.create(arg6, arg7, arg8))
//        | 6 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, ArraySeq.create(arg7, arg8))
//        | 7 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, ArraySeq.create(arg8))
//        | 8 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, null)
//        | _ -> raise <| WrongArityException(8)
//    override x.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj, arg9: obj) = 
//        match x.getRequiredArity() with
//        | 0 -> doInvoke(ArraySeq.create(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9))
//        | 1 -> doInvoke(arg1, ArraySeq.create(arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9))
//        | 2 -> doInvoke(arg1, arg2, ArraySeq.create(arg3, arg4, arg5, arg6, arg7, arg8, arg9))
//        | 3 -> doInvoke(arg1, arg2, arg3, ArraySeq.create(arg4, arg5, arg6, arg7, arg8, arg9))
//        | 4 -> doInvoke(arg1, arg2, arg3, arg4, ArraySeq.create(arg5, arg6, arg7, arg8, arg9))
//        | 5 -> doInvoke(arg1, arg2, arg3, arg4, arg5, ArraySeq.create(arg6, arg7, arg8, arg9))
//        | 6 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, ArraySeq.create(arg7, arg8, arg9))
//        | 7 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, ArraySeq.create(arg8, arg9))
//        | 8 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, ArraySeq.create(arg9))
//        | 9 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, null)
//        | _ -> raise <| WrongArityException(9)
//    override x.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj, arg9: obj, arg10: obj) = 
//        match x.getRequiredArity() with
//        | 0 -> doInvoke(ArraySeq.create(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10))
//        | 1 -> doInvoke(arg1, ArraySeq.create(arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10))
//        | 2 -> doInvoke(arg1, arg2, ArraySeq.create(arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10))
//        | 3 -> doInvoke(arg1, arg2, arg3, ArraySeq.create(arg4, arg5, arg6, arg7, arg8, arg9, arg10))
//        | 4 -> doInvoke(arg1, arg2, arg3, arg4, ArraySeq.create(arg5, arg6, arg7, arg8, arg9, arg10))
//        | 5 -> doInvoke(arg1, arg2, arg3, arg4, arg5, ArraySeq.create(arg6, arg7, arg8, arg9, arg10))
//        | 6 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, ArraySeq.create(arg7, arg8, arg9, arg10))
//        | 7 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, ArraySeq.create(arg8, arg9, arg10))
//        | 8 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, ArraySeq.create(arg9, arg10))
//        | 9 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, ArraySeq.create(arg10))
//        | 10 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, null)
//        | _ -> raise <| WrongArityException(10)
//    override x.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj, arg9: obj, arg10: obj, arg11: obj) = 
//        match x.getRequiredArity() with
//        | 0 -> doInvoke(ArraySeq.create(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11))
//        | 1 -> doInvoke(arg1, ArraySeq.create(arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11))
//        | 2 -> doInvoke(arg1, arg2, ArraySeq.create(arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11))
//        | 3 -> doInvoke(arg1, arg2, arg3, ArraySeq.create(arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11))
//        | 4 -> doInvoke(arg1, arg2, arg3, arg4, ArraySeq.create(arg5, arg6, arg7, arg8, arg9, arg10, arg11))
//        | 5 -> doInvoke(arg1, arg2, arg3, arg4, arg5, ArraySeq.create(arg6, arg7, arg8, arg9, arg10, arg11))
//        | 6 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, ArraySeq.create(arg7, arg8, arg9, arg10, arg11))
//        | 7 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, ArraySeq.create(arg8, arg9, arg10, arg11))
//        | 8 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, ArraySeq.create(arg9, arg10, arg11))
//        | 9 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, ArraySeq.create(arg10, arg11))
//        | 10 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, ArraySeq.create(arg11))
//        | 11 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, null)
//        | _ -> raise <| WrongArityException(11)
//    override x.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj, arg9: obj, arg10: obj, arg11: obj, arg12: obj) = 
//        match x.getRequiredArity() with
//        | 0 -> doInvoke(ArraySeq.create(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12))
//        | 1 -> doInvoke(arg1, ArraySeq.create(arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12))
//        | 2 -> doInvoke(arg1, arg2, ArraySeq.create(arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12))
//        | 3 -> doInvoke(arg1, arg2, arg3, ArraySeq.create(arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12))
//        | 4 -> doInvoke(arg1, arg2, arg3, arg4, ArraySeq.create(arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12))
//        | 5 -> doInvoke(arg1, arg2, arg3, arg4, arg5, ArraySeq.create(arg6, arg7, arg8, arg9, arg10, arg11, arg12))
//        | 6 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, ArraySeq.create(arg7, arg8, arg9, arg10, arg11, arg12))
//        | 7 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, ArraySeq.create(arg8, arg9, arg10, arg11, arg12))
//        | 8 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, ArraySeq.create(arg9, arg10, arg11, arg12))
//        | 9 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, ArraySeq.create(arg10, arg11, arg12))
//        | 10 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, ArraySeq.create(arg11, arg12))
//        | 11 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, ArraySeq.create(arg12))
//        | 12 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, null)
//        | _ -> raise <| WrongArityException(12)
//    override x.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj, arg9: obj, arg10: obj, arg11: obj, arg12: obj, arg13: obj) = 
//        match x.getRequiredArity() with
//        | 0 -> doInvoke(ArraySeq.create(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13))
//        | 1 -> doInvoke(arg1, ArraySeq.create(arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13))
//        | 2 -> doInvoke(arg1, arg2, ArraySeq.create(arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13))
//        | 3 -> doInvoke(arg1, arg2, arg3, ArraySeq.create(arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13))
//        | 4 -> doInvoke(arg1, arg2, arg3, arg4, ArraySeq.create(arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13))
//        | 5 -> doInvoke(arg1, arg2, arg3, arg4, arg5, ArraySeq.create(arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13))
//        | 6 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, ArraySeq.create(arg7, arg8, arg9, arg10, arg11, arg12, arg13))
//        | 7 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, ArraySeq.create(arg8, arg9, arg10, arg11, arg12, arg13))
//        | 8 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, ArraySeq.create(arg9, arg10, arg11, arg12, arg13))
//        | 9 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, ArraySeq.create(arg10, arg11, arg12, arg13))
//        | 10 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, ArraySeq.create(arg11, arg12, arg13))
//        | 11 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, ArraySeq.create(arg12, arg13))
//        | 12 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, ArraySeq.create(arg13))
//        | 13 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, null)
//        | _ -> raise <| WrongArityException(13)
//    override x.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj, arg9: obj, arg10: obj, arg11: obj, arg12: obj, arg13: obj, arg14: obj) = 
//        match x.getRequiredArity() with
//        | 0 -> doInvoke(ArraySeq.create(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14))
//        | 1 -> doInvoke(arg1, ArraySeq.create(arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14))
//        | 2 -> doInvoke(arg1, arg2, ArraySeq.create(arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14))
//        | 3 -> doInvoke(arg1, arg2, arg3, ArraySeq.create(arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14))
//        | 4 -> doInvoke(arg1, arg2, arg3, arg4, ArraySeq.create(arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14))
//        | 5 -> doInvoke(arg1, arg2, arg3, arg4, arg5, ArraySeq.create(arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14))
//        | 6 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, ArraySeq.create(arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14))
//        | 7 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, ArraySeq.create(arg8, arg9, arg10, arg11, arg12, arg13, arg14))
//        | 8 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, ArraySeq.create(arg9, arg10, arg11, arg12, arg13, arg14))
//        | 9 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, ArraySeq.create(arg10, arg11, arg12, arg13, arg14))
//        | 10 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, ArraySeq.create(arg11, arg12, arg13, arg14))
//        | 11 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, ArraySeq.create(arg12, arg13, arg14))
//        | 12 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, ArraySeq.create(arg13, arg14))
//        | 13 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, ArraySeq.create(arg14))
//        | 14 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, null)
//        | _ -> raise <| WrongArityException(14)
//    override x.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj, arg9: obj, arg10: obj, arg11: obj, arg12: obj, arg13: obj, arg14: obj, arg15: obj) = 
//        match x.getRequiredArity() with
//        | 0 -> doInvoke(ArraySeq.create(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15))
//        | 1 -> doInvoke(arg1, ArraySeq.create(arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15))
//        | 2 -> doInvoke(arg1, arg2, ArraySeq.create(arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15))
//        | 3 -> doInvoke(arg1, arg2, arg3, ArraySeq.create(arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15))
//        | 4 -> doInvoke(arg1, arg2, arg3, arg4, ArraySeq.create(arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15))
//        | 5 -> doInvoke(arg1, arg2, arg3, arg4, arg5, ArraySeq.create(arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15))
//        | 6 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, ArraySeq.create(arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15))
//        | 7 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, ArraySeq.create(arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15))
//        | 8 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, ArraySeq.create(arg9, arg10, arg11, arg12, arg13, arg14, arg15))
//        | 9 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, ArraySeq.create(arg10, arg11, arg12, arg13, arg14, arg15))
//        | 10 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, ArraySeq.create(arg11, arg12, arg13, arg14, arg15))
//        | 11 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, ArraySeq.create(arg12, arg13, arg14, arg15))
//        | 12 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, ArraySeq.create(arg13, arg14, arg15))
//        | 13 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, ArraySeq.create(arg14, arg15))
//        | 14 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, ArraySeq.create(arg15))
//        | 15 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, null)
//        | _ -> raise <| WrongArityException(15)
//    override x.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj, arg9: obj, arg10: obj, arg11: obj, arg12: obj, arg13: obj, arg14: obj, arg15: obj, arg16: obj) = 
//        match x.getRequiredArity() with
//        | 0 -> doInvoke(ArraySeq.create(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16))
//        | 1 -> doInvoke(arg1, ArraySeq.create(arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16))
//        | 2 -> doInvoke(arg1, arg2, ArraySeq.create(arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16))
//        | 3 -> doInvoke(arg1, arg2, arg3, ArraySeq.create(arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16))
//        | 4 -> doInvoke(arg1, arg2, arg3, arg4, ArraySeq.create(arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16))
//        | 5 -> doInvoke(arg1, arg2, arg3, arg4, arg5, ArraySeq.create(arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16))
//        | 6 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, ArraySeq.create(arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16))
//        | 7 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, ArraySeq.create(arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16))
//        | 8 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, ArraySeq.create(arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16))
//        | 9 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, ArraySeq.create(arg10, arg11, arg12, arg13, arg14, arg15, arg16))
//        | 10 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, ArraySeq.create(arg11, arg12, arg13, arg14, arg15, arg16))
//        | 11 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, ArraySeq.create(arg12, arg13, arg14, arg15, arg16))
//        | 12 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, ArraySeq.create(arg13, arg14, arg15, arg16))
//        | 13 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, ArraySeq.create(arg14, arg15, arg16))
//        | 14 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, ArraySeq.create(arg15, arg16))
//        | 15 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, ArraySeq.create(arg16))
//        | 16 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, null)
//        | _ -> raise <| WrongArityException(16)
//    override x.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj, arg9: obj, arg10: obj, arg11: obj, arg12: obj, arg13: obj, arg14: obj, arg15: obj, arg16: obj, arg17: obj) = 
//        match x.getRequiredArity() with
//        | 0 -> doInvoke(ArraySeq.create(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17))
//        | 1 -> doInvoke(arg1, ArraySeq.create(arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17))
//        | 2 -> doInvoke(arg1, arg2, ArraySeq.create(arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17))
//        | 3 -> doInvoke(arg1, arg2, arg3, ArraySeq.create(arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17))
//        | 4 -> doInvoke(arg1, arg2, arg3, arg4, ArraySeq.create(arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17))
//        | 5 -> doInvoke(arg1, arg2, arg3, arg4, arg5, ArraySeq.create(arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17))
//        | 6 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, ArraySeq.create(arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17))
//        | 7 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, ArraySeq.create(arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17))
//        | 8 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, ArraySeq.create(arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17))
//        | 9 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, ArraySeq.create(arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17))
//        | 10 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, ArraySeq.create(arg11, arg12, arg13, arg14, arg15, arg16, arg17))
//        | 11 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, ArraySeq.create(arg12, arg13, arg14, arg15, arg16, arg17))
//        | 12 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, ArraySeq.create(arg13, arg14, arg15, arg16, arg17))
//        | 13 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, ArraySeq.create(arg14, arg15, arg16, arg17))
//        | 14 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, ArraySeq.create(arg15, arg16, arg17))
//        | 15 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, ArraySeq.create(arg16, arg17))
//        | 16 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, ArraySeq.create(arg17))
//        | 17 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, null)
//        | _ -> raise <| WrongArityException(17)
//    override x.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj, arg9: obj, arg10: obj, arg11: obj, arg12: obj, arg13: obj, arg14: obj, arg15: obj, arg16: obj, arg17: obj, arg18: obj) = 
//        match x.getRequiredArity() with
//        | 0 -> doInvoke(ArraySeq.create(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18))
//        | 1 -> doInvoke(arg1, ArraySeq.create(arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18))
//        | 2 -> doInvoke(arg1, arg2, ArraySeq.create(arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18))
//        | 3 -> doInvoke(arg1, arg2, arg3, ArraySeq.create(arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18))
//        | 4 -> doInvoke(arg1, arg2, arg3, arg4, ArraySeq.create(arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18))
//        | 5 -> doInvoke(arg1, arg2, arg3, arg4, arg5, ArraySeq.create(arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18))
//        | 6 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, ArraySeq.create(arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18))
//        | 7 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, ArraySeq.create(arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18))
//        | 8 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, ArraySeq.create(arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18))
//        | 9 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, ArraySeq.create(arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18))
//        | 10 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, ArraySeq.create(arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18))
//        | 11 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, ArraySeq.create(arg12, arg13, arg14, arg15, arg16, arg17, arg18))
//        | 12 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, ArraySeq.create(arg13, arg14, arg15, arg16, arg17, arg18))
//        | 13 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, ArraySeq.create(arg14, arg15, arg16, arg17, arg18))
//        | 14 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, ArraySeq.create(arg15, arg16, arg17, arg18))
//        | 15 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, ArraySeq.create(arg16, arg17, arg18))
//        | 16 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, ArraySeq.create(arg17, arg18))
//        | 17 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, ArraySeq.create(arg18))
//        | 18 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, null)
//        | _ -> raise <| WrongArityException(18)
//    override x.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj, arg9: obj, arg10: obj, arg11: obj, arg12: obj, arg13: obj, arg14: obj, arg15: obj, arg16: obj, arg17: obj, arg18: obj, arg19: obj) = 
//        match x.getRequiredArity() with
//        | 0 -> doInvoke(ArraySeq.create(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19))
//        | 1 -> doInvoke(arg1, ArraySeq.create(arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19))
//        | 2 -> doInvoke(arg1, arg2, ArraySeq.create(arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19))
//        | 3 -> doInvoke(arg1, arg2, arg3, ArraySeq.create(arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19))
//        | 4 -> doInvoke(arg1, arg2, arg3, arg4, ArraySeq.create(arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19))
//        | 5 -> doInvoke(arg1, arg2, arg3, arg4, arg5, ArraySeq.create(arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19))
//        | 6 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, ArraySeq.create(arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19))
//        | 7 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, ArraySeq.create(arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19))
//        | 8 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, ArraySeq.create(arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19))
//        | 9 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, ArraySeq.create(arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19))
//        | 10 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, ArraySeq.create(arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19))
//        | 11 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, ArraySeq.create(arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19))
//        | 12 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, ArraySeq.create(arg13, arg14, arg15, arg16, arg17, arg18, arg19))
//        | 13 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, ArraySeq.create(arg14, arg15, arg16, arg17, arg18, arg19))
//        | 14 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, ArraySeq.create(arg15, arg16, arg17, arg18, arg19))
//        | 15 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, ArraySeq.create(arg16, arg17, arg18, arg19))
//        | 16 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, ArraySeq.create(arg17, arg18, arg19))
//        | 17 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, ArraySeq.create(arg18, arg19))
//        | 18 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, ArraySeq.create(arg19))
//        | 19 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19, null)
//        | _ -> raise <| WrongArityException(19)
//    override x.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj, arg9: obj, arg10: obj, arg11: obj, arg12: obj, arg13: obj, arg14: obj, arg15: obj, arg16: obj, arg17: obj, arg18: obj, arg19: obj, arg20: obj) = 
//        match x.getRequiredArity() with
//        | 0 -> doInvoke(ArraySeq.create(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19, arg20))
//        | 1 -> doInvoke(arg1, ArraySeq.create(arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19, arg20))
//        | 2 -> doInvoke(arg1, arg2, ArraySeq.create(arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19, arg20))
//        | 3 -> doInvoke(arg1, arg2, arg3, ArraySeq.create(arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19, arg20))
//        | 4 -> doInvoke(arg1, arg2, arg3, arg4, ArraySeq.create(arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19, arg20))
//        | 5 -> doInvoke(arg1, arg2, arg3, arg4, arg5, ArraySeq.create(arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19, arg20))
//        | 6 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, ArraySeq.create(arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19, arg20))
//        | 7 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, ArraySeq.create(arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19, arg20))
//        | 8 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, ArraySeq.create(arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19, arg20))
//        | 9 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, ArraySeq.create(arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19, arg20))
//        | 10 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, ArraySeq.create(arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19, arg20))
//        | 11 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, ArraySeq.create(arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19, arg20))
//        | 12 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, ArraySeq.create(arg13, arg14, arg15, arg16, arg17, arg18, arg19, arg20))
//        | 13 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, ArraySeq.create(arg14, arg15, arg16, arg17, arg18, arg19, arg20))
//        | 14 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, ArraySeq.create(arg15, arg16, arg17, arg18, arg19, arg20))
//        | 15 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, ArraySeq.create(arg16, arg17, arg18, arg19, arg20))
//        | 16 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, ArraySeq.create(arg17, arg18, arg19, arg20))
//        | 17 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, ArraySeq.create(arg18, arg19, arg20))
//        | 18 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, ArraySeq.create(arg19, arg20))
//        | 19 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19, ArraySeq.create(arg20))
//        | 20 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19, arg20, null)
//        | _ -> raise <| WrongArityException(20)
//    override x.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj, arg9: obj, arg10: obj, arg11: obj, arg12: obj, arg13: obj, arg14: obj, arg15: obj, arg16: obj, arg17: obj, arg18: obj, arg19: obj, arg20: obj, [<Params>] args:obj array) : obj =
//        match x.getRequiredArity() with
//        | 0 -> doInvoke(RestFn.ontoArrayPrepend(args, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19, arg20))
//        | 1 -> doInvoke(arg1, RestFn.ontoArrayPrepend(args, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19, arg20))
//        | 2 -> doInvoke(arg1, arg2, RestFn.ontoArrayPrepend(args, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19, arg20))
//        | 3 -> doInvoke(arg1, arg2, arg3, RestFn.ontoArrayPrepend(args, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19, arg20))
//        | 4 -> doInvoke(arg1, arg2, arg3, arg4, RestFn.ontoArrayPrepend(args, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19, arg20))
//        | 5 -> doInvoke(arg1, arg2, arg3, arg4, arg5, RestFn.ontoArrayPrepend(args, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19, arg20))
//        | 6 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, RestFn.ontoArrayPrepend(args, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19, arg20))
//        | 7 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, RestFn.ontoArrayPrepend(args, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19, arg20))
//        | 8 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, RestFn.ontoArrayPrepend(args, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19, arg20))
//        | 9 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, RestFn.ontoArrayPrepend(args, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19, arg20))
//        | 10 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, RestFn.ontoArrayPrepend(args, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19, arg20))
//        | 11 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, RestFn.ontoArrayPrepend(args, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19, arg20))
//        | 12 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, RestFn.ontoArrayPrepend(args, arg13, arg14, arg15, arg16, arg17, arg18, arg19, arg20))
//        | 13 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, RestFn.ontoArrayPrepend(args, arg14, arg15, arg16, arg17, arg18, arg19, arg20))
//        | 14 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, RestFn.ontoArrayPrepend(args, arg15, arg16, arg17, arg18, arg19, arg20))
//        | 15 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, RestFn.ontoArrayPrepend(args, arg16, arg17, arg18, arg19, arg20))
//        | 16 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, RestFn.ontoArrayPrepend(args, arg17, arg18, arg19, arg20))
//        | 17 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, RestFn.ontoArrayPrepend(args, arg18, arg19, arg20))
//        | 18 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, RestFn.ontoArrayPrepend(args, arg19, arg20))
//        | 19 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19, RestFn.ontoArrayPrepend(args, arg20))
//        | 20 -> doInvoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19, arg20, null)
//        | _ -> raise <| WrongArityException(21)
