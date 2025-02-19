module Clojure.Collections.RTPrint

open System
open System.IO
open System.Text
open Clojure.BigArith
open Clojure.Numerics
open System.Numerics
open System.Text.RegularExpressions



// We have a major circularity problem.
// Our default printer cannot call ToString on the collections -- the ToString methods on collection will be calling the methods here.
// However, we _can_ call ToString on items in a collection.
//
// As part of the initializtion for ClojureCLR as a whole, we will need to set the printFn to a function that can handle the collections.
// Note that this bootstrapping is also done in Clojure(JVM) and ClojureCLR(Classic).

/// Signature for a printer function
type TPrintFn = (obj * TextWriter) -> unit

/// The current printer function
/// To be reset to an actual value during initialization
let mutable internal printFn: TPrintFn option = None



// TODO: figure out how to properly incorporate 'readably' into this interface.
// Probably needs to happen with the functions setthe functions above.


// This was originally in LispReader.
// Copied here so we can use it.

/// Escapes vertical bars.  For use in typenames.
let private vbarEscape(s:String) :string =
    let sb = StringBuilder()
    sb.Append('|')  |> ignore
    for c in s do
        sb.Append(c) |> ignore
        if c = '|' then sb.Append('|') |> ignore else ()
    sb.Append('|') |> ignore
    sb.ToString()

/// Print the metadata of an object with the #^ syntax.
let rec private baseMetaPrinter (x: obj, w: TextWriter) : unit =
    match x with
    | :? IMeta as xo -> // original code has Obj here, but not sure why this is correct.  We only need an IMeta to have metadata.
        let meta = xo.meta ()
        // the real version will check for a meta with count=1 and just a tag key and special case that.
        // However, we don't have keywords yet, so we can't do that here.
        if meta.count() > 0 then
            w.Write("#^")
            print (meta, w)
            w.Write(' ')
    | _ -> ()

///  the basic printer.  Handles many kinds of objects.
and private printBasic(readably:bool, x:obj, w:TextWriter) : unit =

    let printInnerSeq readably (s: ISeq) (w: TextWriter) =
        let rec loop (s: ISeq) =
            if not (isNull s) then 
                 printBasic (readably, s.first(), w) 
                 let next = s.next() 
                 if not (isNull next) then w.Write(' ')
                 loop next
        loop s

    let baseCharPrinter readably (c: char) (w: TextWriter) =
        if not readably then
            w.Write(c)
        else
            w.Write('\\')

            match c with
            | '\n' -> w.Write("newline")
            | '\t' -> w.Write("tab")
            | '\r' -> w.Write("return")
            | ' ' -> w.Write("space")
            | '\f' -> w.Write("formfeed")
            | '\b' -> w.Write("backspace")
            | _ -> w.Write(c)

    let baseStringPrinter readably (s: string) (w: TextWriter) =
        if not readably then
            w.Write(s)
        else
            w.Write('"')
            
            s
            |> Seq.iter (fun c -> 
                w.Write(c)
                match c with
                | '\n' -> w.Write("\\n")
                | '\t' -> w.Write("\\t")
                | '\r' -> w.Write("\\r")
                | '"' -> w.Write("\\\"")
                | '\\' -> w.Write("\\\\")
                | '\f' -> w.Write("\\f")
                | '\b' -> w.Write("\\b")
                | _ -> w.Write(c))
            w.Write('"')


    baseMetaPrinter(x,w)

    match x with
    | null -> w.Write("nil")
    | :? ISeq
    | :? IPersistentList ->
        w.Write('(')
        printInnerSeq readably (RT0.seq (x)) w
        w.Write(')')
    | :? String as s -> baseStringPrinter readably s w
    | :? IPersistentMap ->
        let rec loop (s: ISeq) =
            let e: IMapEntry = downcast s.first ()
            printBasic (readably, e.key (), w)
            w.Write(' ')
            printBasic (readably, e.value (), w)
            if not (isNull (s.next ())) then w.Write(", ")
            loop (s.next ())

        w.Write('{')
        RT0.seq (x) |> loop
        w.Write('}')
    | :? IPersistentVector as v ->
        let n = v.count ()
        w.Write('[')
        for i = 0 to n - 1 do
            printBasic (readably, v.nth (i), w)
            if i < n - 1 then w.Write(" ")

        w.Write(']')
    | :? IPersistentSet ->
        let rec loop (s: ISeq) =
            printBasic (readably, s.first (), w)

            if not (isNull (s.next ())) then w.Write(" ")

            loop (s.next ())

        w.Write("#{")
        RT0.seq (x) |> loop
        w.Write('}')
    | :? Char as ch -> baseCharPrinter readably ch w
    | :? Type as t ->
        // in the original code, this checked LispReader.NameRequiresEscaping (tname)
        // That requires a lot reader-specific knowledge I don't feel like embedding
        // I think that can wait until the real print system is initiailized.
        let tname = vbarEscape(t.AssemblyQualifiedName)
        w.Write("#=")
        w.Write(tname)
    | :? BigDecimal as d when readably ->
        w.Write(d.ToString())
        w.Write("M");
    | :? BigInt as d when readably ->
        w.Write(d.ToString())
        w.Write("N");
    | :? BigInteger as d when readably ->
        w.Write(d.ToString())
        w.Write("BIGINT");     
    // The following is in the original -- ignore for now, let the real printer handle this after Var is defined.
    // Or maybe we need a static mutable binding to hold a Var-printer that can be set later.
    // TODO: make sure we handle this
    //| :? Var as v ->
    //    w.Write($"#=(var {v.Namespace.Name}/{v.Symbol}")
    | :? Regex as r ->
        w.Write($"#\"{r.ToString()}\"")
    | :? Double
    | :? Single ->
        // this case is not in the JVM.
        // When generating initializations for static variables in the classes representing IFns,
        //    let's say the value is the double 7.0.
        //    we generate code that says   (double)RT.readFromString("7")
        //    so we get a boxed int, which CLR won't cast to double.  Sigh.
        //    So I need double/float to print a trailing .0 even when integer-valued.
        let s = x.ToString()
        let s = if not (s.Contains('.')) && not (s.Contains('E')) then s + ".0" else s
        w.Write(s)

    | _ -> w.Write(x.ToString())
   


/// Print an object to a TextWriter
and  print (x: obj, w: TextWriter) =
    match printFn with
    | Some f -> f (x, w)
    | None -> printBasic (true, x, w)

/// Print an object to a string
let printString (x: obj) =
    use sw = new StringWriter()
    print (x, sw)
    sw.ToString()
