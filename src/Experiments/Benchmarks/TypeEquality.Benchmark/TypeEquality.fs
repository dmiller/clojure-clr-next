module TypeEquality

open System
open System.Numerics


let HasSpecialTypeEquals(t : Type) = 
    t = typeof<BigInteger> || t = typeof<String> || t = typeof<DateTime> || t = typeof<Uri>

let HasSpecialTypeEquals2(t : Type) = 
    typeof<BigInteger>.Equals(t) || typeof<String>.Equals(t) || typeof<DateTime>.Equals(t) || typeof<Uri>.Equals(t)

let HasSpecialTypeEqualsOp(t : Type) = 
    Type.op_Equality(t,typeof<BigInteger>) || Type.op_Equality(t,typeof<String>) || Type.op_Equality(t,typeof<DateTime>) || Type.op_Equality(t,typeof<Uri>)


let HasSpecialTypeRefEquals(t : Type) = 
    Object.ReferenceEquals(t,typeof<BigInteger>) || Object.ReferenceEquals(t,typeof<String>) || Object.ReferenceEquals(t,typeof<DateTime>) || Object.ReferenceEquals(t,typeof<Uri>)


let TestObjTypeByCast(o: obj) =
    o :? BigInteger || o :? String || o :? DateTime || o :? Uri

let TestObjByInstanceOfCheck(o: obj) =
    typeof<BigInteger>.IsInstanceOfType(o) || typeof<String>.IsInstanceOfType(o) || typeof<DateTime>.IsInstanceOfType(o) || typeof<Uri>.IsInstanceOfType(o)

let TestObjTypeByType(o: obj) =
    HasSpecialTypeEquals (o.GetType()) 

let TestObjTypeByTypeMatch(o: obj) =
    match o with
    | :? BigInteger -> true
    | :? String -> true
    | :? DateTime -> true
    | :? Uri -> true
    | _ -> false


    //public static bool HasSpecialType(Type t)
    //{
    //    return t == typeof(BigInteger) || t == typeof(String) || t == typeof(DateTime) || t == typeof(Uri);
    //}
