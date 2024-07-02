open PersistentHashMapPerf

open Clojure.Numerics
open Clojure.Collections

//Initializer.init() |> ignore

//let perfClass = PHMTransientConj()

//let sizes = [ 15; 16; 17; 18;]

//let iterCount = 100000

//let mutable acc : obj = null

//let y = perfClass.NextCons(100)
//printf "Count is %d" ((y:>Counted).count())

//let y = perfClass.NextTransientConj(100)
//printf "Count is %d" ((y:?>Counted).count())



//for size in sizes do
//    for i = 0 to iterCount do
//        acc <- perfClass.FirstTransientConj(size) 




//let mutable x : bool = false
//for i = 1 to 1_000_000 do
//    x <-  perfClass.FirstEquivInt(i , i+1)


//for i = 1 to 1_000_000 do
//    x <-  perfClass.NextEquivInt(i , i+1)


let  size: int = 0 

let mutable firstMap : clojure.lang.IPersistentMap = null
let mutable nextMap : Clojure.Collections.IPersistentMap = null

let createFirst(n) =
    let mutable pv = clojure.lang.PersistentHashMap.EMPTY.asTransient () :?> clojure.lang.ITransientAssociative

    for i in 0 .. n do
        //pv <- pv.assoc (i,i)
        pv <- pv.assoc ("a"+i.ToString(),i)

    pv.persistent ()

let createNext(n) =
    let mutable pv = Clojure.Collections.PersistentHashMap.Empty :> Clojure.Collections.IPersistentMap 
           

    for i in 0 .. n do
        //pv <- pv.assoc (i,i)
        pv <- pv.assoc ("a"+i.ToString(),i)

    pv




let pv = createNext(500)

(pv :?> PersistentHashMap).printContents()

System.Console.ReadLine() |> ignore