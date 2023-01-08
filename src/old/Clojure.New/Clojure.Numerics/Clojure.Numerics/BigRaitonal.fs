namespace Clojure.Numerics

open System
open System.Numerics


[<Sealed>]
type BigRational(n: BigInteger, d: BigInteger) =
    let mutable numerator = n
    let mutable denominator = d

    do
        if d.IsZero then
            raise
            <| DivideByZeroException("Denominator is zero")

        let (n1, d1) =
            if n.Sign = 0 then (BigInteger.Zero, BigInteger.One)
            elif d.Sign < 0 then (-n, -d)
            else (n, d)

        let n2, d2 = BigRational.normalize n1 d1
        numerator <- n2
        denominator <- d2


    // integer constructors

    new(x: BigInteger) = BigRational(x, BigInteger.One)
    new(x: int32) = BigRational(BigInteger(x), BigInteger.One)
    new(x: int64) = BigRational(BigInteger(x), BigInteger.One)
    new(x: uint32) = BigRational(BigInteger(x), BigInteger.One)
    new(x: uint64) = BigRational(BigInteger(x), BigInteger.One)


    // non-integer numeric constructors

    new(x: decimal) =
        let coeff, exp = ArithmeticHelpers.deconstructDecimal x
        BigRational(coeff, BigInteger.Pow(ArithmeticHelpers.biTen, exp))

    new(x: double) =
        let n, d =
            match ArithmeticHelpers.deconstructDouble x with
            | ArithmeticHelpers.DoubleData.Zero (_) -> BigInteger.Zero, BigInteger.One
            | ArithmeticHelpers.DoubleData.Infinity (_) -> invalidArg "x" "Argument is infinite"
            | ArithmeticHelpers.DoubleData.NaN (_) -> invalidArg "x" "Argument is not a number"
            | ArithmeticHelpers.DoubleData.Denormalized (isPositive = p; fraction = f; exponent = e)
            | ArithmeticHelpers.DoubleData.Standard (isPositive = p; mantissa = f; exponent = e) ->
                let n = BigInteger(f)

                let d =
                    BigInteger(1 <<< ArithmeticHelpers.doubleSignificandBitLength)

                let n, d =
                    if e > 0 then BigInteger.Pow(n, e), d
                    elif e < 0 then n, BigInteger.Pow(d, -e)
                    else n, d

                let n = if p then n else BigInteger.Negate(n)
                n, d

        BigRational(n, d)


    static member private normalize (n: BigInteger) (d: BigInteger) =

        let gcd = BigInteger.GreatestCommonDivisor(n, d)

        if gcd.IsOne then (n, d) else (n / gcd, d / gcd)

    // some accessors

    member _.Numerator = numerator
    member _.Denominator = denominator

    // Some contants
    static member Zero =
        BigRational(BigInteger.Zero, BigInteger.One)

    static member One =
        BigRational(BigInteger.One, BigInteger.One)

    // basic interfaces

    interface IEquatable<BigRational> with
        member this.Equals(y: BigRational) =
            if this.Denominator = y.Denominator
            then this.Numerator = y.Numerator
            else this.Numerator * y.Denominator = y.Numerator * this.Denominator

    override this.Equals(obj) =
        match obj with
        | :? BigRational as r -> (this :> IEquatable<BigRational>).Equals(r)
        | _ -> false

    override this.GetHashCode() =
        this.Numerator.GetHashCode()
        ^^^ this.Denominator.GetHashCode()

    override this.ToString() =
        this.Numerator.ToString()
        + "/"
        + this.Denominator.ToString()


    interface IComparable<BigRational> with
        member this.CompareTo(y) =
            BigInteger.Compare(this.Numerator * y.Denominator, y.Numerator * this.Denominator)


    interface IComparable with
        member this.CompareTo y =
            match y with
            | null -> 1
            | :? BigRational as r -> (this :> IComparable<BigRational>).CompareTo(r)
            | _ -> invalidArg "y" "Argument must be of type BigRational"


    // some conversions

    member this.ToBigInteger() = this.Numerator / this.Denominator

    member this.ToBigDecimal() =
        BigDecimal.Create(this.Numerator)
        / BigDecimal.Create(this.Denominator)

    member this.ToBigDecimal(c) =
        BigDecimal.Divide(BigDecimal.Create(this.Numerator), BigDecimal.Create(this.Denominator), c)

    // compatibility with JVM implementation
    member this.BigIntegerValue() = this.ToBigInteger()

    interface IConvertible with
        member _.GetTypeCode() = TypeCode.Object
        member this.ToBoolean(_: IFormatProvider) = not this.Numerator.IsZero
        member this.ToByte(_: IFormatProvider) = Convert.ToByte(this.ToBigInteger())
        member this.ToChar(_: IFormatProvider) = Convert.ToChar(this.ToBigInteger())
        member this.ToDateTime(_: IFormatProvider) = Convert.ToDateTime(this.ToBigInteger())

        member this.ToDecimal(fp: IFormatProvider) =
            (this.ToBigDecimal(Context.Decimal128) :> IConvertible)
                .ToDecimal(fp)

        member this.ToDouble(fp: IFormatProvider) =
            (this.ToBigDecimal(Context.Decimal64) :> IConvertible)
                .ToDouble(fp)

        member this.ToInt16(_: IFormatProvider) = Convert.ToInt16(this.ToBigInteger())
        member this.ToInt32(_: IFormatProvider) = Convert.ToInt32(this.ToBigInteger())
        member this.ToInt64(_: IFormatProvider) = Convert.ToInt64(this.ToBigInteger())
        member this.ToSByte(_: IFormatProvider) = Convert.ToSByte(this.ToBigInteger())

        member this.ToSingle(fp: IFormatProvider) =
            (this.ToBigDecimal(Context.Decimal32) :> IConvertible)
                .ToSingle(fp)

        member this.ToString(_: IFormatProvider) = this.ToString()

        member this.ToType(conversionType: Type, fp: IFormatProvider) =
            Convert.ChangeType((this :> IConvertible).ToDouble(fp), conversionType, fp)

        member this.ToUInt16(_: IFormatProvider) = Convert.ToUInt16(this.ToBigInteger())
        member this.ToUInt32(_: IFormatProvider) = Convert.ToUInt32(this.ToBigInteger())
        member this.ToUInt64(_: IFormatProvider) = Convert.ToUInt64(this.ToBigInteger())

    // Some helpful properties

    member this.IsZero = this.Numerator.IsZero
    member this.IsPositive = this.Numerator.Sign > 0
    member this.IsNegative = this.Numerator.Sign < 0
    member this.Sign = this.Numerator.Sign

    // Parsing

    static member Parse(s: string): BigRational =
        let parts = s.Split("/")

        match Array.length parts with
        | 0 -> invalidArg "s" "No characters to parse"
        | 1 -> BigRational(BigInteger.Parse(parts.[0]))
        | 2 -> BigRational(BigInteger.Parse(parts.[0]), BigInteger.Parse(parts.[1]))
        | _ -> invalidArg "s" "More than one /"

    static member TryParse(s: String, value: outref<BigRational>): bool =
        try
            value <- BigRational.Parse s
            true
        with ex -> false



    // Arithmetic operations

    // -(c/d) = (-c)/d
    member this.Negate() =
        if this.IsZero
        then this
        else BigRational(-this.Numerator, this.Denominator)

    static member Negate(x: BigRational) = x.Negate()
    static member (~-)(x: BigRational) = x.Negate()
    static member (~+)(x: BigRational) = x

    // abs (a/b) = abs(a)/b
    member this.Abs() =
        if this.Numerator.Sign < 0
        then BigRational(BigInteger.Abs(this.Numerator), this.Denominator)
        else this

    static member Abs(x: BigRational) = x.Abs()

    // a/b + c/d = (ad+bc)/bd
    member this.Add(y: BigRational) =
        BigRational
            (this.Numerator * y.Denominator
             + this.Denominator * y.Numerator,
             this.Denominator * y.Denominator)

    static member Add(x: BigRational, y: BigRational) = x.Add(y)
    static member (+)(x: BigRational, y: BigRational) = x.Add(y)

    // a/b - c/d = (ad-bc)/bd
    member this.Subtract(y: BigRational) =
        BigRational
            (this.Numerator * y.Denominator
             - this.Denominator * y.Numerator,
             this.Denominator * y.Denominator)

    static member Subtract(x: BigRational, y: BigRational) = x.Subtract(y)
    static member (-)(x: BigRational, y: BigRational) = x.Subtract(y)


    // a/b * c/d = ac/bd
    member this.Multiply(y: BigRational) =
        BigRational(this.Numerator * y.Numerator, this.Denominator * y.Denominator)

    static member Multiply(x: BigRational, y: BigRational) = x.Multiply(y)
    static member (*)(x: BigRational, y: BigRational) = x.Multiply(y)

    // a/b / c/d = ad/bc
    member this.Divide(y: BigRational) =
        BigRational(this.Numerator * y.Denominator, this.Numerator * y.Denominator)

    static member Divide(x: BigRational, y: BigRational) = x.Divide(y)
    static member (/)(x: BigRational, y: BigRational) = x.Divide(y)

    // a/b % c/d = (ad % bc)/bd
    member this.Mod(y: BigRational) =
        BigRational
            (this.Numerator * y.Denominator % this.Denominator
             * y.Numerator,
             this.Denominator * y.Denominator)

    static member Mod(x: BigRational, y: BigRational) = x.Mod(y)
    static member (%)(x: BigRational, y: BigRational) = x.Mod(y)

    // a/b / c/d  == (ad)/(bc)
    // a/b % c/d  == (ad % bc)/bd
    member this.DivRem(y: BigRational, remainder: outref<BigRational>) =
        let ad = this.Numerator * y.Denominator
        let bc = this.Denominator * y.Numerator
        let bd = this.Denominator * y.Denominator
        remainder <- BigRational(ad % bc, bd)
        ad / bc

    static member DivRem(x: BigRational, y: BigRational, remainder: outref<BigRational>) = x.DivRem(y, &remainder)


    // multiplicative inverse of a/b = b/a
    member this.Invert() =
        BigRational(this.Denominator, this.Numerator)

    static member Invert(x: BigRational) = x.Invert()

    member this.Pow(n: int) =
        if n = 0 then
            if this.IsZero then
                raise
                <| ArithmeticException("Cannot compute 0**0")
            else
                BigRational.Zero
        elif n < 0 then
            if this.IsZero then
                raise
                <| ArithmeticException("Cannot raise zero to a negative exponent")
            else
                this.Invert().Pow(-n)
        else
            BigRational(BigInteger.Pow(this.Numerator, n), BigInteger.Pow(this.Denominator, n))


    // LCD( a/b, c/d ) = (bd) / GCD(b,d)
    static member LeastCommonDenominator(x: BigRational, y: BigRational) =
        (x.Denominator * y.Denominator)
        / BigInteger.GreatestCommonDivisor(x.Denominator, y.Denominator)
