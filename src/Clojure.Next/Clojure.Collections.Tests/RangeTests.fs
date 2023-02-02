module RangeTests

open Expecto


//[<Tests>]
//let rangeTests =
//    testList
//        "ConsTests"
//        [

//          testCase "No-meta ctor has no meta"
//          <| fun _ ->

   //   (take 100 (range)) (range 100)
      
    //(range 0) ()   ; exclusive end!
   //   (range 1) '(0)
   //   (range 5) '(0 1 2 3 4)

   //   (range -1) ()
   //   (range -3) ()

   //   (range 2.5) '(0 1 2)
   //   (range 7/3) '(0 1 2)

   //   (range 0 3) '(0 1 2)
   //   (range 0 1) '(0)
   //   (range 0 0) ()
   //   (range 0 -3) ()

   //   (range 3 6) '(3 4 5)
   //   (range 3 4) '(3)
   //   (range 3 3) ()
   //   (range 3 1) ()
   //   (range 3 0) ()
   //   (range 3 -2) ()

   //   (range -2 5) '(-2 -1 0 1 2 3 4)
   //   (range -2 0) '(-2 -1)
   //   (range -2 -1) '(-2)
   //   (range -2 -2) ()
   //   (range -2 -5) ()

   //   (take 3 (range 3 9 0)) '(3 3 3)
   //   (take 3 (range 9 3 0)) '(9 9 9)
   //   (range 0 0 0) ()
   //   (range 3 9 1) '(3 4 5 6 7 8)
   //   (range 3 9 2) '(3 5 7)
   //   (range 3 9 3) '(3 6)
   //   (range 3 9 10) '(3)
   //   (range 3 9 -1) ()
   //   (range 10 10 -1) ()
   //   (range 10 9 -1) '(10)
   //   (range 10 8 -1) '(10 9)
   //   (range 10 7 -1) '(10 9 8)
   //   (range 10 0 -2) '(10 8 6 4 2)

   //   (take 100 (range)) (take 100 (iterate inc 0))

   //   (range 1/2 5 1/3) '(1/2 5/6 7/6 3/2 11/6 13/6 5/2 17/6 19/6 7/2 23/6 25/6 9/2 29/6)
   //   (range 0.5 8 1.2) '(0.5 1.7 2.9 4.1 5.3 6.5 7.7)
   //   (range 0.5 -4 -2) '(0.5 -1.5 -3.5)
   //   (take 3 (range Int64/MaxValue Double/PositiveInfinity)) '(9223372036854775807 9223372036854775808N 9223372036854775809N)  ;;; Long/MAX_VALUE Double/POSITIVE_INFINITY

   //   (reduce + (take 100 (range))) 4950
   //   (reduce + 0 (take 100 (range))) 4950
   //   (reduce + (range 100)) 4950
   //   (reduce + 0 (range 100)) 4950
   //   (reduce + (range 0.0 100.0)) 4950.0
   //   (reduce + 0 (range 0.0 100.0)) 4950.0

   //   (reduce + (iterator-seq (.GetEnumerator (range 100)))) 4950                             ;;; .iterator 
   //   (reduce + (iterator-seq (.GetEnumerator (range 0.0 100.0 1.0)))) 4950.0 ))              ;;; .iterator 
