// Ported from Go src/image/jpeg/reader_test.go


using System.Text;
using DotImage.Jpeg;

namespace DotImage.Tests.Jpeg;

public class JpegReaderTests
{
    private static string TestDataPath(string path) =>
        Path.Combine(AppContext.BaseDirectory, "testdata", path);

    private static IImage DecodeFile(string filename)
    {
        using var f = File.OpenRead(TestDataPath(filename));
        return JpegReader.Decode(f);
    }

    [Theory]
    [InlineData("video-001")]
    [InlineData("video-001.q50.410")]
    [InlineData("video-001.q50.411")]
    [InlineData("video-001.q50.420")]
    [InlineData("video-001.q50.422")]
    [InlineData("video-001.q50.440")]
    [InlineData("video-001.q50.444")]
    [InlineData("video-005.gray.q50")]
    [InlineData("video-005.gray.q50.2x2")]
    [InlineData("video-001.separate.dc.progression")]
    public void DecodeProgressive(string tc)
    {
        var m0 = DecodeFile(tc + ".jpeg");
        var m1 = DecodeFile(tc + ".progressive.jpeg");
        Assert.Equal(m0.Bounds(), m1.Bounds());
        Assert.Equal(Geometry.Rect(0, 0, 150, 103), m0.Bounds());

        switch (m0)
        {
            case YCbCrImage y0 when m1 is YCbCrImage y1:
                Check(y0.Bounds(), y0.Y, y1.Y, y0.YStride, y1.YStride);
                Check(y0.Bounds(), y0.Cb, y1.Cb, y0.CStride, y1.CStride);
                Check(y0.Bounds(), y0.Cr, y1.Cr, y0.CStride, y1.CStride);
                break;
            case GrayImage g0 when m1 is GrayImage g1:
                Check(g0.Bounds(), g0.Pix, g1.Pix, g0.Stride, g1.Stride);
                break;
            default:
                Assert.Fail($"{tc}: unexpected image type {m0.GetType()}");
                break;
        }
    }

    private sealed class EofReader : Stream
    {
        private byte[] _data;
        private byte[] _dataEof;
        public int LenAtEof = -1;

        public EofReader(byte[] data, byte[] dataEof)
        {
            _data = data;
            _dataEof = dataEof;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int n;
            if (_data.Length > 0)
            {
                n = Math.Min(count, _data.Length);
                Buffer.BlockCopy(_data, 0, buffer, offset, n);
                _data = _data[n..];
            }
            else
            {
                n = Math.Min(count, _dataEof.Length);
                Buffer.BlockCopy(_dataEof, 0, buffer, offset, n);
                _dataEof = _dataEof[n..];
                if (_dataEof.Length == 0)
                {
                    if (LenAtEof == -1)
                        LenAtEof = n;
                    return n == 0 ? 0 : n;
                }
            }
            return n;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    [Fact]
    public void DecodeEOF()
    {
        var data = File.ReadAllBytes(TestDataPath("video-001.jpeg"));
        int n = data.Length;
        for (int i = 0; i < n;)
        {
            var r = new EofReader(data[..(n - i)], data[(n - i)..]);
            JpegReader.Decode(r);
            if (i == 0) i = 1;
            else i *= 2;
        }
    }

    private static void Check(Rect bounds, Memory<byte> pix0, Memory<byte> pix1, int stride0, int stride1)
    {
        Assert.True(stride0 > 0 && stride0 % 8 == 0, $"bad stride {stride0}");
        Assert.True(stride1 > 0 && stride1 % 8 == 0, $"bad stride {stride1}");
        var p0 = pix0.Span;
        var p1 = pix1.Span;
        for (int y = 0; y < p0.Length / stride0 && y < p1.Length / stride1; y += 8)
        {
            for (int x = 0; x < stride0 && x < stride1; x += 8)
            {
                if (x >= bounds.Max.X || y >= bounds.Max.Y)
                    continue;
                for (int j = 0; j < 8; j++)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        int index0 = (y + j) * stride0 + (x + i);
                        int index1 = (y + j) * stride1 + (x + i);
                        Assert.True(p0[index0] == p1[index1],
                            $"blocks at ({x}, {y}) differ:\n{PixString(p0, stride0, x, y)}and\n{PixString(p1, stride1, x, y)}");
                    }
                }
            }
        }
    }

    private static string PixString(ReadOnlySpan<byte> pix, int stride, int x, int y)
    {
        var s = new StringBuilder();
        for (int j = 0; j < 8; j++)
        {
            s.Append('\t');
            for (int i = 0; i < 8; i++)
                s.AppendFormat("{0:x2} ", pix[(y + j) * stride + (x + i)]);
            s.Append('\n');
        }
        return s.ToString();
    }

    [Fact]
    public void TruncatedSOSDataDoesntPanic()
    {
        var b = File.ReadAllBytes(TestDataPath("video-005.gray.q50.jpeg"));
        var sosMarker = new byte[] { 0xff, 0xda };
        int i = IndexOf(b, sosMarker);
        Assert.True(i >= 0, "SOS marker not found");
        i += sosMarker.Length;
        int j = Math.Min(i + 10, b.Length);
        for (; i < j; i++)
        {
            try { JpegReader.Decode(new MemoryStream(b[..i])); }
            catch { /* must not panic */ }
        }
    }

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }

    [Fact]
    public void LargeImageWithShortData()
    {
        const string inputHex =
            "ffd8ffe000104a46494600010100000100010000ffdb004300100b0c0e0c0a10" +
            "0e890e1211101318ffd8ffe000104a46494600010100000100010000ffdb0043" +
            "00100b0c0e0c0a100e0d0e1211101318281a181616183123251d283a333d3c39" +
            "33383740485c4e404457453738506d51575f626768673e4d71797064785c6567" +
            "63ffc0000b082000200001011100ffc4001f0000010501010101010100000000" +
            "00000102030405060708090a0bffc400b51000020103030204030505040400" +
            "00017d01020300041105122131010613516107227114328191a10823d8ffdd" +
            "42b1c11552d1f02433627282090a161718191a25262728292a3435363738393a" +
            "434445464748494a535455565758595a00636465666768696a737475767778" +
            "797a838485868788898a92939495969798999aa2a3a4a5a6a7a8a9aab2b3b4" +
            "b5b6b7b8b9bac2c3c4c5c6c7ffd8ffe000104a464946000101000001000100" +
            "00ffdb004300100b0c0e0c0a100e0d0e1211101318281a181616183123251d" +
            "c8c9cad2d3d4d5d6d7d8d9dae1e2e3e4e5e6e7e8e9eaf1f2f3f4f5f6f7f8" +
            "f9faffda00080101003f00b9eb50b0dbc88ae46380dd31d6ad7f2c5421f6c" +
            "6ff434dd3c8face3d80a9cc8734b337fa2b9f6aad6320369f786475e6ab7d" +
            "b2de2970d320279deaf0ca9f24adf46a9248496e377f2e0e0a627f7fdfd9";
        var data = Convert.FromHexString(inputHex);
        Assert.ThrowsAny<Exception>(() => JpegReader.Decode(new MemoryStream(data)));
    }

    [Fact]
    public void PaddedRSTMarker()
    {
        DecodeFile("padded-rst.jpeg");
    }

    [Fact]
    public void ExtraneousData()
    {
        var src = Images.NewRgba(Geometry.Rect(0, 0, 1, 1));
        src.SetRgba(0, 0, new Rgba(0xff, 0, 0, 0xff));
        using var buf = new MemoryStream();
        JpegWriter.Encode(buf, src, new JpegOptions());
        var enc = buf.ToArray();
        Assert.True(enc.Length >= 64);
        Assert.Equal(new byte[] { 0xff, 0xd9 }, enc[^2..]);
        Assert.True(enc.AsSpan(enc.Length - 64).IndexOf((ReadOnlySpan<byte>)[0xff, 0xda]) >= 0);

        var rnd = new Random(1);
        int nerr = 0;
        for (int i = 0; i < 1000 && nerr < 10; i++)
        {
            buf.SetLength(0);
            buf.Write(enc, 0, enc.Length - 2);
            for (int n = rnd.Next(10); n > 0; n--)
            {
                int x = rnd.Next(256);
                if (x != 0xff)
                    buf.WriteByte((byte)x);
                else
                    buf.Write([0xff, 0x00]);
            }
            buf.Write([0xff, 0xd9]);
            try
            {
                var got = JpegReader.Decode(new MemoryStream(buf.ToArray()));
                Assert.Equal(src.Bounds(), got.Bounds());
                if (AverageDelta(src, got) > 2 << 8)
                {
                    nerr++;
                    Assert.Fail($"image #{i} changed too much after a round trip");
                }
            }
            catch (Exception ex)
            {
                nerr++;
                Assert.Fail($"could not decode image #{i}: {ex.Message}");
            }
        }
    }

    private static long AverageDelta(IImage m0, IImage m1)
    {
        var b = m0.Bounds();
        long sum = 0, n = 0;
        for (int y = b.Min.Y; y < b.Max.Y; y++)
        {
            for (int x = b.Min.X; x < b.Max.X; x++)
            {
                var c0 = m0.At(x, y);
                var c1 = m1.At(x, y);
                (uint r0, uint g0, uint b0, _) = c0.Rgba();
                (uint r1, uint g1, uint b1, _) = c1.Rgba();
                sum += Delta(r0, r1) + Delta(g0, g1) + Delta(b0, b1);
                n += 3;
            }
        }
        return sum / n;
    }

    private static long Delta(uint u0, uint u1)
    {
        long d = (long)u0 - u1;
        return d < 0 ? -d : d;
    }

    [Fact]
    public void Issue56724()
    {
        var b = File.ReadAllBytes(TestDataPath("video-001.jpeg"));
        Assert.Throws<EndOfStreamException>(() => JpegReader.Decode(new MemoryStream(b[..24])));
    }

    [Fact]
    public void Issue78368()
    {
        byte[] data =
        [
            0xff, 0xd8, 0xff, 0xdb, 0x00, 0x84, 0x00, 0x20, 0x20, 0x20, 0x20, 0x20,
            0x20, 0xff, 0x20, 0x20, 0x20, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
            0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x20, 0xff, 0xff, 0xff, 0xff,
            0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
            0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
            0xff, 0xff, 0x20, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x01,
            0xff, 0xff, 0xff, 0x20, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
            0xff, 0xff, 0xff, 0xff, 0x20, 0xff, 0xff, 0x20, 0x20, 0xff, 0xff, 0xff,
            0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
            0xff, 0xff, 0xff, 0x20, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
            0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
            0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xc2, 0x00, 0x11, 0x08, 0x00,
            0x20, 0x00, 0x20, 0x03, 0x01, 0x11, 0x00, 0x20, 0x21, 0x01, 0xff, 0x11,
            0x01, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
            0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
            0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
            0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
            0x20, 0x20, 0x20, 0x20, 0xff, 0xc4, 0x00, 0x27, 0x10, 0x01, 0x00, 0x02,
            0x01, 0x04, 0x01, 0x03, 0x04, 0x03, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
            0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0xff, 0xda, 0x00,
            0x08, 0x01, 0x20, 0x20, 0x20, 0x20, 0xed, 0x20, 0x20, 0x20, 0x20, 0x20,
            0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
            0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
            0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
            0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
            0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0xff, 0xee, 0x00, 0x0e, 0x41,
            0x64, 0x6f, 0x62, 0x65, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x00, 0xff,
            0xdb, 0x00, 0x43, 0x00, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
            0x20, 0x20, 0x20, 0x20, 0xff, 0xff, 0xff, 0xff, 0xff, 0x20, 0xff, 0xff,
            0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x20, 0xff, 0xff, 0xff,
            0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
            0xff, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
            0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
            0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
            0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
            0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
            0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
            0xff, 0xd9, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
        ];
        try
        {
            JpegReader.Decode(new MemoryStream(data));
        }
        catch
        {
            // Go's TestIssue78368 ignores decode errors; it only checks for no panic.
        }
    }

    [Fact]
    public void BadRestartMarker()
    {
        var b = File.ReadAllBytes(TestDataPath("video-001.restart2.jpeg"));
        Assert.Equal(4855, b.Length);
        Assert.Equal(0xff, b[2816]);
        Assert.Equal(0xd1, b[2817]);
        var prefix = b[..2816];
        var suffix = b[2816..];

        (string tc, bool want)[] testCases =
        [
            ("PASS:", true),
            ("PASS:\x00", true),
            ("PASS:\x61", true),
            ("PASS:\x61\x62\x63\xff\x00\x64", true),
            ("PASS:\xff", true),
            ("PASS:\xff\x00", true),
            ("PASS:\xff\xff\xff\x00\xff\x00\x00\xff\xff\xff", true),
            ("FAIL:\xff\x03", false),
            ("FAIL:\xff\xd5", false),
            ("FAIL:\xff\xff\xd5", false),
        ];

        foreach (var (tc, want) in testCases)
        {
            var infix = Encoding.Latin1.GetBytes(tc[5..]);
            var data = new byte[prefix.Length + infix.Length + suffix.Length];
            Buffer.BlockCopy(prefix, 0, data, 0, prefix.Length);
            Buffer.BlockCopy(infix, 0, data, prefix.Length, infix.Length);
            Buffer.BlockCopy(suffix, 0, data, prefix.Length + infix.Length, suffix.Length);
            bool got = false;
            try
            {
                JpegReader.Decode(new MemoryStream(data));
                got = true;
            }
            catch
            {
                got = false;
            }
            Assert.Equal(want, got);
        }
    }

    [Theory]
    [InlineData("2x2,1x1,2x2", "video-001.q50.221122.jpeg")]
    [InlineData("2x1,1x2,1x1", "video-001.q50.211211.jpeg")]
    [InlineData("2x2,2x1,1x2", "video-001.q50.222112.jpeg")]
    [InlineData("1x2,1x1,2x1", "video-001.q50.121121.jpeg")]
    public void DecodeFlexSubsampling(string _, string filename)
    {
        var m = DecodeFile(filename);
        Assert.Equal(Geometry.Rect(0, 0, 150, 103), m.Bounds());
        var ycbcr = Assert.IsType<YCbCrImage>(m);
        Assert.Equal(YCbCrSubsampleRatio.Ratio444, ycbcr.SubsampleRatio);
    }
}
