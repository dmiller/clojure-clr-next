namespace Clojure.IO

open Clojure.Collections
open Clojure.Lib
open System.Collections.Generic
open System
open System.IO
open System.Linq
open System.Text
open System.Text.RegularExpressions
open Clojure.Numerics
open System.Numerics
open Clojure.BigArith
open Clojure.Reflection

type ReaderResolver =
    abstract currentNS: unit -> Symbol
    abstract resolveClass: Symbol -> Symbol
    abstract resolveAlias: Symbol -> Symbol
    abstract resolveVar: Symbol -> Symbol

// Temporarily located here until we solve the problem back in Clojure.Collections with a followon to RTSeq
[<AbstractClass; Sealed>]
type RTReader() =

    static member mapUniqueKeys([<ParamArray>] init: obj[]) : IPersistentMap =
        if isNull init then
            PersistentArrayMap.Empty
        elif init.Length <= PersistentArrayMap.hashtableThreshold then
            new PersistentArrayMap(init)
        else
            PersistentHashMap.create (init)

    static member map([<ParamArray>] init: obj[]) : IPersistentMap =
        if isNull init || init.Length = 0 then
            PersistentArrayMap.Empty
        elif init.Length <= PersistentArrayMap.hashtableThreshold then
            PersistentArrayMap.createWithCheck (init)
        else
            PersistentHashMap.createWithCheck (init)

    // the following used to be in clojure.lang.Compiler, but are really used only here(?)

    // THis could almost be in RTVar,given that it is namespace/var-related, but it also needs things from RTType.
    // So maybe this is the best place

    static member val NsSym = Symbol.intern ("ns")
    static member val InNsSym = Symbol.intern ("in-ns")

        // These are used externally 
    static member val LineKeyword = Keyword.intern (null, "line")
    static member val ColumnKeyword = Keyword.intern (null, "column")
    static member val FileKeyword = Keyword.intern (null, "file")
    static member val SourceSpanKeyword = Keyword.intern (null, "source-span")
    static member val StartLineKeyword = Keyword.intern (null, "start-line")
    static member val StartColumnKeyword = Keyword.intern (null, "start-column")
    static member val EndLineKeyword = Keyword.intern (null, "end-line")
    static member val EndColumnKeyword = Keyword.intern (null, "end-column")

    static member MaybeResolveIn(n: Namespace, sym: Symbol) : obj =
        // note: ns-qualified vars must already exist
        if not <| isNull sym.Namespace then
            let ns = RTReader.NamespaceFor(sym)

            if isNull ns then
                RTType.MaybeArrayType(sym)
            else
                ns.findInternedVar (Symbol.intern (sym.Name))
        elif
            sym.Name.IndexOf('.') > 0 && not <| sym.Name.EndsWith(".")
            || sym.Name.Length > 0 && sym.Name[sym.Name.Length - 1] = ']'
        then // TODO: What is this?  I don't remember what this is for.
            RTType.ClassForName(sym.Name)
        elif sym.Equals(RTReader.NsSym) then
            RTVar.NsVar
        elif sym.Equals(RTReader.InNsSym) then
            RTVar.InNSVar
        else
            n.getMapping (sym)

    static member NamespaceFor(sym: Symbol) : Namespace =
        RTReader.NamespaceFor(RTVar.getCurrentNamespace (), sym)

    static member NamespaceFor(inns: Namespace, sym: Symbol) : Namespace =
        //note, presumes non-nil sym.ns
        // first check against currentNS' aliases...
        let nsSym = Symbol.intern (sym.Namespace)

        match inns.lookupAlias (nsSym) with
        | null -> Namespace.find (nsSym)
        | _ as ns -> ns

    // THere is some duplicate code here.  Can we consolidate?
    // THis is also used to be in Compiler, but is only used in LispReader.
    static member ResolveSymbol(sym: Symbol) =
        // already qualifed or classname?
        if sym.Name.IndexOf('.') > 0 then
            sym
        elif not <| isNull sym.Namespace then
            let ns = RTReader.NamespaceFor(sym)

            // This test is nasty.  The second half says ns.Name.Name and sym.Namespace match. Whether null or not.
            if
                isNull ns
                || (if isNull ns.Name.Name then
                        isNull sym.Namespace
                    else
                        ns.Name.Name.Equals(sym.Namespace))
            then
                match RTType.MaybeArrayType(sym) with
                | null -> sym
                | _ as at -> RTReader.ArrayTypeToSymbol(at)
            else
                // we know ns is not null at this point
                Symbol.intern (ns.Name.Name, sym.Name)
        else
            let currentNS = RTVar.getCurrentNamespace ()

            match currentNS.getMapping (sym) with
            | null -> Symbol.intern (currentNS.Name.Name, sym.Name)
            | :? Type as ot -> Symbol.intern (null, Util.nameForType (ot))
            | :? Var as v -> Symbol.intern (v.Namespace.Name.Name, v.Symbol.Name)
            | _ -> null

    static member ArrayTypeToSymbol(t: Type) =

        let rec loop (t: Type) (dim: int) =
            if t.IsArray && dim <= 9 then
                loop (t.GetElementType()) (dim + 1)
            else
                t, dim

        let componentType, dim = loop t 0

        if dim <= 9 && dim >= 1 then
            let nameOpt = RTType.TryPrimTypeToName(componentType)

            match nameOpt with
            | Some name -> Symbol.intern (name + dim.ToString())
            | None -> Symbol.intern (componentType.FullName, dim.ToString())
        else
            Symbol.intern (null, t.FullName)

    static member NamesStaticMember(sym: Symbol) =
        not <| isNull sym.Namespace && isNull <| RTReader.NamespaceFor(sym)


type ReaderFunction = PushbackTextReader * char * obj * obj -> obj

type ReaderException(msg: string, line: int, column: int, inner: exn) =
    inherit Exception(msg, inner)

    static member val ErrNS = "clojure.error"
    static member val ErrLine = Keyword.intern (ReaderException.ErrNS, "line")
    static member val ErrColumn = Keyword.intern (ReaderException.ErrNS, "column")

    member val data: IPersistentMap =
        if line > 0 then
            RTReader.map (ReaderException.ErrLine, line, ReaderException.ErrColumn, column)
        else
            null

    new() = ReaderException(null, -1, -1, null)
    new(msg: string) = ReaderException(msg, -1, -1, null)
    new(msg: string, inner: exn) = ReaderException(msg, -1, -1, inner)



[<AbstractClass; Sealed>]
type LispReader() =

    // Symbol definitions

    static let QuoteSym = Symbol.intern ("quote")
    static let TheVarSym = Symbol.intern ("var")
    static let UnquoteSym = Symbol.intern ("clojure.core", "unquote")
    static let UnquoteSplicingSym = Symbol.intern ("clojure.core", "unquote-splicing")
    static let DerefSym = Symbol.intern ("clojure.core", "deref")
    static let ApplySym = Symbol.intern ("clojure.core", "apply")
    static let ConcatSym = Symbol.intern ("clojure.core", "concat")
    static let HashMapSym = Symbol.intern ("clojure.core", "hash-map")
    static let HashSetSym = Symbol.intern ("clojure.core", "hash-set")
    static let VectorSym = Symbol.intern ("clojure.core", "vector")
    static let ListSym = Symbol.intern ("clojure.core", "list")
    static let WithMetaSym = Symbol.intern ("clojure.core", "with-meta")
    static let SeqSym = Symbol.intern ("clojure.core", "seq")



    // Compiler special forms
    // Obviously this should be over in the Clojure.Compiler, but it is used here.
    // I had hoped to have a version of LispReader that could be delivered separately, but perhaps that is just the EdnReader and LispReader should reside with the compiler?

    static let DefSym = Symbol.intern ("def")
    static let LoopSym = Symbol.intern ("loop*")
    static let RecurSym = Symbol.intern ("recur")
    static let IfSym = Symbol.intern ("if")
    static let CaseSym = Symbol.intern ("case*")
    static let LetSym = Symbol.intern ("let*")
    static let LetfnSym = Symbol.intern ("letfn*")
    static let DoSym = Symbol.intern ("do")
    static let FnSym = Symbol.intern ("fn*")
    static let QuoteSym = Symbol.intern ("quote")
    static let TheVarSym = Symbol.intern ("var")
    static let ImportSym = Symbol.intern ("clojure.core", "import*")
    static let DotSym = Symbol.intern (".")
    static let AssignSym = Symbol.intern ("set!")
    static let DeftypeSym = Symbol.intern ("deftype*")
    static let ReifySym = Symbol.intern ("reify*")
    static let TrySym = Symbol.intern ("try")
    static let ThrowSym = Symbol.intern ("throw")
    static let MonitorEnterSym = Symbol.intern ("monitor-enter")
    static let MonitorExitSym = Symbol.intern ("monitor-exit")
    static let CatchSym = Symbol.intern ("catch")
    static let FinallySym = Symbol.intern ("finally")
    static let NewSym = Symbol.intern ("new")
    static let AmpersandSym = Symbol.intern ("&")

    static let CompilerSpecialSymbols =
        PersistentHashSet.create (
            DefSym,
            LoopSym,
            RecurSym,
            IfSym,
            CaseSym,
            LetSym,
            LetfnSym,
            DoSym,
            FnSym,
            QuoteSym,
            TheVarSym,
            ImportSym,
            DotSym,
            AssignSym,
            DeftypeSym,
            ReifySym,
            TrySym,
            ThrowSym,
            MonitorEnterSym,
            MonitorExitSym,
            CatchSym,
            FinallySym,
            NewSym,
            AmpersandSym
        )

    // Keyword definitions

    static let UnknownKeyword = Keyword.intern (null, "unknown")


    static let TagKeyword = Keyword.intern (null, "tag")
    static let ParamTagsKeyword = Keyword.intern (null, "param-tags")

    // Parser options

    static let OptEofKeword = Keyword.intern (null, "eof")
    static let OptFeaturesKeyword = Keyword.intern (null, "features")
    static let OptReadCondKeyword = Keyword.intern (null, "read-cond")

    static let EofOptionsDefault: bool * obj = (true, null)

    // Platform features - always installed

    static let PlatformKey = Keyword.intern (null, "cljr")
    static let PlatformFeatureSet = PersistentHashSet.create (PlatformKey)

    // Reader conditional options - use with :read-cond

    static let CondAllowKeyword = Keyword.intern (null, "allow")
    static let CondPreserveKeyword = Keyword.intern (null, "preserve")


    // EOF special value to throw on eof
    static let EofThrowKeyword = Keyword.intern (null, "eofthrow")

    // Sentinel values for reading lists
    static let ReadEOF = obj ()
    static let ReadFinished = obj ()
    static let ReadStarted = obj ()

    // Var environments

    /// Dynamically bound Var to a map from Symbols to ...  (symbol -> gensymbol)
    static let GensymEnvVar = Var.create(null).setDynamic ()

    /// sarted-map num -> gensymbol
    static let ArgEnvVar = Var.create(null).setDynamic ()

    /// Dynamically bound Var set to true in a read-cond context
    static let ReadCondEnvVar = Var.create(false).setDynamic ()

    static let DataReadersVar =
        Var
            .intern(Namespace.ClojureNamespace, Symbol.intern ("*data-readers*"), RTMap.map ())
            .setDynamic ()

    static let DefaultDataReaderFnVar =
        Var.intern (Namespace.ClojureNamespace, Symbol.intern ("*default-data-reader-fn*"), RTMap.map ())

    static let DefaultDataReadersVar =
        Var.intern (Namespace.ClojureNamespace, Symbol.intern ("default-data-readers"), RTMap.map ())

    // macro characters and #-dispatch
    static let macros = Array.zeroCreate<ReaderFunction option> (256)
    static let dispatchMacros = Array.zeroCreate<ReaderFunction option> (256)

    // Conditional reading

    static let DefaultFeatureKeyword = Keyword.intern (null, "default")

    static let ReservedFeaturesSet =
        RTMap.set (Keyword.intern (null, "else"), Keyword.intern (null, "none"))


    // A couple of constants
    static let MaxLongAsBigInteger = BigInteger(Int64.MaxValue)
    static let MinLongAsBigInteger = BigInteger(Int64.MinValue)


    static do
        macros[int '"'] <- Some LispReader.stringReader
        macros[int ';'] <- Some LispReader.commentReader
        macros[int '\''] <- Some <| LispReader.wrappingReader (QuoteSym)
        macros[int '@'] <- Some <| LispReader.wrappingReader (DerefSym)
        macros[int '^'] <- Some LispReader.metaReader
        macros[int '`'] <- Some LispReader.syntaxQuoteReader
        macros[int '~'] <- Some LispReader.unquoteReader
        macros[int '('] <- Some LispReader.listReader
        macros[int ')'] <- Some LispReader.unmatchedDelimiterReader
        macros[int '['] <- Some LispReader.vectorReader
        macros[int ']'] <- Some LispReader.unmatchedDelimiterReader
        macros[int '{'] <- Some LispReader.mapReader
        macros[int '}'] <- Some LispReader.unmatchedDelimiterReader
        macros[int '\\'] <- Some LispReader.characterReader
        macros[int '%'] <- Some LispReader.argReader
        macros[int '#'] <- Some LispReader.dispatchReader

        dispatchMacros[int '^'] <- Some LispReader.metaReader
        dispatchMacros[int '#'] <- Some LispReader.symbolicValueReader
        dispatchMacros[int '\''] <- Some LispReader.varReader
        dispatchMacros[int '"'] <- Some LispReader.regexReader
        dispatchMacros[int '('] <- Some LispReader.fnReader
        dispatchMacros[int '{'] <- Some LispReader.setReader
        dispatchMacros[int '='] <- Some LispReader.evalReader
        dispatchMacros[int '!'] <- Some LispReader.commentReader
        dispatchMacros[int '<'] <- Some LispReader.unreadableReader
        dispatchMacros[int '_'] <- Some LispReader.discardReader
        dispatchMacros[int '?'] <- Some LispReader.conditionalReader
        dispatchMacros[int ':'] <- Some LispReader.namespaceMapReader

    static member isMacro(ch: int) = ch < macros.Length && macros[ch].IsSome

    static member getMacro(ch: int) =
        if ch < macros.Length then macros.[ch] else None

    static member isTerminatingMacro(ch: int) =
        LispReader.isMacro (ch)
        && ch <> (int '#')
        && ch <> (int '\'')
        && ch <> (int '%')

    static member parseEofOptions(opts: obj) =
        match opts with
        | :? IPersistentMap as optsMap ->
            let eofValue = optsMap.valAt (OptEofKeword, EofThrowKeyword)

            if EofThrowKeyword.Equals(eofValue) then
                EofOptionsDefault
            else
                (false, eofValue)
        | _ -> EofOptionsDefault

    static member ensurePending(pendingForms: obj) : obj =
        match pendingForms with
        | null -> LinkedList<obj>()
        | _ -> pendingForms

    static member installPlatformFeature(opts: obj) : obj =
        match opts with
        | null -> RTReader.mapUniqueKeys (OptFeaturesKeyword, PlatformFeatureSet)
        | :? IPersistentMap as mopts ->
            match mopts.valAt (OptFeaturesKeyword) with
            | null -> mopts.assoc (OptFeaturesKeyword, PlatformFeatureSet)
            | :? IPersistentSet as features -> mopts.assoc (OptFeaturesKeyword, RTSeq.conj (features, PlatformKey))
            | _ -> raise <| invalidOp "LispReader: the value of :features must be a set"
        | _ -> raise <| invalidOp "LispReader options must be a map"

    static member getReaderResolver() : ReaderResolver option =
        let v = (RTVar.ReaderResolverVar :> IDeref).deref ()

        match v with
        | :? ReaderResolver as r -> Some r
        | _ -> None

    static member getGensymEnv() : IPersistentMap option =
        let m = (GensymEnvVar :> IDeref).deref ()

        match m with
        | :? IPersistentMap as pm -> Some pm
        | _ -> None

    // Entry points for reading

    static member read(r: PushbackTextReader, opts: obj) =
        let eofIsError, eofValue = LispReader.parseEofOptions opts
        LispReader.read (r, eofIsError, eofValue, false, opts)

    static member read(r: PushbackTextReader, eofIsError: bool, eofValue: obj, isRecursive: bool) =
        LispReader.read (r, eofIsError, eofValue, isRecursive, PersistentHashMap.Empty)

    static member read(r: PushbackTextReader, eofIsError: bool, eofValue: obj, isRecursive: bool, opts: obj) =
        LispReader.read (r, eofIsError, eofValue, None, null, isRecursive, opts, null, LispReader.getReaderResolver ())

    static member private read
        (
            r: PushbackTextReader,
            eofIsError: bool,
            eofValue: obj,
            isRecursive: bool,
            opts: obj,
            pendingForms: obj
        ) =
        LispReader.read (
            r,
            eofIsError,
            eofValue,
            None,
            null,
            isRecursive,
            opts,
            LispReader.ensurePending (pendingForms),
            LispReader.getReaderResolver ()
        )

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
    static member private read
        (
            r: PushbackTextReader,
            eofIsError: bool,
            eofValue: obj,
            returnOn: char option,
            returnOnValue: obj,
            isRecursive: bool,
            opts: obj,
            pendingForms: obj,
            resolverOpt: ReaderResolver option
        ) : obj =

        if UnknownKeyword.Equals((RTVar.ReadEvalVar :> IDeref).deref ()) then
            raise <| invalidOp "Reading disallowed - *read-eval* bound to :unknown"

        let fullOpts = LispReader.installPlatformFeature (opts)

        let processPendingForm () : obj option =
            match pendingForms with
            | :? LinkedList<obj> as forms when forms.Count > 0 ->
                let v = forms.First.Value
                forms.RemoveFirst()
                Some v
            | _ -> None

        let readNextNonWhitespaceChar () : int =
            let mutable ch = r.Read()

            while Char.IsWhiteSpace(char ch) do
                ch <- r.Read()

            ch

        // Do we really need a pushback reader here?  Why not use r.Peek()?
        let peekChar () : int =
            let ch = r.Read()
            LispReader.unread (r, ch)
            ch

        let processMacro (macroFn: ReaderFunction, ch: int) : obj option =
            let ret = macroFn (r, (char ch), opts, pendingForms)

            if Object.ReferenceEquals(ret, r) then
                None // no-op macros return the reader
            else
                Some ret


        let readNextForm () : obj option =
            let ch = readNextNonWhitespaceChar ()

            if ch = -1 then
                if eofIsError then
                    raise <| new EndOfStreamException("EOF while reading")
                else
                    Some eofValue
            elif returnOn.IsSome && ch = (int (returnOn.Value)) then
                Some returnOnValue
            elif
                Char.IsDigit(char ch)
                || (  (ch = (int '+') || ch = (int '-')) && Char.IsDigit(char <| peekChar ()))
            then
                Some <| LispReader.readNumber (r, (char ch))
            else
                match LispReader.getMacro (ch) with
                | Some macrofn -> processMacro (macrofn, ch)
                | None ->
                    let rawToken, token, mask, eofSeen = LispReader.readToken (r, (char ch))

                    if eofSeen then
                        if eofIsError then
                            raise <| new EndOfStreamException("EOF while reading symbol")
                        else
                            Some eofValue
                    else
                        Some <| LispReader.interpretToken (rawToken, token, mask, resolverOpt)

        try
            let mutable retVal: obj option = None

            while retVal.IsNone do
                match processPendingForm () with
                | Some x as sx -> retVal <- sx
                | None -> retVal <- readNextForm ()

            retVal.Value
        with ex ->
            if isRecursive then
                reraise ()
            else
                match r with
                | :? LineNumberingTextReader as lnr ->
                    raise <| new ReaderException(ex.Message, lnr.LineNumber, lnr.ColumnNumber, ex)
                | _ -> reraise ()

    // Used internally by many of the reader functions. Defaults to isrecursive, eofIsError.
    static member private readAux(r: PushbackTextReader, opts: obj, pendingForms: obj) =
        LispReader.read (r, true, null, true, opts, pendingForms)

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
            if c - (int 'A') < radix - 10 then
                c - (int 'A') + 10
            else
                -1
        elif ('a' <= ch && ch <= 'z') then
            if c - (int 'z') < radix - 10 then
                c - (int 'a') + 10
            else
                -1
        else
            -1

    static member readUnicodeChar(token: string, offset: int, length: int, radix: int) : int =
        if token.Length <> offset + length then
            raise <| new ArgumentException($"Invalid unicode character: \\{token}")

        let mutable uc = 0

        for i = offset to offset + length - 1 do
            let d = LispReader.charValueInRadix (int token[i], radix)

            if d = -1 then
                raise <| new ArgumentException($"Invalid digit:{token[i]}")

            uc <- uc * radix + d

        uc


    static member readUnicodeChar(r: PushbackTextReader, initch: int, radix: int, length: int, exact: bool) : int =
        let uc = LispReader.charValueInRadix (initch, radix)

        if uc = -1 then
            raise <| new ArgumentException($"Invalid digit: {char initch}")

        let rec loop (i: int) (uc: int) =
            if i = length then
                (i, uc)
            else
                let ch = r.Read()

                if ch = -1 || LispReader.isWhitespace (ch) || LispReader.isMacro (ch) then
                    LispReader.unread (r, ch)
                    (i, uc)
                else
                    let d = LispReader.charValueInRadix (ch, radix)

                    if d = -1 then
                        raise <| new ArgumentException($"Invalid digit: {char ch}")

                    loop (i + 1) (uc * radix + d)

        let (numRead, uc) = loop 1 uc

        if numRead <> length && exact then
            raise
            <| new ArgumentException($"Invalid character length: {numRead}, should be {length}")

        uc


    // Misc. helpers

    static member readDelimitedList
        (
            delim: char,
            r: PushbackTextReader,
            isRecursive: bool,
            opts: obj,
            pendingForms: obj
        ) =

        let firstLine =
            match r with
            | :? LineNumberingTextReader as lntr -> lntr.LineNumber
            | _ -> -1

        let resolver = LispReader.getReaderResolver ()

        seq {
            let mutable finished = false

            while not finished do
                let form =
                    LispReader.read (
                        r,
                        false,
                        ReadEOF,
                        Some delim,
                        ReadFinished,
                        isRecursive,
                        opts,
                        pendingForms,
                        resolver
                    )

                if form = ReadEOF then
                    if firstLine < 0 then
                        raise <| new EndOfStreamException("EOF while reading")
                    else
                        raise
                        <| new EndOfStreamException($"EOF while reading, starting at line {firstLine}")
                elif form = ReadFinished then
                    finished <- true
                else
                    yield form
        }

    static member garg(n: int) =
        let prefix = if n = -1 then "rest" else $"p"
        Symbol.intern (null, $"{prefix}__{RT0.nextID ()}#")


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

    static member readSimpleToken(r: PushbackTextReader, initch: char) =

        let sb = StringBuilder()
        sb.Append(initch) |> ignore

        let rec loop () =
            let ch = r.Read()

            if ch = -1 || LispReader.isWhitespace (ch) || LispReader.isTerminatingMacro (ch) then
                LispReader.unread (r, ch)
                sb.ToString()
            else
                sb.Append(char ch) |> ignore
                loop ()

        loop ()

    static member readToken(r: PushbackTextReader, initch: char) =

        let allowSymEscape =
            RT0.booleanCast ((RTVar.AllowSymbolEscapeVar :> IDeref).deref ())

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
        let rec loop () =
            let ch = r.Read()

            if rawMode then
                if ch = -1 then
                    true // EOF seen in raw mode
                elif ch = int '|' then
                    let ch2 = r.Read()

                    if ch2 = int '|' then
                        sbRaw.Append('|') |> ignore
                        sbToken.Append('|') |> ignore
                        sbMask.Append('a') |> ignore
                        loop ()
                    else
                        LispReader.unread (r, ch2)
                        rawMode <- false
                        sbRaw.Append('|') |> ignore
                        loop ()
                else
                    sbRaw.Append(char ch) |> ignore
                    sbToken.Append(char ch) |> ignore
                    sbMask.Append('a') |> ignore
                    loop ()
            else // not raw mode
                if
                    ch = -1 || LispReader.isWhitespace (ch) || LispReader.isTerminatingMacro (ch)
                then
                    LispReader.unread (r, ch)
                    false
                elif ch = int '|' && allowSymEscape then
                    rawMode <- true
                    sbRaw.Append('|') |> ignore
                    loop ()
                else
                    sbRaw.Append(char ch) |> ignore
                    sbToken.Append(char ch) |> ignore
                    sbMask.Append(char ch) |> ignore
                    loop ()

        let eofSeen = loop ()
        (sbRaw.ToString(), sbToken.ToString(), sbMask.ToString(), eofSeen)

    static member interpretToken(token: string, resolver: ReaderResolver option) =
        LispReader.interpretToken (token, token, token, resolver)

    static member interpretToken
        (
            rawToken: string,
            token: string,
            mask: string,
            resolverOpt: ReaderResolver option
        ) : obj =
        if token = "nil" then
            null
        elif token = "true" then
            true
        elif token = "false" then
            false
        else
            match LispReader.matchSymbol (token, mask, resolverOpt) with
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

            if
                not <| isNull maskNS && maskNS.EndsWith(":/")
                || maskName.EndsWith(":")
                || mask.IndexOf("::", 1) <> -1
            then
                null
            else if mask.StartsWith("::") then
                let m2 = LispReader.keywordPat.Match(mask.Substring(2))

                if not m2.Success then
                    null
                else
                    let ns, name =
                        LispReader.extractNamesUsingMask (token.Substring(2), m2.Groups.[1].Value, m2.Groups.[2].Value)

                    let ks = Symbol.intern (ns, name)

                    match resolverOpt with
                    | Some resolver ->
                        let nsym =
                            if not <| isNull ks.Namespace then
                                resolver.resolveAlias (Symbol.intern (ks.Namespace))
                            else
                                resolver.currentNS ()
                        // auto-resolving keyword
                        if nsym <> null then
                            Keyword.intern (nsym.Name, ks.Name)
                        else
                            null
                    | _ ->
                        let kns =
                            if not <| isNull ks.Namespace then
                                RTVar.getCurrentNamespace().lookupAlias (Symbol.intern (ks.Namespace))
                            else
                                RTVar.getCurrentNamespace ()

                        if not <| isNull kns then
                            Keyword.intern (kns.Name.Name, ks.Name)
                        else
                            null
            else
                let isKeyword = mask.[0] = ':'

                if isKeyword then
                    let m2 = LispReader.keywordPat.Match(mask.Substring(1))

                    if not m2.Success then
                        null
                    else
                        let ns, name =
                            LispReader.extractNamesUsingMask (
                                token.Substring(1),
                                m2.Groups.[1].Value,
                                m2.Groups.[2].Value
                            )

                        Keyword.intern (ns, name)
                else
                    let ns, name = LispReader.extractNamesUsingMask (token, maskNS, maskName)
                    Symbol.intern (ns, name)
        else
            let m3 = LispReader.arraySymbolPat.Match(mask)

            if m3.Success then
                let maskNS = m3.Groups.[1].Value
                let maskName = m3.Groups.[2].Value
                let ns, name = LispReader.extractNamesUsingMask (token, maskNS, maskName)
                Symbol.intern (ns, name)
            else
                null


    // Symbol printing helpers

    static member nameRequiresEscaping(s: string) =
        let isBadChar (c) =
            c = '|'
            || c = '/'
            || LispReader.isWhitespace (int c)
            || LispReader.isTerminatingMacro (int c)

        let isBadFirstChar (c) =
            c = ':' || LispReader.isMacro (int c) || Char.IsDigit(c)

        if String.IsNullOrEmpty(s) then
            true
        else
            let firstChar = s[0]

            s.ToCharArray() |> Array.exists isBadChar
            || isBadChar (firstChar)
            || s.Contains("::")
            || ((firstChar = '+' || firstChar = '-') && s.Length >= 2 && Char.IsDigit(s[1]))

    static member vbarEscape(s: string) =
        let sb = StringBuilder()
        sb.Append('|') |> ignore

        s.ToCharArray()
        |> Array.iter (fun c ->
            sb.Append(c) |> ignore

            if c = '|' then
                sb.Append('|') |> ignore)

        sb.Append('|') |> ignore
        sb.ToString()


    // Reading numbers

    static member val intRE =
        Regex(
            "^([-+]?)(?:(0)|([1-9][0-9]*)|0[xX]([0-9A-Fa-f]+)|0([0-7]+)|([1-9][0-9]?)[rR]([0-9A-Za-z]+)|0[0-9]+)(N)?$"
        )

    static member val ratioRE = Regex("^([-+]?[0-9]+)/([0-9]+)$")
    static member val floatRE = Regex("^([-+]?[0-9]+(\\.[0-9]*)?([eE][-+]?[0-9]+)?)(M)?$")

    static member readNumber(r: PushbackTextReader, initch: char) : obj =
        let sb = StringBuilder()
        sb.Append(initch) |> ignore

        let rec loop () =
            let ch = r.Read()

            if ch = -1 || LispReader.isWhitespace (ch) || LispReader.isMacro (ch) then
                LispReader.unread (r, ch)
                sb.ToString()
            else
                sb.Append(char ch) |> ignore
                loop ()

        let s = loop ()

        match LispReader.matchNumber (s) with
        | None -> raise <| new FormatException($"Invalid number: {s}")
        | Some n -> n

    static member bigIntegerAsInt64(bi: BigInteger) =
        if bi < MinLongAsBigInteger || bi > MaxLongAsBigInteger then
            None
        else
            Some(int64 bi)


    static member matchInteger(m: Match) : obj option =
        if m.Groups.[2].Success then
            if m.Groups.[8].Success then Some BigInt.ZERO else Some 0L
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
                    m.Groups.[7].Value,
                    Int32.Parse(m.Groups.[6].Value, System.Globalization.CultureInfo.InvariantCulture)
                else
                    null, -1

            match n with
            | null, _ -> None
            | n, radix ->
                let bn = BigIntegerExtensions.Parse(n, radix)
                let bn = if isNeg then -bn else bn

                Some
                <| if m.Groups.[8].Success then
                       BigInt.fromBigInteger (bn)
                   else
                       match LispReader.bigIntegerAsInt64 (bn) with
                       | Some ln -> ln :> obj
                       | None -> BigInt.fromBigInteger (bn)

    static member matchFloat(m: Match, s: string) : obj option =
        Some
        <| if m.Groups.[4].Success then
               BigDecimal.Parse(m.Groups.[1].Value)
           else
               Double.Parse(s, System.Globalization.CultureInfo.InvariantCulture)

    static member matchRatio(m: Match) : obj option =
        if m.Success then
            let numerString = m.Groups.[1].Value
            let denomString = m.Groups.[2].Value

            let numerString =
                if numerString.[0] = '+' then
                    numerString.Substring(1)
                else
                    numerString

            Some
            <| Numbers.divide (
                Numbers.ReduceBigInt(BigInt.fromBigInteger (BigInteger.Parse(numerString))),
                Numbers.ReduceBigInt(BigInt.fromBigInteger (BigInteger.Parse(denomString)))
            )
        else
            None

    // An obvious candidate for railway style
    static member matchNumber(s: string) : obj option =
        let m = LispReader.intRE.Match(s)

        if m.Success then
            LispReader.matchInteger (m)
        else
            let m2 = LispReader.floatRE.Match(s)

            if m2.Success then
                LispReader.matchFloat (m2, s)
            else
                let m3 = LispReader.ratioRE.Match(s)
                LispReader.matchRatio (m3)


    // Readers

    static member private characterReader(r: PushbackTextReader, backslash: char, opts: obj, pendingForms: obj) : obj =
        let ch = r.Read()

        if ch = -1 then
            raise <| new EndOfStreamException("EOF while reading character")
        else
            let token = LispReader.readSimpleToken (r, char ch)

            if token.Length = 1 then
                token[0]
            else
                match token with
                | "newline" -> '\n'
                | "space" -> ' '
                | "tab" -> '\t'
                | "backspace" -> '\b'
                | "formfeed" -> '\f'
                | "return" -> '\r'
                | _ ->
                    if token.StartsWith("u") then
                        let uc = LispReader.readUnicodeChar (token, 1, 4, 16) |> char

                        if uc >= '\uD800' && uc <= '\uDFFF' then // surrogate code unit?
                            raise
                            <| new ArgumentException("Invalid character constant: \\u" + (int uc).ToString("x"))

                        char uc
                    elif token.StartsWith("o") then
                        let len = token.Length - 1

                        if len > 3 then
                            raise <| new ArgumentException($"Invalid octal escape sequence length: {len}")

                        let uc = LispReader.readUnicodeChar (token, 1, len, 8)

                        if uc > 255 then // = octal 377
                            raise
                            <| new ArgumentException($"Octal escape sequence must be in range [0, 377]: {uc}")

                        char uc
                    else
                        raise <| new ArgumentException($"Unsupported character: \\{token}")


    static member private stringReader(r: PushbackTextReader, doublequote: char, opts: obj, pendingForms: obj) : obj =
        let sb = StringBuilder()

        let rec loop () =
            let ch = r.Read()

            if ch = -1 then
                raise <| new EndOfStreamException("EOF while reading string")
            elif ch = int '"' then
                sb.ToString()
            elif ch = int '\\' then // escape character
                let ch2 = r.Read()

                if ch2 = -1 then
                    raise <| new EndOfStreamException("EOF while reading string")
                else
                    match char ch2 with
                    | 't' -> sb.Append('\t') |> ignore
                    | 'r' -> sb.Append('\r') |> ignore
                    | 'n' -> sb.Append('\n') |> ignore
                    | 'b' -> sb.Append('\b') |> ignore
                    | 'f' -> sb.Append('\f') |> ignore
                    | '\\' -> sb.Append('\\') |> ignore
                    | '"' -> sb.Append('\"') |> ignore
                    | 'u' ->
                        let ch3 = r.Read()

                        if LispReader.charValueInRadix (ch3, 16) = -1 then
                            raise
                            <| new ArgumentException($"Invalid unicode escape sequence: \\u{char ch3}")

                        let uc = LispReader.readUnicodeChar (r, ch3, 16, 4, true) |> char
                        sb.Append(uc) |> ignore
                    | _ ->
                        //if CharValueInRadix(ch, 8) <> -1  -- this is correct, but we end up with different error message for 8,9 than JVM, so do the following to match:
                        if Char.IsDigit(char ch2) then
                            let uc = LispReader.readUnicodeChar (r, ch2, 8, 3, false)

                            if uc > 255 then // octal 377
                                raise
                                <| new ArgumentException("Octal escape sequence must be in range [0, 377]")

                            sb.Append(char uc) |> ignore
                        else
                            raise <| new ArgumentException($"Unsupported escape character: \\{char ch2}")

                    loop ()

            else
                sb.Append(char ch) |> ignore
                loop ()

        loop ()

    static member private commentReader(r: PushbackTextReader, semicolon: char, opts: obj, pendingForms: obj) : obj =
        let rec loop () =
            let ch = r.Read()

            if ch <> -1 && ch <> (int '\n') && ch <> (int '\r') then
                loop ()

        loop ()
        r // no-op macro -- consumes characters but doesn't read anything

    static member private discardReader(r: PushbackTextReader, caret: char, opts: obj, pendingForms: obj) : obj =
        LispReader.readAux (r, opts, LispReader.ensurePending (pendingForms)) |> ignore
        r // no-op macro -- consumes characters but doesn't read anything

    static member private listReader(r: PushbackTextReader, leftparen: char, opts: obj, pendingForms: obj) : obj =
        let mutable startLine = -1
        let mutable startCol = -1

        let lntr =
            match r with
            | :? LineNumberingTextReader as l -> l
            | _ -> null

        if not <| isNull lntr then
            startLine <- lntr.LineNumber
            startCol <- lntr.ColumnNumber

        let list =
            LispReader.readDelimitedList (')', r, true, opts, LispReader.ensurePending (pendingForms))
            |> Seq.toList

        if list.Length = 0 then
            PersistentList.Empty
        else
            let s = PersistentList.create (list) :?> IObj

            if startLine <> -1 then
                let mutable meta = RT0.meta (s) :> Associative
                meta <- RTMap.assoc (meta, RTReader.LineKeyword, RT0.getWithDefault (meta, RTReader.LineKeyword, startLine))
                meta <- RTMap.assoc (meta, RTReader.ColumnKeyword, RT0.getWithDefault (meta, RTReader.ColumnKeyword, startCol))

                meta <-
                    RTMap.assoc (
                        meta,
                        RTReader.SourceSpanKeyword,
                        RT0.getWithDefault (
                            meta,
                            RTReader.SourceSpanKeyword,
                            RTMap.map (
                                RTReader.StartLineKeyword,
                                startLine,
                                RTReader.StartColumnKeyword,
                                startCol,
                                RTReader.EndLineKeyword,
                                lntr.LineNumber,
                                RTReader.EndColumnKeyword,
                                lntr.ColumnNumber
                            )
                        )
                    )

                s.withMeta (meta :?> IPersistentMap)
            else
                s


    static member private vectorReader(r: PushbackTextReader, leftbracket: char, opts: obj, pendingForms: obj) : obj =
        LazilyPersistentVector.create (
            LispReader.readDelimitedList (']', r, true, opts, LispReader.ensurePending (pendingForms))
        )

    static member private mapReader(r: PushbackTextReader, leftbrace: char, opts: obj, pendingForms: obj) : obj =
        let a =
            LispReader.readDelimitedList('}', r, true, opts, LispReader.ensurePending (pendingForms))
            |> Seq.toArray

        if (a.Length &&& 1) = 1 then
            raise
            <| new ArgumentException("Map literal must contain an even number of forms")

        RTMap.map (a)

    static member private setReader(r: PushbackTextReader, leftbrace: char, opts: obj, pendingForms: obj) : obj =
        let a = 
            LispReader.readDelimitedList('}', r, true, opts, LispReader.ensurePending (pendingForms))
            |> Seq.toArray

        PersistentHashSet.createWithCheck (a)
         
        

    static member private unmatchedDelimiterReader
        (
            r: PushbackTextReader,
            rightDelim: char,
            opts: obj,
            pendingForms: obj
        ) : obj =
        raise <| new ArgumentException($"Unmatched delimiter: {rightDelim}")

    // :a.b{:c 1} => {:a.b/c 1}
    // ::{:c 1}   => {:a.b/c 1}  (where *ns* = a.b)
    // ::a{:c 1}  => {:a.b/c 1}  (where a is aliased to a.b)
    static member private namespaceMapReader
        (
            r: PushbackTextReader,
            leftbrace: char,
            opts: obj,
            pendingForms: obj
        ) : obj =

        let autoChar = r.Read()

        let auto =
            if autoChar = int ':' then
                true
            else
                r.Unread(autoChar)
                false

        let mutable osym: obj = null
        let mutable nextChar = r.Read()

        if LispReader.isWhitespace (nextChar) then
            // the #:: { } case or an error
            if auto then
                while LispReader.isWhitespace (nextChar) do
                    nextChar <- r.Read()

                if nextChar <> int '{' then
                    LispReader.unread (r, nextChar)
                    raise <| new ArgumentException("Namespaced map must specify a namespace")
            else
                LispReader.unread (r, nextChar)
                raise <| new ArgumentException("Namespaced map must specify a namespace")
        elif nextChar <> int '{' then
            // #:foo { } or #::foo { }
            LispReader.unread (r, nextChar)
            osym <- LispReader.read (r, true, null, false, opts, pendingForms)
            nextChar <- r.Read()

            while LispReader.isWhitespace (nextChar) do
                nextChar <- r.Read()

        if nextChar <> int '{' then
            raise <| new ArgumentException("Namespaced map must specify a map")

        // Resolve autoresolved ns
        let mutable ns = String.Empty

        let ssym =
            match osym with
            | :? Symbol as s -> s
            | _ -> null

        if auto then
            let resolver = LispReader.getReaderResolver ()

            if isNull osym then
                ns <-
                    match resolver with
                    | Some r -> r.currentNS().Name
                    | _ -> RTVar.getCurrentNamespace().Name.Name
            elif isNull ssym || not <| isNull ssym.Namespace then
                raise
                <| new ArgumentException($"Namespaced map must specify a valid namespace: {osym}")
            else
                let resolvedNS =
                    match resolver with
                    | Some r -> r.resolveAlias (ssym)
                    | _ ->
                        let rns = RTVar.getCurrentNamespace().lookupAlias (ssym)

                        match rns with
                        | null -> null
                        | _ -> rns.Name

                match resolvedNS with
                | null -> raise <| new ArgumentException($"Unknown auto-resolved namespace alias: {osym}")
                | _ -> ns <- resolvedNS.Name

        elif isNull ssym || not <| isNull ssym.Namespace then
            raise
            <| new ArgumentException($"Namespaced map must specify a valid namespace: {osym}")
        else
            ns <- ssym.Namespace

        // Read map
        let kvs =
            LispReader
                .readDelimitedList('}', r, true, opts, LispReader.ensurePending (pendingForms))
                .ToList()

        if (kvs.Count &&& 1) = 1 then
            raise
            <| new ArgumentException("Namespaced map literal must contain an even number of forms")

        // Construct output map
        let a = Array.zeroCreate<obj> (kvs.Count)
        use mutable e = kvs.GetEnumerator()
        let mutable i = 0

        while e.MoveNext() do
            let mutable k = e.Current
            e.MoveNext() |> ignore
            let v = e.Current

            match k with
            | :? Keyword as kw ->
                k <-
                    if isNull kw.Namespace then
                        Keyword.intern (ns, kw.Name)
                    else
                        Keyword.intern (null, kw.Name)
            | _ ->
                k <-
                    match k with
                    | :? Symbol as s ->
                        if isNull s.Namespace then
                            Symbol.intern (ns, s.Name) :> obj
                        else
                            Symbol.intern (null, s.Name) :> obj
                    | _ -> k

            a[i] <- k
            a[i + 1] <- v
            i <- i + 2

        RTMap.map (a)

    static member val symbolicValuesMap =
        Map(
            [ (Symbol.intern ("Inf"), Double.PositiveInfinity)
              (Symbol.intern ("NaN"), Double.NaN)
              (Symbol.intern ("-Inf"), Double.NegativeInfinity) ]
        )

    static member private symbolicValueReader
        (
            r: PushbackTextReader,
            percent: char,
            opts: obj,
            pendingForms: obj
        ) : obj =
        let o =
            LispReader.read (r, true, null, true, opts, LispReader.ensurePending (pendingForms))

        match o with
        | :? Symbol as s ->
            let ok, d = LispReader.symbolicValuesMap.TryGetValue(s)

            if ok then
                d
            else
                raise <| new ArgumentException($"Unknown symbolic value: {s}")
        | _ -> raise <| new ArgumentException($"Invalid tokey: ##{o}")

    static member private wrappingReader(sym: Symbol) : ReaderFunction =
        fun (r: PushbackTextReader, ch: char, opts: obj, pendingForms: obj) ->
            let o = LispReader.readAux (r, opts, LispReader.ensurePending (pendingForms))
            RTSeq.list (sym, o)


    static member private syntaxQuoteReader
        (
            r: PushbackTextReader,
            backquote: char,
            opts: obj,
            pendingForms: obj
        ) : obj =
        try
            Var.pushThreadBindings (RTMap.map (GensymEnvVar, PersistentHashMap.Empty))
            let form = LispReader.readAux (r, opts, LispReader.ensurePending (pendingForms))
            LispReader.SyntaxQuote(form)
        finally
            Var.popThreadBindings () |> ignore


    static member private SyntaxQuote(form: obj) =
        let ret, checkMeta = LispReader.AnalyzeSyntaxQuote(form)

        if checkMeta then
            match form with
            | :? IObj as iobj when not <| isNull (iobj.meta ()) ->
                let newMeta =
                    iobj
                        .meta()
                        .without(RTReader.LineKeyword)
                        .without(RTReader.ColumnKeyword)
                        .without (RTReader.SourceSpanKeyword)

                if newMeta.count () = 0 then
                    ret
                else
                    RTSeq.list (WithMetaSym, ret, LispReader.SyntaxQuote(iobj.meta ()))
            | _ -> ret
        else
            ret

    static member private IsSpecial(sym: obj) = CompilerSpecialSymbols.Contains(sym)

    static member private AnalyzeSyntaxQuote(form: obj) : obj * bool =
        match form with
        | _ when LispReader.IsSpecial(form) -> RTSeq.list (QuoteSym, form), true
        | :? Symbol as sym ->
            let analyzedSym = LispReader.AnalyzeSymbolForSyntaxQuote(sym)
            RTSeq.list (QuoteSym, analyzedSym), true
        | _ when LispReader.IsUnquote(form) -> RTSeq.second (form), false
        | _ when LispReader.IsUnquoteSplicing(form) -> raise <| new ArgumentException("splice not in list")
        | :? IPersistentCollection ->
            match form with
            | :? IRecord -> form, true
            | :? IPersistentMap ->
                let keyvals: IPersistentVector = LispReader.FlattenMap(form)

                RTSeq.list (
                    ApplySym,
                    HashMapSym,
                    RTSeq.list (SeqSym, RTSeq.cons (ConcatSym, LispReader.SyntaxQuoteExpandList(keyvals.seq ())))
                ),
                true
            | :? IPersistentVector as v ->
                RTSeq.list (
                    ApplySym,
                    VectorSym,
                    RTSeq.list (SeqSym, RTSeq.cons (ConcatSym, LispReader.SyntaxQuoteExpandList(v.seq ())))
                ),
                true
            | :? IPersistentSet as s ->
                RTSeq.list (
                    ApplySym,
                    HashSetSym,
                    RTSeq.list (SeqSym, RTSeq.cons (ConcatSym, LispReader.SyntaxQuoteExpandList(s.seq ())))
                ),
                true
            | :? ISeq
            | :? IPersistentList ->
                match RT0.seq (form) with
                | null -> RTSeq.cons (ListSym, null), true
                | _ as seq -> RTSeq.list (SeqSym, RTSeq.cons (ConcatSym, LispReader.SyntaxQuoteExpandList(seq))), true
            | _ -> raise <| new ArgumentException("Unknown collection type")
        | :? Keyword
        | :? Char
        | :? String -> form, true
        | _ when Numbers.IsNumeric(form) -> form, true
        | _ -> RTSeq.list (QuoteSym, form), true


    static member private AnalyzeSymbolForSyntaxQuote(sym: Symbol) =
        let resolver = LispReader.getReaderResolver ()

        if isNull sym.Namespace && sym.Name.EndsWith("#") then
            // gensym
            let gmap = LispReader.getGensymEnv ()

            match gmap with
            | Some m ->
                let mappedGensym = m.valAt (sym) :?> Symbol

                if isNull mappedGensym then
                    let newGensym =
                        Symbol.intern (
                            null,
                            sym.Name.Substring(0, sym.Name.Length - 1)
                            + "__"
                            + RT0.nextID().ToString()
                            + "__auto__"
                        )

                    GensymEnvVar.set (m.assoc (sym, newGensym)) |> ignore
                    newGensym
                else
                    mappedGensym
            | _ -> raise <| new InvalidDataException("Gensym literal not in syntax-quote")
        elif isNull sym.Namespace && sym.Name.EndsWith(".") then
            // constructor
            let csym = Symbol.intern (null, sym.Name.Substring(0, sym.Name.Length - 1))

            let rsym =
                match resolver with
                | Some r ->
                    let resolvedClass = r.resolveClass (csym)
                    if isNull resolvedClass then csym else resolvedClass
                | None -> RTReader.ResolveSymbol(csym)

            Symbol.intern (null, rsym.Name + ".")
        elif isNull sym.Namespace && sym.Name.StartsWith(".") then
            // method
            sym

        else
            match resolver with
            | Some r ->
                let resolvedNS =
                    match sym.Namespace with
                    | null -> null
                    | _ ->
                        let alias = Symbol.intern (null, sym.Namespace)

                        match r.resolveClass (alias) with
                        | null -> r.resolveAlias (alias)
                        | _ as ns -> ns

                if not <| isNull resolvedNS then
                    //Classname/foo => package.qualified.Classname/foo
                    Symbol.intern (resolvedNS.Name, sym.Name)
                elif isNull sym.Namespace then
                    let resolvedSym =
                        match r.resolveClass (sym) with
                        | null -> r.resolveVar (sym)
                        | _ as rsym -> rsym

                    match resolvedSym with
                    | null -> Symbol.intern (r.currentNS().Name, sym.Name)
                    | _ -> resolvedSym
                else
                    sym
            | _ ->
                // No resolver.  Use the current namespace instead for what makes sense (i.e., mapping)
                let maybeClass =
                    if not <| isNull sym.Namespace then
                        RTVar.getCurrentNamespace().getMapping (Symbol.intern (null, sym.Namespace))
                    else
                        null

                match maybeClass with
                | :? Type as t ->
                    // Classname/foo -> package.qualified.Classname/foo
                    Symbol.intern (t.Name, sym.Name)
                | _ -> RTReader.ResolveSymbol(sym)

    static member private SyntaxQuoteExpandList(seq: ISeq) =
        let rec loop (s: ISeq) (ret: IPersistentVector) =
            match s with
            | null -> ret.seq ()
            | _ ->
                let item = s.first ()

                let nextRet =
                    if LispReader.IsUnquote(item) then
                        ret.cons (RTSeq.list (ListSym, RTSeq.second (item)))
                    elif LispReader.IsUnquoteSplicing(item) then
                        ret.cons (RTSeq.second (item))
                    else
                        ret.cons (RTSeq.list (ListSym, LispReader.SyntaxQuote(item)))

                loop (s.next ()) nextRet

        loop seq PersistentVector.Empty

    (*



   
                    else
                    {
                        object maybeClass = null;
                        if (sym.Namespace != null)
                            maybeClass = Compiler.CurrentNamespace.GetMapping(
                                Symbol.intern(null, sym.Namespace));
                        Type t = maybeClass as Type;

                        if (t != null)
                        {
                            // Classname/foo -> package.qualified.Classname/foo
                            sym = Symbol.intern(t.Name, sym.Name);
                        }
                        else
                            sym = Compiler.resolveSymbol(sym);
                    }
                    return RT.list(Compiler.QuoteSym, sym);
                }

*)


    static member FlattenMap(form: obj) =
        let rec loop (s: ISeq) (kvs: IPersistentVector) =
            match s with
            | null -> kvs
            | _ ->
                let e = s.first () :?> IMapEntry
                let kvs1 = kvs.cons(e.key ()).cons (e.value ())
                loop (s.next ()) kvs1

        loop (RT0.seq (form)) PersistentVector.Empty


    static member IsUnquote(form: obj) =
        match form with
        | :? ISeq as s -> Util.equals (RTSeq.first (s), UnquoteSym)
        | _ -> false


    static member IsUnquoteSplicing(form: obj) =
        match form with
        | :? ISeq as s -> Util.equals (RTSeq.first (s), UnquoteSplicingSym)
        | _ -> false








    static member private unquoteReader(r: PushbackTextReader, comma: char, opts: obj, pendingForms: obj) : obj =
        let ch = r.Read()

        if ch = -1 then
            raise <| new EndOfStreamException("EOF while reading character")

        let pendingForms = LispReader.ensurePending (pendingForms)

        if ch = int '@' then
            let o = LispReader.readAux (r, opts, pendingForms)
            RTSeq.list (UnquoteSplicingSym, o)
        else
            LispReader.unread (r, ch)
            let o = LispReader.readAux (r, opts, pendingForms)
            RTSeq.list (UnquoteSym, o)


    static member private dispatchReader(r: PushbackTextReader, hash: char, opts: obj, pendingForms: obj) : obj =
        let ch = r.Read()

        if ch = -1 then
            raise <| new EndOfStreamException("EOF while reading dispatch character")
        else
            match dispatchMacros[ch] with
            | Some macrofn -> macrofn (r, (char ch), opts, pendingForms)
            | None ->
                LispReader.unread (r, ch)

                match LispReader.CtorReader(r, (char ch), opts, LispReader.ensurePending (pendingForms)) with
                | null -> raise <| invalidOp ($"No dispatch macro for: {char ch}")
                | result -> result

    static member metaReader(r: PushbackTextReader, caret: char, opts: obj, pendingForms: obj) : obj =
        let mutable startLine = -1
        let mutable startCol = -1

        let lntr =
            match r with
            | :? LineNumberingTextReader as l -> l
            | _ -> null

        if not <| isNull lntr then
            startLine <- lntr.LineNumber
            startCol <- lntr.ColumnNumber

        let pendingForms = LispReader.ensurePending (pendingForms)

        let mutable metaAsMap =
            match LispReader.readAux (r, opts, pendingForms) with
            | :? Symbol as s -> RTMap.map (TagKeyword, s)
            | :? String as s -> RTMap.map (TagKeyword, s)
            | :? Keyword as k -> RTMap.map (k, true)
            | :? IPersistentVector as v -> RTMap.map (ParamTagsKeyword, v)
            | :? IPersistentMap as m -> m
            | _ ->
                raise
                <| new ArgumentException("Metadata must be Symbol, String, Keyword, Vector or Map")

        let o = LispReader.readAux (r, opts, pendingForms)

        match o with
        | :? IMeta as im ->

            if startLine <> -1 && o :? ISeq then
                 metaAsMap <-
                    metaAsMap.assoc (RTReader.LineKeyword, RT0.getWithDefault (metaAsMap, RTReader.LineKeyword, startLine))

                 metaAsMap <-
                    metaAsMap.assoc (RTReader.ColumnKeyword, RT0.getWithDefault (metaAsMap, RTReader.ColumnKeyword, startCol))

                 metaAsMap <-
                    metaAsMap.assoc (
                        RTReader.SourceSpanKeyword,
                        RT0.getWithDefault (
                            metaAsMap,
                            RTReader.SourceSpanKeyword,
                            RTMap.map (
                                RTReader.StartLineKeyword,
                                startLine,
                                RTReader.StartColumnKeyword,
                                startCol,
                                RTReader.EndLineKeyword,
                                lntr.LineNumber,
                                RTReader.EndColumnKeyword,
                                lntr.ColumnNumber
                            )
                        )
                    )

            match o with
            | :? IReference as ir ->
                ir.resetMeta (metaAsMap) |> ignore
                o
            | _ ->

                let rec loop (s: ISeq) (ometa: Associative)=
                    match s with
                    | null -> (o :?> IObj).withMeta (ometa :?> IPersistentMap) :> obj
                    | _ ->
                        let kv = s.first () :?> IMapEntry
                        loop (s.next ()) (RTMap.assoc (ometa, kv.key (), kv.value ()))

                loop (RT0.seq (metaAsMap)) (RT0.meta (o))

        | _ -> raise <| new ArgumentException("Metadata can only be applied to IMetas")


    static member varReader(r: PushbackTextReader, quote: char, opts: obj, pendingForms: obj) : obj =
        let o = LispReader.readAux (r, opts, LispReader.ensurePending (pendingForms))
        RTSeq.list (TheVarSym, o)

    static member private regexReader(r: PushbackTextReader, doublequote: char, opts: obj, pendingForms: obj) : obj =
        let sb = StringBuilder()

        let rec loop () =
            let ch = r.Read()

            if ch = -1 then
                raise <| new EndOfStreamException("EOF while reading regex")
            elif ch = int '"' then
                sb.ToString()
            else
                sb.Append(char ch) |> ignore

                if ch = int '\\' then
                    let ch2 = r.Read()

                    if ch2 = -1 then
                        raise <| new EndOfStreamException("EOF while reading regex")
                    else
                        sb.Append(char ch2) |> ignore

                loop ()

        loop ()


    static member private fnReader(r: PushbackTextReader, lparen: char, opts: obj, pendingForms: obj) : obj =
        if not <| isNull ((ArgEnvVar :> IDeref).deref ()) then
            raise <| new InvalidOperationException("Nested #()s are not allowed")

        try
            Var.pushThreadBindings (RTMap.map (ArgEnvVar, PersistentTreeMap.Empty))
            r.Unread(int '(')
            let form = LispReader.readAux (r, opts, LispReader.ensurePending (pendingForms))
            let mutable args = PersistentVector.Empty :> IPersistentVector
            let argSyms = (ArgEnvVar :> IDeref).deref () :?> PersistentTreeMap
            let rargs = (argSyms :> Reversible).rseq ()

            if not <| isNull rargs then
                let highArg = ((rargs.first () :?> IMapEntry).key ()) :?> int

                if highArg > 0 then
                    for i = 1 to highArg do
                        let sym =
                            match (argSyms :> ILookup).valAt (i) with
                            | null -> LispReader.garg (i)
                            | _ as s -> s :?> Symbol

                        args <- args.cons (sym)

                let restSym = (argSyms :> ILookup).valAt (-1)

                if not <| isNull restSym then
                    args <- args.cons (AmpersandSym)
                    args <- args.cons (restSym)

            RTSeq.list (FnSym, args, form)

        finally
            Var.popThreadBindings () |> ignore

    static member private argReader(r: PushbackTextReader, percent: char, opts: obj, pendingForms: obj) : obj =
        let token = LispReader.readSimpleToken (r, '%')

        if isNull ((ArgEnvVar :> IDeref).deref ()) then
            LispReader.interpretToken (token, None)
        else
            let m = LispReader.argPat.Match(token)

            if not m.Success then
                raise <| new ArgumentException("arg literal must be %, %& or %integer")

            if m.Groups.[1].Success then // %&
                LispReader.registerArg (-1)
            else
                let arg =
                    if m.Groups.[2].Success then
                        Int32.Parse(m.Groups.[2].Value, System.Globalization.CultureInfo.InvariantCulture)
                    else
                        1

                LispReader.registerArg (arg)

    static member private registerArg(n: int) =
        let argSyms = (ArgEnvVar :> IDeref).deref () :?> PersistentTreeMap

        if isNull argSyms then
            raise <| new InvalidOperationException("arg literal not in #()")
        else
            let arg = (argSyms :> ILookup).valAt (n) :?> Symbol

            if isNull arg then
                let newArg = LispReader.garg (n)
                ArgEnvVar.set ((argSyms :> IPersistentMap).assoc (n, newArg)) |> ignore
                newArg
            else
                arg

    static member private evalReader(r: PushbackTextReader, at: char, opts: obj, pendingForms: obj) : obj =
        if not <| RT0.booleanCast ((RTVar.ReadEvalVar :> IDeref).deref ()) then
            raise
            <| new InvalidOperationException("EvalReader not allowed when *read-eval* is false")

        let o =
            LispReader.read (r, true, null, true, opts, LispReader.ensurePending (pendingForms))

        match o with
        | :? Symbol as s -> RTType.ClassForName(o.ToString())
        | :? IPersistentList ->
            let fs = RTSeq.first (o) :?> Symbol

            if TheVarSym.Equals(fs) then
                let vs = RTSeq.second (o) :?> Symbol
                RTVar.var (vs.Namespace, vs.Name)
            elif fs.Name.EndsWith(".") then
                let args = RTSeq.toArray (RTSeq.next (o))
                let s = fs.ToString()
                Reflector.InvokeConstructor(RTType.ClassForNameE(s.Substring(0, s.Length - 1)), args)
            elif RTReader.NamesStaticMember(fs) then
                let args = RTSeq.toArray (RTSeq.next (o))
                Reflector.InvokeStaticMethod(fs.Namespace, fs.Name, args)
            else
                let v = RTReader.MaybeResolveIn(RTVar.getCurrentNamespace (), fs)

                match v with
                | :? Var as v -> (v :> IFn).applyTo (RTSeq.next (o))
                | _ -> raise <| new InvalidOperationException($"Can't resolve {fs}")
        | _ -> raise <| new InvalidOperationException("Unsupported #= form")


    static member private CtorReader(r: PushbackTextReader, tilde: char, opts: obj, pendingForms: obj) : obj =
        let realizedPendingForms = LispReader.ensurePending (pendingForms)
        let name = LispReader.read (r, true, null, false, opts, realizedPendingForms)

        match name with
        | :? Symbol as sym ->
            let form = LispReader.read (r, true, null, true, opts, realizedPendingForms)

            if LispReader.IsPreservedReadCond(opts) || RTVar.suppressRead () then
                TaggedLiteral.create (sym, form)
            elif sym.Name.Contains "." then
                LispReader.ReadRecord(form, sym, opts, pendingForms)
            else
                LispReader.ReadTagged(form, sym, opts, pendingForms)

        | _ -> raise <| new ArgumentException("Reader tag must be a symbol")


    static member IsPreservedReadCond(opts: obj) =
        if not <| RT0.booleanCast ((ReadCondEnvVar :> IDeref).deref ()) then
            false
        else
            match opts with
            | :? IPersistentMap as m ->
                let readCond = m.valAt (OptReadCondKeyword)
                CondPreserveKeyword.Equals(readCond)
            | _ -> false

    static member ReadTagged(o: obj, tag: Symbol, opts: obj, pendingForms: obj) =
        let dataReaders = (DataReadersVar :> IDeref).deref () :?> ILookup
        let dataReader = RT0.get (dataReaders, tag)

        match dataReader with
        | :? IFn as f -> f.invoke (o)
        | _ ->
            let defaultDataReaders = (DefaultDataReadersVar :> IDeref).deref () :?> ILookup
            let defaultDataReaderForTag = RT0.get (defaultDataReaders, tag)

            match defaultDataReaderForTag with
            | :? IFn as f -> f.invoke (o)
            | _ ->
                match (DefaultDataReaderFnVar :> IDeref).deref () with
                | :? IFn as f -> f.invoke (tag, o)
                | _ -> raise <| new InvalidOperationException($"No reader function for tag: {tag}")

    static member ReadRecord(form: obj, recordName: Symbol, opts: obj, pendingForms: obj) =
        let readeval = RT0.booleanCast ((RTVar.ReadEvalVar :> IDeref).deref ())

        if not readeval then
            raise
            <| new InvalidOperationException("Record construction syntax can only be used when *read-eval* == true")

        let recordType = RTType.ClassForName(recordName.ToString())
        let allCtors = recordType.GetConstructors()

        match form with
        | :? IPersistentVector as recordEntries ->
            // short form
            if
                allCtors
                |> Array.exists (fun c -> c.GetParameters().Length = recordEntries.count ())
            then
                Reflector.InvokeConstructor(recordType, RTSeq.toArray (recordEntries))
            else
                raise
                <| new ArgumentException(
                    $"Unexpected number of constructor arguments to {recordType}: got {recordEntries.count ()}"
                )
        | :? IPersistentMap as vals ->
            let rec loop (s: ISeq) =
                match s with
                | null -> None
                | _ when not <| (s.first () :? Keyword) -> Some(s.first ())
                | _ -> loop (s.next ())

            let hasBadArg = loop (RTMap.keys (vals))

            match hasBadArg with
            | Some k ->
                raise
                <| new ArgumentException(
                    $"Unreadable defrecord form: key must be of type clojure.lang.Keyword, got {k.ToString()}"
                )
            | None -> Reflector.InvokeStaticMethod(recordType, "create", [| vals :> obj |])

        | _ ->
            raise
            <| new ArgumentException($"Unreadable constructor form starting with \"#{recordName}\"")




    static member private unreadableReader(r: PushbackTextReader, tilde: char, opts: obj, pendingForms: obj) : obj =
        raise <| new ArgumentException("Unreadable form")



    static member private conditionalReader(r: PushbackTextReader, hash: char, opts: obj, pendingForms: obj) : obj =
        LispReader.CheckConditionalAllowed(opts)

        let mutable ch = r.Read()

        if ch = -1 then
            raise <| new EndOfStreamException("EOF while reading character")

        let splicing = ch = int '@'

        if splicing then
            ch <- r.Read()

        while LispReader.isWhitespace (ch) do
            ch <- r.Read()

        if ch = -1 then
            raise <| new EndOfStreamException("EOF while reading character")

        if ch <> int '(' then
            raise <| new InvalidOperationException("read-cond body must be a list")

        try
            Var.pushThreadBindings (RTMap.map (ReadCondEnvVar, true))

            if LispReader.IsPreservedReadCond(opts) then
                match LispReader.getMacro (ch) with // should always be list
                | Some readerFn ->
                    let form = readerFn (r, char ch, opts, LispReader.ensurePending (pendingForms))
                    ReaderConditional.create (form, splicing)
                | None ->
                    raise
                    <| new InvalidOperationException(
                        "No reader function for conditional - internal configuration error"
                    )

            else
                LispReader.ReadCondDelimited(r, splicing, opts, pendingForms)

        finally
            Var.popThreadBindings () |> ignore


    static member HasFeature(feature: obj, opts: obj) =
        match feature with
        | :? Keyword ->
            if DefaultFeatureKeyword.Equals(feature) then
                true
            else
                let custom = (opts :?> IPersistentMap).valAt (OptFeaturesKeyword) :?> IPersistentSet
                not <| isNull custom && custom.contains (feature)
        | _ ->
            raise
            <| new InvalidOperationException($"Feature should be a keyword: {feature}")


    static member ReadCondDelimited(r: PushbackTextReader, splicing: bool, opts: obj, pendingForms: obj) : obj =

        let firstLine =
            match r with
            | :? LineNumberingTextReader as lntr -> lntr.LineNumber
            | _ -> -1

        let topLevel = isNull pendingForms
        let pendingForms = LispReader.ensurePending (pendingForms)


        let mutable result = ReadStarted
        let mutable finished = false


        while not finished do

            if Object.ReferenceEquals(result, ReadStarted) then
                let featureForm =
                    LispReader.read (r, false, ReadEOF, Some ')', ReadFinished, true, opts, pendingForms, None)

                if Object.ReferenceEquals(featureForm, ReadEOF) then
                    if firstLine < 0 then
                        raise <| new EndOfStreamException("EOF while reading")
                    else
                        raise
                        <| new EndOfStreamException($"EOF while reading, starting at line {firstLine}")
                elif Object.ReferenceEquals(featureForm, ReadFinished) then
                    finished <- true
                elif ReservedFeaturesSet.contains (featureForm) then
                    raise <| new ArgumentException($"Feature name {featureForm} is reserved.")
                elif LispReader.HasFeature(featureForm, opts) then

                    //Read the form corresponding to the feature, and assign it to result if everything is kosher
                    let valueForm =
                        LispReader.read (
                            r,
                            false,
                            ReadEOF,
                            Some ')',
                            ReadFinished,
                            true,
                            opts,
                            pendingForms,
                            LispReader.getReaderResolver ()
                        )

                    if Object.ReferenceEquals(valueForm, ReadEOF) then
                        if firstLine < 0 then
                            raise <| new EndOfStreamException("EOF while reading")
                        else
                            raise
                            <| new EndOfStreamException($"EOF while reading, starting at line {firstLine}")
                    elif Object.ReferenceEquals(valueForm, ReadFinished) then
                        if firstLine < 0 then
                            raise <| new ArgumentException("read-cond requires an even number of forms.")
                        else
                            raise
                            <| new ArgumentException(
                                $"read-cond starting on line {firstLine} requires an even number of forms"
                            )
                    else
                        result <- valueForm
                else
                    ()

                // When we already have a result, or when the feature didn't match, discard the next form in the reader
                try
                    Var.pushThreadBindings (RTMap.map (RTVar.SuppressReadVar, true))

                    let form =
                        LispReader.read (
                            r,
                            false,
                            ReadEOF,
                            Some ')',
                            ReadFinished,
                            true,
                            opts,
                            pendingForms,
                            LispReader.getReaderResolver ()
                        )

                    if Object.ReferenceEquals(form, ReadEOF) then
                        if firstLine < 0 then
                            raise <| new EndOfStreamException("EOF while reading")
                        else
                            raise
                            <| new EndOfStreamException($"EOF while reading, starting at line {firstLine}")
                    elif Object.ReferenceEquals(form, ReadFinished) then
                        finished <- true

                finally
                    Var.popThreadBindings () |> ignore

        if Object.ReferenceEquals(result, ReadStarted) then
            r // no features matched, return the reader to indicate no form read
        elif splicing then
            match result with
            | :? IList<Object> as resultAsList ->
                if topLevel then
                    raise
                    <| new InvalidOperationException("Reader conditional splicing not allowed at the top level.")

                let pendingLinked = pendingForms :?> LinkedList<Object>
                let mutable node = pendingLinked.First

                for item in resultAsList do
                    if isNull node then
                        node <- pendingLinked.AddFirst(item)
                    else
                        node <- pendingLinked.AddAfter(node, item)

                r
            | _ ->
                raise
                <| new ArgumentException("Spliced form list in read-cond-splicing must implement IList<Object>")
        else
            result


    static member IsPreservReadCond(opts: obj) =
        if RT0.booleanCast ((ReadCondEnvVar :> IDeref).deref ()) then
            match opts with
            | :? IPersistentMap as m ->
                let readCond = m.valAt (OptReadCondKeyword)
                CondPreserveKeyword.Equals(readCond)
            | _ -> false
        else
            false

    static member CheckConditionalAllowed(opts: obj) =
        let allowed =
            match opts with
            | :? IPersistentMap as m ->
                let readCond = m.valAt (OptReadCondKeyword)
                CondAllowKeyword.Equals(readCond) || CondPreserveKeyword.Equals(readCond)
            | _ -> false

        if not allowed then
            raise <| new InvalidOperationException("Conditional read not allowed")
