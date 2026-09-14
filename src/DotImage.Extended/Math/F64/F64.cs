// Ported from golang.org/x/image/math/f64/f64.go

namespace DotImage.Extended.Math.F64;

/// <summary>A 2-element float64 vector.</summary>
public readonly struct Vec2
{
    private readonly double _0, _1;
    public Vec2(double x, double y) { _0 = x; _1 = y; }
    public double X => _0;
    public double Y => _1;
    public double this[int i] => i switch { 0 => _0, 1 => _1, _ => throw new IndexOutOfRangeException() };
}

/// <summary>A 3-element float64 vector.</summary>
public readonly struct Vec3
{
    private readonly double _0, _1, _2;
    public Vec3(double x, double y, double z) { _0 = x; _1 = y; _2 = z; }
    public double this[int i] => i switch
    {
        0 => _0,
        1 => _1,
        2 => _2,
        _ => throw new IndexOutOfRangeException(),
    };
}

/// <summary>A 4-element float64 vector.</summary>
public readonly struct Vec4
{
    private readonly double _0, _1, _2, _3;
    public Vec4(double x, double y, double z, double w) { _0 = x; _1 = y; _2 = z; _3 = w; }
    public double this[int i] => i switch
    {
        0 => _0,
        1 => _1,
        2 => _2,
        3 => _3,
        _ => throw new IndexOutOfRangeException(),
    };
}

/// <summary>A 3x3 matrix in row major order. m[3*r + c] is the r'th row and c'th column.</summary>
public readonly struct Mat3(double m00, double m01, double m02, double m10, double m11, double m12, double m20, double m21, double m22)
{
    private readonly double[] _m = [m00, m01, m02, m10, m11, m12, m20, m21, m22];
    public double this[int i] => _m[i];
}

/// <summary>A 4x4 matrix in row major order. m[4*r + c] is the r'th row and c'th column.</summary>
public readonly struct Mat4(double m00, double m01, double m02, double m03, double m10, double m11, double m12, double m13, double m20, double m21, double m22, double m23, double m30, double m31, double m32, double m33)
{
    private readonly double[] _m = [m00, m01, m02, m03, m10, m11, m12, m13, m20, m21, m22, m23, m30, m31, m32, m33];
    public double this[int i] => _m[i];
}

/// <summary>A 3x3 affine transformation matrix in row major order, where the bottom row is implicitly [0 0 1].</summary>
public readonly struct Aff3(double m00, double m01, double m02, double m10, double m11, double m12)
{
    private readonly double[] _m = [m00, m01, m02, m10, m11, m12];
    public double this[int i] => _m[i];
}

/// <summary>A 4x4 affine transformation matrix in row major order, where the bottom row is implicitly [0 0 0 1].</summary>
public readonly struct Aff4(double m00, double m01, double m02, double m03, double m10, double m11, double m12, double m13, double m20, double m21, double m22, double m23)
{
    private readonly double[] _m = [m00, m01, m02, m03, m10, m11, m12, m13, m20, m21, m22, m23];
    public double this[int i] => _m[i];
}
