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

