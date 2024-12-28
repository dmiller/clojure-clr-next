namespace Clojure.Collections

open System
open System.Collections
open Clojure.Numerics

[<AllowNullLiteral>]
type IArraySeq = 
    inherit IObj
    inherit ISeq
    inherit IList
    inherit IndexedSeq
    inherit IReduce
    abstract member toArray : unit -> obj[]
    abstract member array : unit -> obj
    abstract member index : unit -> int

[<AllowNullLiteral>]
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

[<AllowNullLiteral>]
type NumericArraySeq<'T when 'T : equality>(meta : IPersistentMap, array : 'T array, index: int) = 
    inherit TypedArraySeq<'T>(meta,array,index)

    interface IList with
        override this.IndexOf(value : obj) = 
            if Numbers.IsNumeric(value) then 
                base.indexOf(value) 
            else 
                -1

[<AllowNullLiteral>]
type ArraySeq_byte(_meta : IPersistentMap, _array : byte array, _index : int) = 
    inherit NumericArraySeq<byte>(_meta,_array,_index)
    override this.Convert (x : obj) = Converters.convertToByte(x)
    override this.NextOne() = new ArraySeq_byte(_meta, _array, _index + 1)
    override this.DuplicateWithMeta(newMeta) = if Object.ReferenceEquals(newMeta,_meta) then this else new ArraySeq_byte(newMeta, _array, _index)

[<AllowNullLiteral>]
type ArraySeq_sbyte(_meta : IPersistentMap, _array : sbyte array, _index : int) = 
    inherit NumericArraySeq<sbyte>(_meta,_array,_index)
    override this.Convert (x : obj) = Converters.convertToSByte(x)
    override this.NextOne() = new ArraySeq_sbyte(_meta, _array, _index + 1)
    override this.DuplicateWithMeta(newMeta) = if Object.ReferenceEquals(newMeta,_meta) then this else new ArraySeq_sbyte(newMeta, _array, _index)

[<AllowNullLiteral>]
type ArraySeq_short(_meta : IPersistentMap, _array : int16 array, _index : int) = 
    inherit NumericArraySeq<int16>(_meta,_array,_index)
    override this.Convert (x : obj) = Converters.convertToShort(x)
    override this.NextOne() = new ArraySeq_short(_meta, _array, _index + 1)
    override this.DuplicateWithMeta(newMeta) = if Object.ReferenceEquals(newMeta,_meta) then this else new ArraySeq_short(newMeta, _array, _index)

[<AllowNullLiteral>]
type ArraySeq_ushort(_meta : IPersistentMap, _array : uint16 array, _index : int) = 
    inherit NumericArraySeq<uint16>(_meta,_array,_index)
    override this.Convert (x : obj) = Converters.convertToUShort(x)
    override this.NextOne() = new ArraySeq_ushort(_meta, _array, _index + 1)
    override this.DuplicateWithMeta(newMeta) = if Object.ReferenceEquals(newMeta,_meta) then this else new ArraySeq_ushort(newMeta, _array, _index)

[<AllowNullLiteral>]
type ArraySeq_int(_meta : IPersistentMap, _array : int array, _index : int) =
    inherit NumericArraySeq<int>(_meta,_array,_index)
    override this.Convert (x : obj) = Converters.convertToInt(x)
    override this.NextOne() = new ArraySeq_int(_meta, _array, _index + 1)
    override this.DuplicateWithMeta(newMeta) = if Object.ReferenceEquals(newMeta,_meta) then this else new ArraySeq_int(newMeta, _array, _index)

[<AllowNullLiteral>]
type ArraySeq_uint(_meta : IPersistentMap, _array : uint32 array, _index : int) = 
    inherit NumericArraySeq<uint32>(_meta,_array,_index)
    override this.Convert (x : obj) = Converters.convertToUInt(x)
    override this.NextOne() = new ArraySeq_uint(_meta, _array, _index + 1)
    override this.DuplicateWithMeta(newMeta) = if Object.ReferenceEquals(newMeta,_meta) then this else new ArraySeq_uint(newMeta, _array, _index)

[<AllowNullLiteral>]
type ArraySeq_long(_meta : IPersistentMap, _array : int64 array, _index : int) = 
    inherit NumericArraySeq<int64>(_meta,_array,_index)
    override this.Convert (x : obj) = Converters.convertToLong(x)
    override this.NextOne() = new ArraySeq_long(_meta, _array, _index + 1)
    override this.DuplicateWithMeta(newMeta) = if Object.ReferenceEquals(newMeta,_meta) then this else new ArraySeq_long(newMeta, _array, _index)

[<AllowNullLiteral>]
type ArraySeq_ulong(_meta : IPersistentMap, _array : uint64 array, _index : int) = 
    inherit NumericArraySeq<uint64>(_meta,_array,_index)
    override this.Convert (x : obj) = Converters.convertToULong(x)
    override this.NextOne() = new ArraySeq_ulong(_meta, _array, _index + 1)
    override this.DuplicateWithMeta(newMeta) = if Object.ReferenceEquals(newMeta,_meta) then this else new ArraySeq_ulong(newMeta, _array, _index)

[<AllowNullLiteral>]
type ArraySeq_float(_meta : IPersistentMap, _array : float32 array, _index : int) = 
    inherit NumericArraySeq<float32>(_meta,_array,_index)
    override this.Convert (x : obj) = Converters.convertToFloat(x)
    override this.NextOne() = new ArraySeq_float(_meta, _array, _index + 1)
    override this.DuplicateWithMeta(newMeta) = if Object.ReferenceEquals(newMeta,_meta) then this else new ArraySeq_float(newMeta, _array, _index)

[<AllowNullLiteral>]
type ArraySeq_double(_meta : IPersistentMap, _array : float array, _index : int) = 
    inherit NumericArraySeq<float>(_meta,_array,_index)
    override this.Convert (x : obj) = Converters.convertToDouble(x)
    override this.NextOne() = new ArraySeq_double(_meta, _array, _index + 1)
    override this.DuplicateWithMeta(newMeta) = if Object.ReferenceEquals(newMeta,_meta) then this else new ArraySeq_double(newMeta, _array, _index)

[<AllowNullLiteral>]
type ArraySeq_char(_meta : IPersistentMap, _array : char array, _index : int) = 
    inherit NumericArraySeq<char>(_meta,_array,_index)
    override this.Convert (x : obj) = Converters.convertToChar(x)
    override this.NextOne() = new ArraySeq_char(_meta, _array, _index + 1)
    override this.DuplicateWithMeta(newMeta) = if Object.ReferenceEquals(newMeta,_meta) then this else new ArraySeq_char(newMeta, _array, _index)

[<AllowNullLiteral>]
type ArraySeq_bool(_meta : IPersistentMap, _array : bool array, _index : int) = 
    inherit NumericArraySeq<bool>(_meta,_array,_index)
    override this.Convert (x : obj) = RT0.booleanCast(x)
    override this.NextOne() = new ArraySeq_bool(_meta, _array, _index + 1)
    override this.DuplicateWithMeta(newMeta) = if Object.ReferenceEquals(newMeta,_meta) then this else new ArraySeq_bool(newMeta, _array, _index)

[<AllowNullLiteral>]
type ArraySeq_decimal(_meta : IPersistentMap, _array : decimal array, _index : int) = 
    inherit NumericArraySeq<decimal>(_meta,_array,_index)
    override this.Convert (x : obj) = Converters.convertToDecimal(x)
    override this.NextOne() = new ArraySeq_decimal(_meta, _array, _index + 1)
    override this.DuplicateWithMeta(newMeta) = if Object.ReferenceEquals(newMeta,_meta) then this else new ArraySeq_decimal(newMeta, _array, _index)

[<AllowNullLiteral>]
type ArraySeq_object(_meta : IPersistentMap, _array : obj array, _index : int) =
    inherit TypedArraySeq<obj>(_meta,_array,_index)
    override this.Convert (x : obj) = x
    override this.NextOne() = new ArraySeq_object(_meta, _array, _index + 1)
    override this.DuplicateWithMeta(newMeta) = if Object.ReferenceEquals(newMeta,_meta) then this else new ArraySeq_object(newMeta, _array, _index)


[<AbstractClass;Sealed>]
type ArraySeq() = 

    static member create() : IArraySeq = null
    static member create(array : obj array) : IArraySeq = 
        if isNull array || array.Length = 0 then null else new ArraySeq_object(null,array,0)
    static member create(array : obj array, firstIndex : int) : IArraySeq = 
        if isNull array || array.Length <= firstIndex then null else new ArraySeq_object(null,array,firstIndex)
    static member createFromObject(array : obj): IArraySeq  = 
        if isNull array || not (array :? Array) || (array :?> Array).Length = 0 then null
        else
            let elementType = array.GetType().GetElementType()
            match Type.GetTypeCode(elementType) with
            | TypeCode.Boolean -> new ArraySeq_bool(null, (array :?> bool[]), 0)
            | TypeCode.Byte -> new ArraySeq_byte(null, (array :?> byte[]), 0)
            | TypeCode.Char -> new ArraySeq_char(null, (array :?> char[]), 0)
            | TypeCode.Decimal -> new ArraySeq_decimal(null, (array :?> decimal[]), 0)
            | TypeCode.Double -> new ArraySeq_double(null, (array :?> double[]), 0)
            | TypeCode.Int16 -> new ArraySeq_short(null, (array :?> int16[]), 0)
            | TypeCode.Int32 -> new ArraySeq_int(null, (array :?> int[]), 0)
            | TypeCode.Int64 -> new ArraySeq_long(null, (array :?> int64[]), 0)
            | TypeCode.SByte -> new ArraySeq_sbyte(null, (array :?> sbyte[]), 0)
            | TypeCode.Single -> new ArraySeq_float(null, (array :?> float32[]), 0)
            | TypeCode.UInt16 -> new ArraySeq_ushort(null, (array :?> uint16[]), 0)
            | TypeCode.UInt32 -> new ArraySeq_uint(null, (array :?> uint32[]), 0)
            | TypeCode.UInt64 -> new ArraySeq_ulong(null, (array :?> uint64[]), 0)
            | _ -> 
                let elementTypes = [| elementType |]
                let arraySeqType = typeof<TypedArraySeq<_>>.MakeGenericType(elementTypes)
                let ctorParams = [| null; array; 0 |]                                      // Originally had PersistentArrayMap.EMPTY -- does this matter?
                Activator.CreateInstance(arraySeqType, ctorParams) :?> IArraySeq
