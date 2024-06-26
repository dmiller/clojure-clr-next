﻿module ArrayCreation

let ZeroCreateArray(n:int) : obj array = Array.zeroCreate n

let ZeroCreateArrayFixed() : obj array = Array.zeroCreate 32

let CreateArray(n:int) : obj array = Array.create n null


// 4-6X slower -- ignoring

//let CloneArray(a:obj array) : obj array = a.Clone() :?> obj array
//let ArrayClone(a: obj array) : obj array = Array.copy a
//let SystemArrayCreateInstance(n:int) : obj array = System.Array.CreateInstance( typeof<obj>, n) :?> obj array


