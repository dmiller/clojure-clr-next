namespace Modding

open BenchmarkDotNet.Attributes
open System

type Modders() =

    // if you'd need to convince yourself experimentally that the three methods are equivalent:
    static member compare(numiters) =
        let r = Random()
        for i = 0 to 20 do
            let k = int(r.NextInt64(0,1_000_000))
            let km1 = k-1
            let m = km1 - (km1 % 32)
            let d = (km1 / 32) *  32
            let b = (((k-1)>>>5) <<<5)
            printfn "%i %i %i %i %A %A" k m b d (m=b) (m=d)


    static member doMod(i) =
        let im1 = i - 1
        im1 - (im1 % 32) 
    

    static member doDiv(i) =
        (( i - 1) / 32) *  32
            
    static member doShift(i) =
        ((i-1) >>> 5) <<< 5



    static member doMod64(i:int64) =
        let im1 = i - 1L
        im1 - (im1 % 32L) 
    

    static member doDiv64(i:int64) =
        (( i - 1L) / 32L) *  32L
            
    static member doShift64(i:int64) =
        ((i-1L) >>> 5) <<< 5


