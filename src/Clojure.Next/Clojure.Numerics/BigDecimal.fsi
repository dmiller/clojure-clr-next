namespace Clojure.Numerics

/// Indicates the rounding mode to use
type RoundingMode =
    | Up
    /// Truncate (round toward 0)
    | Down
    /// Round toward positive infinity
    | Ceiling
    /// Round toward negative infinity
    | Floor
    /// Round to nearest neighbor, round up if equidistant
    | HalfUp
    /// Round to nearest neighbor, round down if equidistant
    | HalfDown
    /// Round to nearest neighbor, round to even neighbor if equidistant
    | HalfEven
    /// <summary>
    /// Do not do any rounding
    /// </summary>
    /// <remarks>This value is not part of the GDAS, but is in java.math.BigDecimal</remarks>
    | Unnecessary
/// Context for rounding
[<StructAttribute>]
type Context =
    { precision: uint32
      roundingMode: RoundingMode }
    /// Create a Context from the given precision and rounding mode
    static member Create: precision:uint32 * roundingMode:RoundingMode -> Context
    /// Create a Context with specified precision and roundingMode = HalfEven
    static member ExtendedDefault: precision:uint32 -> Context
    /// Standard precision for 128-bit decimal
    static member Decimal128: Context
    /// Standard precision for 32-bit decimal
    static member Decimal32: Context
    /// Standard precision for 64-bit decimal
    static member Decimal64: Context
    /// Default mode
    static member Default: Context
    /// No precision limit
    static member Unlimited: Context

/// <summary>
/// Immutable, arbitrary precision, signed decimal.
/// </summary>
/// <remarks>
/// <para>This class is inspired by the General Decimal Arithmetic Specification (http://speleotrove.com/decimal/decarith.html,
/// (PDF: http://speleotrove.com/decimal/decarith.pdf).  However, at the moment, the interface and capabilities comes closer
/// to java.math.BigDecimal, primarily because I only needed to mimic j.m.BigDecimal's capabilities to provide a minimum set
/// of functionality for ClojureCLR.</para>
/// <para>Because of this, as in j.m.BigDecimal, the implementation is closest to the X3.274 subset described in Appendix A
/// of the GDAS: infinite values, NaNs, subnormal values and negative zero are not represented, and most conditions throw exceptions.
/// Exponent limits in the context are not implemented, except a limit to the range of an Int32.
/// However, we do not do "conversion to shorter" for arith ops.</para>
/// <para>The representation is an arbitrary precision integer (the signed coefficient, also called the unscaled value)
/// and an exponent.  The exponent is limited to the range of an Int32.
/// The value of a BigDecimal representation is <c>coefficient * 10^exponent</c>. </para>
/// <para> Note: the representation in the GDAS is
/// [sign,coefficient,exponent] with sign = 0/1 for (pos/neg) and an unsigned coefficient.
/// This yields signed zero, which we do not have.
/// We used a BigInteger for the signed coefficient.
/// That class does not have a representation for signed zero.</para>
/// <para>Note: Compared to j.m.BigDecimal, our coefficient = their <c>unscaledValue</c>
/// and our exponent is the negation of their <c>scale</c>.</para>
/// <para>The representation also tracks the number of significant digits.  This is usually the number of digits in the coefficient,
/// except when the coeffiecient is zero.  This value is computed lazily and cached.</para>
/// <para>This is not a clean-room implementation.
/// I examined at other code, especially OpenJDK implementation of java.math.BigDecimal,
/// to look for special cases and other gotchas.  Then I looked away.
/// I have tried to give credit in the few places where I pretty much did unthinking translation.
/// However, there are only so many ways to skim certain cats, so some similarities are unavoidable.</para>
/// </remarks>
type BigDecimal =
    class
        interface System.IConvertible
        interface System.IEquatable<BigDecimal>
        interface System.IComparable
        interface System.IComparable<BigDecimal>
        private new: coeff:System.Numerics.BigInteger * exp:int * precision:uint -> BigDecimal
        static member (%): x:BigDecimal * y:BigDecimal -> BigDecimal
        /// Multiplies two BigDecimal values.
        static member (*): x:BigDecimal * y:BigDecimal -> BigDecimal
        /// Adds two BigDecimal values
        static member (+): x:BigDecimal * y:BigDecimal -> BigDecimal
        /// Subtracts one BigDecimal value from another
        static member (-): x:BigDecimal * y:BigDecimal -> BigDecimal
        static member (/): x:BigDecimal * y:BigDecimal -> BigDecimal
        /// Returns a value that indicates whether a BigDecimal instance is less than another BigDecimal instance.
        static member (<): left:BigDecimal * right:BigDecimal -> bool
        /// Shifts A BigDecimal value a specified number of digits to the left.
        static member (<<<): x:BigDecimal * shift:int -> BigDecimal
        /// Returns a value that indicates whether a BigDecimal instance is less than or equal to another BigDecimal instance.
        static member (<=): left:BigDecimal * right:BigDecimal -> bool
        /// Returns a value that indicates whether a BigDecimal instance is not equal to another BigDecimal instance.
        static member (<>): left:BigDecimal * right:BigDecimal -> bool
        /// Returns a value that indicates whether a BigDecimal instance is greater than another BigDecimal instance.
        static member (>): left:BigDecimal * right:BigDecimal -> bool
        /// Returns a value that indicates whether a BigDecimal instance is great  than or equal to another BigDecimal instance.
        static member (>=): left:BigDecimal * right:BigDecimal -> bool
        /// Shifts A BigDeimal value a specified number of digits to the right.
        static member (>>>): x:BigDecimal * shift:int -> BigDecimal
        /// Negates a BigDecimal value.
        static member (~-): x:BigDecimal -> BigDecimal
        /// Gets the absolute value a BigDecimal value.
        static member Abs: x:BigDecimal -> BigDecimal
        /// Gets the absolute value a BigDecimal value, rounded per the given context.
        static member Abs: x:BigDecimal * c:Context -> BigDecimal
        /// Adds two BigDecimal instances
        static member Add: x:BigDecimal * y:BigDecimal -> BigDecimal
        /// Adds two BigDecimal values, result rounded by the given context
        static member Add: x:BigDecimal * y:BigDecimal * c:Context -> BigDecimal
        /// Create a BigDecimal from a character array
        static member Create: v:char array -> BigDecimal
        /// Create a BigDecimal from a string representation of the value.
        static member Create: v:System.String -> BigDecimal
        /// Create a BigDecimal from a Double value.
        static member Create: v:double -> BigDecimal
        /// Create a BigDecimal from a Decimal value.
        static member Create: v:decimal -> BigDecimal
        /// Create a BigDecimal from a BigInteger value.
        static member Create: v:System.Numerics.BigInteger -> BigDecimal
        /// Create a BigDecimal from a UInt64 value.
        static member Create: v:uint64 -> BigDecimal
        /// Create a BigDecimal from an Int64 value.
        static member Create: v:int64 -> BigDecimal
        /// Create a BigDecimal from an Int32 value.
        static member Create: v:int32 -> BigDecimal
        /// Create a copy of BigDecimal -- rethink your priorities, BDs are immutable, so why?
        static member Create: bi:BigDecimal -> BigDecimal
        /// Create a BigDecimal from a character array, rounded per the given context
        static member Create: v:char array * c:Context -> BigDecimal
        /// Create a BigDecimal from a string representation of the value, rounded per the given context.
        static member Create: v:System.String * c:Context -> BigDecimal
        /// Create a BigDecimal from the given coefficient, exponent.
        static member Create: coeff:System.Numerics.BigInteger * exp:int -> BigDecimal
        /// Create a BigDecimal from a segment of a character array
        static member Create: v:char array * offset:int * len:int -> BigDecimal
        /// Create a BigDecimal from a segment of a character array, rounded per the given context
        static member Create: v:char array * offset:int * len:int * c:Context -> BigDecimal
        /// Create a BigDecimal from a Double value, rounded per the given context.
        static member CreateC: v:double * c:Context -> BigDecimal
        /// Create a BigDecimal from a Decimal value, rounded per the given context.
        static member CreateC: v:decimal * c:Context -> BigDecimal
        /// Create a BigDecimal from a BigInteger value, rounded per the given context.
        static member CreateC: v:System.Numerics.BigInteger * c:Context -> BigDecimal
        /// Create a BigDecimal from a UInt64 value, rounded per the given context.
        static member CreateC: v:uint64 * c:Context -> BigDecimal
        /// Create a BigDecimal from an Int64 value, rounded per the given context.
        static member CreateC: v:int64 * c:Context -> BigDecimal
        /// Create a BigDecimal from an Int32 value, rounded per the given context.
        static member CreateC: v:int32 * c:Context -> BigDecimal
        static member DivRem: x:BigDecimal * y:BigDecimal * remainder:outref<BigDecimal> -> BigDecimal
        static member DivRem: x:BigDecimal * y:BigDecimal * c:Context * remainder:outref<BigDecimal> -> BigDecimal
        static member Divide: x:BigDecimal * y:BigDecimal -> BigDecimal
        static member Divide: x:BigDecimal * y:BigDecimal * c:Context -> BigDecimal
        static member Mod: x:BigDecimal * y:BigDecimal -> BigDecimal
        static member Mod: x:BigDecimal * y:BigDecimal * c:Context -> BigDecimal
        /// Returns the product of two BigDecimal values.
        static member Multiply: x:BigDecimal * y:BigDecimal -> BigDecimal
        /// Returns the product of two BigDecimal values, result rounded per the given context.
        static member Multiply: x:BigDecimal * y:BigDecimal * c:Context -> BigDecimal
        /// Returns a BigDecimal whose value is the negation of the specified BigDecimal instance.
        static member Negate: x:BigDecimal -> BigDecimal
        /// Returns a BigDecimal whose value is the negation of the specified BigDecimal instance, rounded per the given context.
        static member Negate: x:BigDecimal * c:Context -> BigDecimal
        /// Converts a representation of a number, given as an array of characters,  to its BigDecimal equivalent.
        static member Parse: v:char array -> BigDecimal
        /// Converts a representation of a number, contained in the specified read-only character span, to its BigDecimal equivalent.
        static member Parse: s:System.ReadOnlySpan<char> -> BigDecimal
        /// Converts a string representation of a number to its BigDecimal equivalent.
        static member Parse: s:System.String -> BigDecimal
        /// Converts a representation of a number, given as an array of characters,  to its BigDecimal equivalent, rounded per the given context
        static member Parse: v:char array * c:Context -> BigDecimal
        /// Converts a representation of a number, contained in the specified read-only character span, to its BigDecimal equivalent, rounded per the given Context.
        static member Parse: s:System.ReadOnlySpan<char> * c:Context -> BigDecimal
        /// Converts a string representation of a number to its BigDecimal equivalent, rounded per the given Context.
        static member Parse: s:System.String * c:Context -> BigDecimal
        /// Converts a representation of a number, given as a segment of an array of characters,  to its BigDecimal equivalent.
        static member Parse: v:char array * offset:int * len:int -> BigDecimal
        /// Converts a representation of a number, given as a segment of an array of characters,  to its BigDecimal equivalent, rounded per the given context
        static member Parse: v:char array * offset:int * len:int * c:Context -> BigDecimal
        /// Raises a BigDecimal value to a specified integer power.
        static member Power: x:BigDecimal * n:int -> BigDecimal
        /// Raises a BigDecimal value to a specified integer power.result rounded according to context
        static member Power: x:BigDecimal * n:int * c:Context -> BigDecimal
        /// Rescale first BigDecimal to the exponent of the second BigDecimal
        static member Quantize: lhs:BigDecimal * rhs:BigDecimal * mode:RoundingMode -> BigDecimal
        /// Return an equivalent-valued BigDecimal rescaled to the given exponent.
        static member Rescale: lhs:BigDecimal * newExponent:int * mode:RoundingMode -> BigDecimal
        /// Return a BigDecimal rounded to the given context
        static member Round: v:BigDecimal * c:Context -> BigDecimal
        /// Subtracts one BigDecimal value from another
        static member Subtract: x:BigDecimal * y:BigDecimal -> BigDecimal
        static member Subtract: x:BigDecimal * y:BigDecimal * c:Context -> BigDecimal
        /// Tries to convert a representation of a number, given as an array of characters, to its BigDecimal equivalent, and returns a value indicating if it succeeded.
        static member TryParse: a:char array * value:outref<BigDecimal> -> bool
        /// Tries to convert a representation of a number, contained in the specified read-only character span, to its BigDecimal equivalent, and returns a value indicating if it succeeded.
        static member TryParse: s:System.ReadOnlySpan<char> * value:outref<BigDecimal> -> bool
        /// Tries to convert a string representation of a number to its BigDecimal equivalent, and returns a value indicating if it succeeded.
        static member TryParse: s:System.String * value:outref<BigDecimal> -> bool
        /// Tries to convert a representation of a number, given as an array of characters, to its BigDecimal equivalent (rounded per the given context), and returns a value indicating if it succeeded.
        static member TryParse: a:char array * c:Context * value:outref<BigDecimal> -> bool
        /// Tries to convert a representation of a number, contained in the specified read-only character span, to its BigDecimal equivalent (rounded per the given context), and returns a value indicating if it succeeded.
        static member TryParse: s:System.ReadOnlySpan<char> * c:Context * value:outref<BigDecimal> -> bool
        /// Tries to convert a string representation of a number to its BigDecimal equivalent (rounded per the given context), and returns a value indicating if it succeeded.
        static member TryParse: s:System.String * c:Context * value:outref<BigDecimal> -> bool
        /// Tries to convert a representation of a number, given as a segment of an array of characters, to its BigDecimal equivalent, and returns a value indicating if it succeeded.
        static member TryParse: a:char array * offset:int * len:int * value:outref<BigDecimal> -> bool
        /// Tries to convert a representation of a number, given as a segment of an array of characters, to its BigDecimal equivalent (rounded per the given context), and returns a value indicating if it succeeded.
        static member TryParse: a:char array * offset:int * len:int * c:Context * value:outref<BigDecimal> -> bool

        /// Returns a value that indicates whether a BigDecimal instance is equal to another BigDecimal instance.
        static member op__Equality: left:BigDecimal * right:BigDecimal -> bool



        /// Gets the absolute value of this BigDecimal instance.
        member Abs: unit -> BigDecimal
        /// Gets the absolute value of this BigDecimal instance, rounded per the given context.
        member Abs: c:Context -> BigDecimal
        /// Adds this BigDecimal instance to another.
        member Add: y:BigDecimal -> BigDecimal
        /// Adds this BigDecimal instance to another, result rounded by the given context.
        member Add: y:BigDecimal * c:Context -> BigDecimal
        /// Returns the quotient of this BigDecimal instance divided by another, with the remainder returned in an output parameter.
        member DivRem: y:BigDecimal * remainder:outref<BigDecimal> -> BigDecimal
        /// Returns the quotient of this BigDecimal instance divided by another (result roundered per the given context), with the remainder returned in an output parameter.
        member DivRem: y:BigDecimal * c:Context * remainder:outref<BigDecimal> -> BigDecimal
        /// Divides this BigDecimal instance by another, rounded per the given context.
        member Divide: divisor:BigDecimal -> BigDecimal
        /// Divides this BigDecimal instance by another, rounded per the given context.
        member Divide: rhs:BigDecimal * c:Context -> BigDecimal
        /// Computes the (BigDecimal) integer part of the quotient x/y.
        member DivideInteger: y:BigDecimal -> BigDecimal
        /// Computes the BigDecimal which is the integer part of the quotient x/y, result reounded per the context
        member DivideInteger: y:BigDecimal * c:Context -> BigDecimal
        override Equals: obj:obj -> bool
        override GetHashCode: unit -> int
        member Mod: y:BigDecimal -> BigDecimal
        member Mod: y:BigDecimal * c:Context -> BigDecimal
        /// Shifts this BigDeimal value a specified number of digits to the right.
        member MovePointLeft: n:int -> BigDecimal
        /// Shifts this BigDeimal value a specified number of digits to the left.
        member MovePointRight: n:int -> BigDecimal
        /// Returns the product of this BigDecimal instance and a second BigDecimal value.
        member Multiply: y:BigDecimal -> BigDecimal
        /// Returns the product of this BigDecimal instance and a second BigDecimal value, result rounded per the given context.
        member Multiply: y:BigDecimal * c:Context -> BigDecimal
        /// Returns a BigDecimal whose value is the negation of this BigDecimal instance.
        member Negate: unit -> BigDecimal
        /// Returns a BigDecimal whose value is the negation of this BigDecimal instance, rounded per the given context.
        member Negate: c:Context -> BigDecimal
        /// Raises this BigDecimal instance to a specified integer power.
        member Power: n:int -> BigDecimal
        /// Raises this BigDecimal instance to a specified integer power, rounded according to given context.
        member Power: n:int * c:Context -> BigDecimal
        /// Rescale this to the exponent of the provided BigDecimal
        member Quantize: v:BigDecimal * mode:RoundingMode -> BigDecimal
        /// Return a BigDecimal rounded to the given context
        member Round: c:Context -> BigDecimal
        /// Returns the sign (-1, 0, +1)
        member Signum: unit -> int
        /// Return a BigDecimal numerically equal to this one, but with any trailing zeros removed.
        member StripTrailingZeros: unit -> BigDecimal
        /// Subtracts another BigDecimal instance from this instance.
        member Subtract: y:BigDecimal -> BigDecimal
        /// Subtracts another BigDecimal instance from this instance, result rounded by the given context
        member Subtract: y:BigDecimal * c:Context -> BigDecimal
        /// Convert this BigDecimal instance to a BigInteger by truncation.
        member ToBigInteger: unit -> System.Numerics.BigInteger
        /// Converts the numeric value of this instance to its equivalent string representation.
        member ToScientificString: unit -> string
        /// Converts the numeric value of this instance to its equivalent string representation.
        override ToString: unit -> string
        /// Returns the coefficent
        member Coefficient: System.Numerics.BigInteger
        /// Returns the exponent
        member Exponent: int
        /// Is this BigDecimal instance value negative?
        member IsNegative: bool
        /// Is this BigDecimal instance value positive?
        member IsPositive: bool
        /// is the precision computed?
        member IsPrecisionKnown: bool
        /// Does this BigDecimal have zero value?
        member IsZero: bool
        /// Gets the precision (will compute if necessary and cache)
        member Precision: uint
        /// Gets the precision. Won't compute if not yet determined.  Value 0 => not computed.
        member RawPrecision: uint
        /// A BigDecimal with value = 1
        static member One: BigDecimal
        /// A BigDecimal with value = 10
        static member Ten: BigDecimal
        ///  A BigDecimal with value = 0
        static member Zero: BigDecimal
    end
