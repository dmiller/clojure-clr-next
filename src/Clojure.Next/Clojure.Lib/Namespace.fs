namespace Clojure.Lib

open Clojure.Collections
open System.Reflection
open System
open System.Collections.Generic
open System.Linq
open System.Text
open System.Collections.Concurrent
open System.IO
open System.Threading
open System.Runtime.CompilerServices


module DefaultImports =

    let getTypes(a : Assembly) =
        try 
            a.GetTypes()
        with 
            | _ -> [||]

    let getAllTypesInNamespace(namespaceName : string) =
        AppDomain.CurrentDomain.GetAssemblies()
        |> Array.collect getTypes
        |> Array.filter (fun t -> 
            (t.IsClass || t.IsInterface || t.IsValueType) &&
            t.IsPublic &&
            t.Namespace = namespaceName &&
            not t.IsGenericTypeDefinition &&
            not <| t.Name.StartsWith("_"))

    let createDefaultImportDirectory() : Dictionary<Symbol,Type> =
        let q = getAllTypesInNamespace "System"
        let d = (q:>IEnumerable<Type>).ToDictionary  (fun t -> Symbol.intern t.Name)

        // Adding these to support bootstrapping in clojure.core
        d.Add(Symbol.intern("StringBuilder"), typeof<StringBuilder>)
        d.Add(Symbol.intern("BigInteger"), typeof<System.Numerics.BigInteger>)
        d.Add(Symbol.intern("BigDecimal"), typeof<System.Decimal>)
    
        d

    let createMap() = 
        PersistentHashMap.createD2(createDefaultImportDirectory())

    let imports = createMap()
    

// A Namespace holds a map from symbols to references.
// Symbol to reference mappings come in several flavors:
//    Simple: Symbol
//    Use/refer: Symbol to a Var that is homed in another namespace.
//    Import: Symbol to a Type
// A namespace can also refer to another namespace by an alias.

[<Sealed;AllowNullLiteral>]
type Namespace(name : Symbol) =
    inherit AReference((name:>IMeta).meta())

    // variable-to-value map
    let mappings : AtomicReference<IPersistentMap> = AtomicReference(DefaultImports.imports)

    // variable-to-namespace alias map
    let aliases : AtomicReference<IPersistentMap> = AtomicReference(PersistentArrayMap.Empty)

    // All namespaces, keyed by Symbol
    static let namespaces = ConcurrentDictionary<Symbol,Namespace>()

    static let clojureNamespace = Namespace.findOrCreate(Symbol.intern "clojure.core")

    // Some accessors
    member _.Name = name
    member _.Aliases = aliases.Get()
    member _.Mappings = mappings.Get()    

    static member ClojureNamespace = clojureNamespace
    
    override _.ToString() = name.ToString()

    ///////////////////////////////////////////
    //
    // Namespace manipulation
    //
    ///////////////////////////////////////////

    static member All = namespaces.Values |> RTSeq.seq

    // Find or create a namespace named by the symbol.
    static member findOrCreate(name : Symbol) =  namespaces.GetOrAdd(name, fun _ -> new Namespace(name))

    // Remove a namespace (by name)
    // Return the removed namespace, or null if not found
    // It's an error to try to remove the clojure.core namespace
    static member remove(name : Symbol) =
        if name.Equals(clojureNamespace.Name) then
            raise <| ArgumentException("Cannot remove clojure namespace")
        else
            let (result, ns) = namespaces.TryRemove(name)
            ns

    // Find a namespace given a name.
    // Return null if not found.
    static member find(name : Symbol) = 
        let (result, ns) = namespaces.TryGetValue(name)
        ns

    ///////////////////////////////////////////
    //
    // Interning
    //
    ///////////////////////////////////////////

    // Determine if a mapping is interned
    // An interned mapping is one where a var's ns matches the current ns and its sym matches the mapping key.
    // Once established, interned mappings should never change.
    member private this.isInternedMapping(sym : Symbol, o : obj) =
        match o with
        | :? Var as v -> Object.ReferenceEquals(v.Namespace,this) && v.Name.Equals(sym)
        | _ -> false
    
    // Intern a Symbol in the namespace, with a (new) Var as its value.
    // It is an error to intern a symbol with a namespace.
    // This has to deal with other threads also interning.
    member this.intern(sym:Symbol) =
        if not <| isNull sym.Namespace then
            raise <| ArgumentException("Can't intern namespace-qualified symbol")

        let mutable map = this.Mappings
        let mutable o = map.valAt(sym)
        let mutable v : Var = null

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
                v <- Var.createInternal(this, sym)
            let newMap = map.assoc(sym, v)
            mappings.CompareAndSet(map, newMap) |> ignore
            map <- this.Mappings
            o <- map.valAt(sym)

        // an interned mapping is permanent.  
        // if we have one, use it.

        if this.isInternedMapping(sym,o) then
            // an interned mapping is permanent.  
            // if we have one, use it.
            o :?> Var
        else
            if isNull v then
                // in this case, we didn't create a Var in the loop above, meaning we didn't loop at all, meaning o already had a value.
                // We need to create a Var to use.
                v <- Var.createInternal(this, sym)
            // make sure we can replace the existing value with the new one.
            if (this.checkReplacement(sym, o, v)) then
                // replacement ok.  keep going until we get the new value in.
                while not <| mappings.CompareAndSet(map, map.assoc(sym, v)) do
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

        let defaultResponse() = 
                RTVar.errPrintWriter().WriteLine($"WARNING: {sym} already refers to: {oldVal} in namespace: {name}, being replaced by: {newVal}")
                RTVar.errPrintWriter().Flush()
                true
            
        match oldVal with
        | :? Var as ovar ->
            let ons = ovar.Namespace
            let nns = match newVal  with 
                      | :? Var as nvar -> nvar.Namespace
                      | _ -> null
            if this.isInternedMapping(sym, oldVal) then
                if not <| Object.ReferenceEquals(nns, RTVar.ClojureNamespace) then
                    RTVar.errPrintWriter().WriteLine($"REJECTED: attempt to replace interned var {oldVal} with {newVal} in {name}, you must ns-unmap first")
                    RTVar.errPrintWriter().Flush()
                    false
                else
                    false
            else
                defaultResponse()
        | _ -> defaultResponse()
            

     // Intern a symbol with a specified value.
     // Returns the value that is associated.
    member this.reference(sym: Symbol, value: obj) =
        if not <| isNull sym.Namespace then
            raise <| ArgumentException("Can't intern a namespace-qualified symbol")
            
        let mutable map = this.Mappings
        let mutable o = map.valAt(sym)

        // race condition
        while (isNull o) do
            let newMap = map.assoc(sym, value)
            mappings.CompareAndSet(map, newMap) |> ignore
            map <- this.Mappings
            o <- map.valAt(sym)

        if not <| Object.ReferenceEquals(o,value) && this.checkReplacement(sym, o, value) then
            while not <| mappings.CompareAndSet(map, map.assoc(sym, value)) do
                map <- this.Mappings
            value
        else 
            o

    // Remove a symbol from the namespace
    // It is illegal to remove a namespace-qualified symbol.
    member this.unmap(sym: Symbol) =
        if not <| isNull sym.Namespace then
            raise <| ArgumentException("Can't unintern a namespace-qualified symbol")

        let mutable map = this.Mappings
        while map.containsKey(sym) do
            let newMap = map.without(sym)
            mappings.CompareAndSet(map, newMap) |> ignore
            map <- this.Mappings

    // This comes from the Java verion.
    // During loading of the clojure runtime source, it never gets past the t1 <> t2 test.
    // Not sure we need it, but I'm not getting rid of it.
    static member private areDifferentInstancesOfSameClassName(t1 : Type, t2 : Type) =
        not <| Object.ReferenceEquals(t1,t2) && t1.FullName.Equals(t2.FullName)

    
    member private this.referenceClass(sym : Symbol, value : Type) =
        if not <| isNull sym.Namespace  then
            raise <| ArgumentException("Can't intern namespace-qualified symbol")

        // traditional race condition hacking -- keep looping until we find the symbol/type pair in the map.
        let mutable map = this.Mappings
        let mutable c = map.valAt(sym) :?> Type
        while (isNull c || Namespace.areDifferentInstancesOfSameClassName(c, value)) do
            let newMap = map.assoc(sym, value)
            mappings.CompareAndSet(map, newMap) |> ignore
            map <- this.Mappings
            c <- map.valAt(sym) :?> Type

        if not <| Object.ReferenceEquals(c,value) then
                raise <| InvalidOperationException(sprintf "%A already refers to: %A in namespace: %A" sym c name)
        c


    // Map a symbol to a Type (import)
    // Named import instead of ImportType for core.clj compatibility.
    member this.importClass(sym: Symbol, t: Type) =
        this.referenceClass(sym, t)

    // Mape a symbol to a Type (import) using the type name for the symbol name.
    // Named import instead of ImportType for core.clj compatibility.
    member this.importClass(t: Type) =
        this.importClass(Symbol.intern (Util.nameForType(t)), t)

    // Add a symbol to Var reference.
    member this.refer(sym: Symbol, v: Var) =
        this.reference(sym, v) :?> Var

    ///////////////////////////////////////////
    //
    // Mappings
    //
    ///////////////////////////////////////////

    // Get the value mapped to a symbol.
    member this.getMapping(sym: Symbol) =
        this.Mappings.valAt(sym)

    // Find the Var mapped to a Symbol.
    member this.findInternedVar(sym: Symbol) =
        let o = this.Mappings.valAt(sym)
        match o with
        | :? Var as v when Object.ReferenceEquals(v.Namespace,this) -> v
        | _ -> null

    ///////////////////////////////////////////
    //
    // Aliases
    //
    ///////////////////////////////////////////

    // Find a Namespace aliased by a Symbol.
    member this.lookupAlias(alias: Symbol) =
        this.Aliases.valAt(alias) :?> Namespace

    // Add an alias for a namespace.
    member this.addAlias(alias: Symbol, ns: Namespace) =
        if isNull alias then
            raise <| ArgumentNullException("alias","Expecting Symbol + Namespace")
        if isNull ns then
            raise <| ArgumentNullException("ns","Expecting Symbol + Namespace")

        // race condition
        let mutable map = this.Aliases
        while not <| map.containsKey(alias) do
            let newMap = map.assoc(alias, ns)
            aliases.CompareAndSet(map, newMap) |> ignore
            map <- this.Aliases

        // you can rebind an alias, but only to the initially-aliased namespace

        if not <| Object.ReferenceEquals(map.valAt(alias), ns) then
            raise <| InvalidOperationException($"Alias {alias} already exists in namespace {name}, aliasing {map.valAt(alias)}")

        // Remove an alias.
        // Race condition
        member this.removeAlias(alias: Symbol) =
            let mutable map = this.Aliases
            while map.containsKey(alias) do
                let newMap = map.without(alias)
                aliases.CompareAndSet(map, newMap) |> ignore
                map <- this.Aliases

        ///////////////////////////////////////////
        //
        // core.clj compatibility
        //
        ///////////////////////////////////////////

        // Get the namespace name.
        member this.getName() = name

        // Get the mappings of the namespace.
        member this.getMappings() = this.Mappings

        // Get the aliases.
        member this.getAliases() = this.Aliases

and [<AllowNullLiteral>] private Frame(bindings: Associative, prev: Frame) =

    member _.Bindings = bindings
 
    interface ICloneable with
        member this.Clone() = Frame(bindings, null)

    static member val Top : Frame = Frame(PersistentHashMap.Empty, null)


and [<Sealed;AllowNullLiteral>] TBox(thread: Thread, v: obj) =

    [<VolatileField>]
    let mutable value = v

    member _.Value 
        with get() = value
        and  set(v) = value <- v

    member _.Thread with get() = thread
    
and [<Sealed;AllowNullLiteral>] Var private (_ns: Namespace, sym: Symbol) = 
    inherit ARef(PersistentHashMap.Empty)

    // revision counter
    [<VolatileField>]
    let mutable rev : int = 0

    // If true, supports dynamic binding
    [<VolatileField>]
    let mutable dynamic = false

    // The root value
    [<VolatileField>]
    let mutable root = null

    // Whether the Var has a thread-bound value
    let threadBound = AtomicBoolean(false)

    [<DefaultValue;ThreadStatic>]
    static val mutable private currentFrame : Frame

    static member private CurrentFrame 
        with get() = 
            if isNull Var.currentFrame then Var.currentFrame <- Frame.Top
            Var.currentFrame
        and set (v) = Var.currentFrame <- v


    member _.incrementRev() = rev <- rev + 1
    member _.setRoot(newValue:obj) = root <- newValue
    member _.setThreadBound(b: bool) = threadBound.Set(b)

    override _.ToString() =
        match _ns with
        | null -> $"""#<Var: {if isNull sym then "--unnamed--" else sym.ToString()}>"""
        | _ -> $"#'{_ns}/{sym}"
    // To avoid initialization checks that would be caused by the circular dependency between the Var and the Unbound value in its root,
    // I made the constructor private and created a factory method to create a Var.
    // Called createInternal to avoid name collision with the 'create' method in the public interface.
    static member internal createInternal(ns: Namespace, sym: Symbol) = 
        let v = new Var(ns, sym)
        v.setRoot(Unbound(v))
        v

    static member internal createInternal(ns: Namespace, sym: Symbol, initValue: obj) = 
        let v = new Var(ns, sym)
        v.setRoot(initValue)
        v.incrementRev()
        v

    member _.Namespace = _ns
    member _.Name = sym


    ////////////////////////////////
    //
    //  Special values
    //
    ////////////////////////////////

    static member val private privateKey = Keyword.intern(null,"private")
    static member val private privateMeta = PersistentArrayMap([|Var.privateKey, true|])
    static member val private macroKey = Keyword.intern(null,"macro")
    static member val private nameKey = Keyword.intern(null,"name")
    static member val private nsKey = Keyword.intern(null,"ns")
    static member val private tagKey = Keyword.intern(null,"tag")

    static member val private dissocFn = 
        { new AFn() with
            member _.ToString (): string = "Var.dissocFn"
          interface IFn with
            member _.invoke(m,k) = RTMap.dissoc(m,k)
        }

    static member val private assocFn = 
        { new AFn() with
            member _.ToString (): string = "Var.dissocFn"
          interface IFn with
            member _.invoke(m,k, v) = RTMap.assoc(m, k, v)
        }

    ////////////////////////////////
    //
    //  Factory methods
    //
    ////////////////////////////////

    // Intern a named var in a namespace.
    static member intern(ns: Namespace, sym: Symbol) = ns.intern(sym)
    

    // Intern a named var in a namespace (creating the namespece if necessary).
    static member intern(nsName: Symbol, sym: Symbol) = 
        let ns = Namespace.findOrCreate(nsName)
        Var.intern(ns,sym)

    // Intern a named var in a namespace, with given value (if has a root value already, then change only if replaceRoot is true).
    static member intern(ns: Namespace, sym: Symbol, root: obj, replaceRoot: bool ) = 
        let dvout = ns.intern(sym)
        if not <| dvout.hasRoot() || replaceRoot then
            dvout.bindRoot(root)
        dvout

    // Intern a named var in a namespace, with given value.
    static member intern(ns: Namespace, sym: Symbol, root: obj) = Var.intern(ns, sym, root, true) 

    static member internPrivate(nsName:string, sym :string) = 
        let ns = Namespace.findOrCreate(Symbol.intern nsName)
        let v = Var.intern(ns, Symbol.intern sym)
        v.setMeta(Var.privateMeta)
        v

    // Create an uninterned Var.
    static member create() = Var.createInternal(null, null);
    static member create(root:obj) = Var.createInternal(null, null, root)

    ////////////////////////////////
    //
    //  Binding stack
    //
    ////////////////////////////////

    // Push a new frame of bindings onto the binding stack.
    static member pushThreadBindings(bindings: Associative) =
        let f = Var.CurrentFrame
        let mutable bmap = f.Bindings

        let rec loop (bs:ISeq) =
            match bs with
            | null -> ()
            | _ ->
                let e = bs.first() :?> IMapEntry
                let v = e.key() :?> Var
                if not <| v.isDynamic then
                    raise <| new InvalidOperationException($"Can't dynamically bind non-dynamic var: {v.Namespace}/{v.Name}")
                v.validate(e.value())
                v.setThreadBound(true)
                bmap <- bmap.assoc(v, TBox(Thread.CurrentThread, e.value()))
                loop (bs.next())

        loop (bindings.seq())

        Var.CurrentFrame <- Frame(bmap, f)

    // Pop the topmost binding frame from the stack.
    static member popThreadBindings() =
        let f = Var.CurrentFrame
        if isNull f then
            raise <| new InvalidOperationException("Pop without matching push")
        if (Object.ReferenceEquals(f, Frame.Top)) then
            Var.CurrentFrame = null
        else
            Var.CurrentFrame = f

    // Get the thread-local bindings of the top frame.
    static member getThreadBindings() : Associative =
        let f = Var.CurrentFrame

        let rec loop (bs:ISeq) (ret:IPersistentMap) =
            match bs with
            | null -> ret
            | _ ->
                let e = bs.first() :?> IMapEntry
                let v = e.key() :?> Var
                let tbox = e.value() :?> TBox
                loop (bs.next()) (ret.assoc(v, tbox.Value))

        loop (f.Bindings.seq()) PersistentHashMap.Empty

    // Get the box of the current binding on the stack for this var, or null if no binding.
    member this.getThreadBinding() =
        if threadBound.Get() then
            let e = Var.CurrentFrame.Bindings.entryAt(this)
            match e with
            | null -> null
            | _ -> e.value() :?> TBox
        else null

    ////////////////////////////////
    //
    //  Frame management
    //
    ////////////////////////////////

    // Copying the original Java code here.
    // getThreadBindingFrame has return type Object.
    // resetThreadBindingFrame 
    // I'm not sure why they did it this way.

    static member getThreadBindingFrame() = Var.CurrentFrame :> obj

    static member cloneThreadBindingFrame() = (Var.CurrentFrame :> ICloneable).Clone()

    static member resetThreadBindingFrame(f: obj) = Var.CurrentFrame <- (f :?> Frame)

    ////////////////////////////////
    //
    //  Meta management
    //
    ////////////////////////////////

    // Set the metadata attached to this var.
    // The metadata must contain entries for the namespace and name.
    member this.setMeta(m: IPersistentMap) =
        (this :> IReference).resetMeta(m.assoc(Var.nameKey,sym).assoc(Var.nsKey,_ns)) |> ignore

    // Add a macro=true flag to the metadata.
    member this.setMacro() = (this :> IReference).alterMeta(Var.assocFn,RTSeq.list(Var.macroKey,true)) |> ignore

    // Is the Var a macro?
    member this.isMacro with get() = RT0.booleanCast((this :> IMeta).meta().valAt(Var.macroKey))

    // Is the var public?
    member this.isPublic with get() = not <| RT0.booleanCast((this :> IMeta).meta().valAt(Var.privateKey))

    // Get the tag on the var.
    member this.tag 
        with get() = (this :> IMeta).meta().valAt(Var.tagKey)
        and set(v) = (this :> IReference).alterMeta(Var.assocFn, RTSeq.list(Var.tagKey,v)) |> ignore

    ////////////////////////////////
    //
    //  Dynamic flag management
    //
    ////////////////////////////////

    member this.setDynamic() = dynamic <- true; this

    member this.setDynamic(b: bool) = dynamic <- b; this
    
    member _.isDynamic = dynamic


    ////////////////////////////////
    //
    //  Value management
    //
    ////////////////////////////////

    // Does the Var have a root value?
    member _.hasRoot() = 
        match root with
        | :? Unbound -> false
        | _ -> true

    // Get the root value.
    member _.getRawRoot() = root

    // Does the Var have a value? (root or  thread-bound)
    member this.isBound 
        with get() = this.hasRoot() || (threadBound.Get() && Var.CurrentFrame.Bindings.containsKey(this))

    // Set the value of the var
    // It is an error to set the root binding with this method
    member this.set(v: obj) = 
        this.validate(v)
        let tbox = this.getThreadBinding()
        match tbox with
        | null -> raise <| new InvalidOperationException($"Can't change/establish root binding of: {sym} with set")
        | _ -> 
            if not <| Object.ReferenceEquals(Thread.CurrentThread,tbox.Thread) then
                raise <| new InvalidOperationException($"Can't set!: {sym} from non-binding thread")
            tbox.Value <- v
            v

    // Change the root value.  (And clear the macro flag.)
    [<MethodImpl(MethodImplOptions.Synchronized)>]
    member this.bindRoot(v: obj) =
        this.validate(v)
        let oldRoot = root
        this.setRoot(v)
        this.incrementRev()
        (this:>IReference).alterMeta(Var.dissocFn, RTSeq.list(Var.macroKey)) |> ignore
        this.notifyWatches(oldRoot, v)

    // Change the root value.
    [<MethodImpl(MethodImplOptions.Synchronized)>]
    member this.swapRoot(v: obj) =
        this.validate(v)
        let oldRoot = root
        this.setRoot(v)
        this.incrementRev()
        this.notifyWatches(oldRoot, v)

    // Unbind the Var's root value
    [<MethodImpl(MethodImplOptions.Synchronized)>]
    member this.unbindRoot() =
        this.setRoot(Unbound(this))
        this.incrementRev()
 
    // Set the Var's root to a computed value
    [<MethodImpl(MethodImplOptions.Synchronized)>]
    member this.commuteRoot(fn: IFn) =
        let newRoot = fn.invoke
        this.validate(newRoot)
        let oldRoot = root
        this.setRoot(newRoot)
        this.incrementRev()
        this.notifyWatches(oldRoot,newRoot)

    // Change the var's root to a computed value (based on current value and supplied arguments).
    [<MethodImpl(MethodImplOptions.Synchronized)>]
    member this.alterRoot(fn : IFn, args : ISeq) =
        let newRoot = fn.applyTo(RTSeq.cons(root,args))
        this.validate(newRoot)
        let oldRoot = root
        this.setRoot(newRoot)
        this.incrementRev()
        this.notifyWatches(oldRoot,newRoot)
        newRoot

    ////////////////////////////////
    //
    //  interface definitions
    //
    ////////////////////////////////

    // Gets the value the Var is holding.
    // When IDeref was added and get() was renamed to deref(), this was put in.
    // Why?  Perhaps to void having to change calls to Var.get() all over the place.

    member this.get() = 
        if threadBound.Get() then
            (this:>IDeref).deref()
        else
            root;

    interface IDeref with
        member this.deref() = 
            let tbox = this.getThreadBinding()
            if not <| isNull tbox then
                tbox.Value
            else
                root

    interface IRef with
        override this.setValidator(vf: IFn) = 
            if this.hasRoot() then
                ARef.validate(vf,root)
            this.setValidatorInternal(vf)


    interface Settable with
        member this.doSet(v) = this.set(v)
        member this.doReset(v) = this.bindRoot(v); v


    ////////////////////////////////
    //
    //  core.clj compatibility methods
    //
    ////////////////////////////////

    // Find the var from a namespace-qualified symbol.
    static member find(nsQualifiedSym : Symbol) : Var =
        if isNull nsQualifiedSym.Namespace then
            raise <| new ArgumentNullException("nsQualifiedSym","Symbol must be namespace-qualified")
        let ns = Namespace.find(Symbol.intern(nsQualifiedSym.Namespace))
        if isNull ns then
            raise <| new ArgumentException($"No such namespace: {nsQualifiedSym.Namespace}")
        ns.findInternedVar(Symbol.intern(nsQualifiedSym.Name))

    // The namespace this var is interned in.
    member _.ns = _ns

    // The tag on the Var
    member this.getTag() = this.tag

    // Set the tag on the var
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
        let v = (this:>IDeref).deref()
        match v with
        | :? IFn as f -> f
        | _ -> raise <| new InvalidOperationException($"Var {this} is bound to a non-function.")

    // TODO: Check to see if the original use of Util.Ret1 is necessary.

    interface IFn with
        member this.applyTo(argList : ISeq ) = this.getFn().applyTo(argList)
        member this.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj, arg9: obj, arg10: obj, arg11: obj, arg12: obj, arg13: obj, arg14: obj, arg15: obj, arg16: obj, arg17: obj, arg18: obj, arg19: obj, arg20: obj, args: obj array): obj = 
            this.getFn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19, arg20, args)
        member this.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj, arg9: obj, arg10: obj, arg11: obj, arg12: obj, arg13: obj, arg14: obj, arg15: obj, arg16: obj, arg17: obj, arg18: obj, arg19: obj, arg20: obj): obj = 
            this.getFn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19, arg20)
        member this.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj, arg9: obj, arg10: obj, arg11: obj, arg12: obj, arg13: obj, arg14: obj, arg15: obj, arg16: obj, arg17: obj, arg18: obj, arg19: obj): obj = 
            this.getFn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19)
        member this.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj, arg9: obj, arg10: obj, arg11: obj, arg12: obj, arg13: obj, arg14: obj, arg15: obj, arg16: obj, arg17: obj, arg18: obj): obj = 
            this.getFn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18)
        member this.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj, arg9: obj, arg10: obj, arg11: obj, arg12: obj, arg13: obj, arg14: obj, arg15: obj, arg16: obj, arg17: obj): obj = 
            this.getFn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17)
        member this.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj, arg9: obj, arg10: obj, arg11: obj, arg12: obj, arg13: obj, arg14: obj, arg15: obj, arg16: obj): obj = 
            this.getFn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16)
        member this.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj, arg9: obj, arg10: obj, arg11: obj, arg12: obj, arg13: obj, arg14: obj, arg15: obj): obj = 
            this.getFn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15)
        member this.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj, arg9: obj, arg10: obj, arg11: obj, arg12: obj, arg13: obj, arg14: obj): obj = 
            this.getFn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14)
        member this.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj, arg9: obj, arg10: obj, arg11: obj, arg12: obj, arg13: obj): obj = 
            this.getFn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13)
        member this.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj, arg9: obj, arg10: obj, arg11: obj, arg12: obj): obj = 
            this.getFn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12)
        member this.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj, arg9: obj, arg10: obj, arg11: obj): obj = 
            this.getFn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11)
        member this.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj, arg9: obj, arg10: obj): obj = 
            this.getFn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10)
        member this.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj, arg9: obj): obj = 
            this.getFn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9)
        member this.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj, arg8: obj): obj = 
            this.getFn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8)
        member this.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj, arg7: obj): obj = 
            this.getFn().invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7)
        member this.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj, arg6: obj): obj = 
            this.getFn().invoke(arg1, arg2, arg3, arg4, arg5, arg6)
        member this.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj, arg5: obj): obj = 
            this.getFn().invoke(arg1, arg2, arg3, arg4, arg5)
        member this.invoke(arg1: obj, arg2: obj, arg3: obj, arg4: obj): obj = 
            this.getFn().invoke(arg1, arg2, arg3, arg4)
        member this.invoke(arg1: obj, arg2: obj, arg3: obj): obj = 
            this.getFn().invoke(arg1, arg2, arg3)
        member this.invoke(arg1: obj, arg2: obj): obj = 
            this.getFn().invoke(arg1, arg2)
        member this.invoke(arg1: obj): obj = 
            this.getFn().invoke(arg1)
        member this.invoke(): obj = 
            this.getFn().invoke()




and [<Sealed>] private Unbound(v: Var) =
    inherit AFn()

    override _.ToString (): string = $"Unbound: {v.ToString}"


and [<Sealed;AbstractClass>] RTVar() = 

    static member val ClojureNamespace = Namespace.findOrCreate(Symbol.intern "clojure.core")

    // Pre-defined Vars (namespace-related)

    static member val CurrentNSVar = Var.intern(RTVar.ClojureNamespace, Symbol.intern("*ns*"), RTVar.ClojureNamespace).setDynamic()
    static member val InNSVar = Var.intern(RTVar.ClojureNamespace, Symbol.intern("in-ns"), false)
    static member val NsVar = Var.intern(RTVar.ClojureNamespace, Symbol.intern("*ns*"), false)

    // Pre-defined Vars (I/O-related)

    static member val ErrVar = Var.intern(RTVar.ClojureNamespace, Symbol.intern("*err*"), new StreamWriter(Console.OpenStandardError())).setDynamic()
    static member val OutVar = Var.intern(RTVar.ClojureNamespace, Symbol.intern("*out*"), new StreamWriter(Console.OpenStandardOutput())).setDynamic()
    static member val InVar = Var.intern(RTVar.ClojureNamespace, Symbol.intern("*in*"), new StreamReader(Console.OpenStandardInput())).setDynamic()

    // Pre-defined Vars (printing-related)

    static member val PrintReadablyVar = Var.intern(RTVar.ClojureNamespace, Symbol.intern("*print-readably*"), true).setDynamic()
    static member val PrintMetaVar = Var.intern(RTVar.ClojureNamespace, Symbol.intern("*print-meta*"), false).setDynamic()
    static member val PrintDupVar =     Var.intern(RTVar.ClojureNamespace, Symbol.intern("*print-dup*"), false).setDynamic()
    static member val FlushOnNewlineVar = Var.intern(RTVar.ClojureNamespace, Symbol.intern("*flush-on-newline*"), true).setDynamic()
    static member val PrintInitializedVar = Var.intern(RTVar.ClojureNamespace, Symbol.intern("*print-initialized*"), false).setDynamic()
    static member val PrOnVar = Var.intern(RTVar.ClojureNamespace, Symbol.intern("pr-on"))
    static member val AllowSymbolEscapeVar = Var.intern(RTVar.ClojureNamespace, Symbol.intern("*allow-symbol-escape*"), true).setDynamic()

    // Pre-defined Vars (miscellaneous)

    static member val ReaderResolverVar = Var.intern(RTVar.ClojureNamespace, Symbol.intern("*reader-resolver*"), null).setDynamic()
    static member val ReadEvalVar = Var.intern(RTVar.ClojureNamespace, Symbol.intern("*read-eval*"), RTVar._readEval).setDynamic()


    static member getCurrentNamespace() = (RTVar.CurrentNSVar :> IDeref).deref() :?> Namespace


    static member readTrueFalseUnknown(s: string) : obj  = 
        match s with
        | "true" -> true
        | "false" -> false
        | _ -> Keyword.intern(null, "unknown")

    static member val _readEval =
        let mutable v = Environment.GetEnvironmentVariable("CLOJURE_READ_EVAL")
        if isNull v then
            v <- Environment.GetEnvironmentVariable("clojure.read.eval")
        if isNull v then
            v <- "true"
        RTVar.readTrueFalseUnknown(v)

    // original comment: duck typing stderr plays nice with e.g. swank 
    static member errPrintWriter() : TextWriter =
        let w = (RTVar.ErrVar :> IDeref).deref()
        match w with
        | :? TextWriter as tw -> tw
        | :? Stream as s -> new StreamWriter(s)
        | _ -> failwith "Unknown type for *err*"


    static member var(nsString: string, nameString: string) : Var =
        let ns = Namespace.findOrCreate(Symbol.intern(null, nsString))
        let name = Symbol.intern(null, nameString)
        Var.intern(ns, name)

    static member var(nsString: string, nameString: string, init: obj) : Var =
        let ns = Namespace.findOrCreate(Symbol.intern(null, nsString))
        let name = Symbol.intern(null, nameString)
        Var.intern(ns, name, init)

    (*


    
    *)