module ArrayCreation

let ZeroCreateArray(n:int) : obj array = Array.zeroCreate n

let CreateArray(n:int) : obj array = Array.create n null

let CloneArray(a:obj array) : obj array = a.Clone() :?> obj array

let ArrayClone(a: obj array) : obj array = Array.copy a

let SystemArrayCreateInstance(n:int) : obj array = System.Array.CreateInstance( typeof<obj>, n) :?> obj array

//let Naked(n:int) : obj array =  (# "newarr !0" typeof<obj> (obj) n : obj array #)

