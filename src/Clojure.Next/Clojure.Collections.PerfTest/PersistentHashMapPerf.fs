module PersistentHashMapPerf

type PHMTransientConj() =



    member this.FirstTransientConj(size:int) =
        let mutable pv = clojure.lang.PersistentHashMap.EMPTY.asTransient () :?> clojure.lang.ITransientAssociative

        for i in 0 .. size do
            pv <- pv.assoc (i,i)

        pv.persistent ()


    member this.NextTransientConj(size:int) =
        let mutable pv =
            (Clojure.Collections.PersistentHashMap.Empty :> Clojure.Collections.IEditableCollection) 
                .asTransient () :?> Clojure.Collections.ITransientAssociative

        for i in 0 .. size do
            pv <- pv.assoc(i,i)

        pv.persistent ()

    member this.AlternateTransientConj(size:int) =
        let mutable pv =
            (Clojure.Collections.Alternate.PHashMap.Empty :> Clojure.Collections.IEditableCollection) 
                .asTransient () :?> Clojure.Collections.ITransientAssociative

        for i in 0 .. size do
            pv <- pv.assoc(i,i)

        pv.persistent ()


    member this.FirstEquivInt(x:int, y:int) =
        clojure.lang.Numbers.equal( box x, box y)


    member this.NextEquivInt(x:int, y:int) =
        Clojure.Numerics.Numbers.equal( box x, box y)
