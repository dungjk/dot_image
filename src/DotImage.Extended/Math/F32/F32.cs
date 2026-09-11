// Ported from golang.org/x/image/math/f32/f32.go

namespace DotImage.Extended.Math.F32;

/// <summary>A 2-element float32 vector.</summary>
public readonly struct Vec2
{
    private readonly float _0, _1;
    public Vec2(float x, float y) { _0 = x; _1 = y; }
    public float X => _0;
    public float Y => _1;
    public float this[int i] => i switch { 0 => _0, 1 => _1, _ => throw new IndexOutOfRangeException() };
}

/// <summary>A 3-element float32 vector.</summary>
public readonly struct Vec3
{
    private readonly float _0, _1, _2;
    public Vec3(float x, float y, float z) { _0 = x; _1 = y; _2 = z; }
    public float X => _0;
    public float Y => _1;
    public float Z => _2;
    public float this[int i] => i switch
    {
        0 => _0,
        1 => _1,
        2 => _2,
        _ => throw new IndexOutOfRangeException(),
    };
}

/// <summary>A 4-element float32 vector.</summary>
public readonly struct Vec4
{
    private readonly float _0, _1, _2, _3;
    public Vec4(float x, float y, float z, float w) { _0 = x; _1 = y; _2 = z; _3 = w; }
    public float this[int i] => i switch
    {
        0 => _0,
        1 => _1,
        2 => _2,
        3 => _3,
        _ => throw new IndexOutOfRangeException(),
    };
}

/// <summary>A 3x3 matrix in row major order. m[3*r + c] is the r'th row and c'th column.</summary>
public readonly struct Mat3(float m00, float m01, float m02, float m10, float m11, float m12, float m20, float m21, float m22)
{
    private readonly float[] _m = [m00, m01, m02, m10, m11, m12, m20, m21, m22];
    public float this[int i] => _m[i];
}

/// <summary>A 4x4 matrix in row major order. m[4*r + c] is the r'th row and c'th column.</summary>
public readonly struct Mat4(float m00, float m01, float m02, float m03, float m10, float m11, float m12, float m13, float m20, float m21, float m22, float m23, float m30, float m31, float m32, float m33)
{
    private readonly float[] _m = [m00, m01, m02, m03, m10, m11, m12, m13, m20, m21, m22, m23, m30, m31, m32, m33];
    public float this[int i] => _m[i];
}

/// <summary>A 3x3 affine transformation matrix in row major order, where the bottom row is implicitly [0 0 1].</summary>
public readonly struct Aff3(float m00, float m01, float m02, float m10, float m11, float m12)
{
    private readonly float[] _m = [m00, m01, m02, m10, m11, m12];
    public float this[int i] => _m[i];
}

/// <summary>A 4x4 affine transformation matrix in row major order, where the bottom row is implicitly [0 0 0 1].</summary>
public readonly struct Aff4(float m00, float m01, float m02, float m03, float m10, float m11, float m12, float m13, float m20, float m21, float m22, float m23)
{
    private readonly float[] _m = [m00, m01, m02, m03, m10, m11, m12, m13, m20, m21, m22, m23];
    public float this[int i] => _m[i];
}
