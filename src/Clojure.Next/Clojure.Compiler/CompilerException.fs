namespace Clojure.Compiler

open System
open Clojure.Collections
open Clojure.IO



type CompilerException private (simpleCall: bool, message: string, _source: string, _line: int, _column: int, _sym: Symbol, _phase: Keyword, _cause: Exception) =
    inherit Exception((if simpleCall then message else CompilerException.MakeMsg(_source,_line,_column,_sym,_phase,_cause)), _cause)

    let data : IPersistentMap = 
        if simpleCall then null
        else
            let mutable m : Associative = RTMap.map (
                        CompilerException.ErrorPhaseKeyword, _phase,
                        CompilerException.ErrorLineKeyword, _line, 
                        CompilerException.ErrorColumnKeyword, _column)
            if not <| isNull _source  then m <- m.assoc(CompilerException.ErrorSourceKeyword, _source)
            if not <| isNull _sym then m <- m.assoc(CompilerException.ErrorSymbolKeyword, _sym)
            m :?> IPersistentMap


    // Trying to duplicate the Java/C# variations on constructors for this class is almost impossible.
    // Various constructors call other constructors, but some call the base class constructor directly.
    // Some provide default values for for FileSource and Line and Data
    // Some provide the Message, some generate the message from other parameters.
    // Here are the variations:
    //  1. CompilerException() -- no-arg base, sets FileSource to "<unknown>", Line defaults to 0, data defaults to null
    //  2. CompilerException(message) -- calls base class ctor with message, sets FileSource to "<unknown>", Line defaults to 0, data defaults to null
    //  3. CompilerException(message, Exception) -- calls base class ctor with message and exception, sets FileSource to "<unknown>",  Line defaults to 0, data defaults to null
    //  4. CompilerException(source,line,column,cause) -- call this(source,line,column,null,cause)
    //  5. CompilerException(source,line,column,sym,cause) -- call this(source,line,column,sym,PhaseCompileSyntaxCheckKeyword,cause)
    //  6. CompilerException(source,line,column,sym,phase,cause) -- calls base class ctor with generated message and inner exception, sets FileSource to source, creates the data map
    //
    //  To more or less duplicate this, we'll have a private constructor, that takes all the parameters, plus a flag telling us how to proceed.
    //  We only have one choice on how to call the base constructor.  This will have to be
    //          inherit Exception(some-message, inner)
    //  We pass inner as
    //  For the message, in (1) , the message should be what would happen in no message is passed into the base; "Excpetion fo type 'System.Exception' was thrown."
    //  So (1) can call (2) with that as message.
    //  (2) can call (3) with the inner exceptiion set to null.
    //  (3) can call the private ctor with the message and the inner exception.
    //  (4) calls (5), (5) calls (6), (6) can call the private ctor.
    //  The private ctor needs to distinguish between being called from (3) and being called from (6).
    //  So it will have:  Flag, message, source, line, column, sym, phase, cause


    //  (1)
    new() = CompilerException("Exception of type 'System.Exception' was thrown.")

     // (2)
    new (message: string) = CompilerException(message, null)

    // (3)
    new (message: string, cause: Exception) = CompilerException(true, message, "<unknown>", 0, 0, null, null, cause)

    // (4)
    new(source: string, line: int, column: int, cause: Exception) =
        CompilerException(source, line, column, null, cause)

    // (5)
    new(source: string, line: int, column: int, sym: Symbol, cause: Exception) = 
        CompilerException(source, line, column, sym, CompilerException.PhaseCompileSyntaxCheckKeyword, cause)

    // (6)
    new(source: string, line: int, column: int, sym: Symbol, phase: Keyword, cause: Exception) = 
        CompilerException(false, null, source, line, column, sym, phase, cause)


    // Error keys
    static member val ErrorNamespaceStr = "clojure.error";
    static member val ErrorSourceKeyword = Keyword.intern(CompilerException.ErrorNamespaceStr, "source");
    static member val ErrorLineKeyword = Keyword.intern(CompilerException.ErrorNamespaceStr, "line");
    static member val ErrorColumnKeyword = Keyword.intern(CompilerException.ErrorNamespaceStr, "column");
    static member val ErrorPhaseKeyword = Keyword.intern(CompilerException.ErrorNamespaceStr, "phase");
    static member val ErrorSymbolKeyword = Keyword.intern(CompilerException.ErrorNamespaceStr, "symbol");

    // phases
    static member val PhaseReadKeyword = Keyword.intern(null, "read-source")
    static member val PhaseMacroSyntaxCheckKeyword = Keyword.intern(null, "macro-syntax-check")
    static member val PhaseMacroExpandKeyword = Keyword.intern(null, "macroexpand")
    static member val PhaseCompileSyntaxCheckKeyword = Keyword.intern(null, "compile-syntax-check")
    static member val PhaseCompilationKeyword = Keyword.intern(null, "compilation")
    static member val PhaseExecutionKeyword = Keyword.intern(null, "execution")

    // other
    static member val SpecProblemsKeyword = Keyword.intern("clojure.spec.alpha", "problems");
 

    static member MakeMsg (source: string, line: int, column: int, sym: Symbol, phase: Keyword, cause: Exception) =
        let prefix = if CompilerException.PhaseMacroExpandKeyword.Equals(phase) then "Unexpected error " else "Syntax error "
        let verb = 
            if CompilerException.PhaseReadKeyword.Equals(phase) then "reading source" 
            elif CompilerException.PhaseCompileSyntaxCheckKeyword.Equals(phase) then "compiling" 
            else "macroexpanding"
        let symStr = if sym <> null then sym.ToString() + " " else ""
        let sourceStr = if not <| isNull source && not (source.Equals("NO_SOURCE_PATH")) then source + ":" else ""
        $"{prefix} {verb} {symStr}at ({sourceStr}{line}:{column})."
 

    member this.FileSource = _source
    member this.Line = _line

    interface IExceptionInfo with
        member this.getData() = data

    override this.ToString (): string = 
        match this.InnerException :> obj with
        | :? IExceptionInfo as info ->
            let data = info.getData()
            if CompilerException.PhaseMacroSyntaxCheckKeyword.Equals(data.valAt(CompilerException.ErrorPhaseKeyword)) &&
               not <| isNull (data.valAt(CompilerException.SpecProblemsKeyword)) then
                this.Message
            else
                $"{this.Message}\n{this.InnerException.Message}"
        | _ -> this.Message

