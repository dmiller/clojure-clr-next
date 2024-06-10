using System;
using System.Numerics;
using Microsoft.FSharp.Core;

[CompilationMapping(/*Could not decode attribute arguments.*/)]
public static class FsharpTypeDispatch
{
    [Serializable]
    [CompilationMapping(/*Could not decode attribute arguments.*/)]
    public class TypeDispatch
    {
        public static bool IsNumericType(global::System.Type t)
        {
            //IL_0002: Unknown result type (might be due to invalid IL or missing references)
            //IL_0008: Unknown result type (might be due to invalid IL or missing references)
            //IL_0036: Expected I4, but got Unknown
            switch (global::System.Type.GetTypeCode(t) - 5)
            {
                case 0:
                case 1:
                case 2:
                case 3:
                case 4:
                case 5:
                case 6:
                case 7:
                case 8:
                case 9:
                    return true;
                default:
                    return t == typeof(BigInteger);
            }
        }

        public static bool IsNumericType2(global::System.Type t)
        {
            //IL_0002: Unknown result type (might be due to invalid IL or missing references)
            //IL_0008: Unknown result type (might be due to invalid IL or missing references)
            //IL_0036: Expected I4, but got Unknown
            switch (global::System.Type.GetTypeCode(t) - 5)
            {
                case 0:
                case 1:
                case 2:
                case 3:
                case 4:
                case 5:
                case 6:
                case 7:
                case 8:
                case 9:
                    return true;
                default:
                    return HashCompare.GenericEqualityIntrinsic<global::System.Type>(t, typeof(BigInteger));
            }
        }
    }
}
using System;
using System.Numerics;
using Microsoft.FSharp.Core;

[CompilationMapping(/*Could not decode attribute arguments.*/)]
public static class FsharpTypeDispatch
{
    [Serializable]
    [CompilationMapping(/*Could not decode attribute arguments.*/)]
    public class TypeDispatch
    {
        public static bool IsNumericType(global::System.Type t)
        {
            //IL_0002: Unknown result type (might be due to invalid IL or missing references)
            //IL_0008: Unknown result type (might be due to invalid IL or missing references)
            //IL_0036: Expected I4, but got Unknown
            switch (global::System.Type.GetTypeCode(t) - 5)
            {
                case 0:
                case 1:
                case 2:
                case 3:
                case 4:
                case 5:
                case 6:
                case 7:
                case 8:
                case 9:
                    return true;
                default:
                    return t == typeof(BigInteger);
            }
        }

        public static bool IsNumericType2(global::System.Type t)
        {
            //IL_0002: Unknown result type (might be due to invalid IL or missing references)
            //IL_0008: Unknown result type (might be due to invalid IL or missing references)
            //IL_0036: Expected I4, but got Unknown
            switch (global::System.Type.GetTypeCode(t) - 5)
            {
                case 0:
                case 1:
                case 2:
                case 3:
                case 4:
                case 5:
                case 6:
                case 7:
                case 8:
                case 9:
                    return true;
                default:
                    return HashCompare.GenericEqualityIntrinsic<global::System.Type>(t, typeof(BigInteger));
            }
        }
    }
}
namespace <StartupCode$_out1-il_tmp>
{
    internal static class $FsharpTypeDispatch
    {
    }
}
