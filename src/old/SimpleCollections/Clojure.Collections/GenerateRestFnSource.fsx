open System.IO


let filename = ".\\test.out"
let tw = new StreamWriter(filename)

let writePreamble () =
    fprintf tw "%s" """namespace Clojure.Fn

open System
open Clojure.Collections

[<AbstractClass>]
type RestFn()
    inherit AFunction()

    abstract getRequiredArity() : unit -> int

    static member ontoArrayPrepend(arr : obj array, [<ParamArray>] args: obj array) =
        let mutable ret : ISeq = ArraySeq.create(arr)
        for i = args.Length downto 0 do
            ret <- Helpers.cons(args.[i],ret)
        ret

"""


let writeDoInvokes (max: int) =
    let fnType (n) =
        let paramTypes =
            Array.create (n + 1) "obj" |> String.concat " * "

        paramTypes + " -> obj"

    let argList (n) =
        let startingParams =
            seq { 1 .. n }
            |> Seq.map (fun j -> sprintf "arg%i: obj" j)
            |> Seq.toArray
            |> String.concat ", "

        startingParams
        + (if n = 0 then "" else ", ")
        + "args:obj "
    for i = 0 to max do
        fprintfn tw "    abstract _.doInvoke : %s" (fnType i)
        fprintfn tw "    default _.doInvoke(%s) : obj = null" (argList i)


let writeApplyTo (max: int) =
    let preamble = """
    overide x.applyTo (argList:ISeq) : obj =
        let reqArity = x.getRequiredArity()
        if AFn.boundedLength(argList,reqArity) <= reqArity
        then AFn.ApplyToHelper(x,argList)
        else
            let mutable al = argList
            let n() = al <- al.next(); al
            match reqArity with"""

    let generateCase (index) =
        let head =
            sprintf "            | %i -> x.doInvoke( " index

        let optionalFirstArg =
            if index = 0 then "" else "arglist.first(), "

        let body =
            if index < 2 then
                ""
            else
                Array.create (index - 1) "nl().first()"
                |> String.concat ", "

        let optComma = if index < 2 then "" else ", "

        let tail =
            if index = 0 then "al )" else "al.next() )"

        head + optionalFirstArg + body + optComma + tail

    let lastCase =
        "            |> _ -> raise <| WrongArityException(-1)"

    fprintfn tw "%s" preamble
    for i = 0 to max do
        fprintfn tw "%s" (generateCase i)

    fprintf tw "%s" lastCase



let writeInvoke (i: int) =
    let formalArgs n =
        seq { 1 .. n }
        |> Seq.map (fun i -> sprintf "arg%i: obj" i)
        |> String.concat ", "

    let matchCallArgs m n =
        let initialArgs =
            seq { 1 .. m }
            |> Seq.map (fun i -> sprintf "arg%i" i)
            |> String.concat ", "

        let finalArg =
            if m = n then
                "null"
            else
                let args =
                    seq { (m + 1) .. n }
                    |> Seq.map (fun i -> sprintf "arg%i" i)
                    |> String.concat ", "

                sprintf "ArraySeq.create(%s)" args

        if m = 0 then finalArg else initialArgs + ", " + finalArg


    fprintfn tw "    override x.invoke(%s) = " (formalArgs i)
    fprintfn tw "        match x.getRequiredArity() with"
    for j = 0 to i do
        fprintfn tw "        | %i -> doInvoke(%s)" j (matchCallArgs j i)

    fprintfn tw "        | _ -> raise <| WrongArityException(%i)" i



let writeInvokes (max: int) =
    for i = 0 to max do
        writeInvoke (i)

let writeFinalInvoke (max: int) =
    let argList (n) =
        let startingParams =
            seq { 1 .. n }
            |> Seq.map (fun j -> sprintf "arg%i: obj" j)
            |> Seq.toArray
            |> String.concat ", "

        startingParams + ", [<Params>] args:obj array"

    let matchInitialArgs m n =
        if m = 0 then
            ""
        else
            seq { 1 .. m }
            |> Seq.map (fun i -> sprintf "arg%i" i)
            |> String.concat (", ")

    let matchFinalArg m n =
        if m = n then
            "null"
        else
            let args =
                seq { (m + 1) .. n }
                |> Seq.map (fun i -> sprintf "arg%i" i)
                |> String.concat (", ")

            sprintf "RestFn.ontoArrayPrepend(args, %s)" args



    let matchCall i n =
        match i with
        | 0 -> sprintf "doInvoke(%s)" (matchFinalArg i n)
        | _ -> sprintf "doInvoke(%s, %s)" (matchInitialArgs i n) (matchFinalArg i n)


    fprintfn tw "    override x.invoke(%s) : obj =" (argList max)
    fprintfn tw "        match x.getRequiredArity() with"
    for i = 0 to max do
        fprintfn tw "        | %i -> %s" i (matchCall i max)

    fprintfn tw "        | _ -> raise <| WrongArityException(%i)" (max + 1)

let max = 20
writePreamble ()
writeDoInvokes (max)
writeApplyTo (max)
writeInvokes (max)
writeFinalInvoke (max)

tw.Close()
