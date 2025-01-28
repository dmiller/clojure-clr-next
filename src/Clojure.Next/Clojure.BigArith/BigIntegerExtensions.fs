namespace Clojure.BigArith

open System
open System.Numerics


// We need a parsing function for BigInteger that takes a radix
[<Sealed; AbstractClass>]
type BigIntegerExtensions private () =

    /// The minimum radix supported for parsing.
    [<Literal>]
    static let _minRadix = 2

    /// The maximum radix supported for parsing.
    [<Literal>]
    static let _maxRadix = 36


    /// The number of bits in one 'digit' of the magnitude.
    [<Literal>]
    static let _bitsPerDigit = 32 // uint implementation

    /// The maximum number of digits in radix [i] that will fit into a uint.
    /// RadixDigitsPerDigit[i] = floor(log_i (2^32 - 1))
    /// See the radix.xlsx spreadsheet.
    static let _radixDigitsPerDigit =
        [| 0
           0
           31
           20
           15
           13
           12
           11
           10
           10
           9
           9
           8
           8
           8
           8
           7
           7
           7
           7
           7
           7
           7
           7
           6
           6
           6
           6
           6
           6
           6
           6
           6
           6
           6
           6
           6 |]

    /// The super radix (power of given radix) that fits into a uint.
    /// SuperRadix[i] = 2 ^ RadixDigitsPerDigit[i]
    /// See the radix.xlsx spreadsheet.
    static let _superRadix =
        [| 0
           0
           0x80000000
           0xCFD41B91
           0x40000000
           0x48C27395
           0x81BF1000
           0x75DB9C97
           0x40000000
           0xCFD41B91
           0x3B9ACA00
           0x8C8B6D2B
           0x19A10000
           0x309F1021
           0x57F6C100
           0x98C29B81
           0x10000000
           0x18754571
           0x247DBC80
           0x3547667B
           0x4C4B4000
           0x6B5A6E1D
           0x94ACE180
           0xCAF18367
           0xB640000
           0xE8D4A51
           0x1269AE40
           0x17179149
           0x1CB91000
           0x23744899
           0x2B73A840
           0x34E63B41
           0x40000000
           0x4CFA3CC1
           0x5C13D840
           0x6D91B519
           0x81BF1000 |]

    /// The number of bits in one digit of radix [i] times 1024.
    /// BitsPerRadixDigit[i] = ceiling(1024*log_2(i))
    /// See the radix.xlsx spreadsheet.
    static let _bitsPerRadixDigit =
        [| 0
           0
           1024
           1624
           2048
           2378
           2648
           2875
           3072
           3247
           3402
           3543
           3672
           3790
           3899
           4001
           4096
           4186
           4271
           4350
           4426
           4498
           4567
           4633
           4696
           4756
           4814
           4870
           4923
           4975
           5025
           5074
           5120
           5166
           5210
           5253
           5295 |]


    /// The minimum radix supported for parsing.
    static member MinRadix = _minRadix

    /// The maximum radix supported for parsing.
    static member MaxRadix = _maxRadix


    /// Parse a string in the given radix, yielding a BigInteger
    static member Parse(s: string, radix: int) : BigInteger =

        let result = BigIntegerExtensions.TryParse(s, radix)

        match result with
        | Some bi -> bi
        | _ -> raise <| FormatException("Invalid integer format")


    /// Try to parse a string in the given radix, yielding a BigInteger option
    static member TryParse(s: string, radix: int) : BigInteger option =

        if radix < _minRadix || radix > _maxRadix then
            raise <| ArgumentOutOfRangeException("radix", "Radix must be between 2 and 36")

        // zero length bad,
        // hyphen only bad, plus only bad,
        // hyphen not leading bad, plus not leading bad
        // (overkill) both hyphen and minus present (one would be caught by the tests above)
        let len = s.Length
        let minusIndex = s.LastIndexOf('-')
        let plusIndex = s.LastIndexOf('+')

        if
            len = 0
            || (minusIndex = 0 && len = 1)
            || (plusIndex = 0 && len = 1)
            || (minusIndex > 0)
            || (plusIndex > 0)
        then
            None
        else
            let mutable index, sign =
                if plusIndex <> -1 then 1, 1
                elif minusIndex <> -1 then 1, -1
                else 0, 1

            // skip leading zeros
            while index < len && s[index] = '0' do
                index <- index + 1

            if index = len then
                Some BigInteger.Zero
            else
                BigIntegerExtensions.TryParseAux(s, index, sign, radix)


    static member private TryParseAux(s: string, index: int, sign: int, radix: int) : BigInteger option =
        let len = s.Length
        let numDigits = len - index

        let mutable index = index

        // We can compute size of magnitude.  May be too large by one uint.
        let numBits = ((numDigits * _bitsPerRadixDigit[radix]) >>> 10) + 1
        let numUints = (numBits + _bitsPerDigit - 1) / _bitsPerDigit

        let groupSize = _radixDigitsPerDigit[radix]

        // the first group may be short
        // the first group is the initial value for _data.

        let firstGroupLen =
            match numDigits % groupSize with
            | 0 -> groupSize // exact multiple, so full size
            | _ as n -> n // short group

        match BigIntegerExtensions.TryParseUInt(s, index, firstGroupLen, radix) with
        | None -> None
        | Some firstDigit ->
            let mutable result = BigInteger(firstDigit)

            index <- index + firstGroupLen

            let mult = BigInteger(_superRadix[radix])

            let rec loop (index: int) =
                if index >= len then
                    let signedResult = if sign < 0 then -result else result
                    Some signedResult
                else
                    match BigIntegerExtensions.TryParseUInt(s, index, groupSize, radix) with
                    | None -> None
                    | Some digit ->
                        result <- result * mult + BigInteger(digit)
                        loop (index + groupSize)

            loop index


    /// Convert a substring in a given radix to its equivalent numeric value as a UInt32
    /// The length of the substring must be small enough that the converted value is guaranteed to fit into a uint.
    static member private TryParseUInt(value: string, startIndex: int, len: int, radix: int) : uint option =

        if radix < _minRadix || radix > _maxRadix then
            raise <| ArgumentOutOfRangeException("radix", "Radix must be between 2 and 36")

        let rec loop (i: int) (result: uint) =
            if i >= len then
                Some result
            else
                match BigIntegerExtensions.TryComputeDigitVal(value.[startIndex + i], radix) with
                | Some u ->
                    let result = result * (uint radix) + u
                    loop (i + 1) result
                | None -> None

        loop 0 0u

    /// Convert an (extended) digit to its value in the given radix.
    static member private TryComputeDigitVal(c: char, radix: int) : uint option =
        let v =
            if '0' <= c && c <= '9' then uint c - uint '0'
            elif 'a' <= c && c <= 'z' then uint c - uint 'a' + 10u
            elif 'A' <= c && c <= 'Z' then uint c - uint 'A' + 10u
            else UInt32.MaxValue

        if v < uint radix then Some v else None
