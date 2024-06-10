using System.Numerics;

namespace TypeDispatch.CSharp;

public class TypeDispatch
{
    public static bool IsNumeric(object o)
    {
        return o != null && IsNumericType(o.GetType());
    }

    public static bool IsNumericType(Type type)
    {
        switch (Type.GetTypeCode(type))
        {
            case TypeCode.SByte:
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
            case TypeCode.Double:
            case TypeCode.Single:
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
                return true;
            default: 
                return type == typeof(BigInteger);
        }
        //if (type == typeof(BigInteger) )
        //    return true;
        //return false;
    }

    public static bool HasSpecialType(Type t)
    {
        return t == typeof(BigInteger) || t == typeof(String) || t == typeof(DateTime) || t == typeof(Uri);
    }

    public static bool TestObjectType(object o)
    {
        return HasSpecialType(o.GetType()); 
    }

}
    

