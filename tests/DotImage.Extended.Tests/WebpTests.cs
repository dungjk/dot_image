using System;
using System.IO;
using DotImage;
using DotImage.Png;
using DotImage.Extended.Webp;
using Xunit;
using YCbCrImage = DotImage.YCbCrImage;

namespace DotImage.Extended.Tests;

// Ported from golang.org/x/image/webp/decode_test.go
public class WebpTests
{
    private static readonly string TestDataDir = FindTestDataDir();

    private static string FindTestDataDir()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "TestData")))
        {
            dir = Path.GetDirectoryName(dir);
        }
        return Path.Combine(dir ?? throw new InvalidOperationException("TestData directory not found"), "TestData", "webp");
    }

    private static byte[] Load(string rel)
    {
        string path = Path.Combine(TestDataDir, rel);
        if (!File.Exists(path))
            throw new FileNotFoundException("Test data not found", path);
        return File.ReadAllBytes(path);
    }

    private static string Hex(byte[] x)
    {
        var sb = new System.Text.StringBuilder();
        int i = 0;
        while (i < x.Length)
        {
            int n = System.Math.Min(16, x.Length - i);
            sb.Append(" .");
            for (int j = 0; j < n; j++)
                sb.Append($" {x[i + j]:x2}");
            i += n;
        }
        return sb.ToString();
    }

    private void TestDecodeLossy(string tc, bool withAlpha)
    {
        string webpFilename = tc + ".lossy.webp";
        string pngFilename = webpFilename + ".ycbcr.png";
        if (withAlpha)
        {
            webpFilename = tc + ".lossy-with-alpha.webp";
            pngFilename = webpFilename + ".nycbcra.png";
        }

        IImage img0;
        using (var f0 = new MemoryStream(Load(webpFilename)))
            img0 = Decoder.Decode(f0);

        YCbCrImage? m0 = null;
        byte[]? a0 = null;
        int a0Stride = 0;
        if (withAlpha)
        {
            if (img0 is NYCbCrAImage a)
            {
                m0 = a.YCbCrImage;
                a0 = a.A.ToArray();
                a0Stride = a.AStride;
            }
        }
        else
        {
            m0 = img0 as YCbCrImage;
        }
        Assert.True(m0 != null && m0.SubsampleRatio == YCbCrSubsampleRatio.Ratio420,
            $"{tc}: decoded WEBP image is not a 4:2:0 YCbCr or 4:2:0 NYCbCrA");

        int w = m0!.Rect.Dx();
        int h = m0.Rect.Dy();
        int w2 = (w + 1) / 2;
        int h2 = (h + 1) / 2;

        GrayImage m1;
        using (var f1 = new MemoryStream(Load(pngFilename)))
        {
            m1 = (GrayImage)PngReader.Decode(f1);
        }

        int pngW = 2 * w2;
        int pngH = h + h2;
        if (withAlpha) pngH += h;
        Assert.Equal(new Rect(new Point(0, 0), new Point(pngW, pngH)), m1.Rect);

        (string Name, Memory<byte> Pix, int Stride, Rect R)[] planes =
        {
            ("Y", m0.Y, m0.YStride, new Rect(new Point(0, 0), new Point(w, h))),
            ("Cb", m0.Cb, m0.CStride, new Rect(new Point(0 * w2, h), new Point(1 * w2, h + h2))),
            ("Cr", m0.Cr, m0.CStride, new Rect(new Point(1 * w2, h), new Point(2 * w2, h + h2))),
        };
        if (withAlpha)
        {
            Array.Resize(ref planes, planes.Length + 1);
            planes[planes.Length - 1] =
                ("A", a0!, a0Stride, new Rect(new Point(0, h + h2), new Point(w, 2 * h + h2)));
        }

        var m1Span = m1.Pix.Span;
        foreach (var plane in planes)
        {
            int dx = plane.R.Dx();
            int nDiff = 0;
            var diff = new byte[dx];
            int j = 0;
            var planeSpan = plane.Pix.Span;
            for (int y = plane.R.Min.Y; y < plane.R.Max.Y; j++, y++)
            {
                var got = planeSpan.Slice(j * plane.Stride, dx);
                var want = m1Span.Slice(y * m1.Stride + plane.R.Min.X, dx);
                if (got.SequenceEqual(want)) continue;
                nDiff++;
                if (nDiff > 10)
                {
                    Assert.Fail($"{tc}: {plane.Name} plane: more rows differ");
                }
                for (int i = 0; i < got.Length; i++)
                    diff[i] = (byte)(got[i] - want[i]);
                throw new Xunit.Sdk.XunitException(
                    $"{tc}: {plane.Name} plane: m0 row {j}, m1 row {y}\n" +
                    $"got  {Hex(got.ToArray())}\n" +
                    $"want {Hex(want.ToArray())}\n" +
                    $"diff {Hex(diff)}");
            }
        }
    }

    [Fact]
    public void TestDecodeVP8()
    {
        string[] testCases =
        {
            "blue-purple-pink",
            "blue-purple-pink-large.no-filter",
            "blue-purple-pink-large.simple-filter",
            "blue-purple-pink-large.normal-filter",
            "video-001",
            "yellow_rose",
        };
        foreach (var tc in testCases)
            TestDecodeLossy(tc, false);
    }

    [Fact]
    public void TestDecodeVP8XAlpha()
    {
        TestDecodeLossy("yellow_rose", true);
    }

    [Fact]
    public void TestDecodeVP8L()
    {
        var testCases = new (string Name, string? F0, string? F1)[]
        {
            ("blue-purple-pink", null, null),
            ("blue-purple-pink-large", null, null),
            ("gopher-doc.1bpp", null, null),
            ("gopher-doc.2bpp", null, null),
            ("gopher-doc.4bpp", null, null),
            ("gopher-doc.8bpp", null, null),
            ("gopher-doc.with-alpha", null, null),
            ("tux", null, null),
            ("yellow_rose", null, null),
            ("remapped hgroups", "gopher-doc.skip-hgroup.lossless.webp", "gopher-doc.8bpp.png"),
        };

        foreach (var tc in testCases)
        {
            string f0Name = tc.F0 ?? tc.Name + ".lossless.webp";
            IImage decoded;
            using (Stream s = OpenFile(f0Name))
                decoded = Decoder.Decode(s);

            var m0 = Assert.IsType<NrgbaImage>(decoded);

            string name1 = tc.F1 ?? tc.Name + ".png";
            IImage golden;
            using (Stream s = OpenFile(name1))
                golden = PngReader.Decode(s);

            byte[] m1Pix;
            Rect m1Rect;
            switch (golden)
            {
                case NrgbaImage nrgba:
                    m1Pix = nrgba.Pix.ToArray();
                    m1Rect = nrgba.Rect;
                    break;
                case RgbaImage rgba:
                    m1Pix = rgba.Pix.ToArray();
                    m1Rect = rgba.Rect;
                    break;
                default:
                    throw new Xunit.Sdk.XunitException($"PNG image is {golden.GetType()}, want NRGBA/RGBA");
            }

            Assert.Equal(new Rect(new Point(0, 0), new Point(m0.Rect.Dx(), m0.Rect.Dy())), m1Rect);
            for (int i = 0; i < m0.Pix.Length; i++)
            {
                if (m0.Pix.Span[i] != m1Pix[i])
                {
                    int y = i / m0.Stride;
                    int x = (i - y * m0.Stride) / 4;
                    int j = 4 * (y * m0.Stride + x);
                    throw new Xunit.Sdk.XunitException(
                        $"at ({x}, {y}):\n" +
                        $"got  {m0.Pix.Span[j + 0]:x2} {m0.Pix.Span[j + 1]:x2} {m0.Pix.Span[j + 2]:x2} {m0.Pix.Span[j + 3]:x2}\n" +
                        $"want {m1Pix[j + 0]:x2} {m1Pix[j + 1]:x2} {m1Pix[j + 2]:x2} {m1Pix[j + 3]:x2}");
                }
            }
        }
    }

    private Stream OpenFile(string name) => new MemoryStream(Load(name));

    [Fact]
    public void TestDecodePartitionTooLarge()
    {
        byte[] data =
        {
            (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0xff, 0xff, 0xff, 0x7f,
            (byte)'W', (byte)'E', (byte)'B', (byte)'P', (byte)'V', (byte)'P', (byte)'8', (byte)' ',
            0x78, 0x56, 0x34, 0x12,
            0xbd, 0x01, 0x00, 0x14, 0x00, 0x00, 0xb2, 0x34, 0x0a, 0x9d, 0x01, 0x2a, 0x96, 0x00, 0x67, 0x00,
        };
        Exception? err = null;
        using (var s = new MemoryStream(data))
        {
            try { Decoder.Decode(s); }
            catch (Exception e) { err = e; }
        }
        Assert.NotNull(err);
        Assert.Contains("too much data", err!.Message);
    }

    [Fact]
    public void TestDuplicateVP8X()
    {
        byte[] data =
        {
            (byte)'R', (byte)'I', (byte)'F', (byte)'F', 49, 0, 0, 0, (byte)'W', (byte)'E', (byte)'B', (byte)'P',
            (byte)'V', (byte)'P', (byte)'8', (byte)'X', 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            (byte)'V', (byte)'P', (byte)'8', (byte)'X', 10, 0, 0, 0, 0x10, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        };
        using var s = new MemoryStream(data);
        Assert.Throws<WebpFormatException>(() => Decoder.Decode(s));
    }

    [Fact]
    public void TestVP8XImageTooLarge()
    {
        byte[] data =
        {
            (byte)'R', (byte)'I', (byte)'F', (byte)'F', 22, 0, 0, 0, (byte)'W', (byte)'E', (byte)'B', (byte)'P',
            (byte)'V', (byte)'P', (byte)'8', (byte)'X', 10, 0, 0, 0,
            1 << 4, 0, 0, 0,
            0xff, 0xff, 0x00,
            0xff, 0x7f, 0x00,
        };
        using var s = new MemoryStream(data);
        Assert.Throws<WebpFormatException>(() => Decoder.DecodeConfig(s));
    }

    [Fact]
    public void TestVP8XImageNotQuiteTooLarge()
    {
        byte[] data =
        {
            (byte)'R', (byte)'I', (byte)'F', (byte)'F', 22, 0, 0, 0, (byte)'W', (byte)'E', (byte)'B', (byte)'P',
            (byte)'V', (byte)'P', (byte)'8', (byte)'X', 10, 0, 0, 0,
            1 << 4, 0, 0, 0,
            0xff, 0xff, 0x00,
            0xfe, 0x7f, 0x00,
        };
        Config cfg;
        using (var s = new MemoryStream(data))
            cfg = Decoder.DecodeConfig(s);
        Assert.Equal(0x10000, cfg.Width);
        Assert.Equal(0x7fff, cfg.Height);
    }

    [Fact]
    public void TestVP8XAndVP8LDimensionMismatch()
    {
        foreach (var test in new[] { "blue-purple-pink.lossless.webp", "blue-purple-pink.lossy.webp" })
        {
            byte[] vp8Chunk = Load(test);
            vp8Chunk = vp8Chunk[12..];
            byte[] vp8xChunk =
            {
                (byte)'V', (byte)'P', (byte)'8', (byte)'X', 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            };
            uint fileSize = (uint)(12 + vp8xChunk.Length + vp8Chunk.Length - 8);
            var data = new byte[12 + vp8xChunk.Length + vp8Chunk.Length];
            int pos = 0;
            data[pos++] = (byte)'R'; data[pos++] = (byte)'I'; data[pos++] = (byte)'F'; data[pos++] = (byte)'F';
            data[pos++] = (byte)fileSize; data[pos++] = (byte)(fileSize >> 8); data[pos++] = (byte)(fileSize >> 16); data[pos++] = (byte)(fileSize >> 24);
            data[pos++] = (byte)'W'; data[pos++] = (byte)'E'; data[pos++] = (byte)'B'; data[pos++] = (byte)'P';
            Array.Copy(vp8xChunk, 0, data, pos, vp8xChunk.Length); pos += vp8xChunk.Length;
            Array.Copy(vp8Chunk, 0, data, pos, vp8Chunk.Length);

            using var s = new MemoryStream(data);
            Assert.Throws<WebpFormatException>(() => Decoder.Decode(s));
        }
    }

    [Fact]
    public void TestLargeHuffmanIndexRejected()
    {
        using Stream s = OpenFile("large-huffman-index.lossless.webp");
        var err = Record.Exception(() => Decoder.Decode(s));
        Assert.NotNull(err);
        Assert.Contains("too many Huffman trees", err!.Message);
    }
}