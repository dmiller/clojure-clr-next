namespace Clojure.IO

open Clojure.Collections

// THis type is needed in the reader and the compiler.
// Reader comes first so we put it here

type IExceptionInfo =
    abstract getData : unit -> IPersistentMap

