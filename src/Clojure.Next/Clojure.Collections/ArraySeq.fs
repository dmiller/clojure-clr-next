namespace Clojure.Collections

open System
open System.Collections
open Clojure.Numerics

type IArraySeq = 
    inherit IObj
    inherit ISeq
    inherit IList
    inherit IndexedSeq
    inherit IReduce
    abstract member toArray : unit -> obj[]
    abstract member array : unit -> obj
    abstract member index : unit -> int

type TypedArraySeq<'T when 'T : equality >(_meta : IPersistentMap, _array: 'T array, _index : int) = 
    inherit ASeq(_meta)

    abstract member Convert : obj -> 'T
    abstract member NextOne : unit -> ISeq
    abstract member DuplicateWithMeta : IPersistentMap -> IObj

    default _.Convert (x : obj) =  x :?> 'T
    default _.NextOne() = new TypedArraySeq<'T>(_meta, _array, _index + 1)
    default this.DuplicateWithMeta(newMeta) = if Object.ReferenceEquals(newMeta,_meta) then this else new TypedArraySeq<'T>(newMeta, _array, _index)

     // TODO: first/reduce do a Numbers.num(x) conversion  -- do we need that?  (comment in C#)

    interface IPersistentCollection with
        override this.cons(o) = (this:>ISeq).cons(o)
        override this.count() = _array.Length - _index

    interface ISeq with
        override _.first() = _array.[_index]
        override this.next() = if _index + 1 < _array.Length then this.NextOne() else null

    interface IObj with
        override this.withMeta(newMeta) = this.DuplicateWithMeta(newMeta)

    interface Counted with
        override this.count() = _array.Length - _index

    interface IndexedSeq with
        member _.index() = _index

    interface IReduce with
        override this.reduce(f) = 
            if isNull _array then
                null
            else 
                let rec loop (acc:obj) (i:int) =
                    if i >= _array.Length then
                        acc
                    else
                       match f.invoke(acc,_array[i]) with
                       | :? Reduced as red -> (red:>IDeref).deref()
                       | nextAcc -> loop nextAcc (i+1)
                loop _array[_index] (_index+1)

        override this.reduce(f, init) = 
            if isNull _array then
                null
            else 
                let rec loop (acc:obj) (i:int) =
                    if i >= _array.Length then
                        acc
                    else
                       match f.invoke(acc,_array[i]) with
                       | :? Reduced as red -> (red:>IDeref).deref()
                       | nextAcc -> loop nextAcc (i+1)
                loop (f.invoke(init,_array[_index])) (_index+1)

    interface IList with
        override this.IndexOf(value : obj) = this.indexOf(value)


    member this.indexOf(value : obj) = 
        let v = this.Convert(value)
        let rec loop (i:int) =
            if i >= _array.Length then -1
            elif v.Equals(_array.[i]) then i
            else loop (i + 1)
        loop _index


    interface IArraySeq with
        override this.toArray() = 
            let sz = _array.Length - _index
            let items = Array.zeroCreate sz
            Array.Copy(_array, _index, items, 0, sz)
            items
        override this.array() = _array
        override this.index() = _index

type NumericArraySeq<'T when 'T : equality>(meta : IPersistentMap, array : 'T array, index: int) = 
    inherit TypedArraySeq<'T>(meta,array,index)

    interface IList with
        override this.IndexOf(value : obj) = 
            if Numbers.IsNumeric(value) then 
                base.indexOf(value) 
            else 
                -1


//    [Serializable]
//    public class ArraySeq_byte : NumericArraySeq<byte>
//    {
//        public ArraySeq_byte(IPersistentMap meta, byte[] array, int index)
//            : base(meta,array,index)
//        {
//        }

//        protected override byte Convert(object x)
//        {
//            return Util.ConvertToByte(x);
//        }

//        protected override ISeq NextOne()
//        {
//            return new ArraySeq_byte(_meta, _array, _i + 1);
//        }

//        protected override IObj DuplicateWithMeta(IPersistentMap meta)
//        {
//            if (_meta == meta)
//                return this;

//            return new ArraySeq_byte(meta, _array, _i);
//        }


//    }

//    [Serializable]
//    public class ArraySeq_sbyte : NumericArraySeq<sbyte>
//    {

//        public ArraySeq_sbyte(IPersistentMap meta, sbyte[] array, int index)
//            : base(meta,array,index)
//        {
//        }

//        protected override sbyte Convert(object x)
//        {
//            return Util.ConvertToSByte(x);
//        }

//        protected override ISeq NextOne()
//        {
//            return new ArraySeq_sbyte(_meta, _array, _i + 1);
//        }

//        protected override IObj DuplicateWithMeta(IPersistentMap meta)
//        {
//            if (_meta == meta)
//                return this;

//            return new ArraySeq_sbyte(meta, _array, _i);
//        }
//    }

//    [Serializable]
//    public class ArraySeq_short : NumericArraySeq<short>
//    {
//        public ArraySeq_short(IPersistentMap meta, short[] array, int index)
//            : base(meta,array,index)
//        {
//        }

//        protected override short Convert(object x)
//        {
//            return Util.ConvertToShort(x);
//        }

//        protected override ISeq NextOne()
//        {
//            return new ArraySeq_short(_meta, _array, _i + 1);
//        }

//        protected override IObj DuplicateWithMeta(IPersistentMap meta)
//        {
//            if (_meta == meta)
//                return this;

//            return new ArraySeq_short(meta, _array, _i);
//        }
//    }

//    [Serializable]
//    public class ArraySeq_ushort : NumericArraySeq<ushort>
//    {
//        public ArraySeq_ushort(IPersistentMap meta, ushort[] array, int index)
//            : base(meta,array,index)
//        {
//        }

//        protected override ushort Convert(object x)
//        {
//            return Util.ConvertToUShort(x);
//        }

//        protected override ISeq NextOne()
//        {
//            return new ArraySeq_ushort(_meta, _array, _i + 1);
//        }

//        protected override IObj DuplicateWithMeta(IPersistentMap meta)
//        {
//            if (_meta == meta)
//                return this;

//            return new ArraySeq_ushort(meta, _array, _i);
//        }
//    }

//    [Serializable]
//    public class ArraySeq_int : NumericArraySeq<int>
//    {
//        public ArraySeq_int(IPersistentMap meta, int[] array, int index)
//            : base(meta,array,index)
//        {
//        }

//        protected override int Convert(object x)
//        {
//            return Util.ConvertToInt(x);
//        }

//        protected override ISeq NextOne()
//        {
//            return new ArraySeq_int(_meta, _array, _i + 1);
//        }

//        protected override IObj DuplicateWithMeta(IPersistentMap meta)
//        {
//            if (_meta == meta)
//                return this;

//            return new ArraySeq_int(meta, _array, _i);
//        }
//    }

//    [Serializable]
//    public class ArraySeq_uint : NumericArraySeq<uint>
//    {
//        public ArraySeq_uint(IPersistentMap meta, uint[] array, int index)
//            : base(meta,array,index)
//        {
//        }

//        protected override uint Convert(object x)
//        {
//            return Util.ConvertToUInt(x);
//        }

//        protected override ISeq NextOne()
//        {
//            return new ArraySeq_uint(_meta, _array, _i + 1);
//        }

//        protected override IObj DuplicateWithMeta(IPersistentMap meta)
//        {
//            if (_meta == meta)
//                return this;

//            return new ArraySeq_uint(meta, _array, _i);
//        }
//    }

//    [Serializable]
//    public class ArraySeq_long : NumericArraySeq<long>
//    {
//        public ArraySeq_long(IPersistentMap meta, long[] array, int index)
//            : base(meta,array,index)
//        {
//        }

//        protected override long Convert(object x)
//        {
//            return Util.ConvertToLong(x);
//        }

//        protected override ISeq NextOne()
//        {
//            return new ArraySeq_long(_meta, _array, _i + 1);
//        }

//        protected override IObj DuplicateWithMeta(IPersistentMap meta)
//        {
//            if (_meta == meta)
//                return this;

//            return new ArraySeq_long(meta, _array, _i);
//        }
//    }

//    [Serializable]
//    public class ArraySeq_ulong : NumericArraySeq<ulong>
//    {
//        public ArraySeq_ulong(IPersistentMap meta, ulong[] array, int index)
//            : base(meta,array,index)
//        {
//        }

//        protected override ulong Convert(object x)
//        {
//            return Util.ConvertToULong(x);
//        }

//        protected override ISeq NextOne()
//        {
//            return new ArraySeq_ulong(_meta, _array, _i + 1);
//        }

//        protected override IObj DuplicateWithMeta(IPersistentMap meta)
//        {
//            if (_meta == meta)
//                return this;

//            return new ArraySeq_ulong(meta, _array, _i);
//        }
//    }

//    [Serializable]
//    public class ArraySeq_float : NumericArraySeq<float>
//    {
//        public ArraySeq_float(IPersistentMap meta, float[] array, int index)
//            : base(meta,array,index)
//        {
//        }

//        protected override float Convert(object x)
//        {
//            return Util.ConvertToFloat(x);
//        }

//        protected override ISeq NextOne()
//        {
//            return new ArraySeq_float(_meta, _array, _i + 1);
//        }

//        protected override IObj DuplicateWithMeta(IPersistentMap meta)
//        {
//            if (_meta == meta)
//                return this;

//            return new ArraySeq_float(meta, _array, _i);
//        }
//    }

//    [Serializable]
//    public class ArraySeq_double : NumericArraySeq<double>
//    {
//        public ArraySeq_double(IPersistentMap meta, double[] array, int index)
//            : base(meta,array,index)
//        {
//        }

//        protected override double Convert(object x)
//        {
//            return Util.ConvertToDouble(x);
//        }

//        protected override ISeq NextOne()
//        {
//            return new ArraySeq_double(_meta, _array, _i + 1);
//        }

//        protected override IObj DuplicateWithMeta(IPersistentMap meta)
//        {
//            if (_meta == meta)
//                return this;

//            return new ArraySeq_double(meta, _array, _i);
//        }
//    }

//    [Serializable]
//    public class ArraySeq_char : NumericArraySeq<char>
//    {
//        public ArraySeq_char(IPersistentMap meta, char[] array, int index)
//            : base(meta,array,index)
//        {
//        }

//        protected override char Convert(object x)
//        {
//            return Util.ConvertToChar(x);
//        }

//        protected override ISeq NextOne()
//        {
//            return new ArraySeq_char(_meta, _array, _i + 1);
//        }

//        protected override IObj DuplicateWithMeta(IPersistentMap meta)
//        {
//            if (_meta == meta)
//                return this;

//            return new ArraySeq_char(meta, _array, _i);
//        }
//    }

//    [Serializable]
//    public class ArraySeq_bool : NumericArraySeq<bool>
//    {
//        public ArraySeq_bool(IPersistentMap meta, bool[] array, int index)
//            : base(meta,array,index)
//        {
//        }

//        protected override bool Convert(object x)
//        {
//            return RT.booleanCast(x);
//        }

//        protected override ISeq NextOne()
//        {
//            return new ArraySeq_bool(_meta, _array, _i + 1);
//        }

//        protected override IObj DuplicateWithMeta(IPersistentMap meta)
//        {
//            if (_meta == meta)
//                return this;

//            return new ArraySeq_bool(meta, _array, _i);
//        }
//    }

//    [Serializable]
//    public class ArraySeq_decimal : NumericArraySeq<decimal>
//    {
//        public ArraySeq_decimal(IPersistentMap meta, decimal[] array, int index)
//            : base(meta, array, index)
//        {
//        }

//        protected override decimal Convert(object x)
//        {
//            return Util.ConvertToDecimal(x);
//        }

//        protected override ISeq NextOne()
//        {
//            return new ArraySeq_decimal(_meta, _array, _i + 1);
//        }

//        protected override IObj DuplicateWithMeta(IPersistentMap meta)
//        {
//            if (_meta == meta)
//                return this;

//            return new ArraySeq_decimal(meta, _array, _i);
//        }
//    }

//    [Serializable]
//    public class ArraySeq_object : NumericArraySeq<object>
//    {
//        public ArraySeq_object(IPersistentMap meta, object[] array, int index)
//            : base(meta, array, index)
//        {
//        }

//        protected override object Convert(object x)
//        {
//            return x;
//        }

//        public override int IndexOf(object value)
//        {
//                for (int j = _i; j < _array.Length; j++)
//                    if (value.Equals(_array[j]))
//                        return j - _i;
//            return -1;
//        }

//        protected override ISeq NextOne()
//        {
//            return new ArraySeq_object(_meta, _array, _i + 1);
//        }

//        protected override IObj DuplicateWithMeta(IPersistentMap meta)
//        {
//            if (_meta == meta)
//                return this;

//            return new ArraySeq_object(meta, _array, _i);
//        }
//    }
//}


//[<AbstractClass;Sealed>]
//type ArraySeq() = 
//    static member create : unit -> IArraySeq
//    static member create : obj[] -> IArraySeq
//    static member create : obj[] * int -> IArraySeq
//    static member createFromObject : obj -> IArraySeq


//    public static class ArraySeq
//    {
//        #region C-tors and factory methods

//        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
//        static public IArraySeq create()
//        {
//            return null;
//        }

//        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
//        static public IArraySeq create(params object[] array)
//        {
//            return (array == null || array.Length == 0)
//                ? null
//                : new ArraySeq_object(null,array, 0);
//        }

//        // Not in the Java version, but I can really use this
//        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
//        static public IArraySeq create(object[] array, int firstIndex)
//        {
//            return (array == null || array.Length <= firstIndex )
//                ? null
//                : new ArraySeq_object(null, array, firstIndex);
//        }

//        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
//        internal static IArraySeq createFromObject(Object array)
//        {
//            if (!(array is Array aa) || aa.Length == 0)
//                return null;

//            Type elementType = array.GetType().GetElementType();
//            switch (Type.GetTypeCode(elementType))
//            {
//                case TypeCode.Boolean:
//                    return new ArraySeq_bool(null, (bool[])aa, 0);
//                case TypeCode.Byte:
//                    return new ArraySeq_byte(null, (byte[])aa, 0);
//                case TypeCode.Char:
//                    return new ArraySeq_char(null, (char[])aa, 0);
//                case TypeCode.Decimal:
//                    return new ArraySeq_decimal(null, (decimal[])aa, 0);
//                case TypeCode.Double:
//                    return new ArraySeq_double(null, (double[])aa, 0);
//                case TypeCode.Int16:
//                    return new ArraySeq_short(null, (short[])aa, 0);
//                case TypeCode.Int32:
//                    return new ArraySeq_int(null, (int[])aa, 0);
//                case TypeCode.Int64:
//                    return new ArraySeq_long(null, (long[])aa, 0);
//                case TypeCode.SByte:
//                    return new ArraySeq_sbyte(null, (sbyte[])aa, 0);
//                case TypeCode.Single:
//                    return new ArraySeq_float(null, (float[])aa, 0);
//                case TypeCode.UInt16:
//                    return new ArraySeq_ushort(null, (ushort[])aa, 0);
//                case TypeCode.UInt32:
//                    return new ArraySeq_uint(null, (uint[])aa, 0);
//                case TypeCode.UInt64:
//                    return new ArraySeq_ulong(null, (ulong[])aa, 0);
//                default:
//                    {
//                        Type[] elementTypes = { elementType };
//                        Type arraySeqType = typeof(TypedArraySeq<>).MakeGenericType(elementTypes);
//                        object[] ctorParams = { PersistentArrayMap.EMPTY, array, 0 };
//                        return (IArraySeq)Activator.CreateInstance(arraySeqType, ctorParams);
//                    }
//            }
//        }

//        #endregion
//    }

