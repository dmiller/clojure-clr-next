namespace Clojure.Numerics


// I moved this out of Interfaces.fs in Clojure.Collections because I needed to define BigInt to support this interface.
// Also I added implementing this interface to Ratio also, while I was at it.  Not sure why it wasn't there.


[<AllowNullLiteral>]
type IHashEq =
    abstract hasheq: unit -> int