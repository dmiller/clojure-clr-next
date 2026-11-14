# Post Inventory - 2026-11-13

All published posts and drafts, for marking up project, series and topics.

How to fill it in:

- **Project:** `Next` (ClojureCLR.Next) or `CLR` (mainline ClojureCLR). Values are pre-filled from the discussion so far. `?` means undecided.
- **Series:** pre-filled where the titles make it obvious. Add or correct as needed.
- **Topics:** blank. Use any words you like. Possible starting vocabulary: data-structures, collections, numbers, hashing, performance, benchmarks, concurrency, stm, compiler, types, interop, naming, design, testing, project-status.

## Published posts

| #    | File                                              | Title                                                        | Project | Series             | Topics                  |
| ---- | ------------------------------------------------- | ------------------------------------------------------------ | ------- | ------------------ | ----------------------- |
| 1    | 2023-01-06-clojureclr-reconsidered                | ClojureCLR -- Reconsidered                                   | Next    |                    | design                  |
| 2    | 2023-01-07-plan-of-attack                         | The plan of attack                                           | Next    |                    | design                  |
| 3    | 2023-01-08-consideration                          | For your Cons-ideration                                      | Next    |                    | design; data-structures |
| 4    | 2023-01-09-a-minimal-implementation-of-cons       | A minimal implementation of Cons                             | Next    |                    | design; data-structures |
| 5    | 2023-01-10-odd-questions                          | Odd questions                                                | Next    |                    | design; data-structures |
| 6    | 2023-01-11-this-map-is-the-territory              | This map is the territory                                    | Next    |                    | data-structures         |
| 7    | 2023-01-16-circular-reasoning-part-1              | Circular reasoning (part 1)                                  | Next    | Circular reasoning | design                  |
| 8    | 2023-01-17-circular-reasoning-part-2              | Circular reasoning (part 2)                                  | Next    | Circular reasoning | design                  |
| 9    | 2023-01-18-making-a-hash-of-it                    | Making a hash of it                                          | Next    |                    | numerics                |
| 10   | 2023-01-19-a-numbers-game                         | A numbers game                                               | Next    |                    | numerics                |
| 11   | 2023-01-19-con-sequential-objector                | con-Sequential objector                                      | Next    |                    | design                  |
| 12   | 2023-01-27-doing-a-number                         | Doing a number on Numbers                                    | Next    |                    | numerics                |
| 13   | 2023-01-27-first-code                             | First code                                                   | Next    |                    | design                  |
| 14   | 2023-02-03-a-road-to-maps                         | A road to maps                                               | Next    |                    | data-structures         |
| 15   | 2023-02-03-laziness-and-chunking                  | Laziness and chunking                                        | Next    |                    | data-structures         |
| 16   | 2023-02-05-reductionism                           | Reductionism                                                 | Next    |                    | data-structures         |
| 17   | 2023-02-12-PersistentVector-part-1                | Persistent vectors, Part 1 -- The landscape                  | Next    | Persistent vectors | data-structures         |
| 18   | 2023-02-12-PersistentVector-part-2                | Persistent vectors, Part 2 -- Immutability and persistence   | Next    | Persistent vectors | data-structures         |
| 19   | 2023-02-18-PersistentVector-part-3                | Persistent vectors, Part 3 -- Transiency                     | Next    | Persistent vectors | data-structures         |
| 20   | 2023-02-18-PersistentVector-part-4                | Persistent vectors, Part 4 -- Performance                    | Next    | Persistent vectors | data-structures         |
| 21   | 2024-06-18-mega-dose-of-micro-benchmarks-part-1   | A mega-dose of micro-benchmarks, Part 1 -- Setting the stage | Next    | Micro-benchmarks   | benchmarking            |
| 22   | 2024-06-18-mega-dose-of-micro-benchmarks-part-2   | A mega-dose of micro-benchmarks, Part 2 -- by the numbers    | Next    | Micro-benchmarks   | benchmarking            |
| 23   | 2024-06-18-mega-dose-of-micro-benchmarks-part-3   | A mega-dose of micro-benchmarks, Part 3 -- Finishing touches | Next    | Micro-benchmarks   | benchmarking            |
| 24   | 2024-06-24-corrigendum-static-initialization (*)  | Corrigendum -- Static initialization                         | Next    | Micro-benchmarks   | benchmarking            |
| 25   | 2024-07-02-persistent-hash-map-part-1             | PersistentHashMap, Part 1 -- Making a hash of things         | Next    | PersistentHashMap  | data-structures         |
| 26   | 2024-07-02-persistent-hash-map-part-2             | PersistentHashMap, part 2 -- The root                        | Next    | PersistentHashMap  | data-structures         |
| 27   | 2024-07-02-persistent-hash-map-part-3             | PersistentHashMap, part 3 -- The guts                        | Next    | PersistentHashMap  | data-structures         |
| 28   | 2024-07-02-persistent-hash-map-part-4             | PersistentHashMap, part 4 -- Other matters                   | Next    | PersistentHashMap  | data-structures         |
| 29   | 2024-07-02-persistent-hash-map-part-5             | PersistentHashMap, part 5 -- At a loss                       | Next    | PersistentHashMap  | data-structures         |
| 30   | 2024-07-21-whats-the-point                        | What's the point?  BigDecimal in review                      | Next    |                    | numerics                |
| 31   | 2024-09-05-qualified-methods-for-ClojureCLR       | Qualified methods -- for ClojureCLR                          | CLR     |                    | compiler                |
| 32   | 2024-12-10-say-my-name                            | Say my name. ... Heisenbug.                                  | CLR     |                    | miscellaneous           |
| 33   | 2024-12-22-STM-design                             | STM in Clojure - Design                                      | Next    | STM                | STM                     |
| 34   | 2024-12-22-STM-code                               | STM in Clojure - Code                                        | Next    | STM                | STM                     |
| 35   | 2024-12-22-STM-testing                            | STM in Clojure - Testing                                     | Next    | STM                | STM                     |
| 36   | 2025-02-20-AOT-compilation-issues                 | AOT compilation issues                                       | CLR     |                    | compiler                |
| 37   | 2025-02-28-function-naming                        | The function of naming; the naming of functions              | CLR     |                    | types                   |
| 38   | 2025-03-01-are-you-my-type                        | Are you my type?                                             | CLR     |                    | types                   |
| 39   | 2025-08-31-classic-clojure-compiler-contemplation | C4 - Classic Clojure Compiler Contemplation                  | CLR     | C4                 | compiler design         |
| 40   | 2025-09-01-AST-me-anything                        | C4 - AST me anything                                         | CLR     | C4                 | compiler design         |
| 41   | 2025-09-02-symbolic-of-what                       | C4 - Symbolic of what?                                       | CLR     | C4                 | compiler design         |
| 42   | 2025-09-03-iseq-clarity                           | C4 - ISeq clarity                                            | CLR     | C4                 | compiler design         |
| 43   | 2025-09-04-functional-anatomy                     | C4 - Functional anatomy                                      | CLR     | C4                 | compiler design         |
| 44   | 2025-09-21-typename-syntax-and-resolution         | Typename syntax and resolution in ClojureCLR                 | CLR     |                    | feature                 |
| 45   | 2025-10-15-fn-star-talkin-bout-my-generation      | C4 - fn* -- talkin' 'bout my generation                      | CLR     | C4                 | compiler design         |
| 46   | 2025-10-16-tag-youre-int                          | C4 - Tag! You're int!                                        | CLR     | C4                 | compiler design         |
| 47   | 2025-10-17-primitive-urges                        | C4 - Primitive urges                                         | CLR     | C4                 | compiler design         |
| 48   | 2025-10-18-inline-skating                         | C4 - Inline skating                                          | CLR     | C4                 | compiler design         |
| 49   | 2025-10-19-key-in-site                            | C4 - Key in-site                                             | CLR     | C4                 | compiler design         |
| 50   | 2025-10-20-is-there-a-protocol-for-that           | C4 - Is there a protocol for that?                           | CLR     | C4                 | compiler design         |
| 51   | 2026-04-16-runtime-async                          | Runtime async                                                | CLR     |                    | feature                 |
| 52   | 2026-10-07-stringulation                          | Stringulation                                                | CLR     |                    | performance             |

(*) Front-matter date is 2024-06-18, so its live URL uses `/2024/06/18/`. The URL must stay as it is.

## Drafts

| File                                | Project | Series | Topics          |
| ----------------------------------- | ------- | ------ | --------------- |
| 1999-12-31-symbology-place-holder   | ?       |        |                 |
| 2025-04-20-a-problem-of-hosts       | ?       |        |                 |
| 2025-04-21-i-have-something-to-emit | CLR     | C4     | compiler design |
| 2025-09-14-a-time-for-reflection    | CLR     | C4     | compiler design |
| 2025-09-15-dyn-o-site               | CLR     | C4     | compiler design |
