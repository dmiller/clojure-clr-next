module Clojure.Collections.Numbers


type NumericOpsType =
    | Long = 0
    | ULong = 1
    | BigInteger = 2
    | Ratio = 3
    | BigDecimal = 4
    | Double = 5

type CompOps = 
    abstract equiv: x:obj * y:obj -> bool
    abstract lt: x:obj * y:obj -> bool
    abstract lte: x:obj * y:obj -> bool
    abstract gte: x:obj * y:obj -> bool





let combinerArray = Array2D.create 
