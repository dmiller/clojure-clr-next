namespace Clojure.Collections

open System
open System.Collections

// A persistent Red-Black Tree implementation
// See Okasake, Kahrs, Larsen, et al.


[<AllowNullLiteral>]
type PersistentTreeMap private (_meta: IPersistentMap, _comp: IComparer, _tree: Node, _count: int) =
    inherit APersistentMap()

    new(meta, comp) = PersistentTreeMap(meta, comp, null, 0)
    new(comp) = PersistentTreeMap(null, comp)
    new() = PersistentTreeMap(PersistentTreeMap.defaultComparer)

    static member val Empty = new PersistentTreeMap()

    static member val defaultComparer = 
        { new Object() with 
            override _.ToString() = "#<Default comparer>"
          interface IComparer with 
            member _.Compare(x, y) = Util.compare(x,y)
          //member _.compare(x, y) = Util.compare(x,y) -- can't do this. needed for core.clj compatbility -- we can get around this when we get there
        }

    // Create a PersistentTreeMap from a dictionary
    static member Create(dict: IDictionary) =
        let mutable ret = PersistentTreeMap.Empty :> Associative
        for e in dict do
            let kvp = e :?> DictionaryEntry
            ret <- ret.assoc(kvp.Key, kvp.Value)
        ret :?> PersistentTreeMap

    static member Create(items: ISeq) = 
        let rec loop (s: ISeq) (ret: Associative) =
            match s with
            | null -> ret :?> PersistentTreeMap
            | _ when isNull <| s.next() -> raise <| new ArgumentException($"No value supplied for key: {items.first()}")
            | _ -> loop (s.next().next()) (ret.assoc(s.first(), s.next().first()))
        loop items PersistentTreeMap.Empty


    override _.Equals (o: obj): bool = 
        try 
            base.Equals(o: obj)
        with
        | :? InvalidCastException -> false

    override _.GetHashCode (): int = 
        base.GetHashCode()


    interface IObj with
        override this.withMeta (meta: IPersistentMap): IObj = 
            if Object.ReferenceEquals(meta,_meta) then
                this
            else
                PersistentTreeMap(meta, _comp, _tree, _count)

    interface IMeta with
        member _.meta() = _meta

    interface Associative with
        member this.containsKey (key: obj): bool = not <| isNull this.NodeAt(key) 
        member this.entryAt (key: obj): IMapEntry = this.NodeAt(key)

    interface ILookup with
        member this.valAt (key: obj): obj = (this:>ILookup).valAt(key, null)
        member this.valAt (key: obj, notFound: obj): obj = 
            let n = this.NodeAt(key)
            if isNull n then notFound else n.Value

    interface IPersistentCollection with
        override _.equiv (arg: obj): bool = 
            try 
                base.doEquiv(arg)
            with
            | :? InvalidCastException -> false
        override _.count() = _count
        override this.empty() = PersistentTreeMap(_meta, _comp)
      
    interface Seqable with
        override _.seq() = 
            if _count > 0 then PersistentTreeMapSeq.Create(_tree, true, _count) 
            else null

    interface IPersistentMap with
        override this.assoc (key: obj, value: obj): IPersistentMap = 
            let found = ValueBox<Node>(null)
            let t = this.Add(_tree,key,value,found)
            if isNull t then
                let foundNode = found.Value
                if Object.ReferenceEquals(foundNode.Value, value) then
                    this
                else
                    PersistentTreeMap(_meta, _comp, this.Replace(_tree, key, value), _count)            
            else
                PersistentTreeMap(_meta, _comp, t.Blacken(), _count + 1)
        override this.assocEx(key: obj, value: obj): IPersistentMap = 
            let found = ValueBox<Node>(null)
            let t = this.Add(_tree,key,value,found)
            if isNull t then
                raise <| invalidOp "Key already present"         
            else
                PersistentTreeMap(_meta, _comp, t.Blacken(), _count + 1)
        override this.without (key: obj): IPersistentMap = 
            let found = ValueBox<Node>(null)
            let t = this.Remove(_tree,key,found)
            if isNull t then
                if isNull found.Value then this
                else PersistentTreeMap(_meta, _comp)
            else  PersistentTreeMap(_meta, _comp, t.Blacken(), _count - 1)

    interface Reversible with
        member _.rseq() = 
            if _count > 0 then PersistentTreeMapSeq.Create(_tree, false, _count) 
            else null

    interface Sorted with
        member _.comparator() = _comp
        member _.entryKey (entry: obj): obj = (entry:?> IMapEntry).key()
        member _.seq (ascending: bool): ISeq = 
            if _count > 0 then PersistentTreeMapSeq.Create(_tree, ascending, _count) 
            else null
        member this.seqFrom (key: obj, ascending: bool): ISeq = 
            if _count > 0 then
                let rec loop (stack: ISeq) (t: Node) =
                    if isNull t then
                        if isNull stack then null else PersistentTreeMapSeq(stack, ascending)
                    else
                        let c = this.DoCompare(key, t.Key)
                        if c = 0 then
                            PersistentTreeMapSeq(RTSeq.cons(t, stack), ascending)
                        elif ascending then
                            if c < 0 then                                 
                                loop (RTSeq.cons(t, stack)) t.Left
                            else
                                loop stack t.Right
                        else
                            if c > 0 then
                                loop (RTSeq.cons(t, stack)) t.Right
                            else
                                loop stack t.Left
                loop null _tree
            else
                null
    
    
    member this.NodeAt (key: obj): Node =
        let rec loop (t: Node): Node =
            if isNull t then null
            else
                let c = this.DoCompare(key, t.Key)
                if c = 0 then t
                elif c < 0 then loop t.Left
                else loop t.Right
        loop _tree

    member this.DoCompare (k1: obj, k2: obj): int = _comp.Compare(k1, k2)




and [<AbstractClass; AllowNullLiteral>] internal Node(_key: obj) =
    inherit AMapEntry()

    interface IMapEntry with
        member _.key() = _key
        member _.value() = null

    member _.Key = _key

    abstract member Value : obj with get
    abstract member Left : Node with get
    abstract member Right : Node with get
    abstract member AddLeft : ins: Node -> Node
    abstract member AddRight : ins: Node -> Node
    abstract member RemoveLeft : del: Node -> Node
    abstract member RemoveRight : deL: Node -> Node
    abstract member Blacken : unit -> Node
    abstract member Redden : unit -> Node
    abstract member BalanceLeft : parent: Node -> Node
    abstract member BalanceRight : parent: Node -> Node
    abstract member Replace : key: obj -> value: obj -> left  : Node -> right: Node -> Node

    default _.Value = null
    default _.Left : Node = null
    default _.Right : Node = null

    default _.BalanceLeft(parent: Node) = PersistentTreeMap.MakeBlack(parent.Key, parent.Value, this, parent.Right)
    default _.BalanceRight(parent: Node) = PersistentTreeMap.MakeBlack(parent.Key, parent.Value, parent.Left, this)

    interface IKVReduce with
        member this.kvreduce (f: IFn, init: obj): obj = 
            // TODO: surely there is a better way to structure this
            let mutable ret = init
            let mutable finished = false
            if not <| isNull this.Left then
                ret <- (this.Left :> IKVReduce).kvreduce(f, ret)
                if ret :? Reduced then
                    finished <- true
            if not finished then
                ret <- f.invoke(ret, this.Key, this.Value)
                if ret :? Reduced then
                    finished <- true
            if not finished && not <| isNull this.Right then
                ret <- (this.Right :> IKVReduce).kvreduce(f, ret)
            ret

and [<AllowNullLiteral>] internal Black(_key: obj) = 
    inherit Node(_key)
    
    override this.AddLeft(ins: Node) = ins.BalanceLeft(this)
    override this.AddRight(ins: Node) = ins.BalanceRight(this)
    override this.RemoveLeft(del: Node) = PersistentTreeMap.BalanceLeftDel(_key, this.Value, del, this.Right)
    override this.RemoveRight(del: Node) = PersistentTreeMap.BalanceRightDel(_key, this.Value, this.Left, del)
    override this.Blacken (): Node = this
    override this.Redden () : Node = Red(_key)  
    override this.Replace (key: obj) (value: obj) (left: Node) (right: Node): Node = PersistentTreeMap.MakeBlack(_key, value, left, right)
  
and [<AllowNullLiteral>] internal BlackVal(_key: obj, _val: obj) =  
    inherit Black(_key)

    override _.Value
        with get (): obj = base.Value
        
    interface IMapEntry with
        override _.value (): obj = _val

    override _.Redden (): Node = RedVal(_key, _val)

and [<AllowNullLiteral>] internal BlackBranch(_key:obj, _left: Node, _right: Node) =
    inherit Black(_key)

    override _.Left = _left
    override _.Right = _right
    
    override _.Redden() = RedBranch(_key, _left, _right)

and [<AllowNullLiteral>] internal BlackBranchVal(_key:obj, _val: obj, _left: Node, _right: Node) =
    inherit BlackBranch(_key, _left, _right)

    override _.Value
        with get (): obj = _val

    interface IMapEntry with
        override _.value (): obj = _val

    override _.Redden (): Node = RedBranchVal(_key, _val, _left, _right)

and [<AllowNullLiteral>] internal Red(_key: obj) = 
    inherit Node(_key)
    
    override this.AddLeft(ins: Node) = PersistentTreeBranch.MakeRed(_key, this.Value, ins, this.Right)
    override this.AddRight(ins: Node) = PersistentTreeBranch.MakeRed(_key, this.Value, this.Left, ins)
    override this.RemoveLeft(del: Node) = PersistentTreeMap.MakeRed(_key, this.Value, del, this.Right)
    override this.RemoveRight(del: Node) = PersistentTreeMap.MakeRed(_key, this.Value, this.Left, del)
    override this.Blacken (): Node = Black(_key)
    override this.Redden () : Node = raise <| invalidOp "Invariant violation: Can't redden a red node"
    override this.Replace (key: obj) (value: obj) (left: Node) (right: Node): Node = PersistentTreeMap.MakeRed(key, value, left, right)
    
and [<AllowNullLiteral>] internal RedVal(_key: obj, _val: obj) =  
    inherit Red(_key)

    override _.Value
        with get (): obj = base.Value
        
    interface IMapEntry with
        override _.value (): obj = _val

    override _.Blacken (): Node = BlackVal(_key, _val)


and [<AllowNullLiteral>] internal RedBranch(_key:obj, _left: Node, _right: Node) =
    inherit Red(_key)

    override _.Left = _left
    override _.Right = _right
    
    override _.Blacken() = BlackBranch(_key, _left, _right)

    override _.BalanceLeft (parent: Node): Node = 
        if _left :? Red then
            PersistentTreeMap.MakeRed(_key,_val,_left.Blacken(), PersistentTreeMap.MakeBlack(parent.Key, parent.Value, _right, parent.right))
        elif _right :? Red then
                    PersistentTreeMap.MakeRed(_right.Key, 
                                              _right.Value, 
                                              PersistentTreeMap.MakeBlack(_key, _val, _left, _right.Left),
                                              PersistentTreeMap.MakeBlack(parent.Key, parent.Value, _right.Right, parent.Right))
        else
            base.BalanceLeft(parent)

    override _.BalanceRight (parent: Node): Node =
        if _right :? Red then
            PersistentTreeMap.MakeRed(_key,_val,PersistentTreeMap.MakeBlack(parent.Key, parent.Value, parent.Left, _left), _right.Blacken())
        elif _left :? Red then
            PersistentTreeMap.MakeRed(_left.Key, 
                                      _left.Value, 
                                      PersistentTreeMap.MakeBlack(parent.Key, parent.Value, parent.Left, _left.Left),
                                      PersistentTreeMap.MakeBlack(_key, _val, _left.Right, _right))
        else
            base.BalanceRight(parent)

and [<AllowNullLiteral>] internal RedBranchVal(_key:obj, _val: obj, _left: Node, _right: Node) =
    inherit BlackBranch(_key, _left, _right)

    override _.Value
        with get (): obj = _val

    interface IMapEntry with
        override _.value (): obj = _val

    override _.Blacken (): Node = BlackBranchVal(_key, _val, _left, _right)