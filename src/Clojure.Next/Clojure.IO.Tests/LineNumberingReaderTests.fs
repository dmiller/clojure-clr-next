module LineNumberingReaderTests

open Expecto
open Clojure.IO
open System.IO

let sampleString =  "abc\nde\nfghijk\r\nlmnopq\n\nrstuv";

let sameContents (a:char array) (b:char array) count =
    let mutable i = 0
    let mutable mismatchFound = false
    while i < count && not mismatchFound do
        if a.[i] <> b.[i] then
            mismatchFound <- true
        i <- i + 1
    not mismatchFound

[<Tests>]
let BasicLineNumberingTextReaderTests =
    testList
        "Basic LineNumberingTextReader Tests"
        [ 
        
          ftestCase "LNTR initializes properly"
          <| fun _ ->
                let rdr = new LineNumberingTextReader(new StringReader(sampleString))

                Expect.equal rdr.LineNumber 1 "Line number should be 1"
                Expect.equal rdr.ColumnNumber 1 "Column number should be 1"
                Expect.equal rdr.Index 0 "Index should be 0"
                Expect.isTrue rdr.AtLineStart  "AtLineStart should be true"
                Expect.equal (rdr.Peek()) (int sampleString[0]) "Should be peeking at first character"


          ftestCase "LNTR reads characters one at a time"
           <| fun _ -> 
                let chars = sampleString.ReplaceLineEndings("\n").ToCharArray() |> Array.map int

                let positions = 
                    [| 2; 3; 4; 1;
                       2; 3; 1;
                       2; 3; 4; 5; 6; 7; 1; 
                       2; 3; 4; 5; 6; 7; 1; 
                       1;
                       2; 3; 4; 5;6 |]

                let lines = 
                    [| 1; 1; 1; 2;
                       2; 2; 3;
                       3; 3; 3; 3; 3; 3; 4;
                       4; 4; 4; 4; 4; 4; 5;
                       6;
                       6; 6; 6; 6; 6 |]

                let indexes = 
                    [| 1; 2; 3; 4;
                       5; 6; 7;
                       8; 9; 10; 11; 12; 13; 15;
                       16; 17; 18; 19; 20; 21; 22;
                       23;
                       24; 25; 26; 27; 28 |]
  
                let starts = 
                    [| false; false; false; true;
                       false; false; true;
                       false; false; false; false; false; false; true;
                       false; false; false; false; false; false; true;
                       true;
                       false; false; false; false; false |]

                let rdr = new LineNumberingTextReader(new StringReader(sampleString))

                let mutable ch = rdr.Read()
                let mutable i = 0
                while ch <> -1 do
                    Expect.equal ch chars[i] "Character should be correct"
                    Expect.equal rdr.LineNumber lines[i] "Line number should be correct"
                    Expect.equal rdr.ColumnNumber positions[i] "Column number should be correct"
                    Expect.equal rdr.Index indexes[i] "Index should be correct"
                    Expect.equal rdr.AtLineStart starts[i] "AtLineStart should be correct"
                    i <- i + 1
                    ch <- rdr.Read()

          ftestCase "LNTR reads lines one at a time"
           <| fun _ -> 
                let lines = sampleString.ReplaceLineEndings("\n").Split("\n")

                let positions = 
                    [| 1;1;1;1;1;6 |]

                let lineNums = 
                    [|  2; 3; 4; 5; 6; 6 |]

                let indexes = 
                    [|   4;7;15;22;23;28 |]
  
                let starts = 
                    [| true; true; true; true; true; true |]

                let rdr = new LineNumberingTextReader(new StringReader(sampleString))

                let mutable line = rdr.ReadLine()
                let mutable i = 0
                while not <| isNull line do
                    Expect.equal line lines[i] "Character should be correct"
                    Expect.equal rdr.LineNumber lineNums[i] "Line number should be correct"
                    Expect.equal rdr.ColumnNumber positions[i] "Column number should be correct"
                    Expect.equal rdr.Index indexes[i] "Index should be correct"
                    Expect.equal rdr.AtLineStart starts[i] "AtLineStart should be correct"
                    i <- i + 1
                    line <- rdr.ReadLine()

          ftestCase "LNTR reads into buffer"
           <| fun _ -> 
                let buffers = sampleString.ReplaceLineEndings("\n").ToCharArray() |> Array.chunkBySize 5

                let positions = 
                    [| 2; 4; 2; 7; 4; 6  |]

                let lineNums = 
                    [|  2; 3; 4; 4; 6; 6 |]

                let indexes = 
                    [|   5; 10; 16; 21; 26; 28 |]
  
                let starts = 
                    [| false; false; false; false; false; true; |]

                let rdr = new LineNumberingTextReader(new StringReader(sampleString))

                let buffer : char array = Array.zeroCreate 20

                let mutable count = rdr.Read(buffer,0,5)
                let mutable i = 0
                while count <> 0 do
                    Expect.isTrue (sameContents buffer buffers[i] count) "buffers should match"
                    Expect.equal rdr.LineNumber lineNums[i] "Line number should be correct"
                    Expect.equal rdr.ColumnNumber positions[i] "Column number should be correct"
                    Expect.equal rdr.Index indexes[i] "Index should be correct"
                    Expect.equal rdr.AtLineStart starts[i] "AtLineStart should be correct"
                    i <- i + 1
                    count  <-rdr.Read(buffer,0,5)

              ]



            
