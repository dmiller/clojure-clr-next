module Dispatchers

open System


type Ops = 
    abstract combine: y:Ops -> Ops
    abstract opsWith: x:Ops0 -> Ops
    abstract opsWith: x:Ops1 -> Ops
    abstract opsWith: x:Ops2 -> Ops
    abstract opsWith: x:Ops3 -> Ops
    abstract opsWith: x:Ops4 -> Ops

and Ops0() =
    interface Ops with
        member this.combine y = y.opsWith this
        member this.opsWith (x:Ops0) : Ops = OpsImpl.Impl0
        member this.opsWith (x:Ops1) : Ops  = OpsImpl.Impl1
        member this.opsWith (x:Ops2) : Ops = OpsImpl.Impl2
        member this.opsWith (x:Ops3) : Ops = OpsImpl.Impl3
        member this.opsWith (x:Ops4) : Ops = OpsImpl.Impl4

and Ops1() =
    interface Ops with
        member this.combine y = y.opsWith this
        member this.opsWith (x:Ops0) : Ops = this
        member this.opsWith (x:Ops1) : Ops = this
        member this.opsWith (x:Ops2) : Ops = this
        member this.opsWith (x:Ops3) : Ops = this
        member this.opsWith (x:Ops4) : Ops = this

and Ops2() =
    interface Ops with
        member this.combine y = y.opsWith this
        member this.opsWith (x:Ops0) : Ops = this
        member this.opsWith (x:Ops1) : Ops = OpsImpl.Impl1
        member this.opsWith (x:Ops2) : Ops = this
        member this.opsWith (x:Ops3) : Ops = OpsImpl.Impl2
        member this.opsWith (x:Ops4) : Ops = OpsImpl.Impl4
and Ops3() =
    interface Ops with
        member this.combine y = y.opsWith this
        member this.opsWith (x:Ops0) : Ops = this
        member this.opsWith (x:Ops1) : Ops = OpsImpl.Impl1
        member this.opsWith (x:Ops2) : Ops = OpsImpl.Impl2
        member this.opsWith (x:Ops3) : Ops = this
        member this.opsWith (x:Ops4) : Ops  = OpsImpl.Impl4

and Ops4() =
    interface Ops with
        member this.combine y = y.opsWith this
        member this.opsWith (x:Ops0) : Ops = this
        member this.opsWith (x:Ops1) : Ops = OpsImpl.Impl1
        member this.opsWith (x:Ops2) : Ops = this
        member this.opsWith (x:Ops3) : Ops = this
        member this.opsWith (x:Ops4) : Ops = this

and OpsImpl() =    
    static member Impl0 = Ops0()
    static member Impl1 = Ops1()
    static member Impl2 = Ops2()
    static member Impl3 = Ops3()
    static member Impl4 = Ops4()



let typeOps(x:obj) : Ops =
    match x with
    | :? int64 -> OpsImpl.Impl0
    | :? float -> OpsImpl.Impl1
    | :? char -> OpsImpl.Impl2
    | :? int32 -> OpsImpl.Impl3
    | :? decimal -> OpsImpl.Impl4
    | _ -> OpsImpl.Impl0

let typeCombine(x:obj, y:obj) : Ops = typeOps(x).combine(typeOps(y))



type Category =
    | C0 = 0
    | C1 = 1
    | C2 = 2
    | C3 = 3
    | C4 = 4

let row0 : Category array = [| Category.C0; Category.C1; Category.C2; Category.C3; Category.C4 |]
let row1 : Category array = [| Category.C1; Category.C1; Category.C1; Category.C1; Category.C1 |]
let row2 : Category array = [| Category.C2; Category.C1; Category.C2; Category.C2; Category.C4 |]
let row3 : Category array = [| Category.C3; Category.C1; Category.C2; Category.C3; Category.C4 |]
let row4 : Category array = [| Category.C4; Category.C1; Category.C4; Category.C4; Category.C4 |]
let categoryTable : Category array array = [| row0; row1; row2; row3; row4 |]


let lookitup (i:int) (j:int) : Category = categoryTable[i][j]
    
let categoryTable2D = Array2D.init<Category> 5 5 lookitup

let categoryToOps : Ops array = [| OpsImpl.Impl0; OpsImpl.Impl1; OpsImpl.Impl2; OpsImpl.Impl3; OpsImpl.Impl4 |]

let lookupOps(x:obj) : Category =
    match x with
    | :? int64 -> Category.C0
    | :? float -> Category.C1
    | :? char -> Category.C2
    | :? int32 -> Category.C3
    | :? decimal -> Category.C4

let lookupCombine2D(x:obj, y:obj) : Ops = categoryToOps[int(categoryTable2D[int(lookupOps(x)),int(lookupOps(y))])]




