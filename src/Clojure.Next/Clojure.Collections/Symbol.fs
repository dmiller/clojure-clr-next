namespace Clojure.Collections

open System
open Clojure.Numerics
open Clojure.Numerics.Hashing

[<Serializable>]
type Symbol private (meta: IPersistentMap, ns: string, name: string) =
    inherit AFn()

    let mutable hasheq: int option = None

    [<NonSerialized>]
    let mutable _str: string = null

    [<NonSerialized>]
    let mutable _strEsc: string = null

    member this.Namespace = ns
    member this.Name = name

    private new(ns, name) = Symbol(null, ns, name)

    // Object overrides

    override this.Equals(obj) =
        match obj with
        | _ when Object.ReferenceEquals(this, obj) -> true
        | :? Symbol as sym -> Util.equals(ns, sym.Namespace) && name.Equals(sym.Name)
        | _ -> false

    override this.GetHashCode() =
        match hasheq with
        | Some h -> h
        | None ->
            let hc = hashCombine(Murmur3.HashString(ns),Murmur3.HashString(name));
            hasheq <- Some hc
            hc
    
    interface IHashEq with
        member this.hasheq() = this.GetHashCode()

    override this.ToString() =
        if isNull _str then
            _str <- 
                match ns with
                | null -> name
                | _ -> ns + "/" + name
        
        _str






    // Intern a symbol with the given name  and namespace-name
    static member intern(ns: string, name: string) = Symbol(null, ns, name)

    // Intern a symbol with the given name (extracting the namespace if name is of the form ns/name)
    static member intern(nsname: string) =
        let i = nsname.IndexOf('/')
        if i = -1 || nsname.Equals("/") then
            Symbol(null, nsname)
        else
            Symbol(nsname.Substring(0, i), nsname.Substring(i + 1))


    interface IObj with
        override this.withMeta(m) =
            if Object.ReferenceEquals(meta,m) then this else Symbol(m, ns, name)


    interface IMeta with
        override _.meta() = meta

    interface Named with
        member _.getNamespace() = ns
        member _.getName() = name


    interface IFn with
        member this.invoke(arg1) = RT0.get(arg1, this)
        member this.invoke(arg1, arg2) = RT0.get(arg1, this, arg2)


        (*
        
     
        public override object invoke(Object obj)
        {
            return RT.get(obj, this);
        }


        public override object invoke(Object obj, Object notFound)
        {
            return RT.get(obj, this, notFound);
        }



        /// <summary>
        /// Construct a Symbol during deserialization.
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
        protected Symbol (SerializationInfo info, StreamingContext context)
        {
            _name = String.Intern(info.GetString("_name"));

            string nsStr = info.GetString("_ns");
            _ns = nsStr == null ? null : String.Intern(nsStr);
        }

        #endregion


        private static string NameMaybeEscaped(string s)
        {
            return LispReader.NameRequiresEscaping(s) ? LispReader.VbarEscape(s) : s;
        }

        public string ToStringEscaped()
        {
            if (_strEsc == null)
            {
                if (_ns != null)
                    _strEsc = NameMaybeEscaped(_ns) + "/" + NameMaybeEscaped(_name);
                else
                    _strEsc = NameMaybeEscaped(_name);

            }
            return _strEsc;
        }


        #endregion

        #region IFn members


        public override object invoke(Object obj)
        {
            return RT.get(obj, this);
        }


        public override object invoke(Object obj, Object notFound)
        {
            return RT.get(obj, this, notFound);
        }

        #endregion

        #region IComparable Members

        /// <summary>
        /// Compare this symbol to another object.
        /// </summary>
        /// <param name="obj">The object to compare to.</param>
        /// <returns>neg,zero,pos semantics.</returns>
        public int CompareTo(object obj)
        {
            if (!(obj is Symbol s))
                throw new ArgumentException("Must compare to non-null Symbol", "obj");

            if (Equals(s))
                return 0;
            if (_ns == null && s._ns != null)
                return -1;
            if (_ns != null)
            {
                if (s._ns == null)
                    return 1;
                int nsc = _ns.CompareTo(s._ns);
                if (nsc != 0)
                    return nsc;
            }
            return _name.CompareTo(s._name);
        }

        #endregion

        #region operator overloads

        public static bool operator ==(Symbol x, Symbol y)
        {
            if (ReferenceEquals(x, y))
                return true;

            if ((x is null) || (y is null))
                return false;

            return x.CompareTo(y) == 0;
        }

        public static bool operator !=(Symbol x, Symbol y)
        {
            if (ReferenceEquals(x,y))
                return false;

            if ((x is null) || (y is null))
                return true;

            return x.CompareTo(y) != 0;
        }

        public static bool operator <(Symbol x, Symbol y)
        {
            if (ReferenceEquals(x, y))
                return false; 
            
            if (x is null)
                throw new ArgumentNullException("x");

            return x.CompareTo(y) < 0;
        }

        public static bool operator >(Symbol x, Symbol y)
        {
            if (ReferenceEquals(x, y))
                return false;

            if (x is null)
                throw new ArgumentNullException("x");

            return x.CompareTo(y) > 0;
        }

        #endregion

        #region Other

        ///// <summary>
        ///// Create a copy of this symbol.
        ///// </summary>
        ///// <returns>A copy of this symbol.</returns>
        //private object readResolve()
        //{
        //    return intern(_ns, _name);
        //}

        #endregion

        #region ISerializable Members

        [System.Security.SecurityCritical]
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("_name",_name);
            info.AddValue("_ns", _ns);
        }

        #endregion

        #region IHashEq members

        public int hasheq()
        {
            if (_hasheq == 0)
            {
                _hasheq = Util.hashCombine(Murmur3.HashString(_name), Util.hasheq(_ns));
            }
            return _hasheq;
        }

        #endregion
    }
}
        
        *)