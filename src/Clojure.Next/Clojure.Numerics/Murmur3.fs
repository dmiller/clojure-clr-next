namespace Clojure.Numerics

open System
open System.Collections


/// <summary>
/// Implements Murmur3 hashing algorithm
/// </summary>
/// <remarks>
/// <para>The ClojureJVM version imported the Guava Murmur3 implementation and made some changes.</para>
/// <para>I copied the API stubs, then implemented the API based on the algorithm description at
///     http://en.wikipedia.org/wiki/MurmurHash.
///     See also: https://github.com/aappleby/smhasher.</para>
/// <para>Because the original algorithm was based on unsigned arithmetic,
/// I built methods that implemented those directly, then built versions
/// returning signed integers, as required by most users.</para>
/// <para>Implementation of HashUnordered and HashOrdered are deferred to Clojure.Collections.Util because as we use them, they require Clojure datatype info.</para>
/// </remarks>

// this could have been a module except it was orginally designed with overloaded methods


[<AbstractClass; Sealed>]
type Murmur3 =

    static member val private Seed = 0u
    static member val private C1 = 0xcc9e2d51u
    static member val private C2 = 0x1b873593u
    static member val private R1 = 15
    static member val private R2 = 13
    static member val private M = 5
    static member val private N = 0xe6546b64u

    // hashing and combining functions

    static member internal rotateLeft (x: uint) (n: int) : uint = (x <<< n) ||| (x >>> (32 - n))

    static member internal mixKey(key: uint) : uint =
        let k = key * Murmur3.C1
        let k = Murmur3.rotateLeft k Murmur3.R1
        let k = k * Murmur3.C2
        k

    static member internal mixHash (hash: uint) (key: uint) : uint =
        let h = hash ^^^ key
        let h = Murmur3.rotateLeft h Murmur3.R2
        let h = h * (uint Murmur3.M) + Murmur3.N
        h

    // Finalization mix - force all bits of a hash block to avalanche
    static member internal finalize (hash: uint) (length: int) : uint =
        let h = hash ^^^ (uint length)
        let h = h ^^^ (h >>> 16)
        let h = h * 0x85ebca6bu
        let h = h ^^^ (h >>> 13)
        let h = h * 0xc2b2ae35u
        let h = h ^^^ (h >>> 16)
        h

    static member finalizeCollHash (hash: uint) (count: int) : uint =
        let h1 = Murmur3.Seed
        let k1 = Murmur3.mixKey hash
        let h = Murmur3.mixHash h1 k1
        Murmur3.finalize h count

    static member internal mixCollHashU (hash: uint) (count: int) : uint = Murmur3.finalizeCollHash hash count

    static member mixCollHash (hash: int) (count: int) : int =
        Murmur3.mixCollHashU (uint hash) count |> int


    // uint-returning API

    static member HashIntU(input: uint) : uint =
        match input with
        | 0u -> 0u
        | _ ->
            let key = Murmur3.mixKey (input)
            let hash = Murmur3.mixHash Murmur3.Seed key
            Murmur3.finalize hash 4

    static member HashIntU(input: int) : uint = Murmur3.HashIntU(uint input)

    static member HashLongU(input: uint64) : uint =
        match input with
        | 0UL -> 0u
        | _ ->
            let low = uint input
            let high = uint (input >>> 32)

            let lkey = Murmur3.mixKey low
            let lhash = Murmur3.mixHash Murmur3.Seed lkey

            let hkey = Murmur3.mixKey high
            let hash = Murmur3.mixHash lhash hkey

            Murmur3.finalize hash 8

    static member HashLongU(input: int64) = Murmur3.HashLongU(uint64 input)

    static member HashStringU(input: string) =
        // step through two characters at a time
        let rec loop idx h =
            if idx >= input.Length then
                h
            else
                let key = (uint input.[idx - 1]) ||| ((uint input.[idx]) <<< 16)

                let key = Murmur3.mixKey key
                loop (idx + 2) (Murmur3.mixHash h key)

        let hash = loop 1 Murmur3.Seed

        let hash =
            // deal with remaining character if length is odd
            if input.Length &&& 1 = 1 then
                let key = uint input.[input.Length - 1]
                let key = Murmur3.mixKey key
                hash ^^^ key
            else
                hash

        Murmur3.finalize hash (2 * input.Length)

    // int-return api

    static member HashInt(input: int) : int = Murmur3.HashIntU(uint input) |> int
    static member HashLong(input: int64) : int = Murmur3.HashLongU(uint64 input) |> int
    static member HashString(input: string) : int = Murmur3.HashStringU input |> int
