namespace DotImage.GenDraw;

// The C# templates are paper ports of gen.go's codeRoot/codeNNScaleLeaf/
// codeNNTransformLeaf/codeABLScaleLeaf/codeABLTransformLeaf/codeKernelRoot/
// codeKernelScaleLeafX/codeKernelScaleLeafY/codeKernelTransformLeaf.
//
// As in Go, tokens like $dType/$$sType/$$switch/$$srcf etc. are expanded by the
// GenDraw.Generator engine (ExpnLine/ExpnDollar). Each template line is verbatim
// otherwise. Lines expand to ";" are dropped by GenLeaves.
//
// Indentation is significant: because these templates are emitted inside
// "internal static partial class ScaleImpl" / "internal sealed partial class
// KernelScaler" (each at four spaces), the members start at eight spaces and
// nest four spaces per level. The closing """ of each raw string literal must
// sit at column 0 so that the leading whitespace is preserved verbatim.
public static partial class Generator
{
    // codeRoot: the NnInterpolator/AblInterpolator Scale and Transform entry
    // points plus the dispatch switches. $cbScale/$cbTransform become the
    // per-interpolator callback names ($receiver).
    private const string CodeRoot = """
        internal static void $cbScale(IWritableImage dst, Rect dr, IImage src, Rect sr, Op op, Options? opts)
        {
            // Try to simplify a Scale to a Copy when DstMask is not specified.
            // If DstMask is not nil, Copy will call Scale back with same dr and sr, and cause stack overflow.
            if (dr.Size().Eq(sr.Size()) && (opts == null || opts.Value.DstMask == null))
            {
                ScaleOps.Copy(dst, dr.Min, src, sr, op, opts);
                return;
            }

            var o = opts ?? default;

            // adr is the affected destination pixels.
            Rect adr = dst.Bounds().Intersect(dr);
            (adr, o.DstMask) = ScaleOps.ClipAffectedDestRect(adr, o.DstMask, o.DstMaskP);
            if (adr.Empty() || sr.Empty())
            {
                return;
            }
            // Make adr relative to dr.Min.
            adr = adr.Sub(dr.Min);
            if (op == Op.Over && o.SrcMask == null && ScaleOps.Opaque(src))
            {
                op = Op.Src;
            }

            // sr is the source pixels. If it extends beyond the src bounds,
            // we cannot use the type-specific fast paths, as they access
            // the Pix fields directly without bounds checking.
            //
            // Similarly, the fast paths assume that the masks are nil.
            if (o.DstMask != null || o.SrcMask != null || !sr.In(src.Bounds()))
            {
                switch (op)
                {
                    case Op.Over: $cbScale_Image_Image_Over(dst, dr, adr, src, sr, o); break;
                    case Op.Src: $cbScale_Image_Image_Src(dst, dr, adr, src, sr, o); break;
                }
            }
            else if (src is UniformImage)
            {
                DrawOps.Draw(dst, dr, src, src.Bounds().Min, op);
            }
            else
            {
                $switch $cbScale_$dTypeRN_$sTypeRN$sratio_$op(dst, dr, adr, src, sr, o)
            }
        }

        internal static void $cbTransform(IWritableImage dst, Aff3 s2d, IImage src, Rect sr, Op op, Options? opts)
        {
            // Try to simplify a Transform to a Copy.
            if (s2d[0] == 1 && s2d[1] == 0 && s2d[3] == 0 && s2d[4] == 1)
            {
                int dx = (int)s2d[2];
                int dy = (int)s2d[5];
                if ((double)dx == s2d[2] && (double)dy == s2d[5])
                {
                    ScaleOps.Copy(dst, new Point(sr.Min.X + dx, sr.Min.X + dy), src, sr, op, opts);
                    return;
                }
            }

            var o = opts ?? default;

            Rect dr = TransformRect(s2d, sr);
            // adr is the affected destination pixels.
            Rect adr = dst.Bounds().Intersect(dr);
            (adr, o.DstMask) = ScaleOps.ClipAffectedDestRect(adr, o.DstMask, o.DstMaskP);
            if (adr.Empty() || sr.Empty())
            {
                return;
            }
            if (op == Op.Over && o.SrcMask == null && ScaleOps.Opaque(src))
            {
                op = Op.Src;
            }

            var d2s = Invert(s2d);
            // bias is a translation of the mapping from dst coordinates to src
            // coordinates such that the latter temporarily have non-negative X
            // and Y coordinates. This allows us to write int(f) instead of
            // int(math.Floor(f)), since "round to zero" and "round down" are
            // equivalent when f >= 0, but the former is much cheaper. The X--
            // and Y-- are because the TransformLeaf methods have a "sx -= 0.5"
            // adjustment.
            Point bias = TransformRect(d2s, adr).Min;
            bias = new Point(bias.X - 1, bias.Y - 1);
            d2s = new Aff3(d2s[0], d2s[1], d2s[2] - bias.X, d2s[3], d2s[4], d2s[5] - bias.Y);
            // Make adr relative to dr.Min.
            adr = adr.Sub(dr.Min);

            // sr is the source pixels. If it extends beyond the src bounds,
            // we cannot use the type-specific fast paths, as they access
            // the Pix fields directly without bounds checking.
            //
            // Similarly, the fast paths assume that the masks are nil.
            if (o.DstMask != null || o.SrcMask != null || !sr.In(src.Bounds()))
            {
                switch (op)
                {
                    case Op.Over: $cbTransform_Image_Image_Over(dst, dr, adr, d2s, src, sr, bias, o); break;
                    case Op.Src: $cbTransform_Image_Image_Src(dst, dr, adr, d2s, src, sr, bias, o); break;
                }
            }
            else if (src is UniformImage u)
            {
                TransformUniform(dst, dr, adr, d2s, u, sr, bias, op);
            }
            else
            {
                $switch $cbTransform_$dTypeRN_$sTypeRN$sratio_$op(dst, dr, adr, d2s, src, sr, bias, o)
            }
        }
""";

    // codeNNScaleLeaf / codeNNTransformLeaf.
    private const string CodeNnScaleLeaf = """
        internal static void $cbScale_$dTypeRN_$sTypeRN$sratio_$op($dType dst, Rect dr, Rect adr, $sType src, Rect sr, Options o)
        {
            ulong dw2 = (ulong)dr.Dx() * 2;
            ulong dh2 = (ulong)dr.Dy() * 2;
            ulong sw = (ulong)sr.Dx();
            ulong sh = (ulong)sr.Dy();
            $preOuter
            for (int dy = adr.Min.Y; dy < adr.Max.Y; dy++)
            {
                ulong sy = (2UL * (ulong)dy + 1) * sh / dh2;
                $preInner
                for (int dx = adr.Min.X; dx < adr.Max.X; dx++) { $tweakDx
                    ulong sx = (2UL * (ulong)dx + 1) * sw / dw2;
                    p := $srcu[sr.Min.X + (int)sx, sr.Min.Y + (int)sy]
                    $outputu[dr.Min.X + dx, dr.Min.Y + dy, p]
                }
            }
        }
""";

    private const string CodeNnTransformLeaf = """
        internal static void $cbTransform_$dTypeRN_$sTypeRN$sratio_$op($dType dst, Rect dr, Rect adr, Aff3 d2s, $sType src, Rect sr, Point bias, Options o)
        {
            $preOuter
            for (int dy = adr.Min.Y; dy < adr.Max.Y; dy++)
            {
                double dyf = dr.Min.Y + dy + 0.5;
                $preInner
                for (int dx = adr.Min.X; dx < adr.Max.X; dx++) { $tweakDx
                    double dxf = dr.Min.X + dx + 0.5;
                    int sx0 = (int)(d2s[0] * dxf + d2s[1] * dyf + d2s[2]) + bias.X;
                    int sy0 = (int)(d2s[3] * dxf + d2s[4] * dyf + d2s[5]) + bias.Y;
                    if (!new Point(sx0, sy0).In(sr))
                    {
                        continue;
                    }
                    p := $srcu[sx0, sy0]
                    $outputu[dr.Min.X + dx, dr.Min.Y + dy, p]
                }
            }
        }
""";

    // codeABLScaleLeaf.
    private const string CodeAblScaleLeaf = """
        internal static void $cbScale_$dTypeRN_$sTypeRN$sratio_$op($dType dst, Rect dr, Rect adr, $sType src, Rect sr, Options o)
        {
            int sw = sr.Dx();
            int sh = sr.Dy();
            double yscale = (double)sh / dr.Dy();
            double xscale = (double)sw / dr.Dx();
            int swMinus1 = sw - 1, shMinus1 = sh - 1;
            $preOuter

            for (int dy = adr.Min.Y; dy < adr.Max.Y; dy++)
            {
                double sy = (dy + 0.5) * yscale - 0.5;
                // If sy < 0, we will clamp sy0 to 0 anyway, so it doesn't matter if
                // we truncate instead of flooring.
                int sy0 = (int)sy;
                double yFrac0 = sy - sy0;
                double yFrac1 = 1 - yFrac0;
                int sy1 = sy0 + 1;
                if (sy < 0)
                {
                    sy0 = 0;
                    sy1 = 0;
                    yFrac0 = 0;
                    yFrac1 = 1;
                }
                else if (sy1 > shMinus1)
                {
                    sy0 = shMinus1;
                    sy1 = shMinus1;
                    yFrac0 = 1;
                    yFrac1 = 0;
                }
                $preInner

                for (int dx = adr.Min.X; dx < adr.Max.X; dx++) { $tweakDx
                    double sx = (dx + 0.5) * xscale - 0.5;
                    int sx0 = (int)sx;
                    double xFrac0 = sx - sx0;
                    double xFrac1 = 1 - xFrac0;
                    int sx1 = sx0 + 1;
                    if (sx < 0)
                    {
                        sx0 = 0;
                        sx1 = 0;
                        xFrac0 = 0;
                        xFrac1 = 1;
                    }
                    else if (sx1 > swMinus1)
                    {
                        sx0 = swMinus1;
                        sx1 = swMinus1;
                        xFrac0 = 1;
                        xFrac1 = 0;
                    }

                    s00 := $srcf[sr.Min.X + sx0, sr.Min.Y + sy0]
                    s10 := $srcf[sr.Min.X + sx1, sr.Min.Y + sy0]
                    $blend[xFrac1, s00, xFrac0, s10]
                    s01 := $srcf[sr.Min.X + sx0, sr.Min.Y + sy1]
                    s11 := $srcf[sr.Min.X + sx1, sr.Min.Y + sy1]
                    $blend[xFrac1, s01, xFrac0, s11]
                    $blend[yFrac1, s10, yFrac0, s11]
                    $convFtou[p, s11]
                    $outputu[dr.Min.X + dx, dr.Min.Y + dy, p]
                }
            }
        }
""";

    // codeABLTransformLeaf.
    private const string CodeAblTransformLeaf = """
        internal static void $cbTransform_$dTypeRN_$sTypeRN$sratio_$op($dType dst, Rect dr, Rect adr, Aff3 d2s, $sType src, Rect sr, Point bias, Options o)
        {
            $preOuter
            for (int dy = adr.Min.Y; dy < adr.Max.Y; dy++)
            {
                double dyf = dr.Min.Y + dy + 0.5;
                $preInner
                for (int dx = adr.Min.X; dx < adr.Max.X; dx++) { $tweakDx
                    double dxf = dr.Min.X + dx + 0.5;
                    double sx = d2s[0] * dxf + d2s[1] * dyf + d2s[2];
                    double sy = d2s[3] * dxf + d2s[4] * dyf + d2s[5];
                    if (!new Point((int)sx + bias.X, (int)sy + bias.Y).In(sr))
                    {
                        continue;
                    }

                    sx -= 0.5;
                    int sx0 = (int)sx;
                    double xFrac0 = sx - sx0;
                    double xFrac1 = 1 - xFrac0;
                    sx0 += bias.X;
                    int sx1 = sx0 + 1;
                    if (sx0 < sr.Min.X)
                    {
                        sx0 = sr.Min.X;
                        sx1 = sr.Min.X;
                        xFrac0 = 0;
                        xFrac1 = 1;
                    }
                    else if (sx1 >= sr.Max.X)
                    {
                        sx0 = sr.Max.X - 1;
                        sx1 = sr.Max.X - 1;
                        xFrac0 = 1;
                        xFrac1 = 0;
                    }

                    sy -= 0.5;
                    int sy0 = (int)sy;
                    double yFrac0 = sy - sy0;
                    double yFrac1 = 1 - yFrac0;
                    sy0 += bias.Y;
                    int sy1 = sy0 + 1;
                    if (sy0 < sr.Min.Y)
                    {
                        sy0 = sr.Min.Y;
                        sy1 = sr.Min.Y;
                        yFrac0 = 0;
                        yFrac1 = 1;
                    }
                    else if (sy1 >= sr.Max.Y)
                    {
                        sy0 = sr.Max.Y - 1;
                        sy1 = sr.Max.Y - 1;
                        yFrac0 = 1;
                        yFrac1 = 0;
                    }

                    s00 := $srcf[sx0, sy0]
                    s10 := $srcf[sx1, sy0]
                    $blend[xFrac1, s00, xFrac0, s10]
                    s01 := $srcf[sx0, sy1]
                    s11 := $srcf[sx1, sy1]
                    $blend[xFrac1, s01, xFrac0, s11]
                    $blend[yFrac1, s10, yFrac0, s11]
                    $convFtou[p, s11]
                    $outputu[dr.Min.X + dx, dr.Min.Y + dy, p]
                }
            }
        }
""";

    private static string CodeScaleLeafSource(string receiver) => receiver switch
    {
        "Nn" => CodeNnScaleLeaf,
        "Abl" => CodeAblScaleLeaf,
        _ => throw new Exception($"bad receiver {receiver}"),
    };

    private static string CodeTransformLeafSource(string receiver) => receiver switch
    {
        "Nn" => CodeNnTransformLeaf,
        "Abl" => CodeAblTransformLeaf,
        _ => throw new Exception($"bad receiver {receiver}"),
    };

    // codeKernelRoot: the KernelScaler.Scale entry point. It is emitted inside
    // the KernelScaler class (the $switchS/$switchD dispatch the scaleX/scaleY
    // fast paths).
    private const string KernelScaleRoot = """
        public void Scale(IWritableImage dst, Rect dr, IImage src, Rect sr, Op op, Options? opts)
        {
            if (Dw != dr.Dx() || Dh != dr.Dy() || Sw != sr.Dx() || Sh != sr.Dy())
            {
                Kernel.Scale(dst, dr, src, sr, op, opts);
                return;
            }

            var o = opts ?? default;

            // adr is the affected destination pixels.
            Rect adr = dst.Bounds().Intersect(dr);
            (adr, o.DstMask) = ScaleOps.ClipAffectedDestRect(adr, o.DstMask, o.DstMaskP);
            if (adr.Empty() || sr.Empty())
            {
                return;
            }
            // Make adr relative to dr.Min.
            adr = adr.Sub(dr.Min);
            if (op == Op.Over && o.SrcMask == null && ScaleOps.Opaque(src))
            {
                op = Op.Src;
            }

            if (src is UniformImage && o.DstMask == null && o.SrcMask == null && sr.In(src.Bounds()))
            {
                DrawOps.Draw(dst, dr, src, src.Bounds().Min, op);
                return;
            }

            // Create a temporary buffer:
            // scaleX distributes the source image's columns over the temporary image.
            // scaleY distributes the temporary image's rows over the destination image.
            var tmp = new double[Dw * Sh * 4];

            // sr is the source pixels. If it extends beyond the src bounds,
            // we cannot use the type-specific fast paths, as they access
            // the Pix fields directly without bounds checking.
            //
            // Similarly, the fast paths assume that the masks are nil.
            if (o.SrcMask != null || !sr.In(src.Bounds()))
            {
                ScaleX_Image(tmp, src, sr, o);
            }
            else
            {
                $switchS ScaleX_$sTypeRN$sratio(tmp, src, sr, o)
            }

            if (o.DstMask != null)
            {
                switch (op)
                {
                    case Op.Over: ScaleY_Image_Over(dst, dr, adr, tmp, o); break;
                    case Op.Src: ScaleY_Image_Src(dst, dr, adr, tmp, o); break;
                }
            }
            else
            {
                $switchD ScaleY_$dTypeRN_$op(dst, dr, adr, tmp, o)
            }
        }
""";

    // codeKernelRoot (the Transform half): emitted on ScaleImpl.
    private const string KernelTransformRoot = """
        internal static void KernelTransform(Kernel q, IWritableImage dst, Aff3 s2d, IImage src, Rect sr, Op op, Options? opts)
        {
            var o = opts ?? default;

            Rect dr = TransformRect(s2d, sr);
            // adr is the affected destination pixels.
            Rect adr = dst.Bounds().Intersect(dr);
            (adr, o.DstMask) = ScaleOps.ClipAffectedDestRect(adr, o.DstMask, o.DstMaskP);
            if (adr.Empty() || sr.Empty())
            {
                return;
            }
            if (op == Op.Over && o.SrcMask == null && ScaleOps.Opaque(src))
            {
                op = Op.Src;
            }
            var d2s = Invert(s2d);
            // bias is a translation of the mapping from dst coordinates to src
            // coordinates such that the latter temporarily have non-negative X
            // and Y coordinates. This allows us to write int(f) instead of
            // int(math.Floor(f)), since "round to zero" and "round down" are
            // equivalent when f >= 0, but the former is much cheaper. The X--
            // and Y-- are because the TransformLeaf methods have a "sx -= 0.5"
            // adjustment.
            Point bias = TransformRect(d2s, adr).Min;
            bias = new Point(bias.X - 1, bias.Y - 1);
            d2s = new Aff3(d2s[0], d2s[1], d2s[2] - bias.X, d2s[3], d2s[4], d2s[5] - bias.Y);
            // Make adr relative to dr.Min.
            adr = adr.Sub(dr.Min);

            if (src is UniformImage u && o.DstMask != null && o.SrcMask != null && sr.In(src.Bounds()))
            {
                TransformUniform(dst, dr, adr, d2s, u, sr, bias, op);
                return;
            }

            double xscale = Abs(d2s[0]);
            if (xscale < Abs(d2s[1]))
            {
                xscale = Abs(d2s[1]);
            }
            double yscale = Abs(d2s[3]);
            if (yscale < Abs(d2s[4]))
            {
                yscale = Abs(d2s[4]);
            }

            // sr is the source pixels. If it extends beyond the src bounds,
            // we cannot use the type-specific fast paths, as they access
            // the Pix fields directly without bounds checking.
            //
            // Similarly, the fast paths assume that the masks are nil.
            if (o.DstMask != null || o.SrcMask != null || !sr.In(src.Bounds()))
            {
                switch (op)
                {
                    case Op.Over: KernelTransform_Image_Image_Over(q, dst, dr, adr, d2s, src, sr, bias, xscale, yscale, o); break;
                    case Op.Src: KernelTransform_Image_Image_Src(q, dst, dr, adr, d2s, src, sr, bias, xscale, yscale, o); break;
                }
            }
            else
            {
                $switch KernelTransform_$dTypeRN_$sTypeRN$sratio_$op(q, dst, dr, adr, d2s, src, sr, bias, xscale, yscale, o)
            }
        }
""";

    // codeKernelScaleLeafX.
    private const string KernelScaleXLeaf = """
        internal void ScaleX_$sTypeRN$sratio(double[] tmp, $sType src, Rect sr, Options o)
        {
            int t = 0;
            $preKernelOuter
            for (int y = 0; y < Sh; y++)
            {
                for (int s = 0; s < Horizontal.Sources.Length; s++)
                {
                    var b = Horizontal.Sources[s];
                    $tweakVarP
                    for (int ci = b.I; ci < b.J; ci++)
                    {
                        var cc = Horizontal.Contribs[ci];
                        p += $srcf[sr.Min.X + cc.Coord, sr.Min.Y + y] * cc.Weight
                    }
                    $tweakPr
                    int t4 = t * 4;
                    $tmpOut
                    t++;
                }
            }
        }
""";

    // codeKernelScaleLeafY. The C# loop uses a plain indexed dy (no $tweakDy);
    // d advances per row via $tweakD, as in Go.
    private const string KernelScaleYLeaf = """
        internal void ScaleY_$dTypeRN_$op($dType dst, Rect dr, Rect adr, double[] tmp, Options o)
        {
            $preOuter
            for (int dx = adr.Min.X; dx < adr.Max.X; dx++)
            {
                $preKernelInner
                for (int dy = adr.Min.Y; dy < adr.Max.Y; dy++)
                {
                    var b = Vertical.Sources[dy];
                    double pr = 0, pg = 0, pb = 0, pa = 0;
                    for (int ci = b.I; ci < b.J; ci++)
                    {
                        var cc = Vertical.Contribs[ci];
                        int t4 = (cc.Coord * Dw + dx) * 4;
                        pr += tmp[t4 + 0] * cc.Weight;
                        pg += tmp[t4 + 1] * cc.Weight;
                        pb += tmp[t4 + 2] * cc.Weight;
                        pa += tmp[t4 + 3] * cc.Weight;
                    }
                    $clampToAlpha
                    $outputf[dr.Min.X + dx, dr.Min.Y + dy, ftou, p, b.InvTotalWeight]
                    $tweakD
                }
            }
        }
""";

    // codeKernelTransformLeaf.
    private const string CodeKernelTransformLeafSource = """
        internal static void KernelTransform_$dTypeRN_$sTypeRN$sratio_$op(Kernel q, $dType dst, Rect dr, Rect adr, Aff3 d2s, $sType src, Rect sr, Point bias, double xscale, double yscale, Options o)
        {
            // When shrinking, broaden the effective kernel support so that we still
            // visit every source pixel.
            double xHalfWidth = q.Support, xKernelArgScale = 1.0;
            if (xscale > 1)
            {
                xHalfWidth *= xscale;
                xKernelArgScale = 1 / xscale;
            }
            double yHalfWidth = q.Support, yKernelArgScale = 1.0;
            if (yscale > 1)
            {
                yHalfWidth *= yscale;
                yKernelArgScale = 1 / yscale;
            }

            var xWeights = new double[1 + 2 * (int)DotMath.Ceiling(xHalfWidth)];
            var yWeights = new double[1 + 2 * (int)DotMath.Ceiling(yHalfWidth)];

            $preOuter
            for (int dy = adr.Min.Y; dy < adr.Max.Y; dy++)
            {
                double dyf = dr.Min.Y + dy + 0.5;
                $preInner
                for (int dx = adr.Min.X; dx < adr.Max.X; dx++) { $tweakDx
                    double dxf = dr.Min.X + dx + 0.5;
                    double sx = d2s[0] * dxf + d2s[1] * dyf + d2s[2];
                    double sy = d2s[3] * dxf + d2s[4] * dyf + d2s[5];
                    if (!new Point((int)sx + bias.X, (int)sy + bias.Y).In(sr))
                    {
                        continue;
                    }

                    // TODO: adjust the bias so that we can use int casts
                    // instead of Floor and Ceiling.
                    sx += bias.X;
                    sx -= 0.5;
                    int ix = (int)DotMath.Floor(sx - xHalfWidth);
                    if (ix < sr.Min.X)
                    {
                        ix = sr.Min.X;
                    }
                    int jx = (int)DotMath.Ceiling(sx + xHalfWidth);
                    if (jx > sr.Max.X)
                    {
                        jx = sr.Max.X;
                    }

                    double totalXWeight = 0.0;
                    for (int kx = ix; kx < jx; kx++)
                    {
                        double xWeight = 0.0;
                        double t = Abs((sx - kx) * xKernelArgScale);
                        if (t < q.Support)
                        {
                            xWeight = q.At(t);
                        }
                        xWeights[kx - ix] = xWeight;
                        totalXWeight += xWeight;
                    }
                    for (int x = 0; x < jx - ix; x++)
                    {
                        xWeights[x] /= totalXWeight;
                    }

                    sy += bias.Y;
                    sy -= 0.5;
                    int iy = (int)DotMath.Floor(sy - yHalfWidth);
                    if (iy < sr.Min.Y)
                    {
                        iy = sr.Min.Y;
                    }
                    int jy = (int)DotMath.Ceiling(sy + yHalfWidth);
                    if (jy > sr.Max.Y)
                    {
                        jy = sr.Max.Y;
                    }

                    double totalYWeight = 0.0;
                    for (int ky = iy; ky < jy; ky++)
                    {
                        double yWeight = 0.0;
                        double t = Abs((sy - ky) * yKernelArgScale);
                        if (t < q.Support)
                        {
                            yWeight = q.At(t);
                        }
                        yWeights[ky - iy] = yWeight;
                        totalYWeight += yWeight;
                    }
                    for (int y = 0; y < jy - iy; y++)
                    {
                        yWeights[y] /= totalYWeight;
                    }

                    $tweakVarP
                    for (int ky = iy; ky < jy; ky++)
                    {
                        double yWeight = yWeights[ky - iy];
                        if (yWeight != 0)
                        {
                            for (int kx = ix; kx < jx; kx++)
                            {
                                double w = xWeights[kx - ix] * yWeight;
                                if (w != 0)
                                {
                                    p += $srcf[kx, ky] * w
                                }
                            }
                        }
                    }
                    $clampToAlpha
                    $outputf[dr.Min.X + dx, dr.Min.Y + dy, fffftou, p, 1]
                }
            }
        }
""";
}