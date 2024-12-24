namespace Clojure.Lib

open Clojure.Collections
open System.Reflection
open System
open System.Collections.Generic
open System.Linq
open System.Text
open System.Collections.Concurrent
open System.IO


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
        PersistentHashMap.create(createDefaultImportDirectory())

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

    static member All = namespaces.Values |> RT0.seq

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

    override _.ToString() = name.ToString()

    // This comes from the Java verion.
    // During loading of the clojure runtime source, it never gets past the t1 <> t2 test.
    // Not sure we need it, but I'm not getting rid of it.
    static member areDifferentInstancesOfSameClassName(t1 : Type, t2 : Type) =
        not <| Object.ReferenceEquals(t1,t2) && t1.FullName.Equals(t2.FullName)

    
    member this.referenceClass(sym : Symbol, value : Type) =
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
                v <- Var(this, sym)
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
                v <- Var(this, sym)
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


    
and [<Sealed;AllowNullLiteral>] Var(ns: Namespace, sym: Symbol, root: obj) = 
    inherit ARef(null)

    [<VolatileField>]
    let mutable dynamic = false

    new(ns, sym) = Var(ns, sym, null)  // TODO: Fix this when we get to the real code.
    member _.Namespace = ns
    member _.Name = sym

    member this.setDynamic() = dynamic <- true; this
    member this.setDynamic(b: bool) = dynamic <- b; this

    static member intern(ns: Namespace, sym: Symbol, root: obj) = new Var(ns, sym, root)  // TODO: Fix this when we get to the real code.

and [<Sealed;AbstractClass>] RTVar() = 

    static member ClojureNamespace = Namespace.findOrCreate(Symbol.intern "clojure.core")

    static member ErrVar = Var.intern(RTVar.ClojureNamespace, Symbol.intern("*err*"), new StreamWriter(Console.OpenStandardError())).setDynamic()

    // original comment: duck typing stderr plays nice with e.g. swank 
    static member errPrintWriter() : TextWriter =
        let w = (RTVar.ErrVar :> IDeref).deref()
        match w with
        | :? TextWriter as tw -> tw
        | :? Stream as s -> new StreamWriter(s)
        | _ -> failwith "Unknown type for *err*"



    (*



        /// <summary>
        /// Remove a symbol mapping from the namespace.
        /// </summary>
        /// <param name="sym">The symbol to remove.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
        public void unmap(Symbol sym)
        {
            if (sym.Namespace != null)
                throw new ArgumentException("Can't unintern a namespace-qualified symbol");

            IPersistentMap map = Mappings;
            while (map.containsKey(sym))
            {
                IPersistentMap newMap = map.without(sym);
                _mappings.CompareAndSet(map, newMap);
                map = Mappings;
            }
        }

        /// <summary>
        /// Map a symbol to a Type (import).
        /// </summary>
        /// <param name="sym">The symbol to associate with a Type.</param>
        /// <param name="t">The type to associate with the symbol.</param>
        /// <returns>The Type.</returns>
        /// <remarks>Named importClass instead of ImportType for core.clj compatibility.</remarks>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
        public Type importClass(Symbol sym, Type t)
        {
            return ReferenceClass(sym, t);
        }


        /// <summary>
        /// Map a symbol to a Type (import) using the type name for the symbol name.
        /// </summary>
        /// <param name="t">The type to associate with the symbol</param>
        /// <returns>The Type.</returns>
        /// <remarks>Named importClass instead of ImportType for core.clj compatibility.</remarks>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
        public Type importClass(Type t)
        {
            string n = Util.NameForType(t);   
            return importClass(Symbol.intern(n), t);
        }

        /// <summary>
        /// Add a <see cref="Symbol">Symbol</see> to <see cref="Var">Var</see> reference.
        /// </summary>
        /// <param name="sym"></param>
        /// <param name="var"></param>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
        public Var refer(Symbol sym, Var var)
        {
            return (Var)reference(sym, var);
        }

        #endregion

        #region Mappings

        /// <summary>
        /// Get the value mapped to a symbol.
        /// </summary>
        /// <param name="name">The symbol to look up.</param>
        /// <returns>The mapped value.</returns>
        public object GetMapping(Symbol name)
        {
            return Mappings.valAt(name);
        }

        /// <summary>
        /// Find the <see cref="Var">Var</see> mapped to a <see cref="Symbol">Symbol</see>.
        /// </summary>
        /// <param name="sym">The symbol to look up.</param>
        /// <returns>The mapped var.</returns>
        public Var FindInternedVar(Symbol sym)
        {
            return (Mappings.valAt(sym) is Var v && v.Namespace == this) ? v : null;
        }

        #endregion

        #region Aliases

        /// <summary>
        /// Find the <see cref="Namespace">Namespace</see> aliased by a <see cref="Symbol">Symbol</see>.
        /// </summary>
        /// <param name="alias">The symbol alias.</param>
        /// <returns>The aliased namespace</returns>
        public Namespace LookupAlias(Symbol alias)
        {
            return (Namespace)Aliases.valAt(alias);
        }

        /// <summary>
        /// Add an alias for a namespace.
        /// </summary>
        /// <param name="alias">The alias for the namespace.</param>
        /// <param name="ns">The namespace being aliased.</param>
        /// <remarks>Lowercase name for core.clj compatibility</remarks>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
        public void addAlias(Symbol alias, Namespace ns)
        {
            if (alias == null)
                throw new ArgumentNullException(nameof(alias),"Expecting Symbol + Namespace");
            if ( ns == null )
                throw new ArgumentNullException(nameof(ns), "Expecting Symbol + Namespace");

            IPersistentMap map = Aliases;

            // race condition
            while (!map.containsKey(alias))
            {
                IPersistentMap newMap = map.assoc(alias, ns);
                _aliases.CompareAndSet(map, newMap);
                map = Aliases;
            }
            // you can rebind an alias, but only to the initially-aliased namespace
            if (!map.valAt(alias).Equals(ns))
                throw new InvalidOperationException(String.Format("Alias {0} already exists in namespace {1}, aliasing {2}",
                    alias, _name, map.valAt(alias)));
        }

        /// <summary>
        /// Remove an alias.
        /// </summary>
        /// <param name="alias">The alias name</param>
        /// <remarks>Lowercase name for core.clj compatibility</remarks>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
        public void removeAlias(Symbol alias)
        {
            IPersistentMap map = Aliases;
            while (map.containsKey(alias))
            {
                IPersistentMap newMap = map.without(alias);
                _aliases.CompareAndSet(map, newMap);
                map = Aliases;
            }
        }


        #endregion

        #region core.clj compatibility

        /// <summary>
        /// Get the namespace name.
        /// </summary>
        /// <returns>The <see cref="Symbol">Symbol</see> naming the namespace.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
        public Symbol getName()
        {
            return Name;
        }

        /// <summary>
        /// Get the mappings of the namespace.
        /// </summary>
        /// <returns>The mappings.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
        public IPersistentMap getMappings()
        {
            return Mappings;
        }

        /// <summary>
        /// Get the aliases.
        /// </summary>
        /// <returns>A map of aliases.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
        public IPersistentMap getAliases()
        {
            return Aliases;
        }


        #endregion

        #region ISerializable Members

        [System.Security.SecurityCritical]
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.SetType(typeof(NamespaceSerializationHelper));
            info.AddValue("_name",_name);
        }

        [Serializable]
        class NamespaceSerializationHelper : IObjectReference
        {
#pragma warning disable 649
            readonly Symbol _name;
#pragma warning restore 649

            #region IObjectReference Members

            public object GetRealObject(StreamingContext context)
            {
               return Namespace.findOrCreate(_name);
            }

            #endregion
        }

        #endregion
    }
}
    
    
    *)