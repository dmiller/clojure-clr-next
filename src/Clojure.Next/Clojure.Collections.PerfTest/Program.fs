open PersistentHashMapPerf




let perfClass = PHMTransientConj()

let sizes = [ 15; 16; 17; 18;]

let iterCount = 100000

let mutable acc : obj = null


for size in sizes do
    for i = 0 to iterCount do
        acc <- perfClass.FirstTransientConj(size) 



