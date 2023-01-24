namespace Clojure.Numerics

open System
open System.Numerics
open Clojure.BigArith

type BigInt(lpart: int64, bipart: BigInteger option) =

    member this.Lpart = lpart
    member this.Bipart = bipart

    static member ZERO = BigInt(0, None)
    static member ONE = BigInt(1, None)

    static member private BigIntegerAsInt64(bi: BigInteger) : int64 option =
        if bi.CompareTo(Int64.MaxValue) <= 0 && bi.CompareTo(Int64.MinValue) >= 0 then
            int64 (bi) |> Some
        else
            None

    static member private BigIntegerAsUInt64(bi: BigInteger) : uint64 option =
        if bi.CompareTo(UInt64.MaxValue) <= 0 && bi.CompareTo(Int64.MinValue) >= 0 then
            uint64 (bi) |> Some
        else
            None


    static member fromBigInteger(bi: BigInteger) =
        match BigInt.BigIntegerAsInt64(bi) with
        | Some n -> BigInt(n, None)
        | None -> BigInt(0, Some bi)


    static member fromLong(n: int64) = BigInt(n, None)
    static member valueOf(n: int64) = BigInt(n, None)

    override this.Equals(o) : bool =
        if Object.ReferenceEquals(o,this) then
            true
        else
            match o with
            | :? BigInt as obi ->
                match bipart with
                | Some bi -> obi.Bipart.IsSome && obi.Bipart.Value = bi
                | None -> obi.Bipart.IsNone && obi.Lpart = lpart
            | _ -> false

    override this.GetHashCode() =
        match bipart with
        | Some bi -> bipart.GetHashCode()
        | None -> lpart.GetHashCode()

    override this.ToString() =
        match bipart with
        | Some bi -> bipart.ToString()
        | None -> lpart.ToString()


    member this.AsInt32(ret: outref<int>) : bool =
        ret <- 0

        match bipart with
        | Some _ -> false
        | None ->
            if lpart < Int32.MinValue || lpart > Int32.MaxValue then
                false
            else
                ret <- int (lpart)
                true

    member this.AsInt64(ret: outref<int64>) : bool =
        ret <- 0

        match bipart with
        | Some _ -> false
        | None ->
            ret <- int64 (lpart)
            true

    member this.AsUInt32(ret: outref<uint>) : bool =
        ret <- 0u

        match bipart with
        | Some _ -> false
        | None ->
            if lpart < int64 (UInt32.MinValue) || lpart > int64 (UInt32.MaxValue) then
                false
            else
                ret <- uint (lpart)
                true

    member this.AsUInt64(ret: outref<uint64>) : bool =
        ret <- 0UL

        match bipart with
        | Some bi ->
            match BigInt.BigIntegerAsUInt64(bi) with
            | Some n ->
                ret <- n
                true
            | None -> false
        | None ->
            if lpart < 0 then
                false
            else
                ret <- uint64 (lpart)
                true

    member this.AsDecimal(ret: outref<decimal>) : bool =
        ret <- (this :> IConvertible).ToDecimal(null)
        true

    member this.ToBigInteger() =
        match bipart with
        | Some bi -> bi
        | None -> BigInteger(lpart)


    member this.IntValue() =
        match bipart with
        | Some bi -> int (bi)
        | None -> int (lpart)

    member this.LongValue() =
        match bipart with
        | Some bi -> int64 (bi)
        | None -> int64 (lpart)

    member this.FloatValue() =
        match bipart with
        | Some bi -> float32 (bi)
        | None -> float32 (lpart)

    member this.DoubleValue() =
        match bipart with
        | Some bi -> float (bi)
        | None -> float (lpart)

    member this.ByteValue() =
        match bipart with
        | Some bi -> byte (bi)
        | None -> byte (lpart)

    member this.ShortValue() =
        match bipart with
        | Some bi -> int16 (bi)
        | None -> int16 (lpart)

    // Do we really need the implicit operators?

    static member op_implicit(v: byte) : BigInt = BigInt.fromLong (int64 (v))
    static member op_implicit(v: sbyte) : BigInt = BigInt.fromLong (int64 (v))
    static member op_implicit(v: int16) : BigInt = BigInt.fromLong (int64 (v))
    static member op_implicit(v: uint16) : BigInt = BigInt.fromLong (int64 (v))
    static member op_implicit(v: int32) : BigInt = BigInt.fromLong (int64 (v))
    static member op_implicit(v: uint32) : BigInt = BigInt.fromLong (int64 (v))
    static member op_implicit(v: int64) : BigInt = BigInt.fromLong (v)

    static member op_implicit(v: uint64) : BigInt =
        if v > uint64 (Int64.MaxValue) then
            BigInt.fromBigInteger (BigInteger(v))
        else
            BigInt.fromLong (int64 (v))

    static member op_implicit(v: decimal) : BigInt = BigInt.fromBigInteger (BigInteger(v))
    static member op_implicit(v: double) : BigInt = BigInt.fromBigInteger (BigInteger(v))

    // The original had explicit conversions
    // I do not know how to do multiple op_explicit definitions in F#.
    // They are overloaded only on return type.


    interface IConvertible with

        member this.GetTypeCode() = TypeCode.Object

        member this.ToBoolean(provider: IFormatProvider) : bool =
            match bipart with
            | Some bi -> true
            | None -> lpart <> 0

        member this.ToByte(provider: IFormatProvider) : byte =
            match bipart with
            | Some bi -> byte (bi)
            | None -> byte (lpart)

        member this.ToChar(provider: IFormatProvider) : char =
            match bipart with
            | Some bi ->
                raise (System.InvalidOperationException("Cannot convert BigInteger value (from BigInt) to char"))
            | None -> char (lpart)

        member this.ToDateTime(provider: IFormatProvider) : DateTime =
            raise (System.NotImplementedException())

        member this.ToDecimal(provider: IFormatProvider) : decimal =
            match bipart with
            | Some bi -> decimal (bi)
            | None -> decimal (lpart)

        member this.ToDouble(provider: IFormatProvider) : double =
            match bipart with
            | Some bi -> double (bi)
            | None -> double (lpart)

        member this.ToInt16(provider: IFormatProvider) : int16 =
            match bipart with
            | Some bi -> int16 (bi)
            | None -> int16 (lpart)

        member this.ToInt32(provider: IFormatProvider) : int =
            match bipart with
            | Some bi -> int (bi)
            | None -> int (lpart)

        member this.ToInt64(provider: IFormatProvider) : int64 =
            match bipart with
            | Some bi -> int64 (bi)
            | None -> int64 (lpart)

        member this.ToSByte(provider: IFormatProvider) : sbyte =
            match bipart with
            | Some bi -> sbyte (bi)
            | None -> sbyte (lpart)

        member this.ToSingle(provider: IFormatProvider) : float32 =
            match bipart with
            | Some bi -> float32 (bi)
            | None -> float32 (lpart)

        member this.ToString(provider: IFormatProvider) : string = this.ToString()

        member this.ToType(conversionType: Type, provider: IFormatProvider) : obj =
            if conversionType = typeof<BigInt> then
                this
            elif conversionType = typeof<BigInteger> then
                this.ToBigInteger()
            else
                raise (InvalidCastException())

        member this.ToUInt16(provider: IFormatProvider) : uint16 =
            match bipart with
            | Some bi -> uint16 (bi)
            | None -> uint16 (lpart)

        member this.ToUInt32(provider: IFormatProvider) : uint32 =
            match bipart with
            | Some bi -> uint32 (bi)
            | None -> uint32 (lpart)

        member this.ToUInt64(provider: IFormatProvider) : uint64 =
            match bipart with
            | Some bi -> uint64 (bi)
            | None -> uint64 (lpart)


    member this.ToBigDecimal() =
        match bipart with
        | Some bi -> BigDecimal.Create(bi)
        | None -> BigDecimal.Create(lpart)


    member this.add(y: BigInt) : BigInt =
        let addBig (x: BigInt, y: BigInt) =
            BigInt.fromBigInteger (x.ToBigInteger() + y.ToBigInteger())

        if this.Bipart.IsNone && y.Bipart.IsNone then
            let ret = this.Lpart + y.Lpart

            if ret ^^^ lpart >= 0 || ret ^^^ y.Lpart >= 0 then
                BigInt.valueOf (ret)
            else
                addBig (this, y)
        else
            addBig (this, y)

    member this.multiply(y: BigInt) : BigInt =
        let multBig (x: BigInt, y: BigInt) =
            BigInt.fromBigInteger (x.ToBigInteger() * y.ToBigInteger())

        if this.Bipart.IsNone && y.Bipart.IsNone then
            let ret = this.Lpart * y.Lpart

            if
                ret ^^^ y.Lpart = 0
                || ((this.Lpart <> Int64.MinValue)
                    && (Microsoft.FSharp.Core.Operators.(/) ret y.Lpart) = this.Lpart)
            then
                BigInt.valueOf (ret)
            else
                multBig (this, y)
        else
            multBig (this, y)

    member this.quotient(y: BigInt) : BigInt =
        if this.Bipart.IsNone && y.Bipart.IsNone then
            if lpart = Int64.MinValue && y.Lpart = -1 then
                BigInt.fromBigInteger (- this.ToBigInteger())
            else
                BigInt.valueOf (this.Lpart / y.Lpart)
        else
            BigInt.fromBigInteger (this.ToBigInteger() / y.ToBigInteger())

    member this.remainder(y: BigInt) : BigInt =
        if this.Bipart.IsNone && y.Bipart.IsNone then
            BigInt.valueOf (this.Lpart % y.Lpart)
        else
            BigInt.fromBigInteger (this.ToBigInteger() % y.ToBigInteger())

    member this.lt(y: BigInt) : bool =
        if this.Bipart.IsNone && y.Bipart.IsNone then
            this.Lpart < y.Lpart
        else
            this.ToBigInteger().CompareTo(y.ToBigInteger()) < 0

    // This should be interface IHashEq implementation, but we don't have the yet.

    member this.hasheq() : int =
        match bipart with
        | Some bi -> bi.GetHashCode()
        | None -> Murmur3.HashLong(lpart)
