namespace BabyNumbers.CSharp;

using System;
using System.Globalization;

public static class Numbers
{

    public static double ConvertToDouble(object o)
    {
        switch (Type.GetTypeCode(o.GetType()))
        {
            case TypeCode.Byte:
                return (double)(byte)o;
            case TypeCode.Char:
                return (double)(Char)o;
            case TypeCode.Decimal:
                return (double)(decimal)o;
            case TypeCode.Double:
                return (double)o;
            case TypeCode.Int16:
                return (double)(short)o;
            case TypeCode.Int32:
                return (double)(int)o;
            case TypeCode.Int64:
                return (double)(long)o;
            case TypeCode.SByte:
                return (double)(sbyte)o;
            case TypeCode.Single:
                return (double)(float)o;
            case TypeCode.UInt16:
                return (double)(ushort)o;
            case TypeCode.UInt32:
                return (double)(uint)o;
            case TypeCode.UInt64:
                return (double)(ulong)o;
            default:
                return Convert.ToDouble(o, CultureInfo.InvariantCulture);
        }
    }

    public static ulong ConvertToULong(object o)
    {
        switch (Type.GetTypeCode(o.GetType()))
        {
            case TypeCode.Byte:
                return (ulong)(Byte)o;
            case TypeCode.Char:
                return (ulong)(Char)o;
            case TypeCode.Decimal:
                return (ulong)(decimal)o;
            case TypeCode.Double:
                return (ulong)(double)o;
            case TypeCode.Int16:
                return (ulong)(short)o;
            case TypeCode.Int32:
                return (ulong)(int)o;
            case TypeCode.Int64:
                return (ulong)(long)o;
            case TypeCode.SByte:
                return (ulong)(sbyte)o;
            case TypeCode.Single:
                return (ulong)(float)o;
            case TypeCode.UInt16:
                return (ulong)(ushort)o;
            case TypeCode.UInt32:
                return (ulong)(uint)o;
            case TypeCode.UInt64:
                return (ulong)o;
            default:
                return Convert.ToUInt64(o, CultureInfo.InvariantCulture);
        }
    }

    public static long ConvertToLong(object o)
    {
        switch (Type.GetTypeCode(o.GetType()))
        {
            case TypeCode.Byte:
                return (long)(Byte)o;
            case TypeCode.Char:
                return (long)(Char)o;
            case TypeCode.Decimal:
                return (long)(decimal)o;
            case TypeCode.Double:
                return (long)(double)o;
            case TypeCode.Int16:
                return (long)(short)o;
            case TypeCode.Int32:
                return (long)(int)o;
            case TypeCode.Int64:
                return (long)o;
            case TypeCode.SByte:
                return (long)(sbyte)o;
            case TypeCode.Single:
                return (long)(float)o;
            case TypeCode.UInt16:
                return (long)(ushort)o;
            case TypeCode.UInt32:
                return (long)(uint)o;
            case TypeCode.UInt64:
                return (long)(ulong)o;
            default:
                return Convert.ToInt64(o, CultureInfo.InvariantCulture);
        }
    }

   public interface Ops
    {
        Ops combine(Ops y);
        Ops opsWith(LongOps x);
        Ops opsWith(ULongOps x);
        Ops opsWith(DoubleOps x);
        Ops opsWith(RatioOps x);
        Ops opsWith(ClrDecimalOps x);
        Ops opsWith(BigIntOps x);
        Ops opsWith(BigDecimalOps x);

        bool equiv(object x, object y);
    }

    public abstract class OpsP : Ops
    {

        public abstract Ops combine(Ops y);
        public abstract Ops opsWith(LongOps x);
        public abstract Ops opsWith(ULongOps x);
        public abstract Ops opsWith(DoubleOps x);
        public abstract Ops opsWith(RatioOps x);
        public abstract Ops opsWith(ClrDecimalOps x);
        public abstract Ops opsWith(BigIntOps x);
        public abstract Ops opsWith(BigDecimalOps x);

        public abstract bool equiv(object x, object y);
    }


    public static bool equal(object x, object y)
    {
        return category(x) == category(y)
            && ops(x).combine(ops(y)).equiv(x, y);
    }

    public static bool equiv(object x, object y) { return ops(x).combine(ops(y)).equiv(x, y); }

    public static bool equiv(double x, double y) { return x == y; }
    public static bool equiv(long x, long y) { return x == y; }
    public static bool equiv(ulong x, ulong y) { return x == y; }
    public static bool equiv(decimal x, decimal y) { return x == y; }
    public static bool equiv(double x, Object y) { return x == ConvertToDouble(y); }
    public static bool equiv(Object x, double y) { return ConvertToDouble(x) == y; }
    public static bool equiv(double x, long y) { return x == y; }
    public static bool equiv(long x, double y) { return x == y; }
    public static bool equiv(double x, ulong y) { return x == y; }
    public static bool equiv(ulong x, double y) { return x == y; }
    public static bool equiv(long x, Object y) { return equiv((Object)x, y); }
    public static bool equiv(Object x, long y) { return equiv(x, (Object)y); }
    public static bool equiv(ulong x, Object y) { return equiv((Object)x, y); }
    public static bool equiv(Object x, ulong y) { return equiv(x, (Object)y); }
    public static bool equiv(long x, ulong y) { return equiv((Object)x, (Object)y); }
    public static bool equiv(ulong x, long y) { return equiv((Object)x, (Object)y); }

    public sealed class LongOps : Ops
    {
        public Ops combine(Ops y)
        {
            return y.opsWith(this);
        }

        public Ops opsWith(LongOps x)
        {
            return this;
        }

        public Ops opsWith(DoubleOps x)
        {
            return DOUBLE_OPS;
        }

        public Ops opsWith(RatioOps x)
        {
            return RATIO_OPS;
        }

        public Ops opsWith(BigIntOps x)
        {
            return BIGINT_OPS;
        }

        public Ops opsWith(BigDecimalOps x)
        {
            return BIGDECIMAL_OPS;
        }

        public Ops opsWith(ULongOps x)
        {
            return BIGINT_OPS;
        }

        public Ops opsWith(ClrDecimalOps x)
        {
            return CLRDECIMAL_OPS;
        }

        public bool equiv(object x, object y)
        {
            return ConvertToLong(x) == ConvertToLong(y);
        }
    }

    public sealed class ULongOps : Ops
    {
        public Ops combine(Ops y)
        {
            return y.opsWith(this);
        }

        public Ops opsWith(LongOps x)
        {
            return BIGINT_OPS;
        }

        public Ops opsWith(DoubleOps x)
        {
            return DOUBLE_OPS;
        }

        public Ops opsWith(RatioOps x)
        {
            return RATIO_OPS;
        }

        public Ops opsWith(BigIntOps x)
        {
            return BIGINT_OPS;
        }

        public Ops opsWith(BigDecimalOps x)
        {
            return BIGDECIMAL_OPS;
        }

        public Ops opsWith(ULongOps x)
        {
            return this;
        }

        public Ops opsWith(ClrDecimalOps x)
        {
            return CLRDECIMAL_OPS;
        }


        public bool equiv(object x, object y)
        {
            return false;
        }
    }

    public sealed class DoubleOps : OpsP
    {
        public override Ops combine(Ops y)
        {
            return y.opsWith(this);
        }

        public override Ops opsWith(LongOps x)
        {
            return this;
        }

        public override Ops opsWith(DoubleOps x)
        {
            return this;
        }

        public override Ops opsWith(RatioOps x)
        {
            return this;
        }

        public override Ops opsWith(BigIntOps x)
        {
            return this;
        }

        public override Ops opsWith(BigDecimalOps x)
        {
            return this;
        }

        public override Ops opsWith(ULongOps x)
        {
            return this;
        }

        public override Ops opsWith(ClrDecimalOps x)
        {
            return this;
        }


        public override bool equiv(object x, object y)
        {
            return ConvertToDouble(x) == ConvertToDouble(y);
        }
    }

    public sealed class RatioOps : OpsP
    {
        public override Ops combine(Ops y)
        {
            return y.opsWith(this);
        }

        public override Ops opsWith(LongOps x)
        {
            return this;
        }

        public override Ops opsWith(DoubleOps x)
        {
            return DOUBLE_OPS;
        }

        public override Ops opsWith(RatioOps x)
        {
            return this;
        }

        public override Ops opsWith(BigIntOps x)
        {
            return this;
        }

        public override Ops opsWith(BigDecimalOps x)
        {
            return BIGDECIMAL_OPS;
        }

        public override Ops opsWith(ULongOps x)
        {
            return this;
        }

        public override Ops opsWith(ClrDecimalOps x)
        {
            return BIGDECIMAL_OPS;
        }

        public override bool equiv(object x, object y)
        {
            return false;
        }

    }

    public class ClrDecimalOps : Ops
    {
        public Ops combine(Ops y)
        {
            return y.opsWith(this);
        }

        public Ops opsWith(LongOps x)
        {
            return this;
        }

        public Ops opsWith(DoubleOps x)
        {
            return DOUBLE_OPS;
        }

        public Ops opsWith(RatioOps x)
        {
            return BIGDECIMAL_OPS;
        }

        public Ops opsWith(BigIntOps x)
        {
            return BIGDECIMAL_OPS;
        }

        public Ops opsWith(BigDecimalOps x)
        {
            return BIGDECIMAL_OPS;
        }

        public Ops opsWith(ULongOps x)
        {
            return this;
        }

        public Ops opsWith(ClrDecimalOps x)
        {
            return this;
        }


        public bool equiv(object x, object y)
        {
            return false;
        }
    }

    public class BigIntOps : OpsP
    {
        public override Ops combine(Ops y)
        {
            return y.opsWith(this);
        }

        public override Ops opsWith(LongOps x)
        {
            return this;
        }

        public override Ops opsWith(DoubleOps x)
        {
            return DOUBLE_OPS;
        }

        public override Ops opsWith(RatioOps x)
        {
            return RATIO_OPS;
        }

        public override Ops opsWith(BigIntOps ops)
        {
            return this;
        }

        public override Ops opsWith(BigDecimalOps ops)
        {
            return BIGDECIMAL_OPS;
        }

        public override Ops opsWith(ULongOps x)
        {
            return this;
        }

        public override Ops opsWith(ClrDecimalOps x)
        {
            return BIGDECIMAL_OPS;
        }


        public override bool equiv(object x, object y)
        {
            return false;
        }
    }

    public class BigDecimalOps : OpsP
    {
        public override Ops combine(Ops y)
        {
            return y.opsWith(this);
        }

        public override Ops opsWith(LongOps x)
        {
            return this;
        }

        public override Ops opsWith(DoubleOps x)
        {
            return DOUBLE_OPS;
        }

        public override Ops opsWith(RatioOps x)
        {
            return this;
        }

        public override Ops opsWith(BigIntOps x)
        {
            return this;
        }

        public override Ops opsWith(BigDecimalOps x)
        {
            return this;
        }

        public override Ops opsWith(ULongOps x)
        {
            return this;
        }

        public override Ops opsWith(ClrDecimalOps x)
        {
            return this;
        }


        public override bool equiv(object x, object y)
        {
            return false;
        }
    }

    public static readonly LongOps LONG_OPS = new LongOps();
    public static readonly ULongOps ULONG_OPS = new ULongOps();
    public static readonly DoubleOps DOUBLE_OPS = new DoubleOps();
    public static readonly RatioOps RATIO_OPS = new RatioOps();
    public static readonly ClrDecimalOps CLRDECIMAL_OPS = new ClrDecimalOps();
    public static readonly BigIntOps BIGINT_OPS = new BigIntOps();
    public static readonly BigDecimalOps BIGDECIMAL_OPS = new BigDecimalOps();

    public enum Category { Integer, Floating, Decimal, Ratio }  // TODO

    public static Ops ops(Object x)
    {
        Type xc = x.GetType();

        switch (Type.GetTypeCode(xc))
        {
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
                return LONG_OPS;

            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
                return ULONG_OPS;

            case TypeCode.Single:
            case TypeCode.Double:
                return DOUBLE_OPS;

            case TypeCode.Decimal:
                return CLRDECIMAL_OPS;

            default:
                    return LONG_OPS;
        }
    }



    public static Category category(object x)
    {
        Type xc = x.GetType();
        if (xc == typeof(Int32) || xc == typeof(Int64))
            return Category.Integer;
        else if (xc == typeof(float) || xc == typeof(double))
            return Category.Floating;
        else if (xc == typeof(Uri))
            return Category.Integer;
        else if (xc == typeof(string))
            return Category.Ratio;
        else if (xc == typeof(DateTime) || xc == typeof(decimal))
            return Category.Decimal;
        else
            return Category.Integer;
    }


}