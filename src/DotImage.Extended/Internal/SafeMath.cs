// Ported from golang.org/x/image/internal/safemath/safemath.go

namespace DotImage.Extended.Internal;

/// <summary>
/// Overflow-safe math functions (ported from Go's safemath package).
/// </summary>
public static class SafeMath
{
    /// <summary>
    /// Returns (x * y * z, true), unless at least one argument is negative or
    /// the computation overflows the int type, in which case it returns
    /// (-1, false).
    /// </summary>
    public static (int Value, bool Ok) Mul3(int x, int y, int z)
    {
        if (x < 0 || y < 0 || z < 0)
        {
            return (-1, false);
        }

        ulong hi = System.Math.BigMul((ulong)x, (ulong)y, out ulong lo);
        if (hi != 0)
        {
            return (-1, false);
        }

        hi = System.Math.BigMul(lo, (ulong)z, out lo);
        if (hi != 0)
        {
            return (-1, false);
        }

        int a = (int)lo;
        if (a < 0 || (ulong)a != lo)
        {
            return (-1, false);
        }

        return (a, true);
    }
}
