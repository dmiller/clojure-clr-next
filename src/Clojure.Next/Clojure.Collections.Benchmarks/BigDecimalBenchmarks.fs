module BigDecimalBenchmarks

open System.Numerics

open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Diagnosers
open System

let getBIPrecisionString (bi: BigInteger) =
    if bi.IsZero then
        1u
    else
        let signFix = if bi.Sign < 0 then 1u else 0u
        (bi.ToString().Length |> uint) - signFix


let getCljBIPrecisionString (bi: clojure.lang.BigInteger) =
    if bi.IsZero then
        1u
    else
        let signFix = if bi.Signum < 0 then 1u else 0u
        (bi.ToString().Length |> uint) - signFix

let uintLogTable : uint array =
    [|      
        0u;
        9u;
        99u;
        999u;
        9999u;
        99999u;
        999999u;
        9999999u;
        99999999u;
        999999999u;
        UInt32.MaxValue
    |]

// Algorithm from Hacker's Delight, section 11-4
let uintPrecision (v: uint) =
    let rec loop i =
        if v <= uintLogTable[int i] then
            i
        else
            loop (i + 1u)
    loop 1u

[<Literal>]
let BitsPerUintDigit = 32

let inPlaceDivRem (data: uint[], index: int, divisor: uint) =
    let mutable rem = 0UL
    let mutable seenNonZero = false
    let mutable retIndex = 0
    let len = data.Length

    for i in index .. len - 1 do
        rem <- rem <<< BitsPerUintDigit
        rem <- rem ||| uint64 data.[i]
        let q : uint = uint (rem / uint64 divisor)
        data.[i] <- q
        if q = 0u then
            if not seenNonZero then
                retIndex <- i + 1
        else
            seenNonZero <- true
        rem <- rem % uint64 divisor

    uint rem, retIndex
            

        //static uint InPlaceDivRem(uint[] data, ref int index, uint divisor)
        //{
        //    ulong rem = 0;
        //    bool seenNonZero = false;
        //    int len = data.Length;
        //    for ( int i=index; i<len; i++ )
        //    {
        //        rem <<= BitsPerDigit;
        //        rem |= data[i];
        //        uint q = (uint)(rem/divisor);
        //        data[i] = q;
        //        if (  q == 0 )
        //        {
        //            if ( ! seenNonZero )
        //                index++;
        //        }
        //        else
        //            seenNonZero = true;
        //        rem %= divisor;
        //    }
        //    return (uint)rem;
        //}

let byteArrayToUintArray (bArray: byte[]) =
    let uiArray : uint array = Array.zeroCreate (Math.Ceiling((float bArray.Length) / 4.0) |> int)
    Buffer.BlockCopy(bArray,0, uiArray, 0, bArray.Length)

    let uiArray2 : uint array = Array.zeroCreate (Math.Ceiling((float bArray.Length) / 4.0) |> int)

    for i in 0 .. uiArray.Length - 1 do
        let len = bArray.Length - i * 4
        let span = ReadOnlySpan(bArray, i * 4, len)
        uiArray2.[i] <- BitConverter.ToUInt32(span)

    uiArray


let getBIPrecisionUArray(bi:BigInteger) =
    if bi.IsZero then
        1u
    else
        let uiArray = byteArrayToUintArray (bi.ToByteArray(false,true))
        
        let rec loop i (digits:uint) =
            if i = uiArray.Length - 1 then
                digits + uintPrecision (uint uiArray.[i])
            elif i >= uiArray.Length-1 then
                digits
            else
                let rem, newIndex = inPlaceDivRem(uiArray, i, 1000000000u)
                loop newIndex (digits + 9u)

        loop 0 0u




        //public uint Precision
        //{
        //    get
        //    {
        //        if (IsZero)
        //            return 1;  // 0 is one digit

        //        uint digits = 0;
        //        uint[] work = GetMagnitude();  // need a working copy.
        //        int index=0;
        //        while (index < work.Length-1 )
        //        {
        //            InPlaceDivRem(work,ref index,1000000000U);
        //            digits += 9;
        //        }

        //        if (index == work.Length - 1)
        //            digits += UIntPrecision(work[index]);

        //        return digits;                    
        //    }
        //}



type BigDecimalBenmark() =


    [<Params(1,5, 10, 50,  100, 1_000)>]
    member val size: int = 0 with get, set

    member val biCL: clojure.lang.BigInteger = null with get, set

    [<DefaultValue>]
    val mutable biSN: System.Numerics.BigInteger

    [<GlobalSetup>]
    member this.GlobalSetup() =
        let mutable sn =  BigInteger.One
        let mutable cl = clojure.lang.BigInteger.One

        for i = 1 to this.size do
            sn <- sn * BigInteger(10)
            cl <- cl.Multiply(clojure.lang.BigInteger.Ten)
        
        this.biCL <- cl
        this.biSN <- sn
        
        

    [<Benchmark>]
    member this.CljGetPrecisionDirect() = this.biCL.Precision
    
    [<Benchmark>]
    member this.SnGetPrecisionString() = getBIPrecisionString this.biSN
        
    [<Benchmark>]
    member this.CljGetPrecisionString() = getCljBIPrecisionString this.biCL

