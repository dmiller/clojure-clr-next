namespace Clojure.Lib

open Clojure.Collections

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.IO
open System.Linq
open System.Reflection
open System.Runtime.CompilerServices
open System.Text
open System.Threading

/// Helpers to create a list of types to import.
module DefaultImports =

    let getTypes (a: Assembly) =
        try
            a.GetTypes()
        with _ ->
            [||]

    let getAllTypesInNamespace (namespaceName: string) =
        AppDomain.CurrentDomain.GetAssemblies()
        |> Array.collect getTypes
        |> Array.filter (fun t ->
            (t.IsClass || t.IsInterface || t.IsValueType)
            && t.IsPublic
            && t.Namespace = namespaceName
            && not t.IsGenericTypeDefinition
            && not <| t.Name.StartsWith("_"))

    let createDefaultImportDirectory () : Dictionary<Symbol, Type> =
        let q = getAllTypesInNamespace "System"
        let d = (q :> IEnumerable<Type>).ToDictionary(fun t -> Symbol.intern t.Name)

        // Adding these to support bootstrapping in clojure.core
        d.Add(Symbol.intern ("StringBuilder"), typeof<StringBuilder>)
        d.Add(Symbol.intern ("BigInteger"), typeof<System.Numerics.BigInteger>)
        d.Add(Symbol.intern ("BigDecimal"), typeof<System.Decimal>)

        d

    let createMap () =
        PersistentHashMap.createD2 (createDefaultImportDirectory ())

    let imports = createMap ()


/// A Namespace holds a map from symbols to references.
/// Symbol to reference mappings come in several flavors:
///    Simple: Symbol
///    Use/refer: Symbol to a Var that is homed in another namespace.
///    Import: Symbol to a Type
/// A namespace can also refer to another namespace by an alias.
[<Sealed; AllowNullLiteral>]
type Namespace(_name: Symbol) =
    inherit AReference((_name :> IMeta).meta ())

    // variable-to-value map
    let _mappings: AtomicReference<IPersistentMap> =
        AtomicReference(DefaultImports.imports)

    // variable-to-namespace alias map
    let _aliases: AtomicReference<IPersistentMap> =
        AtomicReference(PersistentArrayMap.Empty)

    // All namespaces, keyed by Symbol
    static let _namespaces = ConcurrentDictionary<Symbol, Namespace>()

    static let _clojureNamespace = Namespace.findOrCreate (Symbol.intern "clojure.core")

    /// The name of the namespace.
    member _.Name = _name

    /// The map of aliases in the namespace.
    member _.Aliases = _aliases.Get()

    /// The map of Symbols to values in the namespace.
    member _.Mappings = _mappings.Get()

    /// The clojure.core namespace.
    static member ClojureNamespace = _clojureNamespace

    override _.ToString() = _name.ToString()

    ///////////////////////////////////////////
    //
    // Namespace manipulation
    //
    ///////////////////////////////////////////

    /// An ISeq of all namespaces.
    static member All = _namespaces.Values |> RTSeq.seq

    /// Find or create a namespace named by the symbol.
    static member findOrCreate(name: Symbol) =
        _namespaces.GetOrAdd(name, (fun _ -> new Namespace(name)))

    /// Remove a namespace (by name)
    /// Return the removed namespace, or null if not found
    /// It's an error to try to remove the clojure.core namespace
    static member remove(name: Symbol) =
        if name.Equals(_clojureNamespace.Name) then
            raise <| ArgumentException("Cannot remove clojure namespace")
        else
            let (result, ns) = _namespaces.TryRemove(name)
            ns

    /// Find a namespace given a name.
    /// Return null if not found.
    static member find(name: Symbol) =
        let (result, ns) = _namespaces.TryGetValue(name)
        ns

    ///////////////////////////////////////////
    //
    // Interning
    //
    ///////////////////////////////////////////

    /// Determine if a mapping is interned
    /// An interned mapping is one where a var's ns matches the current ns and its sym matches the mapping key.
    /// Once established, interned mappings should never change.
    member private this.isInternedMapping(sym: Symbol, o: obj) =
        match o with
        | :? Var as v -> Object.ReferenceEquals(v.Namespace, this) && v.Name.Equals(sym)
        | _ -> false

    /// Intern a Symbol in the namespace, with a (new) Var as its value.
    /// It is an error to intern a symbol with a namespace.
    /// This has to deal with other threads also interning.
    member this.intern(sym: Symbol) =
        if not <| isNull sym.Namespace then
            raise <| ArgumentException("Can't intern namespace-qualified symbol")

        let mutable map = this.Mappings
        let mutable o = map.valAt (sym)
        let mutable v: Var = null

        // if there is no associated var in the map, create one and add it to the map.
        //  iteration to deal with race condition
        // I've copied how this was originally done in the Java version.
        // It's convoluted.
        // Before the loop, o is the value in the map; could be null.
        // We only enter the loop if o is null.
        // In the loop, we create a new Var and put in v (and it never changes after that)
        // In the loop, we try to associate the new Var v with sym.
        // We then do a map lookup.
        // Because the possibility of another thread putting in a value for sym ahead of us,
        //   it is not necessarily the case when we exit that o is the same as v.
        // At exit, o is the value that ended up being associated with sym.
        //   (Either that's what it had before we got to the loop and the loop did nothing, or we tried to put something in.)
        //   and v is the Var we tried to put in as the value for sym.

        while isNull o do
            if isNull v then
                v <- Var.createInternal (this, sym)

            let newMap = map.assoc (sym, v)
            _mappings.CompareAndSet(map, newMap) |> ignore
            map <- this.Mappings
            o <- map.valAt (sym)

        // an interned mapping is permanent.
        // if we have one, use it.

        if this.isInternedMapping (sym, o) then
            // an interned mapping is permanent.
            // if we have one, use it.
            o :?> Var
        else
            if isNull v then
                // in this case, we didn't create a Var in the loop above, meaning we didn't loop at all, meaning o already had a value.
                // We need to create a Var to use.
                v <- Var.createInternal (this, sym)
            // make sure we can replace the existing value with the new one.
            if (this.checkReplacement (sym, o, v)) then
                // replacement ok.  keep going until we get the new value in.
                while not <| _mappings.CompareAndSet(map, map.assoc (sym, v)) do
                    map <- this.Mappings

                v
            else
                // replacement not ok. (error message has been printed)
                // return the existing value.
                o :?> Var

    // Check if namespace mapping is replaceable and warn on problematic cases.
    // Return a boolean indicating if a mapping is replaceable.
    // The semantics of what constitutes a legal replacement mapping is summarized as follows:
    // | classification | in namespace ns        | newval = anything other than ns/name | newval = ns/name                    |
    // |----------------+------------------------+--------------------------------------+-------------------------------------|
    // | native mapping | name -> ns/name        | no replace, warn-if newval not-core  | no replace, warn-if newval not-core |
    // | alias mapping  | name -> other/whatever | warn + replace                       | warn + replace                      |

    member private this.checkReplacement(sym: Symbol, oldVal: obj, newVal: obj) =

        let defaultResponse () =
            RTVar
                .errPrintWriter()
                .WriteLine(
                    $"WARNING: {sym} already refers to: {oldVal} in namespace: {_name}, being replaced by: {newVal}"
                )

            RTVar.errPrintWriter().Flush()
            true

        match oldVal with
        | :? Var as ovar ->
            let ons = ovar.Namespace

            let nns =
                match newVal with
                | :? Var as nvar -> nvar.Namespace
                | _ -> null

            if this.isInternedMapping (sym, oldVal) then
                if not <| Object.ReferenceEquals(nns, RTVar.ClojureNamespace) then
                    RTVar
                        .errPrintWriter()
                        .WriteLine(
                            $"REJECTED: attempt to replace interned var {oldVal} with {newVal} in {_name}, you must ns-unmap first"
                        )

                    RTVar.errPrintWriter().Flush()
                    false
                else
                    false
            else
                defaultResponse ()
        | _ -> defaultResponse ()


    /// Intern a symbol with a specified value.
    /// Returns the value that is associated.
    member this.reference(sym: Symbol, value: obj) =
        if not <| isNull sym.Namespace then
            raise <| ArgumentException("Can't intern a namespace-qualified symbol")

        let mutable map = this.Mappings
        let mutable o = map.valAt (sym)

        // race condition
        while (isNull o) do
            let newMap = map.assoc (sym, value)
            _mappings.CompareAndSet(map, newMap) |> ignore
            map <- this.Mappings
            o <- map.valAt (sym)

        if not <| Object.ReferenceEquals(o, value) && this.checkReplacement (sym, o, value) then
            while not <| _mappings.CompareAndSet(map, map.assoc (sym, value)) do
                map <- this.Mappings

            value
        else
            o

    /// Remove a symbol from the namespace
    /// It is illegal to remove a namespace-qualified symbol.
    member this.unmap(sym: Symbol) =
        if not <| isNull sym.Namespace then
            raise <| ArgumentException("Can't unintern a namespace-qualified symbol")

        let mutable map = this.Mappings

        while map.containsKey (sym) do
            let newMap = map.without (sym)
            _mappings.CompareAndSet(map, newMap) |> ignore
            map <- this.Mappings

    // This comes from the Java verion.
    // During loading of the clojure runtime source, it never gets past the t1 <> t2 test.
    // Not sure we need it, but I'm not getting rid of it.
    static member private areDifferentInstancesOfSameClassName(t1: Type, t2: Type) =
        not <| Object.ReferenceEquals(t1, t2) && t1.FullName.Equals(t2.FullName)


    member private this.referenceClass(sym: Symbol, value: Type) =
        if not <| isNull sym.Namespace then
            raise <| ArgumentException("Can't intern namespace-qualified symbol")

        // traditional race condition hacking -- keep looping until we find the symbol/type pair in the map.
        let mutable map = this.Mappings
        let mutable c = map.valAt (sym) :?> Type

        while (isNull c || Namespace.areDifferentInstancesOfSameClassName (c, value)) do
            let newMap = map.assoc (sym, value)
            _mappings.CompareAndSet(map, newMap) |> ignore
            map <- this.Mappings
            c <- map.valAt (sym) :?> Type

        if not <| Object.ReferenceEquals(c, value) then
            raise
            <| InvalidOperationException(sprintf "%A already refers to: %A in namespace: %A" sym c _name)

        c


    /// Map a symbol to a Type (import)
    /// Named import instead of ImportType for core.clj compatibility.
    member this.importClass(sym: Symbol, t: Type) = this.referenceClass (sym, t)

    /// Map a symbol to a Type (import) using the type name for the symbol name.
    /// Named importClass instead of ImportType for core.clj compatibility.
    member this.importClass(t: Type) =
        this.importClass (Symbol.intern (Util.nameForType (t)), t)

    /// Add a symbol to Var reference.
    member this.refer(sym: Symbol, v: Var) = this.reference (sym, v) :?> Var

    ///////////////////////////////////////////
    //
    // Mappings
    //
    ///////////////////////////////////////////

    /// Get the value mapped to a symbol.
    member this.getMapping(sym: Symbol) = this.Mappings.valAt (sym)

    /// Find the Var mapped to a Symbol.
    member this.findInternedVar(sym: Symbol) =
        match this.Mappings.valAt (sym) with
        | :? Var as v when Object.ReferenceEquals(v.Namespace, this) -> v
        | _ -> null

    ///////////////////////////////////////////
    //
    // Aliases
    //
    ///////////////////////////////////////////

    /// Find a Namespace aliased by a Symbol.
    member this.lookupAlias(alias: Symbol) =
        this.Aliases.valAt (alias) :?> Namespace

    /// Add an alias for a namespace.
    member this.addAlias(alias: Symbol, ns: Namespace) =
        if isNull alias then
            raise <| ArgumentNullException("alias", "Expecting Symbol + Namespace")

        if isNull ns then
            raise <| ArgumentNullException("ns", "Expecting Symbol + Namespace")

        // race condition
        let mutable map = this.Aliases

        while not <| map.containsKey (alias) do
            let newMap = map.assoc (alias, ns)
            _aliases.CompareAndSet(map, newMap) |> ignore
            map <- this.Aliases

        // you can rebind an alias, but only to the initially-aliased namespace

        if not <| Object.ReferenceEquals(map.valAt (alias), ns) then
            raise
            <| InvalidOperationException(
                $"Alias {alias} already exists in namespace {_name}, aliasing {map.valAt (alias)}"
            )

    /// Remove an alias.
    member this.removeAlias(alias: Symbol) =
        let mutable map = this.Aliases

        // Race condition
        while map.containsKey (alias) do
            let newMap = map.without (alias)
            _aliases.CompareAndSet(map, newMap) |> ignore
            map <- this.Aliases

    ///////////////////////////////////////////
    //
    // core.clj compatibility
    //
    ///////////////////////////////////////////

    /// Get the namespace name.
    member this.getName() = _name

    /// Get the mappings of the namespace.
    member this.getMappings() = this.Mappings

    /// Get the aliases.
    member this.getAliases() = this.Aliases

/// A frame in the binding stack of Var values in a thread.
and [<AllowNullLiteral>] private Frame(_bindings: Associative, _prev: Frame) =

    /// The binding in the frame (map from Var to TBox).
    member _.Bindings = _bindings

    interface ICloneable with
        member this.Clone() = Frame(_bindings, null)

    /// The base frame in the stack.
    static member val Top: Frame = Frame(PersistentHashMap.Empty, null)

/// A box for a thread-bound value of a Var.
and [<Sealed; AllowNullLiteral>] TBox(_thread: Thread, v: obj) =

    /// The value in the box.
    [<VolatileField>]
    let mutable _value = v

    /// Get/set the value in the box.
    member _.Value
        with get () = _value
        and set (v) = _value <- v

    /// The thread that owns the box.
    member _.Thread = _thread

/// The Var datatype
and [<Sealed; AllowNullLiteral>] Var private (_ns: Namespace, _sym: Symbol) =
    inherit ARef(PersistentHashMap.Empty)

    /// revision counter
    [<VolatileField>]
    let mutable _rev: int = 0

    /// Supports dynamic binding or not
    [<VolatileField>]
    let mutable _dynamic = false

    /// The root value
    [<VolatileField>]
    let mutable _root = null

    // Whether the Var has a thread-bound value
    let _threadBound = AtomicBoolean(false)

    /// The current frame in the binding stack for this Var.
    [<DefaultValue; ThreadStatic>]
    static val mutable private _currentFrame: Frame

    /// The name of the var (Symbol)
    member _.Symbol = _sym

    /// Get/set the current frame
    static member CurrentFrame
        with private get () =
            if isNull Var._currentFrame then
                Var._currentFrame <- Frame.Top

            Var._currentFrame
        and private set (v) = Var._currentFrame <- v

    /// Increment the revision counter.
    member _.incrementRev() = _rev <- _rev + 1

    /// Set the root value
    member _.setRoot(newValue: obj) = _root <- newValue

    /// Set the indicator of whether the Var is thread-bound.
    member _.setThreadBound(b: bool) = _threadBound.Set(b)

    override _.ToString() =
        match _ns with
        | null -> $"""#<Var: {if isNull _sym then "--unnamed--" else _sym.ToString()}>"""
        | _ -> $"#'{_ns}/{_sym}"

    // To avoid initialization checks that would be caused by the circular dependency between the Var and the Unbound value in its root,
    // I made the constructor private and created a factory method to create a Var.
    // Called createInternal to avoid name collision with the 'create' method in the public interface.
    static member internal createInternal(ns: Namespace, sym: Symbol) =
        let v = new Var(ns, sym)
        v.setRoot (Unbound(v))
        v

    static member internal createInternal(ns: Namespace, sym: Symbol, initValue: obj) =
        let v = new Var(ns, sym)
        v.setRoot (initValue)
        v.incrementRev ()
        v

    /// The namespace of the Var.
    member _.Namespace = _ns

    /// The name of the Var.
    member _.Name = _sym


    ////////////////////////////////
    //
    //  Special values
    //
    ////////////////////////////////

    static member val private _privateKey = Keyword.intern (null, "private")
    static member val private _privateMeta = PersistentArrayMap([| Var._privateKey; true |])
    static member val private _macroKey = Keyword.intern (null, "macro")
    static member val private _nameKey = Keyword.intern (null, "name")
    static member val private _nsKey = Keyword.intern (null, "ns")
    static member val private _tagKey = Keyword.intern (null, "tag")

    static member val private dissocFn =
        { new AFn() with
            member _.ToString() : string = "Var.dissocFn"
          interface IFn with
              member _.invoke(m, k) = RTMap.dissoc (m, k) }

    static member val private assocFn =
        { new AFn() with
            member _.ToString() : string = "Var.dissocFn"
          interface IFn with
              member _.invoke(m, k, v) = RTMap.assoc (m, k, v) }

    ////////////////////////////////
    //
    //  Factory methods
    //
    ////////////////////////////////

    /// Intern a named var in a namespace.
    static member intern(ns: Namespace, sym: Symbol) = ns.intern (sym)


    /// Intern a named var in a namespace (creating the namespece if necessary).
    static member intern(nsName: Symbol, sym: Symbol) =
        let ns = Namespace.findOrCreate (nsName)
        Var.intern (ns, sym)

    /// Intern a named var in a namespace, with given value (if has a root value already, then change only if replaceRoot is true).
    static member intern(ns: Namespace, sym: Symbol, root: obj, replaceRoot: bool) =
        let dvout = ns.intern (sym)

        if not <| dvout.hasRoot () || replaceRoot then
            dvout.bindRoot (root)

        dvout

    /// Intern a named var in a namespace, with given value.
    static member intern(ns: Namespace, sym: Symbol, root: obj) = Var.intern (ns, sym, root, true)

    /// Intern a named var in a namespace, with given value, marked as private
    static member internPrivate(nsName: string, sym: string) =
        let ns = Namespace.findOrCreate (Symbol.intern nsName)
        let v = Var.intern (ns, Symbol.intern sym)
        v.setMeta (Var._privateMeta)
        v

    // /Create an uninterned Var, null value
    static member create() = Var.createInternal (null, null)

    // Create an uninterned Var, with a root value
    static member create(root: obj) = Var.createInternal (null, null, root)

    ////////////////////////////////
    //
    //  Binding stack
    //
    ////////////////////////////////

    /// Push a new frame of bindings onto the binding stack.
    static member pushThreadBindings(bindings: Associative) =
        let f = Var.CurrentFrame
        let mutable bmap = f.Bindings

        let rec loop (bs: ISeq) =
            match bs with
            | null -> ()
            | _ ->
                let e = bs.first () :?> IMapEntry
                let v = e.key () :?> Var

                if not <| v.isDynamic then
                    raise
                    <| new InvalidOperationException($"Can't dynamically bind non-dynamic var: {v.Namespace}/{v.Name}")

                v.validate (e.value ())
                v.setThreadBound (true)
                bmap <- bmap.assoc (v, TBox(Thread.CurrentThread, e.value ()))
                loop (bs.next ())

        loop (bindings.seq ())

        Var.CurrentFrame <- Frame(bmap, f)

    /// Pop the topmost binding frame from the stack.
    static member popThreadBindings() =
        let f = Var.CurrentFrame

        if isNull f then
            raise <| new InvalidOperationException("Pop without matching push")

        if (Object.ReferenceEquals(f, Frame.Top)) then
            Var.CurrentFrame = null
        else
            Var.CurrentFrame = f

    /// Get the thread-local bindings of the top frame.
    static member getThreadBindings() : Associative =
        let f = Var.CurrentFrame

        let rec loop (bs: ISeq) (ret: IPersistentMap) =
            match bs with
            | null -> ret
            | _ ->
                let e = bs.first () :?> IMapEntry
                let v = e.key () :?> Var
                let tbox = e.value () :?> TBox
                loop (bs.next ()) (ret.assoc (v, tbox.Value))

        loop (f.Bindings.seq ()) PersistentHashMap.Empty

    /// Get the box of the current binding on the stack for this var, or null if no binding.
    member this.getThreadBinding() =
        if _threadBound.Get() then
            let e = Var.CurrentFrame.Bindings.entryAt (this)

            match e with
            | null -> null
            | _ -> e.value () :?> TBox
        else
            null

    ////////////////////////////////
    //
    //  Frame management
    //
    ////////////////////////////////

    // Copying the original Java code here.
    // getThreadBindingFrame has return type Object.
    // resetThreadBindingFrame
    // I'm not sure why they did it this way.

    /// Get the crrent frame of the Var in this thread.
    static member getThreadBindingFrame() = Var.CurrentFrame :> obj

    /// Clone the current frame of the Var in this thread.
    static member private cloneThreadBindingFrame() =
        (Var.CurrentFrame :> ICloneable).Clone()

    /// Reset the current frame of the Var in this thread.
    static member private resetThreadBindingFrame(f: obj) = Var.CurrentFrame <- (f :?> Frame)

    ////////////////////////////////
    //
    //  Meta management
    //
    ////////////////////////////////

    /// Set the metadata attached to this var.
    /// The metadata must contain entries for the namespace and name.
    member this.setMeta(m: IPersistentMap) =
        (this :> IReference)
            .resetMeta (m.assoc(Var._nameKey, _sym).assoc (Var._nsKey, _ns))
        |> ignore

    /// Add a macro=true flag to the metadata.
    member this.setMacro() =
        (this :> IReference).alterMeta (Var.assocFn, RTSeq.list (Var._macroKey, true))
        |> ignore

    /// Is the Var a macro?
    member this.isMacro = RT0.booleanCast ((this :> IMeta).meta().valAt (Var._macroKey))

    /// Is the var public?
    member this.isPublic =
        not <| RT0.booleanCast ((this :> IMeta).meta().valAt (Var._privateKey))

    /// Get the tag on the var.
    member this.tag
        with get () = (this :> IMeta).meta().valAt (Var._tagKey)
        and set (v) =
            (this :> IReference).alterMeta (Var.assocFn, RTSeq.list (Var._tagKey, v))
            |> ignore

    ////////////////////////////////
    //
    //  Dynamic flag management
    //
    ////////////////////////////////

    /// Mark the Var as dynamic.
    member this.setDynamic() =
        _dynamic <- true
        this

    /// Set the dynamic flag.
    member this.setDynamic(b: bool) =
        _dynamic <- b
        this

    /// Is the Var dynamic?
    member _.isDynamic = _dynamic


    ////////////////////////////////
    //
    //  Value management
    //
    ////////////////////////////////

    /// Does the Var have a root value?
    member _.hasRoot() =
        match _root with
        | :? Unbound -> false
        | _ -> true

    /// Get the root value.
    member _.getRawRoot() = _root

    /// Does the Var have a value? (root or  thread-bound)
    member this.isBound =
        this.hasRoot ()
        || (_threadBound.Get() && Var.CurrentFrame.Bindings.containsKey (this))

    /// Set the value of the var
    /// It is an error to set the root binding with this method
    member this.set(v: obj) =
        this.validate (v)
        let tbox = this.getThreadBinding ()

        match tbox with
        | null ->
            raise
            <| new InvalidOperationException($"Can't change/establish root binding of: {_sym} with set")
        | _ ->
            if not <| Object.ReferenceEquals(Thread.CurrentThread, tbox.Thread) then
                raise
                <| new InvalidOperationException($"Can't set!: {_sym} from non-binding thread")

            tbox.Value <- v
            v

    /// Change the root value.  (And clear the macro flag.)
    [<MethodImpl(MethodImplOptions.Synchronized)>]
    member this.bindRoot(v: obj) =
        this.validate (v)
        let oldRoot = _root
        this.setRoot (v)
        this.incrementRev ()

        (this :> IReference).alterMeta (Var.dissocFn, RTSeq.list (Var._macroKey))
        |> ignore

        this.notifyWatches (oldRoot, v)

    /// Change the root value.
    [<MethodImpl(MethodImplOptions.Synchronized)>]
    member this.swapRoot(v: obj) =
        this.validate (v)
        let oldRoot = _root
        this.setRoot (v)
        this.incrementRev ()
        this.notifyWatches (oldRoot, v)

    /// Unbind the Var's root value
    [<MethodImpl(MethodImplOptions.Synchronized)>]
    member this.unbindRoot() =
        this.setRoot (Unbound(this))
        this.incrementRev ()

    /// Set the Var's root to a computed value
    [<MethodImpl(MethodImplOptions.Synchronized)>]
    member this.commuteRoot(fn: IFn) =
        let newRoot = fn.invoke
        this.validate (newRoot)
        let oldRoot = _root
        this.setRoot (newRoot)
        this.incrementRev ()
        this.notifyWatches (oldRoot, newRoot)

    /// Change the var's root to a computed value (based on current value and supplied arguments).
    [<MethodImpl(MethodImplOptions.Synchronized)>]
    member this.alterRoot(fn: IFn, args: ISeq) =
        let newRoot = fn.applyTo (RTSeq.cons (_root, args))
        this.validate (newRoot)
        let oldRoot = _root
        this.setRoot (newRoot)
        this.incrementRev ()
        this.notifyWatches (oldRoot, newRoot)
        newRoot

    ////////////////////////////////
    //
    //  interface definitions
    //
    ////////////////////////////////


    // When IDeref was added and get() was renamed to deref(), this was put in.
    // Why?  Perhaps to void having to change calls to Var.get() all over the place.

    /// The current value of the Var in this thread.
    member this.get() =
        if _threadBound.Get() then
            (this :> IDeref).deref ()
        else
            _root

    interface IDeref with
        member this.deref() =
            let tbox = this.getThreadBinding ()
            if not <| isNull tbox then tbox.Value else _root

    interface IRef with
        override this.setValidator(vf: IFn) =
            if this.hasRoot () then
                ARef.validate (vf, _root)

            this.setValidatorInternal (vf)


    interface Settable with
        member this.doSet(v) = this.set (v)

        member this.doReset(v) =
            this.bindRoot (v)
            v


    ////////////////////////////////
    //
    //  core.clj compatibility methods
    //
    ////////////////////////////////

    /// Find the var from a namespace-qualified symbol.
    static member find(nsQualifiedSym: Symbol) : Var =
        if isNull nsQualifiedSym.Namespace then
            raise
            <| new ArgumentNullException("nsQualifiedSym", "Symbol must be namespace-qualified")

        let ns = Namespace.find (Symbol.intern (nsQualifiedSym.Namespace))

        if isNull ns then
            raise <| new ArgumentException($"No such namespace: {nsQualifiedSym.Namespace}")

        ns.findInternedVar (Symbol.intern (nsQualifiedSym.Name))

    /// The namespace this var is interned in.
    member _.ns = _ns

    /// The tag on the Var
    member this.getTag() = this.tag

    /// Set the tag on the var
    member this.setTag(tag: obj) = this.tag <- tag

    ////////////////////////////////
    //
    //  IFn implementation
    //
    ////////////////////////////////

    // Get the current value as a IFn.
    // The original code did not have the direct error check here.
    // The error would be caught later when an invoke was attempted.
    // This should be more informative.

    member this.getFn() =
        let v = (this :> IDeref).deref ()

        match v with
        | :? IFn as f -> f
        | _ ->
            raise
            <| new InvalidOperationException($"Var {this} is bound to a non-function.")

    // TODO: Check to see if the original use of Util.Ret1 is necessary.

    interface IFn with
        member this.applyTo(argList: ISeq) = this.getFn().applyTo (argList)

        member this.invoke
            (
                arg1: obj,
                arg2: obj,
                arg3: obj,
                arg4: obj,
                arg5: obj,
                arg6: obj,
                arg7: obj,
                arg8: obj,
                arg9: obj,
                arg10: obj,
                arg11: obj,
                arg12: obj,
                arg13: obj,
                arg14: obj,
                arg15: obj,
                arg16: obj,
                arg17: obj,
                arg18: obj,
                arg19: obj,
                arg20: obj,
                args: obj array
            ) : obj =
            this
                .getFn()
                .invoke (
                    arg1,
                    arg2,
                    arg3,
                    arg4,
                    arg5,
                    arg6,
                    arg7,
                    arg8,
                    arg9,
                    arg10,
                    arg11,
                    arg12,
                    arg13,
                    arg14,
                    arg15,
                    arg16,
                    arg17,
                    arg18,
                    arg19,
                    arg20,
                    args
                )

        member this.invoke
            (
                arg1: obj,
                arg2: obj,
                arg3: obj,
                arg4: obj,
                arg5: obj,
                arg6: obj,
                arg7: obj,
                arg8: obj,
                arg9: obj,
                arg10: obj,
                arg11: obj,
                arg12: obj,
                arg13: obj,
                arg14: obj,
                arg15: obj,
                arg16: obj,
                arg17: obj,
                arg18: obj,
                arg19: obj,
                arg20: obj
            ) : obj =
            this
                .getFn()
                .invoke (
                    arg1,
                    arg2,
                    arg3,
                    arg4,
                    arg5,
                    arg6,
                    arg7,
                    arg8,
                    arg9,
                    arg10,
                    arg11,
                    arg12,
                    arg13,
                    arg14,
                    arg15,
                    arg16,
                    arg17,
                    arg18,
                    arg19,
                    arg20
                )

        member this.invoke
            (
                arg1: obj,
                arg2: obj,
                arg3: obj,
                arg4: obj,
                arg5: obj,
                arg6: obj,
                arg7: obj,
                arg8: obj,
                arg9: obj,
                arg10: obj,
                arg11: obj,
                arg12: obj,
                arg13: obj,
                arg14: obj,
                arg15: obj,
                arg16: obj,
                arg17: obj,
                arg18: obj,
                arg19: obj
            ) : obj =
            this
                .getFn()
                .invoke (
                    arg1,
                    arg2,
                    arg3,
                    arg4,
                    arg5,
                    arg6,
                    arg7,
                    arg8,
                    arg9,
                    arg10,
                    arg11,
                    arg12,
                    arg13,
                    arg14,
                    arg15,
                    arg16,
                    arg17,
                    arg18,
                    arg19
                )

        member this.invoke
            (
                arg1: obj,
                arg2: obj,
                arg3: obj,
                arg4: obj,
                arg5: obj,
                arg6: obj,
                arg7: obj,
                arg8: obj,
                arg9: obj,
                arg10: obj,
                arg11: obj,
                arg12: obj,
                arg13: obj,
                arg14: obj,
                arg15: obj,
                arg16: obj,
                arg17: obj,
                arg18: obj
            ) : obj =
            this
                .getFn()
                .invoke (
                    arg1,
                    arg2,
                    arg3,
                    arg4,
                    arg5,
                    arg6,
                    arg7,
                    arg8,
                    arg9,
                    arg10,
                    arg11,
                    arg12,
                    arg13,
                    arg14,
                    arg15,
                    arg16,
                    arg17,
                    arg18
                )

        member this.invoke
            (
                arg1: obj,
                arg2: obj,
                arg3: obj,
                arg4: obj,
                arg5: obj,
                arg6: obj,
                arg7: obj,
                arg8: obj,
                arg9: obj,
                arg10: obj,
                arg11: obj,
                arg12: obj,
                arg13: obj,
                arg14: obj,
                arg15: obj,
                arg16: obj,
                arg17: obj
            ) : obj =
            this
                .getFn()
                .invoke (
                    arg1,
                    arg2,
                    arg3,
                    arg4,
                    arg5,
                    arg6,
                    arg7,
                    arg8,
                    arg9,
                    arg10,
                    arg11,
                    arg12,
                    arg13,
                    arg14,
                    arg15,
                    arg16,
                    arg17
                )

        member this.invoke
            (
                arg1: obj,
                arg2: obj,
                arg3: obj,
                arg4: obj,
                arg5: obj,
                arg6: obj,
                arg7: obj,
                arg8: obj,
                arg9: obj,
                arg10: obj,
                arg11: obj,
                arg12: obj,
                arg13: obj,
                arg14: obj,
                arg15: obj,
                arg16: obj
            ) : obj =
            this
                .getFn()
                .invoke (
                    arg1,
                    arg2,
                    arg3,
                    arg4,
                    arg5,
                    arg6,
                    arg7,
                    arg8,
                    arg9,
                    arg10,
                    arg11,
                    arg12,
                    arg13,
                    arg14,
                    arg15,
                    arg16
                )

        member this.invoke
            (
                arg1: obj,
                arg2: obj,
                arg3: obj,
                arg4: obj,
                arg5: obj,
                arg6: obj,
                arg7: obj,
                arg8: obj,
                arg9: obj,
                arg10: obj,
                arg11: obj,
                arg12: obj,
                arg13: obj,
                arg14: obj,
                arg15: obj
            ) : obj =
            this
                .getFn()
                .invoke (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15)

        member this.invoke
            (
                arg1: obj,
                arg2: obj,
                arg3: obj,
                arg4: obj,
                arg5: obj,
                arg6: obj,
                arg7: obj,
                arg8: obj,
                arg9: obj,
                arg10: obj,
                arg11: obj,
                arg12: obj,
                arg13: obj,
                arg14: obj
            ) : obj =
            this
                .getFn()
                .invoke (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14)

        member this.invoke
            (
                arg1: obj,
                arg2: obj,
                arg3: obj,
                arg4: obj,
                arg5: obj,
                arg6: obj,
                arg7: obj,
                arg8: obj,
                arg9: obj,
                arg10: obj,
                arg11: obj,
                arg12: obj,
                arg13: obj
            ) : obj =
            this
                .getFn()
                .invoke (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13)

        member this.invoke
            (
                arg1: obj,
                arg2: obj,
                arg3: obj,
                arg4: obj,
                arg5: obj,
                arg6: obj,
                arg7: obj,
                arg8: obj,
                arg9: obj,
                arg10: obj,
                arg11: obj,
                arg12: obj
            ) : obj =
            this
                .getFn()
                .invoke (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12)

        member this.invoke
            (
                arg1: obj,
                arg2: obj,
                arg3: obj,
                arg4: obj,
                arg5: obj,
                arg6: obj,
                arg7: obj,
                arg8: obj,
                arg9: obj,
                arg10: obj,
                arg11: obj
            ) : obj =
            this
                .getFn()
                .invoke (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11)

        member this.invoke
            (
                arg1: obj,
                arg2: obj,
                arg3: obj,
                arg4: obj,
                arg5: obj,
                arg6: obj,
                arg7: obj,
                arg8: obj,
                arg9: obj,
                arg10: obj
            ) : obj =
            this
                .getFn()
                .invoke (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10)

        member this.invoke
            (
                arg1: obj,
                arg2: obj,
                arg3: obj,
                arg4: obj,
                arg5: obj,
                arg6: obj,
                arg7: obj,
                arg8: obj,
                arg9: obj
            ) : obj =
            this.getFn().invoke (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9)

        member this.invoke
            (
                arg1: obj,
                arg2: obj,
                arg3: obj,
                arg4: obj,
                arg5: obj,
                arg6: obj,
                arg7: obj,
                arg8: obj
            ) : obj =
            this.getFn().invoke (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8)

        member this.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj) : obj =
            this.getFn().invoke (arg1, arg2, arg3, arg4, arg5, arg6, arg7)

        member this.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj) : obj =
            this.getFn().invoke (arg1, arg2, arg3, arg4, arg5, arg6)

        member this.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj) : obj =
            this.getFn().invoke (arg1, arg2, arg3, arg4, arg5)

        member this.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj) : obj =
            this.getFn().invoke (arg1, arg2, arg3, arg4)

        member this.invoke(arg1: obj, arg2: obj, arg3: obj) : obj = this.getFn().invoke (arg1, arg2, arg3)
        member this.invoke(arg1: obj, arg2: obj) : obj = this.getFn().invoke (arg1, arg2)
        member this.invoke(arg1: obj) : obj = this.getFn().invoke (arg1)
        member this.invoke() : obj = this.getFn().invoke ()



/// A special value indicating that a Var is unbound.
and [<Sealed>] private Unbound(v: Var) =
    inherit AFn()

    override _.ToString() : string = $"Unbound: {v.ToString}"


/// Runtime support for Vars and Namespaces
and [<Sealed; AbstractClass>] RTVar() =

    /// The clojure.core namespace
    static member val ClojureNamespace = Namespace.findOrCreate (Symbol.intern "clojure.core")
    // Note: the namespace will be created on initialization.


    // Used mostly by the LispReader
    static member val UnquoteSym = Symbol.intern ("clojure.core", "unquote")
    static member val UnquoteSplicingSym = Symbol.intern ("clojure.core", "unquote-splicing")
    static member val DerefSym = Symbol.intern ("clojure.core", "deref")
    static member val ApplySym = Symbol.intern ("clojure.core", "apply")
    static member val ConcatSym = Symbol.intern ("clojure.core", "concat")
    static member val HashMapSym = Symbol.intern ("clojure.core", "hash-map")
    static member val HashSetSym = Symbol.intern ("clojure.core", "hash-set")
    static member val VectorSym = Symbol.intern ("clojure.core", "vector")
    static member val ListSym = Symbol.intern ("clojure.core", "list")
    static member val WithMetaSym = Symbol.intern ("clojure.core", "with-meta")
    static member val SeqSym = Symbol.intern ("clojure.core", "seq")
    static member val ISeqSym = Symbol.intern ("clojure.lang.ISeq")

    // Compiler special forms
    // One would think they would be over in Clojure.Compiler, but the LispReader needs to know about specials

    static member val DefSym = Symbol.intern ("def")
    static member val LoopSym = Symbol.intern ("loop*")
    static member val RecurSym = Symbol.intern ("recur")
    static member val IfSym = Symbol.intern ("if")
    static member val CaseSym = Symbol.intern ("case*")
    static member val LetSym = Symbol.intern ("let*")
    static member val LetfnSym = Symbol.intern ("letfn*")
    static member val DoSym = Symbol.intern ("do")
    static member val FnSym = Symbol.intern ("fn*")
    static member val TheVarSym = Symbol.intern ("var")
    static member val ImportSym = Symbol.intern ("clojure.core", "import*")
    static member val AssignSym = Symbol.intern ("set!")
    static member val DeftypeSym = Symbol.intern ("deftype*")
    static member val ReifySym = Symbol.intern ("reify*")
    static member val TrySym = Symbol.intern ("try")
    static member val ThrowSym = Symbol.intern ("throw")
    static member val MonitorEnterSym = Symbol.intern ("monitor-enter")
    static member val MonitorExitSym = Symbol.intern ("monitor-exit")
    static member val CatchSym = Symbol.intern ("catch")
    static member val FinallySym = Symbol.intern ("finally")
    static member val QuoteSym = Symbol.intern ("quote")
    static member val NewSym = Symbol.intern ("new")
    static member val DotSym = Symbol.intern (".")
    static member val AmpersandSym = Symbol.intern ("&")

    static member val CompilerSpecialSymbols =
        PersistentHashSet.create (
            RTVar.DefSym,
            RTVar.LoopSym,
            RTVar.RecurSym,
            RTVar.IfSym,
            RTVar.CaseSym,
            RTVar.LetSym,
            RTVar.LetfnSym,
            RTVar.DoSym,
            RTVar.FnSym,
            RTVar.QuoteSym,
            RTVar.TheVarSym,
            RTVar.ImportSym,
            RTVar.DotSym,
            RTVar.AssignSym,
            RTVar.DeftypeSym,
            RTVar.ReifySym,
            RTVar.TrySym,
            RTVar.ThrowSym,
            RTVar.MonitorEnterSym,
            RTVar.MonitorExitSym,
            RTVar.CatchSym,
            RTVar.FinallySym,
            RTVar.NewSym,
            RTVar.AmpersandSym
        )

    // other special symbols
    static member val FnOnceSym: Symbol =
        (Symbol.intern ("fn*") :> IObj)
            .withMeta (RTMap.map (Keyword.intern (null, "once"), true))
        :?> Symbol

    static member val TypeArgsSym = Symbol.intern ("type-args")
    static member val ByRefSym = Symbol.intern ("by-ref")
    static member val ParamTagAnySym = Symbol.intern (null, "_")


    // Keywords for file info

    static member val LineKeyword = Keyword.intern (null, "line")
    static member val ColumnKeyword = Keyword.intern (null, "column")
    static member val FileKeyword = Keyword.intern (null, "file")
    static member val SourceSpanKeyword = Keyword.intern (null, "source-span")
    static member val StartLineKeyword = Keyword.intern (null, "start-line")
    static member val StartColumnKeyword = Keyword.intern (null, "start-column")
    static member val EndLineKeyword = Keyword.intern (null, "end-line")
    static member val EndColumnKeyword = Keyword.intern (null, "end-column")

    // Keywords for inline & similar

    static member val InlineKeyword = Keyword.intern (null, "inline")
    static member val InlineAritiesKeyword = Keyword.intern (null, "inline-arities")
    static member val FormKeywoard = Keyword.intern (null, "form")
    static member val ArglistsKeyword = Keyword.intern (null, "arglists")
    static member val DocKeyword = Keyword.intern (null, "doc")
    static member val DynamicKeyword = Keyword.intern (null, "dynamic")
    static member val RettagKeyword = Keyword.intern (null, "rettag")
    static member val OnceOnlyKeyword = Keyword.intern (null, "once")
    static member val DirectLinkingKeyword = Keyword.intern (null, "direct-linking")
    static member val RedefKeyword = Keyword.intern (null, "redef")
    static member val DeclaredKeyword = Keyword.intern (null, "declared")


    // Keywords for LispReader

    static member val UnknownKeyword = Keyword.intern (null, "unknown")
    static member val ParamTagsKeyword = Keyword.intern (null, "param-tags")

    // Parser options

    static member val OptEofKeword = Keyword.intern (null, "eof")
    static member val OptFeaturesKeyword = Keyword.intern (null, "features")
    static member val OptReadCondKeyword = Keyword.intern (null, "read-cond")

    // Platform features - always installed

    static member val PlatformKey = Keyword.intern (null, "cljr")


    // Reader conditional options - use with :read-cond

    static member val CondAllowKeyword = Keyword.intern (null, "allow")
    static member val CondPreserveKeyword = Keyword.intern (null, "preserve")


    static member val TagKeyword = Keyword.intern (null, "tag")

    // Symbolic items - namespace-related

    static member val NsSym = Symbol.intern ("ns")
    static member val InNsSym = Symbol.intern ("in-ns")

    static member val CurrentNSVar =
        Var
            .intern(RTVar.ClojureNamespace, Symbol.intern ("*ns*"), Namespace.ClojureNamespace)
            .setDynamic ()

    static member val InNSVar = Var.intern (RTVar.ClojureNamespace, RTVar.InNsSym, false)
    static member val NsVar = Var.intern (RTVar.ClojureNamespace, Symbol.intern ("ns"), false)

    // Pre-defined vars (compiler-related)

    static member val InstanceVar =
        Var
            .intern(RTVar.ClojureNamespace, Symbol.intern ("instance?"), false)
            .setDynamic (true)


    // Pre-defined Vars (I/O-related)

    static member val ErrVar =
        Var
            .intern(RTVar.ClojureNamespace, Symbol.intern ("*err*"), new StreamWriter(Console.OpenStandardError()))
            .setDynamic ()

    static member val OutVar =
        Var
            .intern(RTVar.ClojureNamespace, Symbol.intern ("*out*"), new StreamWriter(Console.OpenStandardOutput()))
            .setDynamic ()

    static member val InVar =
        Var
            .intern(RTVar.ClojureNamespace, Symbol.intern ("*in*"), new StreamReader(Console.OpenStandardInput()))
            .setDynamic ()

    // Pre-defined Var (data readers)

    static member val DataReadersVar =
        Var
            .intern(Namespace.ClojureNamespace, Symbol.intern ("*data-readers*"), RTMap.map ())
            .setDynamic ()

    static member val DefaultDataReaderFnVar =
        Var.intern (Namespace.ClojureNamespace, Symbol.intern ("*default-data-reader-fn*"), RTMap.map ())

    static member val DefaultDataReadersVar =
        Var.intern (Namespace.ClojureNamespace, Symbol.intern ("default-data-readers"), RTMap.map ())


    // Pre-defined Vars (printing-related)

    static member val PrintReadablyVar =
        Var
            .intern(RTVar.ClojureNamespace, Symbol.intern ("*print-readably*"), true)
            .setDynamic ()

    static member val PrintMetaVar =
        Var
            .intern(RTVar.ClojureNamespace, Symbol.intern ("*print-meta*"), false)
            .setDynamic ()

    static member val PrintDupVar =
        Var
            .intern(RTVar.ClojureNamespace, Symbol.intern ("*print-dup*"), false)
            .setDynamic ()

    static member val FlushOnNewlineVar =
        Var
            .intern(RTVar.ClojureNamespace, Symbol.intern ("*flush-on-newline*"), true)
            .setDynamic ()

    static member val PrintInitializedVar =
        Var
            .intern(RTVar.ClojureNamespace, Symbol.intern ("*print-initialized*"), false)
            .setDynamic ()

    static member val PrOnVar = Var.intern (RTVar.ClojureNamespace, Symbol.intern ("pr-on"))

    static member val AllowSymbolEscapeVar =
        Var
            .intern(RTVar.ClojureNamespace, Symbol.intern ("*allow-symbol-escape*"), true)
            .setDynamic ()

    // Pre-defined Vars (miscellaneous)

    static member val AllowUnresolvedVarsVar =
        Var
            .intern(RTVar.ClojureNamespace, Symbol.intern ("*allow-unresolved-vars*"), false)
            .setDynamic ()

    // Need to have this located before the initialization of ReadEvalVar
    static member val _readEval =
        let mutable v = Environment.GetEnvironmentVariable("CLOJURE_READ_EVAL")

        if isNull v then
            v <- Environment.GetEnvironmentVariable("clojure.read.eval")

        if isNull v then
            v <- "true"

        RTVar.readTrueFalseUnknown (v)


    static member val ReaderResolverVar =
        Var
            .intern(RTVar.ClojureNamespace, Symbol.intern ("*reader-resolver*"), null)
            .setDynamic ()

    static member val ReadEvalVar =
        Var
            .intern(RTVar.ClojureNamespace, Symbol.intern ("*read-eval*"), RTVar._readEval)
            .setDynamic ()

    static member val SuppressReadVar =
        Var
            .intern(RTVar.ClojureNamespace, Symbol.intern ("*suppress-read*"), null)
            .setDynamic ()


    static member getCurrentNamespace() =
        (RTVar.CurrentNSVar :> IDeref).deref () :?> Namespace

    // In here because both LispReader and EdnReader need it
    static member suppressRead() =
        RT0.booleanCast ((RTVar.SuppressReadVar :> IDeref).deref ())

    static member readTrueFalseUnknown(s: string) : obj =
        match s with
        | "true" -> true
        | "false" -> false
        | _ -> Keyword.intern (null, "unknown")


    // original comment: duck typing stderr plays nice with e.g. swank

    /// Retrieve the standard error TextWriter.
    static member errPrintWriter() : TextWriter =
        let w = (RTVar.ErrVar :> IDeref).deref ()

        match w with
        | :? TextWriter as tw -> tw
        | :? Stream as s -> new StreamWriter(s)
        | _ -> failwith "Unknown type for *err*"

    /// Create a Var with the given namespace and name, null root value, interned in the named namespace (create if necessary).
    static member var(nsString: string, nameString: string) : Var =
        let ns = Namespace.findOrCreate (Symbol.intern (null, nsString))
        let name = Symbol.intern (null, nameString)
        Var.intern (ns, name)

    /// Create a Var with the given namespace, name, and initial value, interned in the named namespace (create if necessary).
    static member var(nsString: string, nameString: string, init: obj) : Var =
        let ns = Namespace.findOrCreate (Symbol.intern (null, nsString))
        let name = Symbol.intern (null, nameString)
        Var.intern (ns, name, init)
