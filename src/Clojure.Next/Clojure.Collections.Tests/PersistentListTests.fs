module PersistentListTests

open Expecto
open Clojure.Collections
open TestHelpers
open System



[<Tests>]
let IPersistentListTests =
    testList
        "PersistenList Tests"
        [

          testCase "ctor for one element"
          <| fun _ ->
              let p = PersistentList("abc") :> ISeq
              Expect.equal (p.first ()) (upcast "abc") "First element should be what was provided"
              Expect.isNull (p.next ()) "Only one element, so next() is null"
              Expect.equal (p.count ()) 1 "Only one element"

          testCase "create for several elements"
          <| fun _ ->
              let vs: obj list = [ 1; "A"; 2; "B" ]
              let xs: ResizeArray<obj> = ResizeArray(vs) // We need an IList
              let p = PersistentList.create (xs)
              let s = p.seq ()

              Expect.equal (p.count ()) 4 "Should have correct count"
              Expect.equal (s.first ()) (box 1) "first element"
              Expect.equal (s.next().first()) (upcast "A") "second element"
              Expect.equal (s.next().next().first()) (box 2) "third element"
              Expect.equal (s.next().next().next().first()) (upcast "B") "fourth element"
              Expect.isNull (s.next().next().next().next()) "end of sequence"

          testCase "Peek yields first element"
          <| fun _ ->
              let vs: obj list = [ "A"; 2; "B" ]
              let xs: ResizeArray<obj> = ResizeArray(vs) // We need an IList
              let p = PersistentList.create (xs)
              Expect.equal (p.peek ()) (upcast "A") "pop yields first element"
              Expect.equal (p.count ()) 3 "pop does not change the list"

          testCase "Pop loses first element"
          <| fun _ ->
              let vs: obj list = [ "A"; 2; "B" ]
              let xs: ResizeArray<obj> = ResizeArray(vs) // We need an IList
              let p = PersistentList.create (xs)
              let p2 = p.pop ()
              Expect.equal (p2.count ()) (p.count () - 1) "Loses an element"
              Expect.equal ((p2 :?> ISeq).first()) ((p :?> ISeq).next().first()) "Loses first element"

          testCase "Pop on singleton yields empty"
          <| fun _ ->
              let p =
                  PersistentList("abc") :> IPersistentStack

              let s = p.pop ()
              Expect.equal (s.count ()) 0 "Should be empty list"

          testCase "Double pop on singleton throws"
          <| fun _ ->
              let p =
                  PersistentList("abc") :> IPersistentStack

              let f () = p.pop().pop() |> ignore
              Expect.throwsT<InvalidOperationException> f "should throw when pop empty"

          testCase "Empty has no elements"
          <| fun _ ->
              let p =
                  PersistentList("abc") :> IPersistentCollection

              let e = p.empty ()
              Expect.equal (e.count ()) 0 "Should have no elements"

          // defer until we have maps
          //testCase "Empty preserves meta"
          //<| fun _ ->
          //    let p = PersistentList("abc") :> IObj

          //    let pm =
          //        p.withMeta (metaForSimpleTests) :?> IPersistentCollection

          //    let e = pm.empty () :?> IObj
          //    Expect.isTrue (LanguagePrimitives.PhysicalEquality (e.meta ()) metaForSimpleTests) "Should have same meta"

          // TODO:  Add IReduce/IReduceInit tests when we have AFnImpl completed
          //        public static class DummyFn
          //        {
          //            public static IFn CreateForReduce()
          //            {
          //                AFnImpl fn = new AFnImpl();
          //                fn._fn2 = ( object x, object y ) => { return Numbers.addP(x,y); };
          //                return fn;
          //            }

          //            internal static IFn CreateForMetaAlter(IPersistentMap meta)
          //            {
          //                AFnImpl fn = new AFnImpl();
          //                fn._fn0 = () => { return meta; };
          //                fn._fn1 = (object x) => { return meta; };
          //                return fn;
          //            }
          //        }
          //}

          //[Test]
          //public void ReduceWithNoStartIterates()
          //{
          //    IFn fn = DummyFn.CreateForReduce();

          //    PersistentList p = (PersistentList)PersistentList.create(new object[] { 1, 2, 3 });
          //    object ret = p.reduce(fn);

          //    Expect(ret).To.Be.An.Instance.Of<long>();
          //    Expect((long)ret).To.Equal(6);
          //}

          //[Test]
          //public void ReduceWithStartIterates()
          //{
          //    IFn fn = DummyFn.CreateForReduce();

          //    PersistentList p = (PersistentList)PersistentList.create(new object[] { 1, 2, 3 });
          //    object ret = p.reduce(fn, 20);

          //    Expect(ret).To.Be.An.Instance.Of<long>();
          //    Expect((long)ret).To.Equal(26);
          //}

          testCase "Verify some basic properties of PersistentList.ISeq"
          <| fun _ ->

              let p1 = PersistentList("abc")
              let p2 = (p1 :> ISeq).cons("def")
              let p3 = p2.cons (7) :?> IPersistentList
              let vals: obj list = [ 7; "def"; "abc" ]

  
              verifyISeqContents (p3.seq ()) vals
              verifyISeqRestTypes (p3.seq ()) typeof<PersistentList>
              verifyISeqCons (p3.seq ()) "pqr" vals
              
              // Defer until we have maps
              //let p4 =
              //    (p3 :?> IObj).withMeta(metaForSimpleTests) :?> ISeq
              //verifyISeqContents (p4.seq ()) vals

              //let p5 = p4.cons ("pqr")

              //Expect.isTrue
              //    (LanguagePrimitives.PhysicalEquality ((p5 :?> IMeta).meta()) ((p4 :?> IMeta).meta()))
              //    "Cons should preserve meta"

          // defer until we have maps
          //testCase "Verify PersistentList.IObj"
          //<| fun _ ->
          //    let vs: obj list = [ 1; "A"; 2; "B" ]
          //    let xs: ResizeArray<obj> = ResizeArray(vs)
          //    let p = PersistentList.create (xs) :?> IObj
          //    let pm = p.withMeta (metaForSimpleTests)
          //    verifyNullMeta p
          //    verifyWithMetaHasCorrectMeta pm
          //    verifyWithMetaNoChange p
          //    verifyWithMetaReturnType p typeof<PersistentList> 
          
          ]
