namespace Clojure.BigArith

open System
open System.Text
open System.Numerics
open System.Globalization

type RoundingMode =
    | Up
    | Down
    | Ceiling
    | Floor
    | HalfUp
    | HalfDown
    | HalfEven
    | Unnecessary

[<Struct>]
type Context =
    { precision: uint32
      roundingMode: RoundingMode }

    /// Standard precision for 32-bit decimal
    static member Decimal32 =
        { precision = 7u
          roundingMode = HalfEven }
    /// Standard precision for 64-bit decimal
    static member Decimal64 =
        { precision = 16u
          roundingMode = HalfEven }
    /// Standard precision for 128-bit decimal
    static member Decimal128 =
        { precision = 34u
          roundingMode = HalfEven }
    /// No precision limit
    static member Unlimited =
        { precision = 0u
          roundingMode = HalfUp }
    /// Default mode
    static member Default =
        { precision = 9ul
          roundingMode = HalfUp }
    /// Create a Context with specified precision and roundingMode = HalfEven
    static member ExtendedDefault precision =
        { precision = precision
          roundingMode = HalfEven }
    /// Create a Context from the given precision and rounding mode
    static member Create(precision, roundingMode) =
        { precision = precision
          roundingMode = roundingMode }


// Parser support

[<Struct>]
type internal ParserSpan = { Start: int; Length: int } // shall we go to the trouble of making these uints?

type internal ParseData =
    { wholePart: ParserSpan
      fractionPart: ParserSpan
      exponent: ParserSpan }

type internal ParseResult = ParseResult of ParseData * int


type BigDecimal private (coeff, exp, precision) =

    // Precision

    // Constructor precision is shadowed with a mutable.
    // Value of 0 indicates precision not computed
    let mutable precision: uint = precision

    // Compute actual precision and cache it.
    member private _.GetPrecision() =
        match precision with
        | 0u -> precision <- Math.Max(ArithmeticHelpers.getBIPrecision (coeff), 1u)
        | _ -> ()

        precision

    member this.Precision = this.GetPrecision()
    member _.RawPrecision = precision
    member this.IsPrecisionKnown = this.RawPrecision <> 0u

    // Other accessors

    member _.Coefficient = coeff
    member _.Exponent = exp

    // Useful info

    member this.Signum(): int =
        match this.Coefficient.Sign with
        | 0 -> 0
        | sign when sign > 0 -> 1
        | _ -> -1

    member this.IsZero = this.Coefficient.IsZero
    member this.IsPositive = this.Coefficient.Sign > 0
    member this.IsNegative = this.Coefficient.Sign < 0


    // Useful constants

    static member Zero = BigDecimal(BigInteger.Zero, 0, 1u)
    static member One = BigDecimal(BigInteger.One, 0, 1u)
    static member Ten = BigDecimal(new BigInteger(10), 0, 2u)

    // Exponent support

    static member checkExponent (candidate: int64) (isZero: bool): int option =
        match candidate with
        | x when x <= (int64 Int32.MaxValue)
                 && x >= (int64 Int32.MinValue) -> Some(int32 x)
        | x when isZero && x < 0L -> Some Int32.MinValue
        | x when isZero && x > 0L -> Some Int32.MaxValue
        | _ -> None

    static member checkExponentE (candidate: int64) (isZero: bool): int =
        match BigDecimal.checkExponent candidate isZero with
        | Some e -> e
        | None when candidate > 0L -> raise <| ArithmeticException("Overflow in scale")
        | None -> raise <| ArithmeticException("Underflow in scale")



    // Rounding/quantize/rescale

    static member private roundingDivide2 x y mode =
        let (q, r) = BigInteger.DivRem(x, y)
        let isNeg = q.Sign < 0
        let cmp bi = BigInteger.Abs(bi + bi).CompareTo(y)

        let increment =
            if r.IsZero then
                false
            else
                match mode with
                | Unnecessary ->
                    raise
                    <| ArithmeticException("Rounding is required, but prohibited.")
                | Ceiling -> not isNeg
                | Floor -> isNeg
                | Down -> false
                | Up -> true
                | HalfDown -> (cmp r) > 0
                | HalfUp -> (cmp r) >= 0
                | HalfEven -> let c = (cmp r) in (c > 0) || (c = 0 && not q.IsEven)

        if increment
        then if q.Sign < 0 || (q.IsZero && x.Sign < 0) then q - BigInteger.One else q + BigInteger.One
        else q

    static member private round (v: BigDecimal) c =
        let vp = v.GetPrecision()

        if (vp <= c.precision) then
            v
        else
            let drop = vp - c.precision
            let divisor = ArithmeticHelpers.biPowerOfTen (drop)

            let rounded =
                BigDecimal.roundingDivide2 v.Coefficient divisor c.roundingMode

            let exp =
                BigDecimal.checkExponentE ((int64 v.Exponent) + (int64 drop)) rounded.IsZero

            let result = BigDecimal(rounded, exp, 0u)

            if c.precision > 0u then BigDecimal.round result c else result

    static member Round(v: BigDecimal, c: Context) = BigDecimal.round v c
    member x.Round(c: Context) = BigDecimal.Round(x, c)

    static member Rescale(lhs: BigDecimal, newExponent, mode): BigDecimal =

        let increaseExponent delta =
            // delta negative => increasing the exponent => we might have to round to a new precision
            let decrease = -delta |> uint
            let p = lhs.Precision

            if p < decrease then
                BigDecimal(BigInteger.Zero, newExponent, 0u)
            else
                let newPrecision = p - decrease

                let r =
                    lhs.Round
                        ({ precision = newPrecision
                           roundingMode = mode })

                if (r.Exponent = newExponent) then r else BigDecimal.Rescale(r, newExponent, mode)

        let decreaseExponent delta =
            // delta positive => decrease the exponent => multiply by 10^some power and don't underflow
            let newCoeff =
                lhs.Coefficient
                * ArithmeticHelpers.biPowerOfTen (delta)

            let oldPrec = lhs.Precision

            let newPrec =
                oldPrec + (if oldPrec = 0u then 0u else delta)

            BigDecimal(newCoeff, newExponent, newPrec)

        let delta =
            BigDecimal.checkExponentE ((int64 lhs.Exponent) - (int64 newExponent)) false

        if delta = 0 then lhs
        elif lhs.Coefficient.IsZero then BigDecimal(BigInteger.Zero, newExponent, 0u)
        elif delta < 0 then increaseExponent delta
        else decreaseExponent (uint delta)


    static member Quantize(lhs: BigDecimal, rhs: BigDecimal, mode: RoundingMode) =
        BigDecimal.Rescale(lhs, rhs.Exponent, mode)

    member this.Quantize(v, mode) = BigDecimal.Quantize(this, v, mode)


    // Parsing

    // Using ReadOnlySpan<char> (ROSC) as input to consolidate versions for char arrays, strings, and similar.
    // Unfortunately, using spans has a lot of limitations, so I could not use them through most of the code.
    // Opted instead to convert to a char array and use (offset,length) pairs (ParserSpan) to encode regions.
    // Using a parsing model simlar to parser combinators, but much simpler because we have a simple linear model:
    //  +/- whole-part . fraction-part E +/- exponent
    // All parts are optional with the following constraints:
    //  (1) there must be a digit in whole-part + fraction-part
    //  (2) if there is an E, there must be a digit in exponent
    // I may completely redo this.  Seems more complicated than necessary, but it was a nice exercise.

    static member private DoParse(buf: ReadOnlySpan<char>): Result<BigDecimal, String> =
        let input = buf.ToArray()

        let inputIsEmpty posn = posn >= input.Length

        let isSign ch = ch = '+' || ch = '-'

        let hasLeadingSign = not (inputIsEmpty 0) && isSign input.[0]

        let numDigits (pspan: ParserSpan) =
            match pspan with
            | { Length = 0 } -> 0
            | { Length = len; Start = posn } when isSign input.[posn] -> len - 1
            | { Length = len } -> len

        let parseNonEmpty pr: Result<ParseResult, string> =
            match input.Length with
            | 0 -> Error "No characters"
            | _ -> Ok pr

        let parseDigits (posn: int) (leadingSignOk: bool) (noDigitsOk: bool): Result<ParserSpan * int, string> =
            let leadingSignPresent =
                not (inputIsEmpty posn) && (isSign input.[posn])

            if leadingSignPresent && not leadingSignOk then
                Error "Misplaced +/-"
            else
                let startPosn =
                    if leadingSignPresent then posn + 1 else posn

                let mutable i = startPosn

                while i < input.Length && Char.IsDigit(input.[i]) do // TODO: use a sequence function for this.
                    i <- i + 1

                if i = startPosn && not noDigitsOk
                then Error "missing digits"
                else Ok({ Start = posn; Length = i - posn }, i)

        let parseWhole (ParseResult (data, posn)): Result<ParseResult, string> =
            match (parseDigits posn true true) with
            | Ok (span, nextPosn) -> Ok(ParseResult({ data with wholePart = span }, nextPosn))
            | Error msg -> Error msg

        let parseFraction (ParseResult (data, posn) as pr): Result<ParseResult, string> =
            if inputIsEmpty posn || input.[posn] <> '.' then
                Ok pr
            else
                match parseDigits (posn + 1) false true with
                | Ok (start, rest) -> Ok(ParseResult({ data with fractionPart = start }, rest))
                | Error msg -> Error msg


        let parseExponent (ParseResult (data, posn) as pr): Result<ParseResult, string> =
            if inputIsEmpty posn
               || (input.[posn] <> 'E' && input.[posn] <> 'e') then
                Ok pr
            else
                match parseDigits (posn + 1) true false with
                | Ok (span, nextPosn) -> Ok(ParseResult({ data with exponent = span }, nextPosn))
                | Error msg -> Error "No digits in coefficient"

        let parseEmpty (ParseResult (data, posn) as pr): Result<ParseResult, string> =
            if inputIsEmpty posn then Ok pr else Error "Unused characters at end"

        let parseConsistencyChecks (ParseResult (data, posn) as pr): Result<ParseResult, string> =
            let numWholePartDigits =
                if isSign input.[0] then data.wholePart.Length - 1 else data.wholePart.Length

            if numWholePartDigits = 0
               && data.fractionPart.Length = 0 then
                Error "No digits in coefficient"
            else
                Ok pr

        let leadingZeroCountInSpan (span: ParserSpan) =
            let nonZeroChar c = c <> '0'

            let hasLeadingSign =
                not (inputIsEmpty span.Start)
                && isSign input.[span.Start]

            let (skip, maxCount) =
                if hasLeadingSign then (span.Start + 1, span.Length - 1) else (span.Start, span.Length)

            let found =
                input
                |> Seq.skip skip
                |> Seq.tryFindIndex nonZeroChar

            match found with // remenmber to skip the leading sign, which will exist when we call this
            | Some index -> Math.Min(index, maxCount)
            | None -> maxCount

        let leadingZeroCount (data: ParseData) =
            let wholeZeroCount = leadingZeroCountInSpan data.wholePart
            let wholeDigitCount = numDigits data.wholePart

            if wholeZeroCount = wholeDigitCount then
                wholeZeroCount
                + leadingZeroCountInSpan data.fractionPart
            else
                wholeZeroCount

        let givenExponent (data: ParseData) =
            match data.exponent.Length with
            | 0 -> Ok 0
            | _ ->
                match Int32.TryParse
                          ((new ReadOnlySpan<char>(input, data.exponent.Start, data.exponent.Length)),
                           NumberStyles.AllowLeadingSign,
                           CultureInfo.InvariantCulture) with
                | true, i -> Ok i
                | _ -> Error "Invalid exponent"

        let computeExponent (data: ParseData) isZero =
            let indicatedExponent = -data.fractionPart.Length

            match givenExponent data with
            | Ok 0 as v -> Ok indicatedExponent
            | Ok exp ->
                match BigDecimal.checkExponent ((int64 indicatedExponent) + (int64 exp)) isZero with
                | Some e -> Ok e
                | None -> Error "Invalid exponent"
            | Error msg as v -> Error msg

        let constructBD (data: ParseData): Result<BigDecimal, string> =
            let digits =
                Array.zeroCreate (data.wholePart.Length + data.fractionPart.Length)

            Array.Copy(input, data.wholePart.Start, digits, 0, data.wholePart.Length)
            Array.Copy(input, data.fractionPart.Start, digits, data.wholePart.Length, data.fractionPart.Length)
            let bi = (BigInteger.Parse(String(digits)))

            let precision =
                (numDigits data.wholePart)
                + data.fractionPart.Length
                - leadingZeroCount (data)

            let precision =
                if precision = 0 || bi.IsZero then 1 else precision

            match computeExponent data bi.IsZero with
            | Ok exp -> Ok(BigDecimal(bi, exp, uint precision))
            | Error msg -> Error msg

        let emptyData =
            { wholePart = { Start = 0; Length = 0 }
              fractionPart = { Start = 0; Length = 0 }
              exponent = { Start = 0; Length = 0 } }

        let result =
            Ok(ParseResult(emptyData, 0))
            |> Result.bind parseNonEmpty
            |> Result.bind parseWhole
            |> Result.bind parseFraction
            |> Result.bind parseExponent
            |> Result.bind parseEmpty
            |> Result.bind parseConsistencyChecks

        match result with
        | Error x -> Error x
        | Ok (ParseResult (data, b)) as pr -> constructBD data

    static member private DoParseE(buf: ReadOnlySpan<char>): BigDecimal =
        match BigDecimal.DoParse buf with
        | Ok bd -> bd
        | Error x -> invalidArg "input" x

    static member Parse(s: String) = BigDecimal.DoParseE(s.AsSpan())

    static member Parse(s: String, c) = BigDecimal.round (BigDecimal.Parse(s)) c

    static member Parse(s: ReadOnlySpan<char>) = BigDecimal.DoParseE(s)

    static member Parse(s: ReadOnlySpan<char>, c) = BigDecimal.round (BigDecimal.Parse(s)) c

    // Kept the following for backwards compatibility, don't really need now that we have spans.

    static member Parse(v: char array) = BigDecimal.DoParseE(ReadOnlySpan(v))

    static member Parse(v: char array, c) =
        BigDecimal.round (BigDecimal.Create(v)) c

    static member Parse(v: char array, offset: int, len: int) =
        BigDecimal.DoParseE(ReadOnlySpan(v, offset, len))

    static member Parse(v: char array, offset: int, len: int, c) =
        BigDecimal.round (BigDecimal.Create(v, offset, len)) c

    static member TryParse(s: String, value: outref<BigDecimal>): bool =
        match BigDecimal.DoParse(s.AsSpan()) with
        | Ok bd ->
            value <- bd
            true
        | Error _ -> false

    static member TryParse(s: String, c, value: outref<BigDecimal>): bool =
        match BigDecimal.DoParse(s.AsSpan()) with
        | Ok bd ->
            value <- BigDecimal.round bd c
            true
        | Error _ -> false

    static member TryParse(s: ReadOnlySpan<char>, value: outref<BigDecimal>): bool =
        match BigDecimal.DoParse(s) with
        | Ok bd ->
            value <- bd
            true
        | Error _ -> false

    static member TryParse(s: ReadOnlySpan<char>, c, value: outref<BigDecimal>): bool =
        match BigDecimal.DoParse(s) with
        | Ok bd ->
            value <- BigDecimal.round bd c
            true
        | Error _ -> false

    // Kept the following for backwards compatibility, don't really need now that we have spans.

    static member TryParse(a: char array, value: outref<BigDecimal>): bool =
        match BigDecimal.DoParse(ReadOnlySpan(a)) with
        | Ok bd ->
            value <- bd
            true
        | Error _ -> false

    static member TryParse(a: char array, c, value: outref<BigDecimal>): bool =
        match BigDecimal.DoParse(ReadOnlySpan(a)) with
        | Ok bd ->
            value <- BigDecimal.round bd c
            true
        | Error _ -> false

    static member TryParse(a: char array, offset: int, len: int, value: outref<BigDecimal>): bool =
        match BigDecimal.DoParse(ReadOnlySpan(a, offset, len)) with
        | Ok bd ->
            value <- bd
            true
        | Error _ -> false

    static member TryParse(a: char array, offset: int, len: int, c, value: outref<BigDecimal>): bool =
        match BigDecimal.DoParse(ReadOnlySpan(a, offset, len)) with
        | Ok bd ->
            value <- BigDecimal.round bd c
            true
        | Error _ -> false


    // Factory methods

    static member Create(bi: BigDecimal) =
        BigDecimal(bi.Coefficient, bi.Exponent, bi.RawPrecision)

    static member Create(coeff, exp) = BigDecimal(coeff, exp, 0u)
    static member Create(v: int32) = BigDecimal(BigInteger(v), 0, 0u)

    static member CreateC(v: int32, c) =
        BigDecimal.round (BigDecimal.Create(v)) c

    static member Create(v: int64) = BigDecimal(BigInteger(v), 0, 0u)

    static member CreateC(v: int64, c) =
        BigDecimal.round (BigDecimal.Create(v)) c

    static member Create(v: uint64) = BigDecimal(BigInteger(v), 0, 0u)

    static member CreateC(v: uint64, c) =
        BigDecimal.round (BigDecimal.Create(v)) c

    static member Create(v: BigInteger) = BigDecimal(v, 0, 0u)

    static member CreateC(v: BigInteger, c) =
        BigDecimal.round (BigDecimal.Create(v)) c

    static member Create(v: decimal) =
        if v = 0m then
            BigDecimal.Zero
        else
            let coeff, exp = ArithmeticHelpers.deconstructDecimal v
            BigDecimal(coeff, -exp, 0u)

    static member CreateC(v: decimal, c) =
        BigDecimal.round (BigDecimal.Create(v)) c

    static member Create(v: double) =
        if Double.IsNaN(v)
        then invalidArg "value" "NaN is not supported in BigDecimal"

        if Double.IsInfinity(v)
        then invalidArg "value" "Infinity is not supported in BigDecimal"

        //let dbytes = BitConverter.GetBytes(v)
        //let signficand = ArithmeticHelpers.getDoubleSignificand dbytes
        //let biasedExp = ArithmeticHelpers.getDoubleBiasedExponent dbytes
        //let leftShift = (int biasedExp) - ArithmeticHelpers.doubleShiftBias;
        //if signficand = 0UL && biasedExp = 0us then BigDecimal(BigInteger.Zero,0,1u)
        //else
        //    let ( coeff, leftShift ) =
        //        if ( signficand = 0UL)
        //        then ( ( if v < 0.0 then BigInteger.MinusOne else BigInteger.One),
        //               (int biasedExp) - ArithmeticHelpers.doubleExponentBias )
        //        else
        //            let unadjustedCoeff = BigInteger(signficand ||| 0x10000000000000UL)
        //            ( (if v < 0.0 then BigInteger.Negate(unadjustedCoeff) else unadjustedCoeff),
        //              leftShift)
        //    let (coeffToUse, expToUse) =
        //        if leftShift < 0
        //            then ( coeff*BigInteger.Pow(ArithmeticHelpers.biFive,-leftShift), leftShift)
        //        elif leftShift > 0
        //            then ( coeff <<< leftShift , 0 )
        //        else ( coeff, 0 )
        //    BigDecimal(coeffToUse,expToUse,0u)


        match ArithmeticHelpers.deconstructDouble v with
        | ArithmeticHelpers.DoubleData.Zero (_) -> BigDecimal(BigInteger.Zero, 0, 1u)
        | ArithmeticHelpers.DoubleData.Denormalized (isPositive = p; fraction = f; exponent = e) -> invalidArg "a" "a"
        | ArithmeticHelpers.DoubleData.Infinity (_)
        | ArithmeticHelpers.DoubleData.NaN (_) ->
            raise
            <| InvalidOperationException("Should not reach this case (Infinity")
        | ArithmeticHelpers.DoubleData.Standard (isPositive = p; mantissa = m; exponent = e; isSignificandZero = z) ->
            let coeff, leftShift =
                if z then
                    ((if p then BigInteger.One else BigInteger.MinusOne), e)
                else
                    ((if p then BigInteger(m) else BigInteger.Negate(BigInteger(m))),
                     e - ArithmeticHelpers.doubleSignificandBitLength)

            let coeffToUse, expToUse =
                if leftShift < 0 then
                    (coeff
                     * BigInteger.Pow(ArithmeticHelpers.biFive, -leftShift),
                     leftShift)
                elif leftShift > 0 then
                    (coeff <<< leftShift, 0)
                else
                    (coeff, 0)

            BigDecimal(coeffToUse, expToUse, 0u)

    static member CreateC(v: double, c) =
        BigDecimal.round (BigDecimal.Create(v)) c

    static member Create(v: String) = BigDecimal.Parse(v)
    static member Create(v: String, c) = BigDecimal.Parse(v, c)

    // Kept the following for backwards compatibility.  Don't really need now that we have spans.

    static member Create(v: char array) = BigDecimal.Parse(v)
    static member Create(v: char array, c) = BigDecimal.Parse(v, c)
    static member Create(v: char array, offset: int, len: int) = BigDecimal.Parse(v, offset, len)
    static member Create(v: char array, offset: int, len: int, c) = BigDecimal.Parse(v, offset, len, c)


    // ToString implementation

    member _.ToScientificString() =
        let sb = StringBuilder(coeff.ToString())

        let coeffLen, negOffset =
            if coeff.Sign < 0 then (sb.Length - 1, 1) else (sb.Length, 0)

        let adjustedExp = (int64 exp) + (int64 coeffLen) - 1L

        if exp <= 0 && adjustedExp >= -6L then
            if exp <> 0 then // we need a decimal point
                match -exp with
                | numDec when numDec < coeffLen ->
                    sb.Insert(coeffLen - numDec + negOffset, '.')
                    |> ignore
                | numDec when numDec = coeffLen -> sb.Insert(negOffset, "0.") |> ignore
                | numDec ->
                    let numZeros = numDec - coeffLen
                    sb.Insert(negOffset, "0", numZeros) |> ignore
                    sb.Insert(negOffset, "0.") |> ignore
            else
                ()
        else
            if coeffLen > 1 then sb.Insert(negOffset + 1, '.') |> ignore

            sb.Append('E') |> ignore

            if adjustedExp >= 0L then sb.Append('+') |> ignore

            sb.Append(adjustedExp) |> ignore

        sb.ToString()

    override this.ToString(): String = this.ToScientificString()


    // support for some of the arithmetic operations

    /// Align the bigger BigDecimal by increasing its coefficient and decreasing its exponent
    static member private computeAlign (big: BigDecimal) (small: BigDecimal) =
        let deltaExp = (big.Exponent - small.Exponent) |> uint

        BigDecimal
            (big.Coefficient
             * ArithmeticHelpers.biPowerOfTen (deltaExp),
             small.Exponent,
             0u)

    /// Create matching pair with the same alignment (exponent)
    static member private align (x: BigDecimal) (y: BigDecimal) =
        if y.Exponent > x.Exponent then (x, BigDecimal.computeAlign y x)
        elif x.Exponent > y.Exponent then (BigDecimal.computeAlign x y, y)
        else (x, y)


    //////////////////////////////////
    // Baasic interfaces
    //////////////////////////////////

    interface IComparable<BigDecimal> with
        member this.CompareTo(y: BigDecimal) =
            let x1, y1 = BigDecimal.align this y
            x1.Coefficient.CompareTo(y1.Coefficient)

    interface IComparable with
        member this.CompareTo y =
            match y with
            | null -> 1
            | :? BigDecimal as bd -> (this :> IComparable<BigDecimal>).CompareTo(bd)
            | _ -> invalidArg "y" "Argument must be of type BigDecimal"

    interface IEquatable<BigDecimal> with
        member this.Equals(y: BigDecimal) =
            if this.Exponent <> y.Exponent then false else this.Coefficient.Equals(y.Coefficient)

    static member op_LessThan(left: BigDecimal, right: BigDecimal): bool =
        (left :> IComparable<BigDecimal>).CompareTo(right) < 0

    static member op_LessThanOrEqual(left: BigDecimal, right: BigDecimal): bool =
        (left :> IComparable<BigDecimal>).CompareTo(right)
        <= 0

    static member op_GreaterThan(left: BigDecimal, right: BigDecimal): bool =
        (left :> IComparable<BigDecimal>).CompareTo(right) > 0

    static member op_GreaterThanOrEqual(left: BigDecimal, right: BigDecimal): bool =
        (left :> IComparable<BigDecimal>).CompareTo(right)
        >= 0

    static member op__Equality(left: BigDecimal, right: BigDecimal): bool =
        (left :> IEquatable<BigDecimal>).Equals(right)

    static member op_Inequality(left: BigDecimal, right: BigDecimal): bool =
        (left :> IEquatable<BigDecimal>).Equals(right)

    override this.Equals(obj) =
        match obj with
        | :? BigDecimal as bd -> (this :> IEquatable<BigDecimal>).Equals(bd)
        | _ -> false


    static member private hashCombine seed hash =
        // a la boost  -- maybe someday use Murmur3 instead?
        (seed
         ^^^ (hash + 0x9e3779b9 + (seed <<< 6)) + (seed >>> 2))

    override this.GetHashCode() =
        BigDecimal.hashCombine (this.Coefficient.GetHashCode()) (this.Exponent.GetHashCode())

    member this.ToBigInteger() =
        BigDecimal
            .Rescale(this, 0, RoundingMode.Down)
            .Coefficient

    interface IConvertible with
        member _.GetTypeCode() = TypeCode.Object
        member this.ToBoolean(_: IFormatProvider) = not this.IsZero
        member this.ToByte(_: IFormatProvider) = this.ToBigInteger() |> byte
        member this.ToChar(_: IFormatProvider) = this.ToBigInteger() |> uint16 |> char

        member _.ToDateTime(_: IFormatProvider) =
            raise
            <| InvalidCastException("Cannot convert to DateTime")

        member this.ToDecimal(_: IFormatProvider) = this.ToString() |> Decimal.Parse

        // As j.m.BigDecimal puts it: "Somewhat inefficient, but guaranteed to work."
        // However, JVM's double parser goes to +/- Infinity when out of range,
        // while CLR's throws an exception.
        // Hate dealing with that.
        member this.ToDouble(fp: IFormatProvider) =
            try
                Double.Parse(this.ToString(), fp)
            with :? OverflowException -> if this.IsNegative then Double.NegativeInfinity else Double.PositiveInfinity


        member this.ToInt16(_: IFormatProvider) = this.ToBigInteger() |> int16
        member this.ToInt32(_: IFormatProvider) = this.ToBigInteger() |> int32
        member this.ToInt64(_: IFormatProvider) = this.ToBigInteger() |> int64
        member this.ToSByte(_: IFormatProvider) = this.ToBigInteger() |> sbyte
        member this.ToSingle(_: IFormatProvider) = this.ToBigInteger() |> single
        member this.ToString(_: IFormatProvider) = this.ToString()

        member this.ToType(conversionType: Type, fp: IFormatProvider) =
            Convert.ChangeType((this :> IConvertible).ToDouble(fp), conversionType, fp)

        member this.ToUInt16(_: IFormatProvider) = this.ToBigInteger() |> uint16
        member this.ToUInt32(_: IFormatProvider) = this.ToBigInteger() |> uint32
        member this.ToUInt64(_: IFormatProvider) = this.ToBigInteger() |> uint64


    //////////////////////////////////
    // Arithmetic operations
    //////////////////////////////////

    // Negation

    member this.Negate() =
        if this.Coefficient.IsZero
        then this
        else BigDecimal(BigInteger.Negate(this.Coefficient), this.Exponent, this.RawPrecision)

    member this.Negate(c) = BigDecimal.round (this.Negate()) c
    static member Negate(x: BigDecimal) = x.Negate()
    static member Negate(x: BigDecimal, c) = x.Negate(c)


    // Absolute value

    member this.Abs() =
        if this.Coefficient.Sign < 0 then this.Negate() else this

    member this.Abs(c) =
        if this.Coefficient.Sign < 0 then this.Negate(c) else BigDecimal.round this c

    static member Abs(x: BigDecimal) = x.Abs()
    static member Abs(x: BigDecimal, c) = x.Abs(c)


    //  Addition and subtraction

    member this.Add(y: BigDecimal) =
        let xa, ya = BigDecimal.align this y in BigDecimal(xa.Coefficient + ya.Coefficient, xa.Exponent, 0u)

    member this.Add(y: BigDecimal, c: Context) =
        // TODO: Optimize for one arg or the other being zero.
        // TODO: Optimize for differences in exponent along with the desired precision is large enough that the add is irreleveant
        // Translated the Sun Java code pretty directly.
        let result = this.Add(y)

        if c.precision = 0u
           || c.roundingMode = RoundingMode.Unnecessary then
            result
        else
            BigDecimal.round result c

    static member Add(x: BigDecimal, y: BigDecimal) = x.Add(y)
    static member Add(x: BigDecimal, y: BigDecimal, c: Context) = x.Add(y, c)
    static member (+)(x: BigDecimal, y: BigDecimal) = x.Add(y)

    member this.Subtract(y: BigDecimal) =
        let xa, ya = BigDecimal.align this y in BigDecimal(xa.Coefficient - ya.Coefficient, xa.Exponent, 0u)

    member this.Subtract(y: BigDecimal, c: Context) =
        // TODO: Optimize for one arg or the other being zero.
        // TODO: Optimize for differences in exponent along with the desired precision is large enough that the add is irreleveant
        // Translated the Sun Java code pretty directly.
        let result = this.Subtract(y)

        if c.precision = 0u
           || c.roundingMode = RoundingMode.Unnecessary then
            result
        else
            BigDecimal.round result c

    static member Subtract(x: BigDecimal, y: BigDecimal) = x.Subtract(y)
    static member Subtract(x: BigDecimal, y: BigDecimal, c: Context) = x.Subtract(y, c)
    static member (-)(x: BigDecimal, y: BigDecimal) = x.Subtract(y)
    static member (~-)(x: BigDecimal) = x.Negate()


    // Multiplication

    member this.Multiply(y: BigDecimal) =
        BigDecimal(this.Coefficient * y.Coefficient, this.Exponent + y.Exponent, 0u)

    member this.Multiply(y: BigDecimal, c: Context) = BigDecimal.round (this.Multiply(y)) c
    static member Multiply(x: BigDecimal, y: BigDecimal) = x.Multiply(y)
    static member Multiply(x: BigDecimal, y: BigDecimal, c: Context) = x.Multiply(y, c)
    static member (*)(x: BigDecimal, y: BigDecimal) = x.Multiply(y)


    // Division

    // Remove insignificant trailing zeros until the preferred exponent is reached or no more zeros can be removed
    static member private stripZerosToMatchExponent (bd: BigDecimal) (preferredExp: int64) =

        // Took this one from the OpenJDK implementation, with some minor edits
        // And then made it tail-recursive and non-mutatating for F#

        if BigInteger.Compare(BigInteger.Abs(bd.Coefficient), ArithmeticHelpers.biTen)
           >= 0
           && (int64 bd.Exponent) < preferredExp
           && bd.Coefficient.IsEven then
            let quo, rem =
                BigInteger.DivRem(bd.Coefficient, ArithmeticHelpers.biTen)

            if rem.IsZero then
                let newExp =
                    BigDecimal.checkExponentE ((int64 bd.Exponent) + 1L) quo.IsZero

                let newPrec =
                    if bd.RawPrecision > 0u then bd.RawPrecision - 1u else bd.RawPrecision

                BigDecimal.stripZerosToMatchExponent (BigDecimal(quo, newExp, newPrec)) preferredExp
            else
                bd
        else
            bd

    ///Return a BigDecimal numerically equal to this one, but with any trailing zeros removed.
    member this.StripTrailingZeros() =
        // Not needed in this code, but apparently at some point ClojureCLR needed it.
        BigDecimal.stripZerosToMatchExponent this Int64.MaxValue


    member lhs.Divide(rhs: BigDecimal, c: Context) =

        (*
            The specification talks about the division algorithm in terms of repeated subtraction.
            I'll try to re-analyze this in terms of divisions on integers.</para>
            Assume we want to divide one BigDecimal by another:
               [x,a] / [y,b] = [(x/y), a-b]

            where [x,a] signifies x is the (big)integer coefficient, a is the exponent so [x,a] has value x * 10^a.

            Here, (x/y) indicates a result rounded to the desired precision p. For the moment, assume x, y non-negative.
            We want to compute (x/y) using integer-only arithmetic, yielding a quotient+remainder q+r
            where q has up to p precision and r is used to compute the rounding.  So actually, the result will be [q, a-b+c],
            where c is some adjustment factor to make q be in the range [0,10^0).
            We will need to adjust either x or y to make sure we can compute x/y and make q be in this range.
            Let px be the precision of x (number of digits), let py be the precision of y. Then

                x = x' * 10^px
                y = y' * 10^py

            where x' and y' are in the range [.1,1).  However, we'd really like to have:

                (a) x' in [.1,1)
                (b) y' in [x',10*x')

            So that  x'/y' is in the range (.1,1].
            We can use y' as defined above if y' meets (b), else multiply y' by 10 (and decrease py by 1).
            Having done this, we now have

                x/y = (x'/y') * 10^(px-py)

            This gives us  10^(px-py-1) < x/y < 10^(px-py).
            We'd like q to have p digits of precision.  So,

                if px-py = p, ok.
                if px-py < p, multiply x by 10^(p - (px-py)).
                if px-py < p, multiply y by 10^(px-py-p).

            Using these adjusted values of x and y, divide to get q and r, round using those, then adjust the exponent.
        *)

        if c.precision = 0u then
            lhs.Divide(rhs)
        else

            let preferredExp =
                (int64 lhs.Exponent) - (int64 rhs.Exponent)

            if rhs.Coefficient.IsZero then // x/0
                if lhs.Coefficient.IsZero then
                    raise
                    <| ArithmeticException("Division undefined (0/0)") // NaN
                else
                    raise <| ArithmeticException("Division by zero") // INF

            if lhs.Coefficient.IsZero then
                let expToUse =
                    Math.Max(Math.Min(preferredExp, (int64 Int32.MaxValue)), (int64 Int32.MinValue))
                    |> int32

                BigDecimal(BigInteger.Zero, expToUse, 0u) // 0/y
            else
                let xprec = (int32 lhs.Precision)
                let yprec = (int32 rhs.Precision)

                // Determine if we need to make an adjustment to get x', y' into relation (b).

                let x = lhs.Coefficient
                let y = rhs.Coefficient

                let xtest =
                    BigInteger.Abs
                        (if xprec < yprec then
                            x
                            * ArithmeticHelpers.biPowerOfTen (uint (yprec - xprec))
                         else
                             x)

                let ytest =
                    BigInteger.Abs
                        (if xprec > yprec then
                            y
                            * ArithmeticHelpers.biPowerOfTen (uint (xprec - yprec))
                         else
                             y)

                let yAdjusted, adjust =
                    if (ytest < xtest) then y * ArithmeticHelpers.biTen, 1 else y, 0

                // Now make sure x and y themselves are in the proper range.

                let delta = (int32 c.precision) - (xprec - yprec)

                let xprime, yprime =
                    if (delta > 0) then
                        x * ArithmeticHelpers.biPowerOfTen (delta |> uint), yAdjusted
                    else
                        x,
                        yAdjusted
                        * ArithmeticHelpers.biPowerOfTen (-delta |> uint)

                let roundedInt =
                    BigDecimal.roundingDivide2 xprime yprime c.roundingMode

                let exp =
                    BigDecimal.checkExponentE (preferredExp - (int64 delta) + (int64 adjust)) roundedInt.IsZero

                let result =
                    BigDecimal.round (BigDecimal(roundedInt, exp, 0u)) c

                // Thanks to the OpenJDK implementation for pointing this out.
                // TODO: Have ROundingDivide2 return a flag indicating if the remainder is 0.  Then we can lose the multiply.

                if (result.Multiply(rhs) :> IComparable<BigDecimal>)
                    .CompareTo(lhs) = 0 then
                    BigDecimal.stripZerosToMatchExponent result preferredExp // Apply preferred scale rules for exact quotients
                else
                    result

    // This was commented out in the C# code
    // if (c.RoundingMode == RoundingMode.Ceiling ||
    //     c.RoundingMode == RoundingMode.Floor)
    // {
    //     // OpenJDK code says:
    //     // The floor (round toward negative infinity) and ceil
    //     // (round toward positive infinity) rounding modes are not
    //     // invariant under a sign flip.  If xprime/yprime has a
    //     // different sign than lhs/rhs, the rounding mode must be
    //     // changed.
    //     if ((xprime._coeff.Signum != lhs._coeff.Signum) ^
    //         (yprime._coeff.Signum != rhs._coeff.Signum))
    //     {
    //         c = new Context(c.Precision,
    //                              (c.RoundingMode == RoundingMode.Ceiling) ?
    //                              RoundingMode.Floor : RoundingMode.Ceiling);
    //     }
    // }


    member dividend.Divide(divisor: BigDecimal) =

        // Throws an exception if the rounding mode is RoundingMode.UNNECESSARY and we have a repeating fraction.

        // I completely ripped off the OpenJDK implementation.
        // I could not compete.

        if divisor.Coefficient.IsZero then // x/0
            if dividend.Coefficient.IsZero then
                raise
                <| ArithmeticException("Division undefined (0/0)") // NaN
            else
                raise <| ArithmeticException("Division by zero") // INF

        let preferredExp =
            Math.Max
                ((int64 Int32.MinValue),
                 Math.Min(int64 Int32.MaxValue, int64 dividend.Exponent)
                 - (int64 divisor.Exponent))
            |> int32

        if dividend.Coefficient.IsZero then
            BigDecimal(BigInteger.Zero, preferredExp, 0u) // 0/y
        else
            (*  OpenJDK says:
            ** If the quotient this/divisor has a terminating decimal
            ** expansion, the expansion can have no more than
            ** (a.precision() + ceil(10*b.precision)/3) digits.
            ** Therefore, create a MathContext object with this
            ** precision and do a divide with the UNNECESSARY rounding
            ** mode.
            *)
            let extraPrecision =
                Math.Ceiling(10.0 * (double divisor.Precision) / 3.0)
                |> int64

            let cPrecision =
                Math.Min((int64 dividend.Precision) + extraPrecision, (int64 Int32.MaxValue))
                |> uint

            let c =
                Context.Create(cPrecision, RoundingMode.Unnecessary)

            let quotient =
                try
                    dividend.Divide(divisor, c)
                with :? ArithmeticException ->
                    raise
                    <| ArithmeticException("Non-terminating decimal expansion; no exact representable decimal result.")

            let qExp = quotient.Exponent
            // divide(BigDecimal, mc) tries to adjust the quotient to
            // the desired one by removing trailing zeros; since the
            // exact divide method does not have an explicit digit
            // limit, we can add zeros too.
            if preferredExp < qExp
            then BigDecimal.Rescale(quotient, preferredExp, RoundingMode.Unnecessary)
            else quotient


    member this.DivideInteger(y: BigDecimal): BigDecimal =
        // I am indebted to the OpenJDK implementation for the algorithm.
        // However, the spec I'm working from specifies an exponent of zero always.
        // The OpenJDK implementation does otherwise.
        // So I modified it to yield a zero exponent.

        // If we were not going with a zero exponent, this would be the calculation
        //let preferredExp = Math.Max(Math.Min((int64 this.Exponent) - (int64 y.Exponent), (int64 Int32.MaxValue)), (int64 Int32.MaxValue)) |> int

        let preferredExp = 0

        if (this.Abs() :> IComparable<BigDecimal>)
            .CompareTo(y.Abs()) < 0 then
            BigDecimal(BigInteger.Zero, preferredExp, 0u)
        elif this.Coefficient.IsZero
             && not y.Coefficient.IsZero then
            BigDecimal.Rescale(this, preferredExp, RoundingMode.Unnecessary)
        else
            // Perform a divide with enough digits to round to a correct
            // integer value; then remove any fractional digits


            let maxDigits =
                Math.Min
                    ((int64 this.Precision)
                     + (int64 (Math.Ceiling(10.0 * (float y.Precision) / 3.0)))
                     + Math.Abs((int64 this.Exponent) - (int64 y.Exponent))
                     + 2L,
                     (int64 Int32.MaxValue))
                |> int

            let quotient =
                this.Divide(y, Context.Create(maxDigits |> uint, RoundingMode.Down))

            let quotient =
                if y.Exponent < 0 then
                    BigDecimal.stripZerosToMatchExponent
                        (BigDecimal.Rescale(quotient, 0, RoundingMode.Down))
                        (int64 preferredExp)
                else
                    quotient

            let quotient =
                if quotient.Exponent > preferredExp
                then BigDecimal.Rescale(quotient, preferredExp, RoundingMode.Unnecessary) // pad with zeros if nesary
                else quotient

            quotient

    member this.DivideInteger(y: BigDecimal, c: Context): BigDecimal =
        // I am indebted to the OpenJDK implementation for the algorithm.
        // However, the spec I'm working from specifies an exponent of zero always.
        // The OpenJDK implementation does otherwise.
        // So I modified it to yield a zero exponent.

        if c.precision = 0u
           || (this.Abs() :> IComparable<BigDecimal>)
               .CompareTo(y.Abs()) < 0 then // exact result || zero result
            this.DivideInteger(y)
        else
            // Calculate preferred Scale
            // If we were not going with a zero exponent, this would be the calculation
            //let preferredExp = Math.Max(Math.Min((int64 this.Exponent) - (int64 y.Exponent), (int64 Int32.MaxValue)), (int64 Int32.MaxValue)) |> int
            let preferredExp = 0

            (*  OpenJDK says:
            * Perform a normal divide to mc.precision digits.  If the
            * remainder has absolute value less than the divisor, the
            * integer portion of the quotient fits into mc.precision
            * digits.  Next, remove any fractional digits from the
            * quotient and adjust the scale to the preferred value.
            *)

            let result =
                this.Divide(y, Context.Create(c.precision, RoundingMode.Down))

            let resultExp = result.Exponent

            let result =
                match resultExp with
                | _ when resultExp > 0 ->
                    (*
                    * Result is an integer. See if quotient represents the
                    * full integer portion of the exact quotient; if it does,
                    * the computed remainder will be less than the divisor.
                    *)
                    let product = result.Multiply(y)
                    // If the quotient is the full integer value,
                    // |dividend-product| < |divisor|.
                    if (this.Subtract(product).Abs() :> IComparable<BigDecimal>)
                           .CompareTo(y.Abs())
                       >= 0 then
                        raise <| ArithmeticException("Diision impossible")
                    else
                        result

                | _ when resultExp < 0 ->

                    (*
                    * Integer portion of quotient will fit into precision
                    * digits; recompute quotient to scale 0 to avoid double
                    * rounding and then try to adjust, if necessary.
                    *)
                    BigDecimal.Rescale(result, 0, RoundingMode.Down)

                | _ -> result

            if preferredExp < resultExp
               && c.precision > result.Precision then
                BigDecimal.Rescale(result, 0, RoundingMode.Unnecessary)
            else
                BigDecimal.stripZerosToMatchExponent result (int64 preferredExp)

    // NB: if we were not clamping the exponent to 0, the expression above wouuld be
    //let precisionDiff = (int c.precision) - (int (result.GetPrecision()))
    //if preferredExp < resultExp && precisionDiff > 0
    //then BigDecimal.Rescale(result, resultExp + Math.Max(precisionDiff, preferredExp - resultExp), RoundingMode.Unnecessary)
    //else BigDecimal.Rescale(result, 0, RoundingMode.Unnecessary);
    // though that Math.Max looks like it alwasy taked precisionDiff because it is positive and the other expression is negative.
    // But that's what my old code comment had.

    member this.DivRem(y: BigDecimal, remainder: outref<BigDecimal>): BigDecimal =
        // x = q * y + r
        let q = this.DivideInteger(y)
        remainder <- this - q * y
        q

    member this.DivRem(y: BigDecimal, c: Context, remainder: outref<BigDecimal>): BigDecimal =
        // x = q * y + r
        if c.roundingMode = RoundingMode.Unnecessary then
            this.DivRem(y, &remainder)
        else
            let q = this.DivideInteger(y, c)
            remainder <- this - q * y
            q

    member this.Mod(y: BigDecimal): BigDecimal = let _, r = this.DivRem(y) in r
    member this.Mod(y: BigDecimal, c: Context): BigDecimal = let _, r = this.DivRem(y, c) in r
    static member Divide(x: BigDecimal, y: BigDecimal): BigDecimal = x.Divide(y)
    static member Divide(x: BigDecimal, y: BigDecimal, c: Context): BigDecimal = x.Divide(y, c)
    static member Mod(x: BigDecimal, y: BigDecimal): BigDecimal = x.Mod(y)
    static member Mod(x: BigDecimal, y: BigDecimal, c: Context): BigDecimal = x.Mod(y, c)

    static member DivRem(x: BigDecimal, y: BigDecimal, remainder: outref<BigDecimal>): BigDecimal =
        x.DivRem(y, &remainder)

    static member DivRem(x: BigDecimal, y: BigDecimal, c: Context, remainder: outref<BigDecimal>): BigDecimal =
        x.DivRem(y, c, &remainder)

    static member (/)(x: BigDecimal, y: BigDecimal) = x.Divide(y)
    static member (%)(x: BigDecimal, y: BigDecimal) = x.Mod(y)


    // Exponentiation

    member this.Power(n: int) =
        if n < 0 || n > 999999999
        then invalidArg "n" "Exponent must be between 0 and 999999999"

        let exp =
            BigDecimal.checkExponentE ((int64 this.Exponent) * (int64 n)) this.Coefficient.IsZero

        BigDecimal(BigInteger.Pow(this.Coefficient, n), exp, 0u)

    member this.Power(n: int, c: Context): BigDecimal =
        // Following the OpenJDK implementation.
        // This is an implementation of the X3.274-1996 algorithm:
        // - An ArithmeticException exception is thrown if
        // -- abs(n) > 999999999
        // -- c.precision = 0 and n < 0
        // -- c.precision > 0 and n has more than c.precision decimal digits
        // - if n is zero, ONE is returned even is the base is zero; otherwise:
        // -- if n is positive, the result is calculated via
        //    the repeated squaring technique into a single accumulator.
        //    The individual multiplications with the accumulator use the
        //    same math context settings as in c except for a
        //    precision increased to c.precision + elength + 1
        //    where elength is the number of decimal digits in n.
        // -- if n is negative, the result is calculated as if
        //    n were positive; this value is then divided into one
        //    using the working precision specified above.
        // -- The final value from either the positive or negative case
        //    is then rounded to the destination precision.

        if c.precision = 0u then
            this.Power(n)
        elif n < -999999999 || n > 999999999 then
            raise <| ArithmeticException("invalid operation")
        elif n = 0 then
            BigDecimal.One
        else
            let mutable mag = Math.Abs(n)

            let workC =
                if c.precision > 0u then
                    let elength =
                        ArithmeticHelpers.uintPrecision (uint mag)

                    if elength > c.precision then
                        raise
                        <| ArithmeticException("Invalid precision for exponentiation") // X3.274 rule

                    Context.Create((c.precision + elength + 1u), c.roundingMode)
                else
                    c
            // I suppose I could encapsulate this as a tail-recursive function. Another day
            let mutable acc = BigDecimal.One
            let mutable bitSeen = false
            for i = 1 to 31 do
                mag <- mag + mag // shift left 1 bit

                if mag < 0 then // top bit is set
                    bitSeen <- true
                    acc <- acc.Multiply(this, workC) // acc = acc*x

                if i <> 31 && bitSeen then // not the last bit
                    acc <- acc.Multiply(acc, workC) // acc = acc*acc [square]

            // if negative n, calculate the reciprocal using working precision
            if n < 0 then acc <- BigDecimal.One.Divide(acc, workC)
            // round to final precision and strip zeros
            BigDecimal.round acc c

    static member Power(x: BigDecimal, n: int): BigDecimal = x.Power(n)
    static member Power(x: BigDecimal, n: int, c: Context): BigDecimal = x.Power(n, c)


    // Shift operations

    member this.MovePointRight(n: int): BigDecimal =
        let newExp =
            BigDecimal.checkExponentE ((int64 this.Exponent) + (int64 n)) this.Coefficient.IsZero

        BigDecimal(this.Coefficient, newExp, this.RawPrecision)

    member this.MovePointLeft(n: int): BigDecimal =
        let newExp =
            BigDecimal.checkExponentE ((int64 this.Exponent) - (int64 n)) this.Coefficient.IsZero

        BigDecimal(this.Coefficient, newExp, this.RawPrecision)

    static member (<<<)(x: BigDecimal, shift: int): BigDecimal = x.MovePointLeft(shift)
    static member (>>>)(x: BigDecimal, shift: int): BigDecimal = x.MovePointRight(shift)
