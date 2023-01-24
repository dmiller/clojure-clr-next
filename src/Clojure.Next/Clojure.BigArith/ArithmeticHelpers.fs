namespace Clojure.BigArith

open System
open System.Numerics


module internal ArithmeticHelpers =

    // BigInteger helpers

    let biFive = BigInteger(5)
    let biTen = BigInteger(10)

    let getBIPrecision (bi: BigInteger) =
        if bi.IsZero then
            1u
        else
            let signFix = if bi.Sign < 0 then 1u else 0u
            (bi.ToString().Length |> uint) - signFix


    // I would do this, but we end up with a one-off error on exact powers of 10 due to Log10 inexactness
    //if bi.IsZero
    //then 1u
    //else
    //    let log = BigInteger.Log10 (if bi.Sign <= 0 then -bi else bi)
    //    1u+((Math.Floor(log) |> uint32)

    let biPowersOfTen =
        [| BigInteger.One
           BigInteger(10)
           BigInteger(100)
           BigInteger(1000)
           BigInteger(10000)
           BigInteger(100000)
           BigInteger(1000000)
           BigInteger(10000000)
           BigInteger(100000000)
           BigInteger(1000000000)
           BigInteger(10000000000L)
           BigInteger(100000000000L) |]

    let biPowerOfTen (n: uint) =
        if n < uint biPowersOfTen.Length then
            biPowersOfTen.[(int n)]
        else
            let buf = Array.create ((int n) + 1) '0'
            buf.[0] <- '1'
            BigInteger.Parse(String(buf))


    // double representation

    /// Exponent bias in the 64-bit floating point representation.
    let doubleExponentBias = 1023

    /// The size in bits of the significand in the 64-bit floating point representation.
    let doubleSignificandBitLength = 52

    /// How much to shift to accommodate the exponent and the binary digits of the significand.
    let doubleShiftBias =
        doubleExponentBias + doubleSignificandBitLength

    /// Extract the sign bit from a byte-array representaition of a double.
    let getDoubleSign (v: byte []) = v.[7] &&& 0x80uy

    /// Extract the significand (AKA mantissa, coefficient without the implied 52nd digit 1) from a byte-array representation of a double.
    let getDoubleSignificand (v: byte []) =
        let i1 =
            (uint v.[0])
            ||| ((uint v.[1]) <<< 8)
            ||| ((uint v.[2]) <<< 16)
            ||| ((uint v.[3]) <<< 24)

        let i2 =
            (uint v.[4])
            ||| ((uint v.[5]) <<< 8)
            ||| ((uint (v.[6] &&& 0xFuy)) <<< 16)

        uint64 i1 ||| ((uint64 i2) <<< 32)

    /// Extract the (baised) exponent from a byte-array representation of a double.
    let getDoubleBiasedExponent (v: byte []) =
        ((uint16 (v.[7] &&& 0x7fuy)) <<< 4)
        ||| ((uint16 (v.[6] &&& 0xF0uy)) >>> 4)

    type DoubleData =
        | Zero of isPositive: bool // +/- 0
        | Denormalized of isPositive: bool * fraction: uint64 * exponent: int // Denormalized  +/- 2^-1022 x 0.fraction  -1022 passed in exponent
        | Infinity of isPositive: bool // =/1 infinity
        | NaN of significand: uint64 // NaN
        | Standard of isPositive: bool * mantissa: uint64 * exponent: int * isSignificandZero: bool
    // +/- 2^exponent * mantissa
    // isSigificandZero implies mantissa = 1000....00
    // allows special case of 1 << some power

    let deconstructDouble (v: double): DoubleData =
        let dbytes = BitConverter.GetBytes(v)
        let significand = getDoubleSignificand dbytes
        let biasedExp = getDoubleBiasedExponent dbytes
        let isPositive = getDoubleSign dbytes = 0uy
        let setBit52 s = s ||| 0x10000000000000UL

        match biasedExp with
        | 0us when significand = 0UL -> Zero(isPositive)
        | 0us -> Denormalized(isPositive = isPositive, fraction = significand, exponent = (-doubleExponentBias + 1))
        | 0x7ffus when significand = 0UL -> Infinity(isPositive)
        | 0x7ffus -> NaN(significand = significand)
        | _ ->
            Standard
                (isPositive = isPositive,
                 mantissa = setBit52 significand,
                 exponent = (int biasedExp) - doubleExponentBias,
                 isSignificandZero = (significand = 0UL))




    /// Given a Decimal value, return a BigInteger with the mantissa and the exponent.
    let deconstructDecimal (v: decimal): BigInteger * int =
        if v = 0m then
            BigInteger.Zero, 0
        else
            let ints = Decimal.GetBits(v)
            let sign = if v < 0m then -1 else 1
            let exp = (ints.[3] &&& 0x00FF0000) >>> 16
            let byteLength = Buffer.ByteLength(ints) - 4
            let bytes: byte array = Array.zeroCreate byteLength
            Buffer.BlockCopy(ints, 0, bytes, 0, byteLength)

            let coeff =
                BigInteger(ReadOnlySpan(bytes), true, false)

            let coeff = if sign = -1 then -coeff else coeff
            coeff, exp


    // Miscellaneous

    let uintlogTable =
        [ 0u
          9u
          99u
          999u
          9999u
          99999u
          999999u
          9999999u
          99999999u
          999999999u
          UInt32.MaxValue ]

    /// Log base 10 of a uint
    let uintPrecision v =
        // Algorithm from Hacker's Delight, section 11-4
        // except they use a for-loop, of course
        match v with
        | 0u -> 1u
        | _ ->
            uintlogTable
            |> List.findIndex (fun x -> v <= x)
            |> uint
