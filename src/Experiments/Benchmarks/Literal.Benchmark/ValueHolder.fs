module VH

open System

[<Literal>]
let public LitConst = 17

let f() = DateTime.Now

let public moduleLet = f()

type ValueHolder()  = 
    
    let letVar : int = 17

    member public this.GetLetVar = letVar

    static member val public StaticVal : int = 17

    member val public NonstaticVal : int = 17
   