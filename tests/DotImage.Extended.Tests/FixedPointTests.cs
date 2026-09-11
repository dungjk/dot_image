using DotImage.Extended.Math.Fixed;
using FixedInt26_6 = DotImage.Extended.Math.Fixed.Int26_6;
using FixedInt52_12 = DotImage.Extended.Math.Fixed.Int52_12;
using Xunit;

namespace DotImage.Extended.Tests;

public class FixedPointTests
{
    // Ported from golang.org/x/image/math/fixed fixed_test.go (testCases).
    public static System.Collections.Generic.IEnumerable<object[]> StringFloorRoundCeilCases =>
    [
        new object[] { 0, "0:00", "0:0000", 0, 0, 0 },
        new object[] { 1, "1:00", "1:0000", 1, 1, 1 },
        new object[] { 1.25, "1:16", "1:1024", 1, 1, 2 },
        new object[] { 2.5, "2:32", "2:2048", 2, 3, 3 },
        new object[] { 63 / 64.0, "0:63", "0:4032", 0, 1, 1 },
        new object[] { -0.5, "-0:32", "-0:2048", -1, 0, 0 },
        new object[] { -4.125, "-4:08", "-4:0512", -5, -4, -4 },
        new object[] { -7.75, "-7:48", "-7:3072", -8, -8, -7 },
    ];

    // Ported from fixed_test.go (mulTestCases).
    public static System.Collections.Generic.IEnumerable<object[]> MulCases =>
    [
        new object[] { 0, 1.5, "0:00", "0:0000" },
        new object[] { +1.25, +4, "5:00", "5:0000" },
        new object[] { +1.25, -4, "-5:00", "-5:0000" },
        new object[] { -1.25, +4, "-5:00", "-5:0000" },
        new object[] { -1.25, -4, "5:00", "5:0000" },
        new object[] { 1.25, 1.5, "1:56", "1:3584" },
        new object[] { 1234.5, -8888.875, "-10973316:12", "-10973316:0768" },
        new object[] { 1.515625, 1.531250, "2:21", "2:1314" },
        new object[] { 0.500244140625, 0.500732421875, "0:16", "0:1026" },
        new object[] { 0.015625, 0.000244140625, "0:00", "0:0000" },
        new object[] { 1.44140625, 1.44140625, "2:04", "2:0318" },
        new object[] { 1.44140625, 1.441650390625, "2:04", "2:0320" },
    ];

    [Theory]
    [MemberData(nameof(StringFloorRoundCeilCases))]
    public void Int26_6_StringFloorRoundCeil(double x, string s26_6, string s52_12, int floor, int round, int ceil)
    {
        _ = s52_12;
        var v = new FixedInt26_6((int)(x * (1 << 6)));
        Assert.Equal(s26_6, v.ToString());
        Assert.Equal(floor, v.Floor());
        Assert.Equal(round, v.Round());
        Assert.Equal(ceil, v.Ceil());
        Assert.Equal(v, v.Mul(FixedPoint.I(1)));
    }

    [Theory]
    [MemberData(nameof(StringFloorRoundCeilCases))]
    public void Int52_12_StringFloorRoundCeil(double x, string s26_6, string s52_12, int floor, int round, int ceil)
    {
        _ = s26_6;
        var v = new FixedInt52_12((long)(x * (1L << 12)));
        Assert.Equal(s52_12, v.ToString());
        Assert.Equal(floor, v.Floor());
        Assert.Equal(round, v.Round());
        Assert.Equal(ceil, v.Ceil());
        Assert.Equal(v, v.Mul(FixedPoint.I52(1)));
    }

    [Theory]
    [MemberData(nameof(MulCases))]
    public void Int26_6_Mul(double x, double y, string s26_6, string s52_12)
    {
        _ = s52_12;
        var v = new FixedInt26_6((int)(x * (1 << 6))).Mul(new FixedInt26_6((int)(y * (1 << 6))));
        Assert.Equal(s26_6, v.ToString());
    }

    [Theory]
    [MemberData(nameof(MulCases))]
    public void Int52_12_Mul(double x, double y, string s26_6, string s52_12)
    {
        _ = s26_6;
        var v = new FixedInt52_12((long)(x * (1L << 12))).Mul(new FixedInt52_12((long)(y * (1L << 12))));
        Assert.Equal(s52_12, v.ToString());
    }

    [Fact]
    public void Int26_6_MulByOneMinusIota()
    {
        const int totalBits = 32;
        const int fracBits = 6;

        var oneMinusIota = new FixedInt26_6((1 << fracBits) - 1);
        double oneMinusIotaF = (double)((1 << fracBits) - 1) / (1 << fracBits);

        Assert.Equal(((1 << fracBits) - 1) << (fracBits - fracBits), oneMinusIota.Raw);

        foreach (bool neg in new[] { false, true })
        {
            for (int i = 0; i < totalBits; i++)
            {
                int rawX = 1 << i;
                var x = new FixedInt26_6(rawX);
                if (neg)
                {
                    x = new FixedInt26_6(-rawX);
                }
                else if (i == totalBits - 1)
                {
                    continue;
                }

                // want equals x * oneMinusIota, rounded to nearest.
                var want = new FixedInt26_6(0);
                if (-(1 << fracBits) < x.Raw && x.Raw < (1 << fracBits))
                {
                    // (x * oneMinusIota) isn't exactly representable as an
                    // Int26_6. Calculate the rounded value using float64 math.
                    double xF = x.Raw / (double)(1 << fracBits);
                    double wantF = xF * oneMinusIotaF * (1 << fracBits);
                    want = new FixedInt26_6((int)System.Math.Floor(wantF + 0.5));
                }
                else
                {
                    // (x * oneMinusIota) is exactly representable.
                    want = new FixedInt26_6(oneMinusIota.Raw << (i - fracBits));
                    if (neg)
                    {
                        want = new FixedInt26_6(-want.Raw);
                    }
                }

                FixedInt26_6 got = x.Mul(oneMinusIota);
                Assert.Equal(want, got);
            }
        }
    }

    [Fact]
    public void Int52_12_MulByOneMinusIota()
    {
        const int totalBits = 64;
        const int fracBits = 12;

        var oneMinusIota = new FixedInt52_12((1 << fracBits) - 1);
        double oneMinusIotaF = (double)((1 << fracBits) - 1) / (1 << fracBits);

        foreach (bool neg in new[] { false, true })
        {
            for (int i = 0; i < totalBits; i++)
            {
                long rawX = 1L << i;
                var x = new FixedInt52_12(rawX);
                if (neg)
                {
                    x = new FixedInt52_12(-rawX);
                }
                else if (i == totalBits - 1)
                {
                    continue;
                }

                // want equals x * oneMinusIota, rounded to nearest.
                var want = new FixedInt52_12(0);
                if (-(1 << fracBits) < x.Raw && x.Raw < (1 << fracBits))
                {
                    double xF = x.Raw / (double)(1 << fracBits);
                    double wantF = xF * oneMinusIotaF * (1 << fracBits);
                    want = new FixedInt52_12((long)System.Math.Floor(wantF + 0.5));
                }
                else
                {
                    want = new FixedInt52_12(oneMinusIota.Raw << (i - fracBits));
                    if (neg)
                    {
                        want = new FixedInt52_12(-want.Raw);
                    }
                }

                FixedInt52_12 got = x.Mul(oneMinusIota);
                Assert.Equal(want, got);
            }
        }
    }

    [Fact]
    public void Point26_6_AddSubMulDivIn()
    {
        Point26_6 p = FixedPoint.P(2, 3);
        Point26_6 q = FixedPoint.P(-1, 5);

        Assert.Equal(0, p.Add(q).X.Raw - FixedPoint.I(1).Raw);
        Assert.Equal(8 << 6, p.Add(q).Y.Raw);

        Assert.Equal(3 << 6, p.Sub(q).X.Raw);
        Assert.Equal(-2 << 6, p.Sub(q).Y.Raw);

        var scaled = p.Mul(FixedPoint.I(2));
        Assert.Equal(4 << 6, scaled.X.Raw);
        Assert.Equal(6 << 6, scaled.Y.Raw);

        var divided = p.Div(FixedPoint.I(2));
        Assert.Equal(1 << 6, divided.X.Raw);
        Assert.Equal(3 << 5, divided.Y.Raw);

        var r = FixedPoint.R(0, 0, 4, 4);
        Assert.True(p.In(r));
        Assert.False(new Point26_6(FixedPoint.I(4), FixedPoint.I(0)).In(r));
        Assert.False(new Point26_6(FixedPoint.I(-2), FixedPoint.I(0)).In(r));
    }

    [Fact]
    public void Rectangle26_6_IntersectUnionEmptyIn()
    {
        var r = FixedPoint.R(5, 5, 10, 10);
        Assert.True(r.In(FixedPoint.R(0, 0, 11, 11)));

        var s = FixedPoint.R(1, 1, 7, 7);
        var i = r.Intersect(s);
        Assert.Equal(5 << 6, i.Min.X.Raw);
        Assert.Equal(5 << 6, i.Min.Y.Raw);
        Assert.Equal(7 << 6, i.Max.X.Raw);
        Assert.Equal(7 << 6, i.Max.Y.Raw);

        var u = r.Union(s);
        Assert.Equal(1 << 6, u.Min.X.Raw);
        Assert.Equal(10 << 6, u.Max.X.Raw);
        Assert.Equal(1 << 6, u.Min.Y.Raw);
        Assert.Equal(10 << 6, u.Max.Y.Raw);

        Assert.False(r.Empty());
        Assert.True(r.Intersect(FixedPoint.R(11, 11, 12, 12)).Empty());
        Assert.True(FixedPoint.R(5, 5, 5, 10).Empty());
    }
}