namespace Clojure.Compiler

open Clojure.Collections
open System
open System.Text
open System.Text.RegularExpressions

[<AbstractClass;Sealed>] 
type Munger private () =

    // Name munging

    // Why use a PersistentHashMap rather than a dictionary?
    static member val private _charMap = PersistentHashMap.create( 
            '-', "_",
            //		                         '.', "_DOT_",
             ':', "_COLON_",
             '+', "_PLUS_",
             '>', "_GT_",
             '<', "_LT_",
             '=', "_EQ_",
             '~', "_TILDE_",
             '!', "_BANG_",
             '@', "_CIRCA_",
             '#', "_SHARP_",
             '\'',"_SINGLEQUOTE_",
             '"', "_DOUBLEQUOTE_",
             '%', "_PERCENT_",
             '^', "_CARET_",
             '&', "_AMPERSAND_",
             '*', "_STAR_",
             '|', "_BAR_",
             '{', "_LBRACE_",
             '}', "_RBRACE_",
             '[', "_LBRACK_",
             ']', "_RBRACK_",
             '/', "_SLASH_",
             '\\', "_BSLASH_",
             '?', "_QMARK_"
        )

    static member CharMap = Munger._charMap

    static member val DemungeMap = Munger.CreateDemungeMap()

    static member CreateDemungeMap() =
        // DemungeMap maps strings to characters in the opposite
        // direction that CharMap does, plus it maps "$" to '/'

        let mutable m = RTMap.map("$", '/')

        let rec loop (s:ISeq) =
            match s with
            | null -> ()
            | _ -> 
                let me = s.first() :?> IMapEntry
                m <- m.assoc(me.value(), me.key())
                loop(s.next())
        loop(RT0.seq(Munger._charMap))

        m

    // DEMUNGE_PATTERN searches for the first of any occurrence of
    // the strings that are keys of DEMUNGE_MAP.
    // Note: Regex matching rules mean that #"_|_COLON_" "_COLON_"
    // returns "_", but #"_COLON_|_" "_COLON_" returns "_COLON_"
    // as desired.  Sorting string keys of DEMUNGE_MAP from longest to
    // shortest ensures correct matching behavior, even if some strings are
    // prefixes of others.

    static member val LengthComparer = 
       { new System.Collections.IComparer with 
            member _.Compare(x,y) = 
                let xlen = (x :?> string).Length
                let ylen = (y :?> string).Length
                ylen - xlen
                                }

    static member private CreateDemungePattern() =
        let mungeStrs = RT0.toArray(RTMap.keys(Munger.DemungeMap))
        Array.Sort(mungeStrs, Munger.LengthComparer)
        let sb = StringBuilder()
        for i = 0 to mungeStrs.Length - 1 do
            if i > 0 then sb.Append("|") |> ignore
            sb.Append(Regex.Escape(Regex.Escape(mungeStrs[i] :?> string))) |> ignore

        new Regex(sb.ToString())

    static member val DemungePattern = Munger.CreateDemungePattern()


    static member Munge(name: string) =
        let sb = StringBuilder()
        for i = 0 to name.Length - 1 do
            let c = name.[i]
            match (Munger.CharMap :> Associative).valAt(c) with
            | null -> sb.Append(c) |> ignore
            | _ as sub -> sb.Append(sub) |> ignore
        sb.ToString()

    static member Demunge(mungedName: string) =
        let sb = StringBuilder()
        let mutable m = Munger.DemungePattern.Match(mungedName)
        let mutable pos = 0
        while m.Success do
            let start = m.Index
            let len = m.Length
            if start > pos then
                sb.Append(mungedName.Substring(pos, start - pos)) |> ignore
            sb.Append(Munger.DemungeMap.valAt(m.Value)) |> ignore
            pos <- start + len
            m <- m.NextMatch()
        if pos < mungedName.Length then
            sb.Append(mungedName.Substring(pos)) |> ignore
        sb.ToString()