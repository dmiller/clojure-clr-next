namespace Clojure.Collections

open System
open System.Collections
open System.Collections.Generic
open Clojure.Numerics

/// A persistent Red-Black Tree implementation
// See Okasake, Kahrs, Larsen, et al.
[<AllowNullLiteral>]
type PersistentTreeMap private (_meta: IPersistentMap, _comp: IComparer, _tree: Node, _count: int) =
    inherit APersistentMap()

    static member val defaultComparer =
        { new Object() with
            override _.ToString() = "#<Default comparer>"
          interface IComparer with
              member _.Compare(x, y) = Util.compare (x, y)
              //member _.compare(x, y) = Util.compare(x,y) -- can't do this. needed for core.clj compatbility -- we can get around this when we get there
               }

    static member val Empty = new PersistentTreeMap()

    new(meta, comp) = PersistentTreeMap(meta, comp, null, 0)
    new(comp) = PersistentTreeMap(null, comp)
    new() = PersistentTreeMap(PersistentTreeMap.defaultComparer)

    /// Create a PersistentTreeMap from a dictionary
    static member Create(dict: IDictionary) =
        let mutable ret = PersistentTreeMap.Empty :> IPersistentMap

        for e in dict do
            let kvp = e :?> DictionaryEntry
            ret <- ret.assoc (kvp.Key, kvp.Value)

        ret

    /// Create a PersistentTreeMap from a sequence of alternating keys and values
    static member Create(items: ISeq) =
        let rec loop (s: ISeq) (ret: IPersistentMap) =
            match s with
            | null -> ret :?> PersistentTreeMap
            | _ when isNull <| s.next () ->
                raise <| new ArgumentException($"No value supplied for key: {items.first ()}")
            | _ -> loop (s.next().next ()) (ret.assoc (s.first (), s.next().first ()))

        loop items PersistentTreeMap.Empty

    /// Create a PersistentTreeMap with supplied comparator from a sequence of alternating keys and values
    static member Create(comp: IComparer, items: ISeq) =
        let rec loop (s: ISeq) (ret: IPersistentMap) =
            match s with
            | null -> ret :?> PersistentTreeMap
            | _ when isNull <| s.next () ->
                raise <| new ArgumentException($"No value supplied for key: {items.first ()}")
            | _ -> loop (s.next().next ()) (ret.assoc (s.first (), s.next().first ()))

        loop items (PersistentTreeMap(comp))

    override _.Equals(o: obj) : bool =
        try
            base.Equals(o: obj)
        with :? InvalidCastException ->
            false

    override _.GetHashCode() : int = base.GetHashCode()


    interface IObj with
        override this.withMeta(meta: IPersistentMap) : IObj =
            if Object.ReferenceEquals(meta, _meta) then
                this
            else
                PersistentTreeMap(meta, _comp, _tree, _count)

    interface IMeta with
        member _.meta() = _meta

    interface Associative with
        member this.containsKey(key: obj) : bool = not <| isNull (this.NodeAt(key))
        member this.entryAt(key: obj) : IMapEntry = this.NodeAt(key)

    interface ILookup with
        member this.valAt(key: obj) : obj = (this :> ILookup).valAt (key, null)

        member this.valAt(key: obj, notFound: obj) : obj =
            let n = this.NodeAt(key)
            if isNull n then notFound else n.Value

    interface IPersistentCollection with
        override _.equiv(arg: obj) : bool =
            try
                base.doEquiv (arg)
            with :? InvalidCastException ->
                false

        override _.count() = _count
        override this.empty() = PersistentTreeMap(_meta, _comp)

    interface Seqable with
        override _.seq() =
            if _count > 0 then
                PersistentTreeMapSeq.create (_tree, true, _count)
            else
                null

    interface Counted with
        override this.count() : int = _count

    interface IPersistentMap with
        override this.count() = _count

        override this.assoc(key: obj, value: obj) : IPersistentMap =
            let found = ValueBox<Node>(null)
            let t = this.Add(_tree, key, value, found)

            if isNull t then
                let foundNode = found.Value

                if Object.ReferenceEquals(foundNode.Value, value) then
                    this
                else
                    PersistentTreeMap(_meta, _comp, this.Replace(_tree, key, value), _count)
            else
                PersistentTreeMap(_meta, _comp, t.Blacken(), _count + 1)

        override this.assocEx(key: obj, value: obj) : IPersistentMap =
            let found = ValueBox<Node>(null)
            let t = this.Add(_tree, key, value, found)

            if isNull t then
                raise <| invalidOp "Key already present"
            else
                PersistentTreeMap(_meta, _comp, t.Blacken(), _count + 1)

        override this.without(key: obj) : IPersistentMap =
            let found = ValueBox<Node>(null)
            let t = this.Remove(_tree, key, found)

            if isNull t then
                if isNull found.Value then
                    this
                else
                    PersistentTreeMap(_meta, _comp)
            else
                PersistentTreeMap(_meta, _comp, t.Blacken(), _count - 1)

    interface Reversible with
        member _.rseq() =
            if _count > 0 then
                PersistentTreeMapSeq.create (_tree, false, _count)
            else
                null

    interface Sorted with
        member _.comparator() = _comp
        member _.entryKey(entry: obj) : obj = (entry :?> IMapEntry).key ()

        member _.seq(ascending: bool) : ISeq =
            if _count > 0 then
                PersistentTreeMapSeq.create (_tree, ascending, _count)
            else
                null

        member this.seqFrom(key: obj, ascending: bool) : ISeq =
            if _count > 0 then
                let rec loop (stack: ISeq) (t: Node) =
                    if isNull t then
                        if isNull stack then
                            null
                        else
                            PersistentTreeMapSeq(stack, ascending)
                    else
                        let c = this.DoCompare(key, t.Key)

                        if c = 0 then
                            PersistentTreeMapSeq(RTSeq.cons (t, stack), ascending)
                        elif ascending then
                            if c < 0 then
                                loop (RTSeq.cons (t, stack)) t.Left
                            else
                                loop stack t.Right
                        else if c > 0 then
                            loop (RTSeq.cons (t, stack)) t.Right
                        else
                            loop stack t.Left

                loop null _tree
            else
                null

    interface IKVReduce with
        override _.kvreduce(f: IFn, init: obj) : obj =
            let ret =
                if isNull _tree then
                    init
                else
                    (_tree :> IKVReduce).kvreduce (f, init)

            match ret with
            | :? Reduced as r -> (r :> IDeref).deref ()
            | _ -> ret

    interface IEnumerable<KeyValuePair<obj, obj>> with
        member this.GetEnumerator() : IEnumerator<KeyValuePair<obj, obj>> =
            new PersistentTreeMapNodeEnumerator(_tree, true)

    interface IEnumerable<IMapEntry> with
        member this.GetEnumerator() : IEnumerator<IMapEntry> =
            new PersistentTreeMapNodeEnumerator(_tree, true)

    interface IEnumerable with
        member this.GetEnumerator() : IEnumerator =
            new PersistentTreeMapNodeEnumerator(_tree, true)

    interface IDictionary with
        member this.GetEnumerator() : IDictionaryEnumerator =
            new PersistentTreeMapNodeEnumerator(_tree, true)

    member private this.NodeAt(key: obj) : Node =
        let rec loop (t: Node) : Node =
            if isNull t then
                null
            else
                let c = this.DoCompare(key, t.Key)

                if c = 0 then t
                elif c < 0 then loop t.Left
                else loop t.Right

        loop _tree

    member this.DoCompare(k1: obj, k2: obj) : int = _comp.Compare(k1, k2)

    member private this.Add(t: Node, key: obj, value: obj, found: ValueBox<Node>) : Node =
        if isNull t then
            if
                Object.ReferenceEquals(_comp, PersistentTreeMap.defaultComparer)
                && not (isNull key || Numbers.IsNumeric key || key :? IComparable)
            then
                raise
                <| new ArgumentException($"Default comparator requires nil, Number, or Comparable: {key}")

            if isNull value then Red(key) else RedVal(key, value)
        else
            let c = this.DoCompare(key, t.Key)

            if c = 0 then
                found.Value <- t
                null
            else
                let ins =
                    if c < 0 then
                        this.Add(t.Left, key, value, found)
                    else
                        this.Add(t.Right, key, value, found)

                if isNull ins then null
                elif c < 0 then t.AddLeft(ins)
                else t.AddRight(ins)

    member private this.Remove(t: Node, key: obj, found: ValueBox<Node>) : Node =
        if isNull t then
            null
        else
            let c = this.DoCompare(key, t.Key)

            if c = 0 then
                found.Value <- t
                PersistentTreeMap.Append(t.Left, t.Right)
            else
                let del =
                    if c < 0 then
                        this.Remove(t.Left, key, found)
                    else
                        this.Remove(t.Right, key, found)

                if isNull del && isNull found.Value then
                    null
                elif c < 0 then
                    if t.Left :? Black then
                        PersistentTreeMap.BalanceLeftDel(t.Key, t.Value, del, t.Right)
                    else
                        PersistentTreeMap.MakeRed(t.Key, t.Value, del, t.Right)
                else if t.Right :? Black then
                    PersistentTreeMap.BalanceRightDel(t.Key, t.Value, t.Left, del)
                else
                    PersistentTreeMap.MakeRed(t.Key, t.Value, t.Left, del)

    static member private Append(left: Node, right: Node) : Node =
        if isNull left then
            right
        elif isNull right then
            left
        elif left :? Red then
            if right :? Red then
                let app = PersistentTreeMap.Append(left.Right, right.Left)

                if app :? Red then
                    PersistentTreeMap.MakeRed(
                        app.Key,
                        app.Value,
                        PersistentTreeMap.MakeRed(left.Key, left.Value, left.Left, app.Left),
                        PersistentTreeMap.MakeRed(right.Key, right.Value, app.Right, right.Right)
                    )
                else
                    PersistentTreeMap.MakeRed(
                        left.Key,
                        left.Value,
                        left.Left,
                        PersistentTreeMap.MakeRed(right.Key, right.Value, app, right.Right)
                    )
            else
                PersistentTreeMap.MakeRed(left.Key, left.Value, left.Left, PersistentTreeMap.Append(left.Right, right))
        elif right :? Red then
            PersistentTreeMap.MakeRed(right.Key, right.Value, PersistentTreeMap.Append(left, right.Left), right.Right)
        else
            let app = PersistentTreeMap.Append(left.Right, right.Left)

            if app :? Red then
                PersistentTreeMap.MakeRed(
                    app.Key,
                    app.Value,
                    PersistentTreeMap.MakeBlack(left.Key, left.Value, left.Left, app.Left),
                    PersistentTreeMap.MakeBlack(right.Key, right.Value, app.Right, right.Right)
                )
            else
                PersistentTreeMap.BalanceLeftDel(
                    left.Key,
                    left.Value,
                    left.Left,
                    PersistentTreeMap.MakeBlack(right.Key, right.Value, app, right.Right)
                )

    static member internal BalanceLeftDel(key: obj, value: obj, del: Node, right: Node) : Node =
        if del :? Red then
            PersistentTreeMap.MakeRed(key, value, del.Blacken(), right)
        elif right :? Black then
            PersistentTreeMap.RightBalance(key, value, del, right.Redden())
        elif right :? Red && right.Left :? Black then
            PersistentTreeMap.MakeRed(
                right.Left.Key,
                right.Left.Value,
                PersistentTreeMap.MakeBlack(key, value, del, right.Left.Left),
                PersistentTreeMap.RightBalance(right.Key, right.Value, right.Left.Right, right.Right.Redden())
            )
        else
            raise <| invalidOp "Invariant violation"

    static member internal BalanceRightDel(key: obj, value: obj, left: Node, del: Node) : Node =
        if del :? Red then
            PersistentTreeMap.MakeRed(key, value, left, del.Blacken())
        elif left :? Black then
            PersistentTreeMap.LeftBalance(key, value, left.Redden(), del)
        elif left :? Red && left.Right :? Black then
            PersistentTreeMap.MakeRed(
                left.Right.Key,
                left.Right.Value,
                PersistentTreeMap.LeftBalance(left.Key, left.Value, left.Left.Redden(), left.Right.Left),
                PersistentTreeMap.MakeBlack(key, value, left.Right.Right, del)
            )
        else
            raise <| invalidOp "Invariant violation"

    static member private LeftBalance(key: obj, value: obj, ins: Node, right: Node) : Node =
        if ins :? Red && ins.Left :? Red then
            PersistentTreeMap.MakeRed(
                ins.Key,
                ins.Value,
                ins.Left.Blacken(),
                PersistentTreeMap.MakeBlack(key, value, ins.Right, right)
            )
        elif ins :? Red && ins.Right :? Red then
            PersistentTreeMap.MakeRed(
                ins.Right.Key,
                ins.Right.Value,
                PersistentTreeMap.MakeBlack(ins.Key, ins.Value, ins.Left, ins.Right.Left),
                PersistentTreeMap.MakeBlack(key, value, ins.Right.Right, right)
            )
        else
            PersistentTreeMap.MakeBlack(key, value, ins, right)

    static member private RightBalance(key: obj, value: obj, left: Node, ins: Node) : Node =
        if ins :? Red && ins.Right :? Red then
            PersistentTreeMap.MakeRed(
                ins.Key,
                ins.Value,
                PersistentTreeMap.MakeBlack(key, value, left, ins.Left),
                ins.Right.Blacken()
            )
        elif ins :? Red && ins.Left :? Red then
            PersistentTreeMap.MakeRed(
                ins.Left.Key,
                ins.Left.Value,
                PersistentTreeMap.MakeBlack(key, value, left, ins.Left.Left),
                PersistentTreeMap.MakeBlack(ins.Key, ins.Value, ins.Left.Right, ins.Right)
            )
        else
            PersistentTreeMap.MakeBlack(key, value, left, ins)

    member private this.Replace(t: Node, key: obj, value: obj) : Node =
        let c = this.DoCompare(key, t.Key)

        t.Replace(
            t.Key,
            (if c = 0 then value else t.Value),
            (if c < 0 then this.Replace(t.Left, key, value) else t.Left),
            (if c > 0 then this.Replace(t.Right, key, value) else t.Right)
        )

    static member internal MakeRed(key: obj, value: obj, left: Node, right: Node) : Node =
        if isNull left && isNull right then
            if isNull value then Red(key) else RedVal(key, value)
        else if isNull value then
            RedBranch(key, left, right)
        else
            RedBranchVal(key, value, left, right)

    static member internal MakeBlack(key: obj, value: obj, left: Node, right: Node) : Node =
        if isNull left && isNull right then
            if isNull value then Black(key) else BlackVal(key, value)
        else if isNull value then
            BlackBranch(key, left, right)
        else
            BlackBranchVal(key, value, left, right)

and [<AbstractClass; AllowNullLiteral>] internal Node(_key: obj) =
    inherit AMapEntry()

    interface IMapEntry with
        member _.key() = _key
        member _.value() = null

    member _.Key = _key

    abstract member Value: obj
    abstract member Left: Node
    abstract member Right: Node
    abstract member AddLeft: ins: Node -> Node
    abstract member AddRight: ins: Node -> Node
    abstract member RemoveLeft: del: Node -> Node
    abstract member RemoveRight: deL: Node -> Node
    abstract member Blacken: unit -> Node
    abstract member Redden: unit -> Node
    abstract member BalanceLeft: parent: Node -> Node
    abstract member BalanceRight: parent: Node -> Node
    abstract member Replace: key: obj * value: obj * left: Node * right: Node -> Node

    default _.Value = null
    default _.Left: Node = null
    default _.Right: Node = null

    default this.BalanceLeft(parent: Node) =
        PersistentTreeMap.MakeBlack(parent.Key, parent.Value, this, parent.Right)

    default this.BalanceRight(parent: Node) =
        PersistentTreeMap.MakeBlack(parent.Key, parent.Value, parent.Left, this)

    interface IKVReduce with
        member this.kvreduce(f: IFn, init: obj) : obj =
            // TODO: surely there is a better way to structure this
            let mutable ret = init
            let mutable finished = false

            if not <| isNull this.Left then
                ret <- (this.Left :> IKVReduce).kvreduce (f, ret)

                if ret :? Reduced then
                    finished <- true

            if not finished then
                ret <- f.invoke (ret, this.Key, this.Value)

                if ret :? Reduced then
                    finished <- true

            if not finished && not <| isNull this.Right then
                ret <- (this.Right :> IKVReduce).kvreduce (f, ret)

            ret

and [<AllowNullLiteral>] internal Black(_key: obj) =
    inherit Node(_key)

    override this.AddLeft(ins: Node) = ins.BalanceLeft(this)
    override this.AddRight(ins: Node) = ins.BalanceRight(this)

    override this.RemoveLeft(del: Node) =
        PersistentTreeMap.BalanceLeftDel(_key, this.Value, del, this.Right)

    override this.RemoveRight(del: Node) =
        PersistentTreeMap.BalanceRightDel(_key, this.Value, this.Left, del)

    override this.Blacken() : Node = this
    override this.Redden() : Node = Red(_key)

    override this.Replace(key: obj, value: obj, left: Node, right: Node) : Node =
        PersistentTreeMap.MakeBlack(_key, value, left, right)

and [<AllowNullLiteral>] internal BlackVal(_key: obj, _val: obj) =
    inherit Black(_key)

    override _.Value: obj = _val

    interface IMapEntry with
        override _.value() : obj = _val

    override _.Redden() : Node = RedVal(_key, _val)

and [<AllowNullLiteral>] internal BlackBranch(_key: obj, _left: Node, _right: Node) =
    inherit Black(_key)

    override _.Left = _left
    override _.Right = _right

    override _.Redden() = RedBranch(_key, _left, _right)

and [<AllowNullLiteral>] internal BlackBranchVal(_key: obj, _val: obj, _left: Node, _right: Node) =
    inherit BlackBranch(_key, _left, _right)

    override _.Value: obj = _val

    interface IMapEntry with
        override _.value() : obj = _val

    override _.Redden() : Node = RedBranchVal(_key, _val, _left, _right)

and [<AllowNullLiteral>] internal Red(_key: obj) =
    inherit Node(_key)

    override this.AddLeft(ins: Node) =
        PersistentTreeMap.MakeRed(_key, this.Value, ins, this.Right)

    override this.AddRight(ins: Node) =
        PersistentTreeMap.MakeRed(_key, this.Value, this.Left, ins)

    override this.RemoveLeft(del: Node) =
        PersistentTreeMap.MakeRed(_key, this.Value, del, this.Right)

    override this.RemoveRight(del: Node) =
        PersistentTreeMap.MakeRed(_key, this.Value, this.Left, del)

    override this.Blacken() : Node = Black(_key)

    override this.Redden() : Node =
        raise <| invalidOp "Invariant violation: Can't redden a red node"

    override this.Replace(key: obj, value: obj, left: Node, right: Node) : Node =
        PersistentTreeMap.MakeRed(key, value, left, right)

and [<AllowNullLiteral>] internal RedVal(_key: obj, _val: obj) =
    inherit Red(_key)

    override _.Value: obj = _val

    interface IMapEntry with
        override _.value() : obj = _val

    override _.Blacken() : Node = BlackVal(_key, _val)


and [<AllowNullLiteral>] internal RedBranch(_key: obj, _left: Node, _right: Node) =
    inherit Red(_key)

    override _.Left = _left
    override _.Right = _right

    override _.Blacken() = BlackBranch(_key, _left, _right)

    override this.BalanceLeft(parent: Node) : Node =
        if _left :? Red then
            PersistentTreeMap.MakeRed(
                _key,
                this.Value,
                _left.Blacken(),
                PersistentTreeMap.MakeBlack(parent.Key, parent.Value, _right, parent.Right)
            )
        elif _right :? Red then
            PersistentTreeMap.MakeRed(
                _right.Key,
                _right.Value,
                PersistentTreeMap.MakeBlack(_key, this.Value, _left, _right.Left),
                PersistentTreeMap.MakeBlack(parent.Key, parent.Value, _right.Right, parent.Right)
            )
        else
            base.BalanceLeft(parent)

    override this.BalanceRight(parent: Node) : Node =
        if _right :? Red then
            PersistentTreeMap.MakeRed(
                _key,
                this.Value,
                PersistentTreeMap.MakeBlack(parent.Key, parent.Value, parent.Left, _left),
                _right.Blacken()
            )
        elif _left :? Red then
            PersistentTreeMap.MakeRed(
                _left.Key,
                _left.Value,
                PersistentTreeMap.MakeBlack(parent.Key, parent.Value, parent.Left, _left.Left),
                PersistentTreeMap.MakeBlack(_key, this.Value, _left.Right, _right)
            )
        else
            base.BalanceRight(parent)

and [<AllowNullLiteral>] internal RedBranchVal(_key: obj, _val: obj, _left: Node, _right: Node) =
    inherit RedBranch(_key, _left, _right)

    override _.Value: obj = _val

    interface IMapEntry with
        override _.value() : obj = _val

    override _.Blacken() : Node =
        BlackBranchVal(_key, _val, _left, _right)

and [<AllowNullLiteral>] PersistentTreeMapSeq(_meta: IPersistentMap, _stack: ISeq, _asc: bool, _cnt: int) =
    inherit ASeq(_meta)

    new(stack, asc, cnt) = PersistentTreeMapSeq(null, stack, asc, cnt)
    new(stack, asc) = PersistentTreeMapSeq(null, stack, asc, -1)

    static member internal create(t: Node, asc: bool, cnt: int) =
        PersistentTreeMapSeq(PersistentTreeMapSeq.Push(t, null, asc), asc, cnt)

    interface IObj with
        override this.withMeta(meta: IPersistentMap) : IObj =
            if Object.ReferenceEquals(meta, _meta) then
                this
            else
                PersistentTreeMapSeq(meta, _stack, _asc, _cnt)

    interface ISeq with
        override _.first() = _stack.first ()

        override _.next() =
            let t = _stack.first () :?> Node
            let next = if _asc then t.Right else t.Left
            let nextStack = PersistentTreeMapSeq.Push(next, _stack.next (), _asc)

            if isNull nextStack then
                null
            else
                PersistentTreeMapSeq(nextStack, _asc, _cnt - 1)

    interface Counted with
        override this.count() =
            if _cnt < 0 then (this :> ASeq).DoCount() else _cnt

    interface IPersistentCollection with
        override this.count() : int = (this :> Counted).count ()

    static member private Push(t: Node, stack: ISeq, asc: bool) : ISeq =
        let rec loop (s: ISeq) (t: Node) =
            if isNull t then
                s
            else
                loop (RTSeq.cons (t, s)) (if asc then t.Left else t.Right)

        loop stack t

and [<AllowNullLiteral>] internal PersistentTreeMapNodeEnumerator(_startNode: Node, _asc: bool) =

    let _stack = new Stack<Node>()
    let mutable _beforeStart = true

    do PersistentTreeMapNodeEnumerator.Push(_startNode, _stack, _asc)

    static member internal Push(t: Node, stack: Stack<Node>, asc: bool) =
        let rec loop (t: Node) =
            if isNull t then
                ()
            else
                stack.Push(t)
                loop (if asc then t.Left else t.Right)

        loop t

    member _.Peek() =
        if _beforeStart then
            raise <| new InvalidOperationException("Enumerator before start of sequence")

        _stack.Peek()

    interface IDictionaryEnumerator with
        member this.Entry =
            let n = this.Peek()
            DictionaryEntry(n.Key, n.Value)

        member this.Key = (this :> IDictionaryEnumerator).Entry.Key
        member this.Value = (this :> IDictionaryEnumerator).Entry.Value

    interface IEnumerator<KeyValuePair<Object, Object>> with
        member this.Current =
            let n = this.Peek()
            KeyValuePair(n.Key, n.Value)

    interface IEnumerator<IMapEntry> with
        member this.Current = this.Peek()

    interface IEnumerator with
        member this.Current = this.Peek()

        member this.MoveNext() : bool =
            if _beforeStart then
                _beforeStart <- false
            else
                let t = _stack.Pop()
                PersistentTreeMapNodeEnumerator.Push((if _asc then t.Right else t.Left), _stack, _asc)

            _stack.Count > 0

        member this.Reset() : unit =
            _stack.Clear()
            PersistentTreeMapNodeEnumerator.Push(_startNode, _stack, _asc)
            _beforeStart <- true

    interface IDisposable with
        member this.Dispose() = ()
