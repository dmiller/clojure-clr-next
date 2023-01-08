namespace rec Clojure.Collections

open System
open System.IO
open System.Collections
open System.Text.RegularExpressions
open System.Reflection
open System.Globalization



// We are going to take the RT static class in the original and split it into pieces.
// Some things are needed fairly early, just to get the collections up and running.
// Some are needed really as part of the Clojure environment at runtime.  Those things will be deferred.

//  Ditto Util.  Keeping the distinctions betweene the two for now so that we can track back to the originals.
// TODO:  merge into a single module?

// TODO:  Some of these functions need to be rewritten to use protocols for extensions.


module RT =


    // There is a massive circularity with RT.seq.
    // Original checks for ASeq, LazySeq and uses StringSeq, ArraySeq, chunkEnumeratorSeq
    // To pull all of those things in would require one massive recursive definition of half of the collection.
    // If instead, Seqable or ISeq was a protocol, we'd be done.
    // TODO: PROTOCOLS!!!
    let seq (x: obj) =
        match x with
        | null -> null
        | :? ISeq as s -> s.seq ()
        | :? Seqable as s -> s.seq ()
        //| :? string as s-> StringSeq.create(s)
        //| _ when x.GetType().IsArray -> ArraySeq.createFromObject(x)
        //| :? IEnumerable as e -> chunkEnumeratorSeq(e.GetEnumerator())
        | _ ->
            raise
            <| InvalidOperationException($"Don't know how to create ISeq from {x.GetType().FullName}")


    let first (x: obj) =
        let s =
            match x with
            | :? ISeq as s -> s
            | _ -> seq (x)

        s.first ()

    let next (x: obj) =
        let s =
            match x with
            | :? ISeq as s -> s
            | _ -> seq (x)

        if s = null then null else s.next ()

    let second (x: obj) = x |> next |> first
    let third (x: obj) = x |> next |> next |> first
    let fourth (x: obj) = x |> next |> next |> next |> first


    // Was RT.Length
    let seqLength (list: ISeq): int =
        let rec step (s: ISeq) cnt =
            if s = null then cnt else step (s.next ()) (cnt + 1)

        step list 0

    // TODO: Prime candidate for protocols
    let count (o: obj): int =
        match o with
        | null -> 0
        | :? Counted as c -> c.count ()
        | :? IPersistentCollection as c ->
            let rec step (s: ISeq) cnt =
                match s with
                | null -> cnt
                | :? Counted as c -> cnt + c.count ()
                | _ -> step (s.next ()) (cnt + 1)

            step (seq c) 0
        | :? String as s -> s.Length
        | :? ICollection as c -> c.Count
        | :? DictionaryEntry -> 2
        | :? Array as a -> a.GetLength(0)
        | _ when o.GetType().IsGenericType
                 && o.GetType().Name = "KeyValuePair`2" -> 2
        | _ ->
            raise
            <| InvalidOperationException
                ("count not supported on this type: "
                 + Util.nameForType (o.GetType()))

    // TODO:Prime candidate for protocols
    let nth (coll: obj, n: int): obj =
        if n < 0 then
            raise
            <| ArgumentOutOfRangeException("n", "index must be non-negative")

        match coll with
        | null -> null
        | :? Indexed as idx -> idx.nth (n)
        | :? string as s -> box s.[n]
        | _ when coll.GetType().IsArray ->
            // JVM has a call to Reflector.prepRet here, which is a no-op for us.
            // TODO: don't forget to look at this when we get to codegen
            // TODO: This was in my C# code -- verify that we can alwyas do this cast.
            (coll :?> Array).GetValue(n)
        | :? IList as il -> il.[n]
        //| :? JReMatcher as jrem -> jrem.group(n)  // TODO: uncomment when we have JReMatcher
        | :? Match as m -> upcast m.Groups.[n]
        | :? IMapEntry as me ->
            match n with
            | 0 -> me.key ()
            | 1 -> me.value ()
            | _ ->
                raise
                <| ArgumentOutOfRangeException("n", "index out of bounds for IMapEntry, must be 0,1")
        | :? DictionaryEntry as de ->
            match n with
            | 0 -> de.Key
            | 1 -> de.Value
            | _ ->
                raise
                <| ArgumentOutOfRangeException("n", "index out of bounds for DictionaryEntry, must be 0,1")
        | _ when coll.GetType().IsGenericType
                 && coll.GetType().Name.Equals("KeyValuePair`2") ->
            match n with
            | 0 ->
                coll
                    .GetType()
                    .InvokeMember("Key", BindingFlags.GetProperty, null, coll, null)
            | 1 ->
                coll
                    .GetType()
                    .InvokeMember("Value", BindingFlags.GetProperty, null, coll, null)
            | _ ->
                raise
                <| ArgumentOutOfRangeException("n", "index out of bounds for KeyValuePair, must be 0,1")
        | :? Sequential as sql ->
            let rec step (s: ISeq) (i: int) =
                if i = n then
                    s.first ()
                elif isNull s then
                    raise
                    <| ArgumentOutOfRangeException("n", "past end of collection")
                else
                    step (s.next ()) (i + 1)

            step (RT.seq (coll)) 0
        | _ ->
            raise
            <| InvalidOperationException
                ("nth not supported on type"
                 + Util.nameForType (coll.GetType()))




    // TODO: Prime candidate for protocols
    let get (coll: obj, key: obj): obj =

        let getByNth coll key =
            let n = Util.convertToInt (key)

            if n >= 0 && n < RT.count (coll) then RT.nth (coll, n) else null

        match coll with
        | null -> null
        | :? ILookup as il -> il.valAt (key)
        | :? IDictionary as d -> d.[key]
        | :? IPersistentSet as s -> s.get (key)
        | :? string as s when Util.isNumeric (key) -> getByNth s key
        | _ when coll.GetType().IsArray -> getByNth coll key
        | :? ITransientSet as tset -> tset.get (key)
        | _ -> null

    // TODO: Prime candidate for protocols
    // This was called get -- but we can't have overloads in modules!
    let get3 (coll: obj, key: obj, notFound: obj): obj =

        let getByNth coll key =
            let n = Util.convertToInt (key)

            if n >= 0 && n < RT.count (coll) then RT.nth (coll, n) else notFound

        match coll with
        | null -> null
        | :? ILookup as il -> il.valAt (key, notFound)
        | :? IDictionary as d -> if d.Contains(key) then d.[key] else notFound
        | :? IPersistentSet as s -> if s.contains (key) then s.get (key) else notFound
        | :? string as s when Util.isNumeric (key) -> getByNth s key
        | _ when coll.GetType().IsArray -> getByNth coll key
        | :? ITransientSet as tset -> if tset.contains (key) then tset.get (key) else notFound
        | _ -> notFound

    // need to move defn of Reduced to before Helpers (currently in Sequences)

    let isReduced (x: obj): bool =
        match x with
        | :? Reduced -> true
        | _ -> false



    // Note that our default printer cannot call ToString on the collections -- those methods will be calling this.  Circularity city.
    // However, it can call ToString on items in a collection.


    // TODO: figure out how to properly incorporate 'readably' into this interface.
    // Probably needs to happen with the functions setthe functions above.

    let rec baseMetaPrinter (x: obj, w: TextWriter): unit =
        match x with
        | :? IMeta as xo -> // original code has Obj here, but not sure why this is correct.  We only need an IMeta to have metadata.
            let meta = xo.meta () // the real version will check for a meta with count=1 and just a tag key and special case that.
            w.Write("#^")
            print (meta, w)
            w.Write(' ')
        | _ -> ()

    and basePrinter (readably: bool, x: obj, w: TextWriter): unit =

        let printInnerSeq readably (s: ISeq) (w: TextWriter) =
            let rec step (s: ISeq) =
                if s <> null then basePrinter (readably, s, w)

                if s.next () <> null then w.Write(' ')
                s.next () |> step

            step s

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

        RTEnv.metaPrinterFn (x, w)

        match x with
        | null -> w.Write("nil")
        | :? ISeq
        | :? IPersistentList ->
            w.Write('(')
            printInnerSeq readably (seq (x)) w
            w.Write(')')
        | :? String as s -> baseStringPrinter readably s w
        | :? IPersistentMap ->
            let rec step (s: ISeq) =
                let e: IMapEntry = downcast s.first ()
                basePrinter (readably, e.key (), w)
                w.Write(' ')
                basePrinter (readably, e.value (), w)
                if s.next () <> null then w.Write(", ")
                step (s.next ())

            w.Write('{')
            seq (x) |> step
            w.Write('}')
        | :? IPersistentVector as v ->
            let n = v.count ()
            w.Write('[')
            for i = 0 to n - 1 do
                basePrinter (readably, v.nth (i), w)
                if i < n - 1 then w.Write(" ")

            w.Write(']')
        | :? IPersistentSet ->
            let rec step (s: ISeq) =
                basePrinter (readably, s.first (), w)

                if not (isNull (s.next ())) then w.Write(" ")

                step (s.next ())

            w.Write("#{")
            seq (x) |> step
            w.Write('}')
        | :? Char as ch -> baseCharPrinter readably ch w
        | _ -> w.Write(x.ToString())

    // TODO:  Figure out how best to integrate the rest of these

    //    else if (x is Type type)
    //    {
    //        string tName = type.AssemblyQualifiedName;
    //        if (LispReader.NameRequiresEscaping(tName))
    //            tName = LispReader.VbarEscape(tName);
    //        w.Write("#=");
    //        w.Write(tName);
    //    }
    //    else if (x is BigDecimal && readably)
    //    {
    //        w.Write(x.ToString());
    //        w.Write("M");
    //    }
    //    else if (x is BigInt && readably)
    //    {
    //        w.Write(x.ToString());
    //        w.Write("N");
    //    }
    //    else if (x is BigInteger && readably)
    //    {
    //        w.Write(x.ToString());
    //        w.Write("BIGINT");
    //    }
    //    else if (x is Var)
    //    {
    //        Var v = x as Var;
    //        w.Write("#=(var {0}/{1})", v.Namespace.Name, v.Symbol);
    //    }
    //    else if (x is Regex r)
    //    {
    //        w.Write("#\"{0}\"", r.ToString());
    //    }
    //    //else
    //    //    w.Write(x.ToString());
    //    // The clause above is what Java has, and would have been nice.
    //    // Doesn't work for me, for one reason:
    //    // When generating initializations for static variables in the classes representing IFns,
    //    //    let's say the value is the double 7.0.
    //    //    we generate code that says   (double)RT.readFromString("7")
    //    //    so we get a boxed int, which CLR won't cast to double.  Sigh.
    //    //    So I need double/float to print a trailing .0 even when integer-valued.
    //    else if (x is double || x is float)
    //    {
    //        string s = x.ToString();
    //        if (!s.Contains('.') && !s.Contains('E'))
    //            s += ".0";
    //        w.Write(s);
    //    }
    //    else
    //        w.Write(x.ToString());
    //}


    and print (x: obj, w: TextWriter): unit = RTEnv.printFn (x, w)

    let printString (x: obj) =
        use sw = new StringWriter()
        print (x, sw)
        sw.ToString()


module Util =

    let hash x =
        match x with
        | null -> 0
        | _ -> x.GetHashCode()

    //a la boost
    let hashCombine (seed: int, hash: int) =
        seed
        ^^^ (hash + 0x9e3779b9 + (seed <<< 6) + (seed >>> 2))

    let equals (x, y) =
        Object.ReferenceEquals(x, y)
        || x <> null && x.Equals(y)

    let private isNullableType (t: Type) =
        t.IsGenericType
        && t.GetGenericTypeDefinition() = typedefof<Nullable<_>>

    let private getNonNullableType (t: Type) =
        if isNullableType t then t.GetGenericArguments().[0] else t









    let private isNumericType (t: Type) =
        let t = getNonNullableType (t)

        if t.IsEnum then
            false
        else
            match Type.GetTypeCode(t) with
            | TypeCode.SByte
            | TypeCode.Byte
            | TypeCode.Int16
            | TypeCode.UInt16
            | TypeCode.Int32
            | TypeCode.UInt32
            | TypeCode.Int64
            | TypeCode.UInt64
            | TypeCode.Single
            | TypeCode.Double -> true
            | _ when RTEnv.isExtraNumericType t -> true
            | _ -> false

    let isNumeric (o: obj) = o <> null && isNumericType (o.GetType())

    let numericEquals (x: obj, y: obj) = RTEnv.numericEquals (x, y)

    let baseNumericEqualityFn (x, y) = x.Equals(y)

    let pcequiv (k1: obj, k2: obj) =
        match k1, k2 with
        | :? IPersistentCollection as pc1, _ -> pc1.equiv (k2)
        | _, (:? IPersistentCollection as pc2) -> pc2.equiv (k1)
        | _ -> k1.Equals(k2)

    let equiv (k1: obj, k2: obj) =
        if Object.ReferenceEquals(k1, k2) then true
        elif isNull k1 then false
        else if isNumeric k1 && isNumeric k2 then numericEquals (k1, k2)
        else pcequiv (k1, k2)

    // TODO: Benchmark this against alternative implementations: just use Convert, or match on TypeCode.
    let convertToInt (o: obj): int =
        match o with
        | :? Byte as x -> (int x)
        | :? Char as x -> (int x)
        | :? Decimal as x -> (int x)
        | :? Double as x -> (int x)
        | :? Int16 as x -> (int x)
        | :? Int32 as x -> (int x)
        | :? Int64 as x -> (int x)
        | :? SByte as x -> (int x)
        | :? Single as x -> (int x)
        | :? UInt16 as x -> (int x)
        | :? UInt32 as x -> (int x)
        | :? UInt64 as x -> (int x)
        | _ -> Convert.ToInt32(o, CultureInfo.InvariantCulture)




    //public static int hasheq(object x)
//{
//    Type xc = x.GetType();

    //    if (xc == typeof(long))
//    {
//        long lpart = Util.ConvertToLong(x);
//        //return (int)(lpart ^ (lpart >> 32));
//        return Murmur3.HashLong(lpart);
//    }
//    if (xc == typeof(double))
//    {
//        if (x.Equals(-0.0))
//            return 0;  // match 0.0
//        return x.GetHashCode();
//    }

    //    return hasheqFrom(x, xc);
//}
    // Another function to be set up in the Clojure environment -- TODO

    // This will give us an initial value.
    // Not handled: BigDecimal, BigInteger, BigRational, BigInt

    let baseHashNumber (o: obj): int =
        match o with
        | :? uint64 as n -> Murmur3.HashLongU n |> int
        | :? uint32 as n -> Murmur3.HashLongU(uint64 n) |> int
        | :? uint16 as n -> Murmur3.HashLongU(uint64 n) |> int
        | :? byte as n -> Murmur3.HashLongU(uint64 n) |> int
        | :? int64 as n -> Murmur3.HashLong n |> int
        | :? int32 as n -> Murmur3.HashLong(int64 n) |> int
        | :? int16 as n -> Murmur3.HashLong(int64 n) |> int
        | :? sbyte as n -> Murmur3.HashLong(int64 n) |> int
        | :? float as n when n = -0.0 -> (0.0).GetHashCode() // make neg zero match pos zero
        | :? float as n -> n.GetHashCode()
        | :? float32 as n when n = -0.0f -> (0.0f).GetHashCode() // make neg zero match pos zero
        | :? float32 as n -> n.GetHashCode()
        | _ -> o.GetHashCode()

    let hashNumber (o: obj): int = RTEnv.hashNumberFn o



    let hasheq (o: obj): int =
        match o with
        | null -> 0
        | :? IHashEq as he -> he.hasheq ()
        | :? String as s -> Murmur3.HashInt(s.GetHashCode())
        | _ when isNumeric o -> hashNumber o
        | _ -> o.GetHashCode()



    // These functions originally were in my Murmur3 package.
    // Moved them here because:
    //   (1) they would have made mutual references between Util and Murmur3
    //   (2) they would have made Murmur3 dependent on Clojure interfaces.
    //       Might want to move Murmur3 to be independent, so leaving them there would have prevented that.


    let hashOrderedU (xs: IEnumerable): uint =
        let mutable n = 0
        let mutable hash = 1u

        for x in xs do
            hash <- 31u * hash + (hasheq x |> uint)
            n <- n + 1

        Murmur3.finalizeCollHash hash n

    let hashUnorderedU (xs: IEnumerable): uint =
        let mutable n = 0
        let mutable hash = 0u

        for x in xs do
            hash <- hash + (hasheq x |> uint)
            n <- n + 1

        Murmur3.finalizeCollHash hash n

    let hashOrdered (xs: IEnumerable): int = hashOrderedU xs |> int
    let hashUnordered (xs: IEnumerable): int = hashUnorderedU xs |> int

    let nameForType (t: Type) =
        //| null -> "<null>"  // prior version printed a message
        if t.IsNested then
            let fullName = t.FullName
            let index = fullName.LastIndexOf('.')
            fullName.Substring(index + 1)
        else
            t.Name


    let mask (hash, shift) = (hash >>> shift) &&& 0x01f

    let bitCount (x) =
        let x = x - ((x >>> 1) &&& 0x55555555)

        let x =
            (((x >>> 2) &&& 0x33333333) + (x &&& 0x33333333))

        let x = (((x >>> 4) + x) &&& 0x0f0f0f0f)
        (x * 0x01010101) >>> 24

    // A variant of the above that avoids multiplying
    // This algo is in a lot of places.
    // See, for example, http://aggregate.org/MAGIC/#Population%20Count%20(Ones%20Count)
    let bitCountU (x: uint) =
        let x = x - ((x >>> 1) &&& 0x55555555u)

        let x =
            (((x >>> 2) &&& 0x33333333u) + (x &&& 0x33333333u))

        let x = (((x >>> 4) + x) &&& 0x0f0f0f0fu)
        let x = x + (x >>> 8)
        let x = x + (x >>> 16)
        x &&& 0x0000003fu


////open System
////open System.Collections


////type Reduced(v:obj) =
////    let value = v








//////       /// <summary>
//////       /// Provides a function to create a list from a sequence of arguments. (Internal use only.)
//////       /// </summary>
//////       /// <remarks>Internal use only.  Used to interface with core.clj.</remarks>
//////       public sealed class PLCreator : RestFn
//////       {
//////           public override int getRequiredArity()
//////           {
//////               return 0;
//////           }

//////           /// <summary>
//////           /// The creator method.
//////           /// </summary>
//////           /// <param name="args">A sequence of elements.</param>
//////           /// <returns>A new list.</returns>
//////           protected override object doInvoke(object args)
//////           {
//////               if (args is IArraySeq ias)
//////               {
//////                   object[] argsarray = (object[])ias.ToArray();
//////                   IPersistentList ret = EMPTY;
//////                   for (int i = argsarray.Length - 1; i >= ias.index(); i--)
//////                       ret = (IPersistentList)ret.cons(argsarray[i]);
//////                   return ret;
//////               }

//////               List<object> list = new List<object>();
//////               for (ISeq s = RT.seq(args); s != null; s = s.next())
//////                   list.Add(s.first());
//////               return create(list);
//////           }

//////           [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
//////           static public object invokeStatic(ISeq args)
//////           {
//////               if (args is IArraySeq ias)
//////               {
//////                   object[] argsarray = (object[])ias.ToArray();
//////                   IPersistentList ret = EMPTY;
//////                   for (int i = argsarray.Length - 1; i >= 0; i--)
//////                       ret = (IPersistentList)ret.cons(argsarray[i]);
//////                   return ret;
//////               }

//////               List<object> list = new List<object>();
//////               for (ISeq s = RT.seq(args); s != null; s = s.next())
//////                   list.Add(s.first());
//////               return create(list);
//////           }
//////       }

//////       static readonly IFn _creator = new PLCreator();
//////       /// <summary>
//////       /// An <see cref="IFn">IFn</see> to create a list from a sequence of items.
//////       /// </summary>
//////       /// <remarks>The name is without our usual leading underscore for compatiblity with core.clj.</remarks>
//////       [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
//////       public static IFn creator { get { return _creator; } }



//////   }
//////}








////module Helpers =





////    let vectorToString (x:obj) : string = "HELP! WRITE ME!!!  TODO HELL!!!!"

////    //else if (x is IPersistentVector)
////    // {
////    //     IPersistentVector v = x as IPersistentVector;
////    //     int n = v.count();
////    //     w.Write('[');
////    //     for (int i = 0; i < n; i++)
////    //     {
////    //         print(v.nth(i), w);
////    //         if (i < n - 1)
////    //             w.Write(" ");
////    //     }
////    //     w.Write(']');
////    // }


////    let cons(x:obj,coll:obj) : ISeq =
////        match coll with
////        | null -> PersistentList(x)
////        | :? ISeq as s -> Cons(s,x)
////        | _ -> Cons(x,seq(coll))


////        //public static ISeq cons(object x, object coll)
////        //{
////        //    if (coll == null)
////        //        return new PersistentList(x);


////        //    if (coll is ISeq s)
////        //        return new Cons(x, s);

////        //    return new Cons(x, seq(coll));
////        //}


module RTEnvInitialization =


    let initialize (): unit =
        RTEnv.setPrintFn (fun (x, w) -> RT.basePrinter (true, x, w))
        RTEnv.setMetaPrintFn RT.baseMetaPrinter
        RTEnv.setNumericEqualityFn Util.baseNumericEqualityFn
        RTEnv.isInitialized <- true

    do initialize ()
