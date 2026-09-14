using System.Text;
using DotImage.Extended.Math.Fixed;

namespace DotImage.Extended.Font.Plan9;

/// <summary>Ported from golang.org/x/image/font/plan9font. Implements font
/// faces for the Plan 9 font and subfont file formats described at
/// https://9p.io/magic/man2html/6/font</summary>
public static class Plan9Font
{
    public static IFontFace ParseFont(byte[] data, Func<string, byte[]> readFile)
    {
        var f = new Plan9Face { ReadFile = readFile };
        bool first = true;
        bool ok = false;
        var row = new StringBuilder();
        while (data.Length > 0)
        {
            int i = Array.IndexOf(data, (byte)'\n');
            if (i < 0)
            {
                throw new Exception("plan9font: invalid font: no final newline");
            }
            row.Clear();
            row.Append(Encoding.ASCII.GetString(data, 0, i));
            data = data[(i + 1)..];
            if (first)
            {
                var s = row.ToString();
                int height = NextInt32(ref s, out ok);
                if (!ok) throw new Exception($"plan9font: invalid font: invalid header {row}");
                int ascent = NextInt32(ref s, out ok);
                if (!ok) throw new Exception($"plan9font: invalid font: invalid header {row}");
                if (height < 0 || 0xffff < height || ascent < 0 || 0xffff < ascent)
                {
                    throw new Exception($"plan9font: invalid font: invalid header {row}");
                }
                f.Height = height;
                f.Ascent = ascent;
                first = false;
                continue;
            }
            var rs = row.ToString();
            int lo = NextInt32(ref rs, out ok);
            if (!ok) throw new Exception($"plan9font: invalid font: invalid row {row}");
            int hi = NextInt32(ref rs, out ok);
            if (!ok) throw new Exception($"plan9font: invalid font: invalid row {row}");
            int offset = NextInt32(ref rs, out _);
            f.RuneRanges.Add(new RuneRange
            {
                Lo = lo,
                Hi = hi,
                Offset = offset,
                RelFilename = rs.TrimStart(),
            });
        }
        return f;
    }

    public static IFontFace ParseSubfont(byte[] data, int firstRune)
    {
        var (remaining, m) = ParseImage(data);
        data = remaining;
        if (data.Length < 3 * 12)
        {
            throw new Exception("plan9font: invalid subfont: header too short");
        }
        int n = Atoi(data[(0 * 12)..]);
        int height = Atoi(data[(1 * 12)..]);
        int ascent = Atoi(data[(2 * 12)..]);
        data = data[(3 * 12)..];
        if (n < 0 || height < 0 || ascent < 0)
        {
            throw new Exception("plan9font: invalid subfont: dimension too large");
        }
        else if (data.Length != 6 * (n + 1))
        {
            throw new Exception("plan9font: invalid subfont: data length mismatch");
        }

        var bounds = m.Rect;
        var img = new AlphaImage
        {
            Pix = new byte[bounds.Dx() * bounds.Dy()],
            Stride = bounds.Dx(),
            Rect = bounds,
        };
        for (int y = bounds.Min.Y; y < bounds.Max.Y; y++)
        {
            int i = img.PixOffset(bounds.Min.X, y);
            for (int x = bounds.Min.X; x < bounds.Max.X; x++)
            {
                img.Pix.Span[i] = m.At(x, y);
                i++;
            }
        }

        return new Plan9Subface
        {
            FirstRune = firstRune,
            N = n,
            Height = height,
            Ascent = ascent,
            Fontchars = ParseFontchars(data),
            Img = img,
        };
    }

    private static int NextInt32(ref string s, out bool ok)
    {
        int i = 0;
        while (i < s.Length && s[i] <= ' ') i++;
        int j = i;
        while (j < s.Length && s[j] > ' ') j++;
        if (i == j || !ParseBase0(s[i..j], out int n))
        {
            ok = false;
            return 0;
        }
        while (j < s.Length && s[j] <= ' ') j++;
        s = s[j..];
        ok = true;
        return n;
    }

    // Parses an integer with base-0 semantics (mirrors strconv.ParseInt(s, 0)):
    // a leading "0x"/"0X" implies hexadecimal, a leading "0" and a following
    // octal digit implies octal, otherwise decimal. The entire string must be a
    // valid integer, otherwise false is returned.
    private static bool ParseBase0(string s, out int n)
    {
        n = 0;
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            string hex = s[2..];
            if (hex.Length == 0) return false;
            foreach (char c in hex)
            {
                int d;
                if (c >= '0' && c <= '9') d = c - '0';
                else if (c >= 'a' && c <= 'f') d = c - 'a' + 10;
                else if (c >= 'A' && c <= 'F') d = c - 'A' + 10;
                else return false;
                n = unchecked(n * 16 + d);
            }
            return true;
        }
        if (s.Length > 1 && s[0] == '0')
        {
            bool anyOctal = false;
            foreach (char c in s[1..])
            {
                if (c < '0' || c > '7') return false;
                anyOctal = true;
                n = unchecked(n * 8 + (c - '0'));
            }
            return anyOctal;
        }
        int i = 0;
        bool neg = false;
        if (i < s.Length && (s[i] == '+' || s[i] == '-'))
        {
            neg = s[i] == '-';
            i++;
        }
        int start = i;
        int val = 0;
        while (i < s.Length && s[i] >= '0' && s[i] <= '9')
        {
            val = unchecked(val * 10 + (s[i] - '0'));
            i++;
        }
        if (i == start || i != s.Length) return false;
        n = neg ? -val : val;
        return true;
    }

    private static Plan9Fontchar[] ParseFontchars(byte[] p)
    {
        var fc = new Plan9Fontchar[p.Length / 6];
        for (int i = 0; i < fc.Length; i++)
        {
            fc[i] = new Plan9Fontchar
            {
                X = (uint)(p[0] | (p[1] << 8)),
                Top = p[2],
                Bottom = p[3],
                Left = (sbyte)p[4],
                Width = p[5],
            };
            p = p[6..];
        }
        return fc;
    }

    private static int Atoi(byte[] b)
    {
        int i = 0;
        while (i < b.Length && b[i] == ' ') i++;
        int n = 0;
        while (i < b.Length && b[i] >= '0' && b[i] <= '9')
        {
            n = n * 10 + b[i] - '0';
            if (n > 999999) return -1;
            i++;
        }
        return n;
    }

    private static readonly byte[] Compressed = Encoding.ASCII.GetBytes("compressed\n");

    private static (byte[] remaining, Plan9Image m) ParseImage(byte[] data)
    {
        if (!data.StartsWith(Compressed))
        {
            throw new Exception("plan9font: unsupported uncompressed format");
        }
        data = data[Compressed.Length..];

        const int hdrSize = 5 * 12;
        if (data.Length < hdrSize)
        {
            throw new Exception("plan9font: invalid image: header too short");
        }
        var hdr = data[..hdrSize];
        data = data[hdrSize..];

        bool isNew = false;
        for (int i = 0; i < 10; i++)
        {
            if (hdr[i] != ' ')
            {
                isNew = true;
                break;
            }
        }
        if (hdr[11] != ' ')
        {
            throw new Exception("plan9font: invalid image: bad header");
        }
        if (!isNew)
        {
            throw new Exception("plan9font: unsupported ldepth format");
        }

        int depth = 0;
        string s = Encoding.ASCII.GetString(hdr[..12]).Trim();
        switch (s)
        {
            case "k1": depth = 1; break;
            case "k2": depth = 2; break;
            default:
                throw new Exception($"plan9font: unsupported pixel format {s}");
        }

        var r = Ator(hdr[12..]);
        if (r.Min.X < 0 || r.Max.X < 0 || r.Min.Y < 0 || r.Max.Y < 0 ||
            r.Min.X > r.Max.X || r.Min.Y > r.Max.Y)
        {
            throw new Exception("plan9font: invalid image: bad rectangle");
        }

        int width = BytesPerLine(r, depth);
        if (width > 0xffff || r.Dy() > 0x7fff)
        {
            throw new Exception("plan9font: unsupported dimensions");
        }
        var m = new Plan9Image
        {
            Depth = depth,
            Width = width,
            Rect = r,
            Pix = new byte[width * r.Dy()],
        };

        int miny = r.Min.Y;
        while (miny != r.Max.Y)
        {
            if (data.Length < 2 * 12)
            {
                throw new Exception("plan9font: invalid image: data band too short");
            }
            int maxy = Atoi(data[(0 * 12)..]);
            int nb = Atoi(data[(1 * 12)..]);
            data = data[(2 * 12)..];
            if (maxy < 0 || nb < 0)
            {
                throw new Exception("plan9font: invalid image: dimension too large");
            }
            if (data.Length < nb)
            {
                throw new Exception("plan9font: invalid image: data band length mismatch");
            }
            var buf = data[..nb];
            data = data[nb..];

            if (maxy <= miny || r.Max.Y < maxy)
            {
                throw new Exception($"plan9font: bad maxy {maxy}");
            }
            var rr = new Rect(new Point(r.Min.X, miny), new Point(r.Max.X, maxy));
            Decompress(m, rr, buf);
            miny = maxy;
        }
        return (data, m);
    }

    private const int CompShortestMatch = 3;
    private const int CompWindowSize = 1024;

    private static void Decompress(Plan9Image m, Rect r, byte[] data)
    {
        if (!r.In(m.Rect))
        {
            throw new Exception("plan9font: decompress: bad rectangle");
        }
        int bpl = BytesPerLine(r, m.Depth);
        var mem = new byte[CompWindowSize];
        int memi = 0;
        int datai = 0;
        int y = r.Min.Y;
        int linei = m.ByteOffset(r.Min.X, y);
        int eline = linei + bpl;
        while (true)
        {
            if (linei == eline)
            {
                y++;
                if (y == r.Max.Y) break;
                linei = m.ByteOffset(r.Min.X, y);
                eline = linei + bpl;
            }
            if (datai == data.Length)
            {
                throw new Exception("plan9font: decompress: buffer too small");
            }
            byte c = data[datai];
            datai++;
            if (c >= 128)
            {
                for (int cnt = c - 128 + 1; cnt != 0; cnt--)
                {
                    if (datai == data.Length)
                    {
                        throw new Exception("plan9font: decompress: buffer too small");
                    }
                    if (linei == eline)
                    {
                        throw new Exception("plan9font: decompress: phase error");
                    }
                    m.Pix[linei] = data[datai];
                    linei++;
                    mem[memi] = data[datai];
                    memi++;
                    datai++;
                    if (memi == mem.Length) memi = 0;
                }
            }
            else
            {
                if (datai == data.Length)
                {
                    throw new Exception("plan9font: decompress: buffer too small");
                }
                int offs = data[datai] + ((c & 3) << 8) + 1;
                datai++;
                int omemi;
                if (memi < offs)
                {
                    omemi = memi + (CompWindowSize - offs);
                }
                else
                {
                    omemi = memi - offs;
                }
                for (int cnt = (c >> 2) + CompShortestMatch; cnt != 0; cnt--)
                {
                    if (linei == eline)
                    {
                        throw new Exception("plan9font: decompress: phase error");
                    }
                    m.Pix[linei] = mem[omemi];
                    linei++;
                    mem[memi] = mem[omemi];
                    memi++;
                    omemi++;
                    if (omemi == mem.Length) omemi = 0;
                    if (memi == mem.Length) memi = 0;
                }
            }
        }
    }

    private static Rect Ator(byte[] b) => new(Atop(b), Atop(b[(2 * 12)..]));

    private static Point Atop(byte[] b) => new(Atoi(b), Atoi(b[12..]));

    private static int BytesPerLine(Rect r, int depth)
    {
        if (depth <= 0 || 32 < depth)
        {
            throw new Exception("plan9font: invalid depth");
        }
        if (r.Min.X >= 0)
        {
            int l = (r.Max.X * depth + 7) / 8;
            l -= (r.Min.X * depth) / 8;
            return l;
        }
        int t = (-r.Min.X * depth + 7) / 8;
        return t + (r.Max.X * depth + 7) / 8;
    }
}

internal struct Plan9Fontchar
{
    public uint X;
    public byte Top;
    public byte Bottom;
    public sbyte Left;
    public byte Width;
}

internal sealed class Plan9Subface : IFontFace
{
    public int FirstRune;
    public int N;
    public int Height;
    public int Ascent;
    public Plan9Fontchar[] Fontchars = [];
    public AlphaImage Img = null!;

    public void Dispose() { }

    public Int26_6 GetKern(Rune r0, Rune r1) => new(0);

    public FontMetrics Metrics
    {
        get
        {
            var xbounds = GetGlyphBounds(new Rune('x'));
            var hbounds = GetGlyphBounds(new Rune('H'));
            return new FontMetrics(
                Int26_6.FromInt(Height),
                Int26_6.FromInt(Ascent),
                Int26_6.FromInt(Height - Ascent),
                -xbounds.Bounds.MinY,
                -hbounds.Bounds.MinY,
                new Point(0, 1));
        }
    }

    public GlyphMetrics GetGlyph(Point26_6 dot, Rune r)
    {
        int ri = r.Value - FirstRune;
        if (ri < 0 || N <= ri)
        {
            return default;
        }
        ref var i = ref Fontchars[ri];
        ref var j = ref Fontchars[ri + 1];
        int minX = ((dot.X.Raw + 32) >> 6) + i.Left;
        int minY = ((dot.Y.Raw + 32) >> 6) + i.Top - Ascent;
        var dr = new Rect(
            new Point(minX, minY),
            new Point(minX + (int)(j.X - i.X), minY + i.Bottom - i.Top));

        return new GlyphMetrics(
            dr,
            Img,
            new Point((int)i.X, i.Top),
            Int26_6.FromRaw(i.Width << 6),
            true);
    }

    public GlyphBounds GetGlyphBounds(Rune r)
    {
        int ri = r.Value - FirstRune;
        if (ri < 0 || N <= ri)
        {
            return default;
        }
        ref var i = ref Fontchars[ri];
        ref var j = ref Fontchars[ri + 1];
        var bounds = new Rectangle26_6(
            Int26_6.FromInt(i.Left),
            Int26_6.FromInt(i.Top - Ascent),
            Int26_6.FromInt(i.Left + (int)(j.X - i.X)),
            Int26_6.FromInt(i.Bottom - Ascent));
        return new GlyphBounds(bounds, Int26_6.FromRaw(i.Width << 6), true);
    }

    public Int26_6 GetGlyphAdvance(Rune r)
    {
        int ri = r.Value - FirstRune;
        if (ri < 0 || N <= ri)
        {
            return new(0);
        }
        return Int26_6.FromRaw(Fontchars[ri].Width << 6);
    }
}

internal struct RuneRange
{
    public int Lo, Hi;
    public int Offset;
    public string RelFilename;
    public Plan9Subface? Subface;
    public bool Bad;
}

internal sealed class Plan9Face : IFontFace
{
    // For subfont files, if reading the given file name fails, we try appending
    // ".n" where n is the log2 of the grayscale depth in bits (so at most 3) and
    // then work down to 0. This was done in Plan 9 when antialiased fonts were
    // introduced so that the 1-bit displays could keep using the 1-bit forms but
    // higher depth displays could use the antialiased forms.
    private static readonly string[] SubfontSuffixes = ["", ".3", ".2", ".1", ".0"];

    public int Height;
    public int Ascent;
    public Func<string, byte[]> ReadFile = null!;
    public List<RuneRange> RuneRanges = [];

    public void Dispose() { }

    public Int26_6 GetKern(Rune r0, Rune r1) => new(0);

    public FontMetrics Metrics
    {
        get
        {
            var xbounds = GetGlyphBounds(new Rune('x'));
            var hbounds = GetGlyphBounds(new Rune('H'));
            return new FontMetrics(
                Int26_6.FromInt(Height),
                Int26_6.FromInt(Ascent),
                Int26_6.FromInt(Height - Ascent),
                -xbounds.Bounds.MinY,
                -hbounds.Bounds.MinY,
                new Point(0, 1));
        }
    }

    public GlyphMetrics GetGlyph(Point26_6 dot, Rune r)
    {
        var (s, rr) = Subface(r);
        if (s != null)
        {
            return s.GetGlyph(dot, rr);
        }
        return default;
    }

    public GlyphBounds GetGlyphBounds(Rune r)
    {
        var (s, rr) = Subface(r);
        if (s != null)
        {
            return s.GetGlyphBounds(rr);
        }
        return default;
    }

    public Int26_6 GetGlyphAdvance(Rune r)
    {
        var (s, rr) = Subface(r);
        if (s != null)
        {
            return s.GetGlyphAdvance(rr);
        }
        return new(0);
    }

    internal (Plan9Subface? subface, Rune rr) Subface(Rune r)
    {
        foreach (int ri in new[] { r.Value, 0xfffd })
        {
            for (int i = 0; i < RuneRanges.Count; i++)
            {
                var x = RuneRanges[i];
                if (ri < x.Lo || x.Hi < ri || x.Bad)
                {
                    continue;
                }
                if (x.Subface == null)
                {
                    byte[] data;
                    try
                    {
                        data = ReadSubfontFile(x.RelFilename);
                    }
                    catch (Exception)
                    {
                        Console.Error.WriteLine($"plan9font: couldn't read subfont {x.RelFilename}");
                        x.Bad = true;
                        RuneRanges[i] = x;
                        continue;
                    }
                    try
                    {
                        var sub = (Plan9Subface)Plan9Font.ParseSubfont(data, x.Lo - x.Offset);
                        x.Subface = sub;
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"plan9font: couldn't parse subfont {x.RelFilename}: {ex.Message}");
                        x.Bad = true;
                        RuneRanges[i] = x;
                        continue;
                    }
                }
                return (x.Subface, new Rune(ri));
            }
        }
        return (null, default);
    }

    private byte[] ReadSubfontFile(string name)
    {
        Exception? firstErr = null;
        foreach (string suffix in SubfontSuffixes)
        {
            try
            {
                return ReadFile(name + suffix);
            }
            catch (Exception ex)
            {
                firstErr ??= ex;
            }
        }
        throw firstErr!;
    }
}

internal sealed class Plan9Image
{
    public int Depth;
    public int Width;
    public Rect Rect;
    public byte[] Pix = [];

    public int ByteOffset(int x, int y)
    {
        x -= Rect.Min.X;
        y -= Rect.Min.Y;
        int a = y * Width;
        if (Depth < 8)
        {
            int np = 8 / Depth;
            if (x < 0)
            {
                return a + (x - np + 1) / np;
            }
            return a + x / np;
        }
        return a + x * (Depth / 8);
    }

    public byte At(int x, int y)
    {
        byte b = Pix[ByteOffset(x, y)];
        switch (Depth)
        {
            case 1:
                byte mask = (byte)(1 << (7 - (x & 7)));
                if ((b & mask) != 0) return 0xff;
                return 0;
            case 2:
                int shift = (x & 3) << 1;
                int val = (b << shift) & 0xc0;
                val |= val >> 2;
                val |= val >> 4;
                return (byte)val;
        }
        return 0;
    }
}
