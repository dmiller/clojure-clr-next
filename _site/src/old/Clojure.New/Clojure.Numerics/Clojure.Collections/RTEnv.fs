namespace Clojure.Collections

open System
open System.IO

// WIP: Rough design for setting up some settable variables that help define the runtime environment.
// I would have put them into the RT and Util modules, but you cannot put mutablet let bindings in a recursive module.
// One solution would be to combine RT and Util into a single class or otherwise reconfiger, get rid of the need for recursive modules there
//  and move this stuff back in with them.
//
//  But I'm delaying that decision for now.  I'm guessing there will be more design work around configurable runtime environment.


module RTEnv =


    // The Clojure initialization will have to add the types System.Numeric.BigInteger, Clojure.Numerics.BigDecimal, Clojure.Numerics.BigRational, Clojure.BigInt
    let mutable private extraNumericTypes: Type list = List.empty

    let addExtraNumericTypes (ts: Type seq) =
        extraNumericTypes <- extraNumericTypes |> List.append (Seq.toList (ts))

    let removeExtraNumericType (ts: Type seq) =
        extraNumericTypes <- extraNumericTypes |> List.except ts

    let isExtraNumericType (t: Type) = extraNumericTypes |> List.contains t



    // Similarly, we need to provide a method for comparing numeric types for equality
    let dummyNumericEquality (x: obj, y: obj): bool = x.Equals(y)


    let mutable internal numericEqualityFn: (obj * obj) -> bool = dummyNumericEquality
    let setNumericEqualityFn (neFn: ((obj * obj) -> bool)) = numericEqualityFn <- neFn

    let numericEquals (a: obj, b: obj) = numericEqualityFn (a, b)



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

    let mutable hashNumberFn: obj -> int = baseHashNumber
    let setHashNumberFn (hnFn: obj -> int) = hashNumberFn <- hnFn

    // printing



    // the real printer to use in Clojure requires a lot of Clojure infrastructure.
    // We provide a base printer that can be used as a default case later.
    // The initialization of the Clojure environment will have to install its own printer.


    type PrintFnType = (obj * TextWriter) -> unit

    let dummyPrinter: PrintFnType = fun (o, tw) -> tw.Write(o.ToString())


    let mutable internal metaPrinterFn: PrintFnType = dummyPrinter
    let setMetaPrintFn (prfn: PrintFnType): unit = metaPrinterFn <- prfn


    let mutable internal printFn: PrintFnType = dummyPrinter
    let setPrintFn (prfn: PrintFnType): unit = printFn <- prfn


    let mutable isInitialized: bool = false
