namespace Clojure.IO

open Clojure.Collections
open Clojure.Lib
open System.Collections.Generic
open System
open System.IO
open System.Text
open System.Text.RegularExpressions
open Clojure.Numerics
open System.Numerics
open Clojure.BigArith

type ReaderResolver =
    abstract currentNS : unit -> Symbol
    abstract resolveClass : Symbol -> Symbol
    abstract resolveAlias : Symbol -> Symbol
    abstract resolveVar : Symbol -> Symbol

// Temporarily located here until we solve the problem back in Clojure.Collections with a followon to RTSeq
[<AbstractClass;Sealed>]
type RTReader() = 

    static member mapUniqueKeys( [<ParamArray>] init: obj[]) : IPersistentMap =
        if isNull init then
            PersistentArrayMap.Empty
        elif init.Length <= PersistentArrayMap.hashtableThreshold then
            new PersistentArrayMap(init)
        else
            PersistentHashMap.create(init)

    static member map( [<ParamArray>] init: obj[]) : IPersistentMap =
        if isNull init || init.Length = 0 then
            PersistentArrayMap.Empty
        elif init.Length <= PersistentArrayMap.hashtableThreshold then
            PersistentArrayMap.createWithCheck(init)
        else
            PersistentHashMap.createWithCheck(init)

type ReaderFunction = PushbackTextReader * char * obj * obj -> obj

type ReaderException(msg: string, line: int, column: int, inner: exn) =
    inherit Exception(msg, inner)

    static member val ErrNS = "clojure.error"
    static member val ErrLine = Keyword.intern(ReaderException.ErrNS, "line")
    static member val ErrColumn = Keyword.intern(ReaderException.ErrNS, "column")

    member val data : IPersistentMap = if line > 0 then RTReader.map(ReaderException.ErrLine, line, ReaderException.ErrColumn, column) else null

    new() = ReaderException(null, -1, -1, null)
    new(msg: string) = ReaderException(msg, -1, -1, null)
    new(msg: string, inner: exn) = ReaderException(msg, -1, -1, inner)



[<AbstractClass;Sealed>]
type LispReader() = 

    // Symbol definitions

    static let QuoteSym = Symbol.intern("quote")
    static let TheVarSym = Symbol.intern("var")
    static let UnquoteSym = Symbol.intern("clojure.core", "unquote")
    static let UnquoteSplicingSym = Symbol.intern("clojure.core", "unquote-splicing")
    static let DerefSym = Symbol.intern("clojure.core", "deref")
    static let ApplySym = Symbol.intern("clojure.core", "apply")
    static let ConcatSym = Symbol.intern("clojure.core", "concat")
    static let hashMapSym = Symbol.intern("clojure.core", "hash-map")
    static let HashSetSym = Symbol.intern("clojure.core", "hash-set")
    static let VectorSym = Symbol.intern("clojure.core", "vector")
    static let ListSym = Symbol.intern("clojure.core", "list")
    static let WithMetaSym = Symbol.intern("clojure.core", "with-meta")
    static let SeqSym = Symbol.intern("clojure.core", "seq")

    // Keyword definitions

    static let UnknownKeyword = Keyword.intern(null, "unknown")
    
    // Parser options

    static let OptEofKeword = Keyword.intern(null, "eof")
    static let OptFeaturesKeyword = Keyword.intern(null, "features")
    static let OptReadCondKeyword = Keyword.intern(null, "read-cond")

    static let EofOptionsDefault : bool * obj = (true, null)

    // Platform features - always installed

    static let PlatformKey = Keyword.intern(null, "cljr")
    static let PlatformFeatureSet = PersistentHashSet.create(PlatformKey)


    // EOF special value to throw on eof
    static let EofThrowKeyword = Keyword.intern(null, "eofthrow")

    // Sentinel values for reading lists
    static let ReadEOF = obj()
    static let ReadFinished = obj()


    // Var environments

    /// Dynamically bound Var to a map from Symbols to ...  (symbol -> gensymbol)
    static let GensymEnvVar = Var.create(null).setDynamic()

    /// sarted-map num -> gensymbol
    static let ArgEnvVar = Var.create(null).setDynamic()

    /// Dynamically bound Var set to true in a read-cond context
    static let ReadCondEnvVar = Var.create(false).setDynamic()

    // macro characters and #-dispatch
    static let macros = Array.zeroCreate<ReaderFunction option>(256)
    static let dispatchMacros = Array.zeroCreate<ReaderFunction option>(256)

    static do 
        macros[0] <- None
        // TODO: Lots of initialization here

    static member isMacro(ch: int) = ch < macros.Length && macros[ch].IsSome
    static member getMacro(ch: int) = if ch < macros.Length then macros.[ch] else None
    static member isTerminatingMacro(ch: int) = LispReader.isMacro(ch) && ch <> (int '#') && ch <> (int '\'') && ch <> (int '%')

    static member parseEofOptions(opts : obj) =        
        match opts with
        | :? IPersistentMap as optsMap ->
                let eofValue = optsMap.valAt(OptEofKeword, EofThrowKeyword)
                if EofThrowKeyword.Equals(eofValue) then
                    EofOptionsDefault
                else
                    (false, eofValue)
        | _ -> EofOptionsDefault

    static member EnsurePending(pendingForms: obj) : obj =
        match pendingForms with
        | null -> LinkedList<obj>()
        | _ -> pendingForms

    static member InstallPlatformFeature(opts: obj) : obj =
        match opts with
        | null -> RTReader.mapUniqueKeys(OptFeaturesKeyword, PlatformFeatureSet)
        | :? IPersistentMap as mopts ->
            match mopts.valAt(OptFeaturesKeyword) with
            | null -> mopts.assoc(OptFeaturesKeyword, PlatformFeatureSet)
            | :? IPersistentSet as features -> mopts.assoc(OptFeaturesKeyword, RTSeq.conj(features, PlatformKey))
            | _ -> raise <| invalidOp "LispReader: the value of :features must be a set"
        | _ -> raise <| invalidOp "LispReader options must be a map"

    static member getReaderResolver() : ReaderResolver option =
        let v = (RTVar.ReaderResolverVar:>IDeref).deref()
        match v with 
        | :? ReaderResolver as r -> Some r
        | _ -> None

    static member getCurrentNamespace() = (RTVar.CurrentNSVar :> IDeref).deref() :?> Namespace

    // Entry points for reading

    static member read(r: PushbackTextReader, opts: obj) =
        let eofIsError, eofValue = LispReader.parseEofOptions opts
        LispReader.read(r, eofIsError, eofValue, false, opts)
        
    static member read(r: PushbackTextReader, eofIsError: bool, eofValue: obj, isRecursive: bool) =
        LispReader.read(r, eofIsError, eofValue, isRecursive, PersistentHashMap.Empty)

    static member read(r: PushbackTextReader, eofIsError: bool, eofValue: obj, isRecursive: bool, opts: obj) =
        LispReader.read(r, eofIsError, eofValue, None, null, isRecursive, opts, null, LispReader.getReaderResolver())

    static member private read(
        r: PushbackTextReader, 
        eofIsError: bool, 
        eofValue: obj, 
        isRecursive: bool,
        opts: obj,
        pendingForms: obj) = 
        LispReader.read(r, eofIsError, eofValue, None, null, isRecursive, opts, LispReader.EnsurePending(pendingForms), LispReader.getReaderResolver())
        
    // They all end up here
    // This is the main read function
    // Roughly, it reads and returns the next form in the stream.
    // Reading can be recursive.  For example, when reading a list, we first see the '('.  
    // '(' is a macro character, associated with the list reader function.
    //  The list reader function will iteratively call read and start to accumulate the list elements.
    //  It knows it is done, when it sees the closing ')'.  The call to read that sees the closing ')' doesn't actually return anything useful.
    //  It consumes the ')', and needs to signal back to the caller that it is done.  This is the use of the returnOn/returnOnValue parameters.
    //  We need to detect that there is nothing more to read, that we have an EOF condition.
    //  This might be an error, or we might just want to return an indicator.  the eofIsError/eofValue parameters determine this.
    //  One last little wrinkle: Sometimes when we read the next form, it is to be ignored.  So we have to try again.  Thinks of #_(foo bar).
    //  The #_ is a reader macro that says to ignore the next form.  So we read the next form, and then we have to try again to read the next form.
    //  In the Java/C# code, this was set up as an infinite loop.  Most branches of the code in the loop would return, except for the branch that noticed 'ignore this' macros;
    //    that branch did a continue.
    //  Because we don't have 'continue'/'return' constructs, we break the body out as an auxiliary function that returns an 'obj option'.  None means 'continue', Some means 'return'.
    //  And now I think we are ready to.
    //  Oh, yeah.  The Resolver is passed along -- it deals with namespace resolution issues.  And the pending forms are passed along -- they don't appear to actually be used at this time.
    static member private read(
        r: PushbackTextReader, 
        eofIsError: bool, 
        eofValue: obj, 
        returnOn: char option,
        returnOnValue: obj,
        isRecursive: bool,
        opts: obj,
        pendingForms: obj,
        resolverOpt : ReaderResolver option) : obj = 

        if UnknownKeyword.Equals((RTVar.ReadEvalVar :> IDeref).deref()) then
            raise <| invalidOp "Reading disallowed - *read-eval* bound to :unknown"

        let fullOpts = LispReader.InstallPlatformFeature(opts)

        let processPendingForm() : obj option =
            match pendingForms with
            | :? LinkedList<obj> as forms when forms.Count > 0 ->
                    let v = forms.First.Value
                    forms.RemoveFirst()
                    Some v
            | _ -> None

        let readNextNonWhitespaceChar() : int =
            let mutable ch = r.Read()
            while Char.IsWhiteSpace(char ch) do
                ch <- r.Read()
            ch

        // Do we really need a pushback reader here?  Why not use r.Peek()?
        let peekChar() : int = 
            let ch = r.Read()
            LispReader.unread(r, ch)
            ch

        let processMacro(macroFn: ReaderFunction, ch: int) : obj option =
            let ret = macroFn(r, (char ch), opts, pendingForms)
            if Object.ReferenceEquals(ret, r) then None          // no-op macros return the reader
            else Some ret


        let readNextForm() : obj option =
            let ch = readNextNonWhitespaceChar()
            if ch = -1 then
                if eofIsError then
                    raise <| new Exception("EOF while reading")
                else
                    Some eofValue
            elif returnOn.IsSome && ch = (int (returnOn.Value)) then
                Some returnOnValue
            elif Char.IsDigit(char ch) || ( ch = (int '+') || ch = (int '-') && Char.IsDigit(char <| peekChar())) then
                Some <| LispReader.readNumber(r, (char ch))
            else 
                match LispReader.getMacro(ch) with
                | Some macrofn ->  processMacro(macrofn, ch)
                | None ->
                    let rawToken, token, mask, eofSeen = LispReader.readToken(r, (char ch))
                    if eofSeen then
                        if eofIsError then
                            raise <| new EndOfStreamException("EOF while reading symbol")
                        else
                            Some eofValue
                    else
                        Some <| LispReader.interpretToken(rawToken, token, mask, resolverOpt)

        try
            let mutable retVal : obj option = None
            while retVal.IsNone do
                match processPendingForm() with
                | Some x as sx -> retVal <- sx
                | None -> retVal <- readNextForm()                
            retVal.Value
        with
        | ex ->
            if isRecursive then
                reraise()
            else
                match r with
                | :? LineNumberingTextReader as lnr ->
                    raise <| new ReaderException(ex.Message, lnr.LineNumber, lnr.ColumnNumber, ex)
                | _ -> reraise()

    // Used internally by many of the reader functions. Defaults to isrecursive, eofIsError. 
    static member private readAux(r: PushbackTextReader, opts: obj, pendingForms: obj) =
        LispReader.read(r, true, null, true, opts, pendingForms)

    // Character hacks

    static member internal isWhitespace(ch: int) = 
        Char.IsWhiteSpace(char ch) || ch = (int ',')

    static member unread(r: PushbackTextReader, ch: int) =
        if ch <> -1 then
            r.Unread(ch)
  
    // Roughly a match to Java Character.digit(char,int),
    // though I don't handle all unicode digits.
    static member charValueInRadix(c: int, radix: int) = 
        let ch = char c

        if (Char.IsDigit(ch)) then
            if c - (int '0') < radix then c - (int '0') else -1
        elif ('A' <= ch && ch <= 'Z') then
            if c - (int 'A') < radix - 10 then c - (int 'A') + 10 else -1
        elif ('a' <= ch && ch <= 'z') then
            if c - (int 'z') < radix - 10 then c - (int 'z') + 10 else -1
        else
            -1

    static member readUnicodeChar(token: string, offset: int, length: int, radix: int) : int =
        if token.Length <> offset + length then     
            raise <| new ArgumentException($"Invalid unicode character: \\{token}")

        let mutable uc = 0
        for i = offset to offset + length - 1 do
            let d = LispReader.charValueInRadix(int token[i],radix)
            if d = -1 then 
                raise <| new ArgumentException($"Invalid digit:{token[i]}")
            uc <- uc * radix + d

        uc


    static member readUnicodeChar(r: PushbackTextReader, initch: int, radix: int, length: int, exact:bool) : int =
        let uc = LispReader.charValueInRadix(initch, radix)
        if uc = -1 then
            raise <| new ArgumentException($"Invalid digit: {char initch}")

        let rec loop (i: int) (uc: int) = 
            if i = length then
                (i, uc)
            else
                let ch = r.Read()
                if ch = -1 || LispReader.isWhitespace(ch) || LispReader.isMacro(ch) then
                    LispReader.unread(r, ch)
                    (i, uc)
                else
                    let d = LispReader.charValueInRadix(ch, radix)
                    if d = -1 then
                        raise <| new ArgumentException($"Invalid digit: {char ch}")
                    loop (i + 1) (uc * radix + d)
        let (numRead, uc) = loop 1 uc
        if  numRead <> length && exact then
            raise <| new ArgumentException($"Invalid character length: {numRead}, should be {length}")
        uc
  

    // Misc. helpers

    static member readDelimitedList(delim: char, r: PushbackTextReader, isRecursive: bool, opts: obj, pendingForms: obj) = 

        let firstLine =
            match r with
            | :? LineNumberingTextReader as lntr -> lntr.LineNumber
            | _ -> -1

        let resolver = LispReader.getReaderResolver()

        seq { 
                let mutable finished = false
                while not finished do
                    let form = LispReader.read(r, false, ReadEOF, Some delim, ReadFinished, isRecursive, opts, pendingForms, resolver)
                    if form = ReadEOF then
                        if firstLine < 0 then
                            raise <| new EndOfStreamException("EOF while reading")
                        else
                            raise <| new EndOfStreamException($"EOF while reading, starting at line {firstLine}")
                    elif form = ReadFinished then
                        finished <- true
                    else
                        yield form                
            }

    static member garg(n:int) = 
        let prefix =  if n = -1 then "rest" else $"p"
        Symbol.intern(null, $"{prefix}__{RT0.nextID()}#" )


    // Reading tokens

    // ClojureCLR makes this harder than the corresponding Java code, due to the need to accommodate |....| syntax in symbols.
    // In fact, the Java version of readToken is what is called readSimpleToken below:  
    //     just read and accumulate characters until we hit EOF, whitespace or a terminating macro character.
    // with |-escaping, we have to work in different mode: raw mode and normal mode.
    // While we are in normal mode, we accumulate characters as in readSimpleToken and stop by the same criteria.
    // In raw mode, we accumulate characters until we hit a |.  When we hit a |, we have to lookahead to the next character to see if it is a |, in which case accumulate a single |.
    // If it is just a single |, we don't accumulate it and we switch back to normal mode.
    // For technical reasons, we accumulate two 'tokens' -- the raw token and the regular token.  
    // The raw token is the token as it appears in the source code.
    // The regular token is the token with the |'s stripped off.
    // Finally, we accumulate a mask that tells us which characters in the token were read while escaped.  
    // The mask has the character 'a' for each character in the token that was read while escaped.
    // Consider the token:   |///|/|//|
    // The raw token is:     |///|/|//|
    // The regular token is: //////
    // The mask is:          aaa/aa
    // We pass the mask to interpretToken, which passes it matchSymbol, which uses it to determine if the token is a symbol, a keyword, or an array symbol.
    // To do so, it interprests the mask using various regular expressions.  
    // Where there is a / or : in the mask, it is really there and can be used to delinate namespaces from names, distinguish keywords from symbols, etc.
    //
    // readSimpleToken is still useful in places where we don't need to worry about |...| syntax. This includes the character reader and arg reader.

    static member  readSimpleToken(r: PushbackTextReader, initch: char) =

        let sb = StringBuilder()
        sb.Append(initch)  |> ignore

        let rec loop() =
            let ch = r.Read()
            if ch = -1 || LispReader.isWhitespace(ch) || LispReader.isTerminatingMacro(ch) then
                LispReader.unread(r, ch)
                sb.ToString()
            else
                sb.Append(char ch) |> ignore
                loop()

        loop()

    static member readToken(r: PushbackTextReader, initch: char) =
        
        let allowSymEscape = RT0.booleanCast((RTVar.AllowSymbolEscapeVar :> IDeref).deref())
        let mutable rawMode = false

        let sbRaw = StringBuilder()
        let sbToken = StringBuilder()
        let sbMask = StringBuilder()

        if allowSymEscape && initch = '|' then
            rawMode <- true
            sbRaw.Append(initch) |> ignore
        else
            sbRaw.Append(initch) |> ignore
            sbToken.Append(initch) |> ignore
            sbMask.Append(initch) |> ignore

        // we stop looping if we run into a terminating character.
        // We grab the character and loop otherwise.
        // The definition of a terminating character depends on whether we are in raw mode or not.
        // The value returned by the loop if eofSeen -- if true, then we hit EOF while reading.
        // eofSeen can be true only in raw mode -- we hit eof before we found our closing '|'. This is an error.
        // If we hit eof in normal mode, we just stop reading the token -- this is just a terminating character.
        // Note that a closing '|' in raw mode is not a terminating character.  We can have a symbol like |a|b|c|d => abcd.
        let rec loop() =
            let ch = r.Read()
            if rawMode then
                if ch = -1 then
                    true   // EOF seen in raw mode
                elif ch = int '|' then
                    let ch2 = r.Read()
                    if ch2 = int '|' then
                        sbRaw.Append('|') |> ignore
                        sbToken.Append('|') |> ignore
                        sbMask.Append('a') |> ignore
                        loop()
                    else
                        LispReader.unread(r, ch2)
                        rawMode <- false
                        sbRaw.Append('|') |> ignore
                        loop()
                else
                    sbRaw.Append(char ch) |> ignore
                    sbToken.Append(char ch) |> ignore
                    sbMask.Append('a') |> ignore
                    loop()
            else // not raw mode
                if ch = -1 || LispReader.isWhitespace(ch) || LispReader.isTerminatingMacro(ch) then
                    LispReader.unread(r, ch)
                    false
                elif ch = int '|' && allowSymEscape then
                    rawMode <- true
                    sbRaw.Append('|') |> ignore
                    loop()
                else
                    sbRaw.Append(char ch) |> ignore
                    sbToken.Append(char ch) |> ignore
                    sbMask.Append(char ch) |> ignore
                    loop()
        let eofSeen = loop()
        (sbRaw.ToString(), sbToken.ToString(), sbMask.ToString(), eofSeen)

    static member interpretToken(token: string, resolver: ReaderResolver option) =
        LispReader.interpretToken(token, token, token, resolver)

    static member interpretToken(rawToken: string, token: string, mask: string, resolverOpt: ReaderResolver option) : obj =
        if token = "nil" then
            null
        elif token = "true" then
            true
        elif token = "false" then
            false
        else
            match LispReader.matchSymbol(token, mask, resolverOpt) with
            | null -> raise <| new ArgumentException($"Invalid token: {rawToken}")
            | ret -> ret


        // Java originals, for comparison
        //static Pattern symbolPat = Pattern.compile("[:]?([\\D&&[^/]].*/)?(/|[\\D&&[^/]][^/]*)");
        //static Pattern arraySymbolPat = Pattern.compile("([\\D&&[^/:]].*)/([1-9])");

        //static Regex symbolPat = new Regex("[:]?([\\D&&[^/]].*/)?(/|[\\D&&[^/]][^/]*)");
        //static readonly Regex arraySymbolPat = new Regex("([\\D&&[^/:]].*)/([1-9])");

        static member val symbolPat = Regex("^[:]?([^\\p{Nd}/].*/)?(/|[^\\p{Nd}/][^/]*)$")
        static member val arraySymbolPat = Regex("^([^\\p{Nd}/].*/)([1-9])$")
        static member val keywordPat = Regex("^[:]?([^/].*/)?(/|[^/][^/]*)$")
        static member val argPat = Regex("^%(?:(&)|([1-9][0-9]*))?$")

        static member private extractNamesUsingMask(token: string, maskNS: string, maskName: string) : (string * string) =
            if String.IsNullOrEmpty(maskNS) then
                (null, token)
            else
                (token.Substring(0, maskNS.Length - 1), token.Substring(maskNS.Length))

        static member matchSymbol(token: string, mask: string, resolverOpt: ReaderResolver option) =
            let m = LispReader.symbolPat.Match(mask)
            if m.Success then
                let maskNS = m.Groups.[1].Value
                let maskName = m.Groups.[2].Value
                if not <| isNull maskNS && maskNS.EndsWith(":/") 
                   || maskName.EndsWith(":") 
                   || mask.IndexOf("::", 1) <> -1 then
                    null
                else
                    if mask.StartsWith("::") then
                        let m2 = LispReader.keywordPat.Match(mask.Substring(2))
                        if not m2.Success then
                            null
                        else
                            let ns, name = LispReader.extractNamesUsingMask(token.Substring(2), m2.Groups.[1].Value, m2.Groups.[2].Value)
                            let ks = Symbol.intern(ns, name)
                            match resolverOpt with
                            | Some resolver  ->
                                let nsym = 
                                    if not <| isNull ks.Namespace then
                                        resolver.resolveAlias(Symbol.intern(ks.Namespace))
                                    else
                                        resolver.currentNS()
                                // auto-resolving keyword
                                if nsym <> null then
                                    Keyword.intern(nsym.Name, ks.Name)
                                else
                                    null
                            | _ ->
                                let kns = 
                                    if not <| isNull ks.Namespace  then
                                        LispReader.getCurrentNamespace().lookupAlias(Symbol.intern(ks.Namespace))
                                    else
                                        LispReader.getCurrentNamespace()
                                if not <| isNull kns then
                                    Keyword.intern(kns.Name.Name, ks.Name)
                                else
                                    null
                    else
                        let isKeyword = mask.[0] = ':'
                        if isKeyword then
                            let m2 = LispReader.keywordPat.Match(mask.Substring(1))
                            if not m2.Success then
                                null
                            else
                                let ns, name = LispReader.extractNamesUsingMask(token.Substring(1), m2.Groups.[1].Value, m2.Groups.[2].Value)
                                Keyword.intern(ns, name)
                        else
                            let ns, name = LispReader.extractNamesUsingMask(token, maskNS, maskName)
                            Symbol.intern(ns, name)
            else
                let m3 = LispReader.arraySymbolPat.Match(mask)
                if m3.Success then
                    let maskNS = m3.Groups.[1].Value
                    let maskName = m3.Groups.[2].Value
                    let ns, name = LispReader.extractNamesUsingMask(token, maskNS, maskName)
                    Symbol.intern(ns, name)
                else
                    null


    // Symbol printing helpers

    static member nameRequiresEscaping(s: string) = 
        let isBadChar(c) = c = '|' || c = '/' || LispReader.isWhitespace(int c) || LispReader.isTerminatingMacro(int c)
        let isBadFirstChar(c) = c = ':' || LispReader.isMacro(int c) || Char.IsDigit(c)
   
        if String.IsNullOrEmpty(s) then
            true
        else
            let firstChar = s[0]

            s.ToCharArray() |> Array.exists isBadChar ||
            isBadChar(firstChar) || 
            s.Contains("::") ||
            ((firstChar = '+' || firstChar = '-') && s.Length >=2 && Char.IsDigit(s[1]))

    static member vbarEscape(s:string) =
        let sb = StringBuilder()
        sb.Append('|') |> ignore
        s.ToCharArray() 
        |> Array.iter (fun c -> 
                sb.Append(c) |> ignore
                if c = '|' then sb.Append('|') |> ignore   )
        sb.Append('|') |> ignore
        sb.ToString()


    // Reading numbers

    static member val intRE = Regex("^([-+]?)(?:(0)|([1-9][0-9]*)|0[xX]([0-9A-Fa-f]+)|0([0-7]+)|([1-9][0-9]?)[rR]([0-9A-Za-z]+)|0[0-9]+)(N)?$")
    static member val ratioRE = Regex("^([-+]?[0-9]+)/([0-9]+)$")
    static member val floatRE = Regex("^([-+]?[0-9]+(\\.[0-9]*)?([eE][-+]?[0-9]+)?)(M)?$")

    static member readNumber(r: PushbackTextReader, initch: char) : obj =
        let sb = StringBuilder()
        sb.Append(initch) |> ignore
        let rec loop() =
            let ch = r.Read()
            if ch = -1 || LispReader.isWhitespace(ch) || LispReader.isMacro(ch) then
                LispReader.unread(r, ch)
                sb.ToString()
            else
                sb.Append(char ch) |> ignore
                loop()
        let s = loop()
        match LispReader.matchNumber(s) with
        | None -> raise <| new FormatException($"Invalid number: {s}")
        | Some  n -> n

    // In our old clojure.lang.BigNumber, we had an 
    static member parseBigIntegerInRadix(s: string, radix: int) =
        // TODO:  Need to enable other radixes
        BigInteger.Parse(s)

    static member bigIntegerAsInt64(bi: BigInteger) =
        if bi < Int64.MinValue || bi > Int64.MaxValue then
            None
        else
            Some (int64 bi)


    static member matchInteger(m: Match) : obj option =
        if m.Groups.[2].Success then
            if m.Groups.[8].Success then
                Some BigInt.ZERO
            else
                Some 0L
        else
            let isNeg = m.Groups.[1].Value = "-"
            let n = 
                if m.Groups.[3].Success then
                    m.Groups.[3].Value, 10
                elif m.Groups.[4].Success then
                    m.Groups.[4].Value, 16
                elif m.Groups.[5].Success then
                    m.Groups.[5].Value, 8
                elif m.Groups.[7].Success then
                    m.Groups.[7].Value, Int32.Parse(m.Groups.[6].Value, System.Globalization.CultureInfo.InvariantCulture)
                else
                    null, -1
            match n with
            | null, _ -> None
            | n, radix ->
                let bn = LispReader.parseBigIntegerInRadix(n, radix)
                let bn = if isNeg then -bn else bn
                Some <| 
                if m.Groups.[8].Success then
                    BigInt.fromBigInteger(bn)
                else
                    match LispReader.bigIntegerAsInt64(bn) with
                    | Some ln -> ln :> obj
                    | None -> BigInt.fromBigInteger(bn)

    static member matchFloat(m: Match, s: string) : obj option =
        Some <|
                if m.Groups.[4].Success then
                    BigDecimal.Parse(m.Groups.[1].Value)
                else
                    Double.Parse(s, System.Globalization.CultureInfo.InvariantCulture)

    static member matchRatio(m: Match) : obj option =
        if m.Success then
            let numerString = m.Groups.[1].Value
            let denomString = m.Groups.[2].Value
            let numerString  = if numerString.[0] = '+' then numerString.Substring(1) else numerString
            Some <|Numbers.divide(
                Numbers.ReduceBigInt(BigInt.fromBigInteger(BigInteger.Parse(numerString))),
                Numbers.ReduceBigInt(BigInt.fromBigInteger(BigInteger.Parse(denomString))))
        else
            None

    // An obvious candidate for railway style
    static member matchNumber(s: string) : obj option =
        let m = LispReader.intRE.Match(s)
        if m.Success then
            LispReader.matchInteger(m)
        else
            let m2 = LispReader.floatRE.Match(s)
            if m2.Success then
                LispReader.matchFloat(m2, s)
            else
                let m3 = LispReader.ratioRE.Match(s)
                LispReader.matchRatio(m3)




    (*


    *)