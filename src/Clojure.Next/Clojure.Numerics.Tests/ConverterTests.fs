module ConverterTests

open Expecto
open Clojure.Numerics.Converters

let integerTestValues: obj list =
    [ 97y; 97y; 97s; 97us; 97; 97u; 97L; 97UL; 'a'; "97" ]

let floatTestValuesForIntegerConverters: obj list = [ 97.2f; 97.2; 97.2M ]


let floatTestValuesForFloatConverters: obj list = [ 97.2f; 97.2; 97.2M; "97.2" ]

let charTestValues: obj list =
    List.append [ 97y :> obj; 97y; 97s; 97us; 97; 97u; 97L; 97UL; 'a'; "a" ] floatTestValuesForIntegerConverters

let converterTest (converter: obj -> 'T) (value: 'T) (arg: obj) =
    let n = converter arg
    Expect.equal n value "Value should match"

let inline floatEqual (delta: ^T) (exp: ^T) (actual: ^T) =
    let diff = exp - actual
    let relDiff = diff / actual
    relDiff < delta && relDiff > -delta

let inline deltaConverterTest (converter: obj -> 'T) (value: 'T) (delta: 'T) (arg: obj) =
    let n = converter arg
    Expect.isTrue (floatEqual delta n value) "Value should match"

let intTest (tester: obj -> unit) =
    for v in integerTestValues do
        tester v

    for v in floatTestValuesForIntegerConverters do
        tester v

let inline floatTestAgainstIntegers (tester: obj -> unit) =
    for v in integerTestValues do
        tester v

let inline floatTestAgainstFloats (tester: obj -> unit) =
    for v in floatTestValuesForFloatConverters do
        tester v


[<Tests>]
let test1 =
    testList
        "Test converters"
        [ testCase "intConverters"
          <| fun _ ->
              intTest (converterTest convertToInt 97)
              intTest (converterTest convertToUInt 97u)
              intTest (converterTest convertToLong 97L)
              intTest (converterTest convertToULong 97UL)
              intTest (converterTest convertToShort 97s)
              intTest (converterTest convertToUShort 97us)
              intTest (converterTest convertToByte 97uy)
              intTest (converterTest convertToSByte 97y)

          testCase "charConverter"
          <| fun _ ->
              let tester = converterTest convertToChar 'a'

              for v in charTestValues do
                  tester v

          ftestCase "floatConverters"
          <| fun _ ->
              floatTestAgainstIntegers (deltaConverterTest convertToFloat 97f 1e-6f)
              floatTestAgainstFloats (deltaConverterTest convertToFloat 97.2f 1e-6f)
              floatTestAgainstIntegers (deltaConverterTest convertToDouble 97.0 1e-6)
              floatTestAgainstFloats (deltaConverterTest convertToDouble 97.2 1e-6)
              floatTestAgainstIntegers (deltaConverterTest convertToDecimal 97.0M 0.000001M)
              floatTestAgainstFloats (deltaConverterTest convertToDecimal 97.2M 0.000001M) ]
