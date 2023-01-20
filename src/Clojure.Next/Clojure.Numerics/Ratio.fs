namespace System.Numerics

open System
open Clojure.BigArith

[<Sealed>]
type Ratio(numerator: BigInteger, denominator: BigInteger) =

    member _.Numerator = numerator
    member _.Denominator = denominator

    override this.Equals(o) =
        match o with
        | :? Ratio as r -> r.Numerator.Equals(numerator) && r.Denominator.Equals(denominator)
        | _ -> false

    override _.GetHashCode() =
        numerator.GetHashCode() ^^^ denominator.GetHashCode()

    override _.ToString() =
        numerator.ToString() + "/" + denominator.ToString()

    interface IComparable with
        member this.CompareTo(obj: obj) : int =
            raise (System.NotImplementedException())


    //        #region IComparable Members

    //        public int CompareTo(object obj)
    //        {
    //            return Numbers.compare(this, obj);
    //        }

    //        #endregion


    interface IConvertible with
        member this.GetTypeCode() : TypeCode = TypeCode.Object
        member this.ToBoolean(provider: IFormatProvider) : bool = not (numerator.Equals(BigInteger.Zero))

        member this.ToByte(provider: IFormatProvider) : byte =
            Convert.ToByte((this :> IConvertible).ToDouble(provider))

        member this.ToChar(provider: IFormatProvider) : char =
            Convert.ToChar((this :> IConvertible).ToDouble(provider))

        member this.ToDateTime(provider: IFormatProvider) : DateTime =
            Convert.ToDateTime((this :> IConvertible).ToDouble(provider))

        member this.ToDecimal(provider: IFormatProvider) : decimal =
            (this.ToBigDecimal(Context.Decimal128) :> IConvertible).ToDecimal(provider)

        member this.ToDouble(provider: IFormatProvider) : float =
            (this.ToBigDecimal(Context.Decimal128) :> IConvertible).ToDouble(provider)

        member this.ToInt16(provider: IFormatProvider) : int16 =
            Convert.ToInt16((this :> IConvertible).ToDouble(provider))

        member this.ToInt32(provider: IFormatProvider) : int =
            Convert.ToInt32((this :> IConvertible).ToDouble(provider))

        member this.ToInt64(provider: IFormatProvider) : int64 =
            Convert.ToInt64((this :> IConvertible).ToDouble(provider))

        member this.ToSByte(provider: IFormatProvider) : sbyte =
            Convert.ToSByte((this :> IConvertible).ToDouble(provider))

        member this.ToSingle(provider: IFormatProvider) : float32 =
            Convert.ToSingle((this :> IConvertible).ToDouble(provider))

        member this.ToString(provider: IFormatProvider) : string = this.ToString()

        member this.ToType(conversionType: Type, provider: IFormatProvider) : obj =
            Convert.ChangeType((this :> IConvertible).ToDouble(provider), conversionType)

        member this.ToUInt16(provider: IFormatProvider) : uint16 =
            Convert.ToUInt16((this :> IConvertible).ToDouble(provider))

        member this.ToUInt32(provider: IFormatProvider) : uint32 =
            Convert.ToUInt32((this :> IConvertible).ToDouble(provider))

        member this.ToUInt64(provider: IFormatProvider) : uint64 =
            Convert.ToUInt64((this :> IConvertible).ToDouble(provider))

    member this.BigIntegerValue() = numerator / denominator

    member this.ToBigDecimal() =
        let num = BigDecimal.Create(this.Numerator)
        let den = BigDecimal.Create(this.Denominator)
        num.Divide(den)

    member this.ToBigDecimal(c: Context) =
        let num = BigDecimal.Create(this.Numerator)
        let den = BigDecimal.Create(this.Denominator)
        num.Divide(den, c)
