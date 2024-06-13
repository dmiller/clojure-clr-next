open PersistentHashMapPerf




let perfClass = PHMTransientConj()

//let sizes = [ 15; 16; 17; 18;]

//let iterCount = 100000

//let mutable acc : obj = null


//for size in sizes do
//    for i = 0 to iterCount do
//        acc <- perfClass.FirstTransientConj(size) 


System.Threading.Thread.Sleep(10000)

let mutable x : bool = false
for i = 1 to 1_000_000 do
    x <-  perfClass.FirstEquivInt(i , i+1)

System.Threading.Thread.Sleep(1000)

for i = 1 to 1_000_000 do
    x <-  perfClass.NextEquivInt(i , i+1)

