namespace Clojure.IO

open Clojure.Collections
open Clojure.Lib
open System.Collections.Generic
open System

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

    [<Literal>]
    let ErrNS = "clojure.error"

    static member val ErrLine = Keyword.intern(ReaderException.ErrNS, "line")
    static member val ErrColumn = Keyword.intern(ErrNS, "column")

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


    // Entry points for reading

    static member read(r: PushbackTextReader, opts: obj) =
        let eofIsError, eofValue = LispReader.parseEofOptions opts
        LispReader.read(r, eofIsError, eofValue, false, opts)
        
    static member read(r: PushbackTextReader, eofIsError: bool, eofValue: obj, isRecursive: bool) =
        LispReader.read(r, eofIsError, eofValue, isRecursive, PersistentHashMap.Empty)

    static member read(r: PushbackTextReader, eofIsError: bool, eofValue: obj, isRecursive: bool, opts: obj) =
        LispReader.read(r, eofIsError, eofValue, None, null, isRecursive, opts, null, (RTVar.ReaderResolverVar:>IDeref).deref() :?> ReaderResolver)

    static member private read(
        r: PushbackTextReader, 
        eofIsError: bool, 
        eofValue: obj, 
        isRecursive: bool,
        opts: obj,
        pendingForms: obj) = 
        LispReader.read(r, eofIsError, eofValue, None, null, isRecursive, opts, EnsurePending(pendingForms), (RTVar.ReaderResolverVar:>IDeref).deref() :?> ReaderResolver)
        
    // They all end up here
    static member private read(
        r: PushbackTextReader, 
        eofIsError: bool, 
        eofValue: obj, 
        returnOn: char option,
        returnOnValue: obj,
        isRecursive: bool,
        opts: obj,
        pendingForms: obj,
        resolver : ReaderResolver) = 

        if UnknownKeyword.Equals((RTVar.ReadEvalVar :> IDeref).deref()) then
            raise <| invalidOp "Reading disallowed - *read-eval* bound to :unknown"

        let fullOpts = LispReader.InstallPlatformFeature(opts)

        try
            eofValue
        with
        | ex ->
            if isRecursive then
                reraise()
            else
                raise <| new ReaderException(r.LineNumber, r.ColumnNumber, ex)