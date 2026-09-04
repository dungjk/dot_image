// Ported from Go src/image/gif/writer.go


using System.Buffers.Binary;
using System.Numerics;
using DotImage.Color;
using DotImage.Compress;
using DotImage.Draw;

namespace DotImage.Gif;

public sealed class GifOptions
{
    public int NumColors { get; set; } = 256;
    public IDrawer? Drawer { get; set; }
}

public static class GifWriter
{
    private const byte GcLabel = 0xF9;
    private const byte GcBlockSize = 0x04;

    private const byte FColorTable = 1 << 7;
    private const byte SExtension = 0x21;
    private const byte SImageDescriptor = 0x2C;
    private const byte STrailer = 0x3B;

    public static void Encode(Stream w, IImage m, GifOptions? o = null)
    {
        var b = m.Bounds();
        if (b.Dx() >= 1 << 16 || b.Dy() >= 1 << 16)
            throw new InvalidOperationException("gif: image is too large to encode");

        var opts = o ?? new GifOptions();
        if (opts.NumColors < 1 || opts.NumColors > 256)
            opts.NumColors = 256;
        opts.Drawer ??= DrawOps.FloydSteinberg;

        PalettedImage? pm = m as PalettedImage;
        if (pm == null && m.ColorModel() is Palette cp)
        {
            pm = Images.NewPaletted(b, cp);
            for (int y = b.Min.Y; y < b.Max.Y; y++)
            {
                for (int x = b.Min.X; x < b.Max.X; x++)
                    pm.Set(x, y, cp.Convert(m.At(x, y)));
            }
        }

        if (pm == null || pm.Palette.Length > opts.NumColors)
        {
            var colors = new IColor[opts.NumColors];
            for (int i = 0; i < opts.NumColors; i++)
                colors[i] = BuiltInPalettes.Plan9[i];
            var palette = new Palette(colors);
            pm = Images.NewPaletted(b, palette);
            opts.Drawer.Draw(pm, b, m, b.Min);
        }

        if (!b.Min.Eq(new Point(0, 0)))
        {
            var dup = new PalettedImage
            {
                Pix = pm.Pix,
                Stride = pm.Stride,
                Rect = b.Sub(b.Min),
                Palette = pm.Palette,
            };
            pm = dup;
        }

        EncodeAll(w, new GifAnimation
        {
            Image = [pm],
            Delay = [0],
            Config = new Config(pm.Palette, b.Dx(), b.Dy()),
        });
    }

    public static void EncodeAll(Stream w, GifAnimation g)
    {
        if (g.Image.Count == 0)
            throw new InvalidOperationException("gif: must provide at least one image");
        if (g.Image.Count != g.Delay.Count)
            throw new InvalidOperationException("gif: mismatched image and delay lengths");
        if (g.Disposal != null && g.Image.Count != g.Disposal.Length)
            throw new InvalidOperationException("gif: mismatched image and disposal lengths");

        var enc = new Encoder(g);
        if (g.Config.Width == 0 && g.Config.Height == 0)
        {
            var p = g.Image[0].Bounds().Max;
            enc.Config = new Config(g.Config.ColorModel, p.X, p.Y);
        }
        else if (g.Config.ColorModel is Palette)
        {
            enc.Config = g.Config;
        }
        else if (g.Config.ColorModel != null)
        {
            throw new InvalidOperationException("gif: GIF color model must be a color.Palette");
        }

        enc.BackgroundIndex = g.BackgroundIndex;
        enc.LoopCount = g.LoopCount;
        enc.Disposal = g.Disposal;
        enc.Write(w);
    }

    public static int EncodeColorTable(Span<byte> dst, Palette p, int size)
    {
        if (size >= 8)
            throw new InvalidOperationException("gif: cannot encode color table with more than 256 entries");

        for (int i = 0; i < p.Length; i++)
        {
            var c = p[i];
            byte r, g, b;
            if (c is Rgba rgba)
            {
                r = rgba.R;
                g = rgba.G;
                b = rgba.B;
            }
            else
            {
                var (rr, gg, bb, _) = c.Rgba();
                r = (byte)(rr >> 8);
                g = (byte)(gg >> 8);
                b = (byte)(bb >> 8);
            }
            dst[3 * i] = r;
            dst[3 * i + 1] = g;
            dst[3 * i + 2] = b;
        }

        int n = 1 << (size + 1);
        if (n > p.Length)
            dst.Slice(3 * p.Length, 3 * (n - p.Length)).Clear();
        return 3 * n;
    }

    private sealed class Encoder
    {
        private readonly List<PalettedImage> _images;
        private readonly List<int> _delays;
        private Stream _w = Stream.Null;
        private Exception? _err;
        public Config Config;
        public int LoopCount;
        public byte BackgroundIndex;
        public byte[]? Disposal;
        private int _globalCt;
        private readonly byte[] _buf = new byte[256];
        private readonly byte[] _globalColorTable = new byte[3 * 256];
        private readonly byte[] _localColorTable = new byte[3 * 256];

        public Encoder(GifAnimation g)
        {
            _images = g.Image;
            _delays = g.Delay;
            Config = g.Config;
            LoopCount = g.LoopCount;
            BackgroundIndex = g.BackgroundIndex;
            Disposal = g.Disposal;
        }

        public void Write(Stream w)
        {
            _w = new BufferedStream(w);
            WriteHeader();
            for (int i = 0; i < _images.Count; i++)
            {
                byte disposal = 0;
                if (Disposal != null)
                    disposal = Disposal[i];
                WriteImageBlock(_images[i], _delays[i], disposal);
            }
            WriteByte(STrailer);
            Flush();
            if (_err != null)
                throw _err;
        }

        private void Flush()
        {
            if (_err != null) return;
            try { _w.Flush(); }
            catch (Exception ex) { _err = ex; }
        }

        private void Write(ReadOnlySpan<byte> p)
        {
            if (_err != null) return;
            try { _w.Write(p); }
            catch (Exception ex) { _err = ex; }
        }

        private void WriteByte(byte b)
        {
            if (_err != null) return;
            try { _w.WriteByte(b); }
            catch (Exception ex) { _err = ex; }
        }

        private void WriteHeader()
        {
            if (_err != null) return;
            try
            {
                var header = System.Text.Encoding.ASCII.GetBytes("GIF89a");
                _w.Write(header);
            }
            catch (Exception ex)
            {
                _err = ex;
                return;
            }

            BinaryPrimitives.WriteUInt16LittleEndian(_buf.AsSpan(0, 2), (ushort)Config.Width);
            BinaryPrimitives.WriteUInt16LittleEndian(_buf.AsSpan(2, 2), (ushort)Config.Height);
            Write(_buf.AsSpan(0, 4));

            if (Config.ColorModel is Palette p && p.Length > 0)
            {
                int paddedSize = Log2(p.Length);
                _buf[0] = (byte)(FColorTable | paddedSize);
                _buf[1] = BackgroundIndex;
                _buf[2] = 0x00;
                Write(_buf.AsSpan(0, 3));
                try
                {
                    _globalCt = EncodeColorTable(_globalColorTable, p, paddedSize);
                }
                catch (Exception ex)
                {
                    _err = ex;
                    return;
                }
                Write(_globalColorTable.AsSpan(0, _globalCt));
            }
            else
            {
                _buf[0] = 0x00;
                _buf[1] = 0x00;
                _buf[2] = 0x00;
                Write(_buf.AsSpan(0, 3));
            }

            if (_images.Count > 1 && LoopCount >= 0)
            {
                _buf[0] = 0x21;
                _buf[1] = 0xff;
                _buf[2] = 0x0b;
                Write(_buf.AsSpan(0, 3));
                try
                {
                    var appId = System.Text.Encoding.ASCII.GetBytes("NETSCAPE2.0");
                    _w.Write(appId);
                }
                catch (Exception ex)
                {
                    _err = ex;
                    return;
                }
                _buf[0] = 0x03;
                _buf[1] = 0x01;
                BinaryPrimitives.WriteUInt16LittleEndian(_buf.AsSpan(2, 2), (ushort)LoopCount);
                _buf[4] = 0x00;
                Write(_buf.AsSpan(0, 5));
            }
        }

        private bool ColorTablesMatch(int localLen, int transparentIndex)
        {
            int localSize = 3 * localLen;
            if (transparentIndex >= 0)
            {
                int trOff = 3 * transparentIndex;
                return _globalColorTable.AsSpan(0, trOff).SequenceEqual(_localColorTable.AsSpan(0, trOff))
                    && _globalColorTable.AsSpan(trOff + 3, localSize - trOff - 3)
                        .SequenceEqual(_localColorTable.AsSpan(trOff + 3, localSize - trOff - 3));
            }
            return _globalColorTable.AsSpan(0, localSize).SequenceEqual(_localColorTable.AsSpan(0, localSize));
        }

        private void WriteImageBlock(PalettedImage pm, int delay, byte disposal)
        {
            if (_err != null) return;
            if (pm.Palette.Length == 0)
            {
                _err = new InvalidOperationException("gif: cannot encode image block with empty palette");
                return;
            }

            var b = pm.Bounds();
            if (b.Min.X < 0 || b.Max.X >= 1 << 16 || b.Min.Y < 0 || b.Max.Y >= 1 << 16)
            {
                _err = new InvalidOperationException("gif: image block is too large to encode");
                return;
            }

            if (!b.In(Geometry.Rect(0, 0, Config.Width, Config.Height)))
            {
                _err = new InvalidOperationException("gif: image block is out of bounds");
                return;
            }

            int transparentIndex = -1;
            for (int i = 0; i < pm.Palette.Length; i++)
            {
                var c = pm.Palette[i];
                if (c == null!)
                {
                    _err = new InvalidOperationException("gif: cannot encode color table with nil entries");
                    return;
                }
                var (_, _, _, a) = c.Rgba();
                if (a == 0)
                {
                    transparentIndex = i;
                    break;
                }
            }

            if (delay > 0 || disposal != 0 || transparentIndex != -1)
            {
                _buf[0] = SExtension;
                _buf[1] = GcLabel;
                _buf[2] = GcBlockSize;
                _buf[3] = transparentIndex != -1 ? (byte)(0x01 | disposal << 2) : (byte)(0x00 | disposal << 2);
                BinaryPrimitives.WriteUInt16LittleEndian(_buf.AsSpan(4, 2), (ushort)delay);
                _buf[6] = transparentIndex != -1 ? (byte)transparentIndex : (byte)0;
                _buf[7] = 0x00;
                Write(_buf.AsSpan(0, 8));
            }

            _buf[0] = SImageDescriptor;
            BinaryPrimitives.WriteUInt16LittleEndian(_buf.AsSpan(1, 2), (ushort)b.Min.X);
            BinaryPrimitives.WriteUInt16LittleEndian(_buf.AsSpan(3, 2), (ushort)b.Min.Y);
            BinaryPrimitives.WriteUInt16LittleEndian(_buf.AsSpan(5, 2), (ushort)b.Dx());
            BinaryPrimitives.WriteUInt16LittleEndian(_buf.AsSpan(7, 2), (ushort)b.Dy());
            Write(_buf.AsSpan(0, 9));

            int paddedSize = Log2(pm.Palette.Length);
            if (Config.ColorModel is Palette gp && pm.Palette.Length <= gp.Length && ReferenceEquals(pm.Palette, gp))
            {
                WriteByte(0);
            }
            else
            {
                int ct;
                try
                {
                    ct = EncodeColorTable(_localColorTable, pm.Palette, paddedSize);
                }
                catch (Exception ex)
                {
                    _err = ex;
                    return;
                }

                if (ct <= _globalCt && ColorTablesMatch(pm.Palette.Length, transparentIndex))
                    WriteByte(0);
                else
                {
                    WriteByte((byte)(FColorTable | paddedSize));
                    Write(_localColorTable.AsSpan(0, ct));
                }
            }

            int litWidth = paddedSize + 1;
            if (litWidth < 2) litWidth = 2;
            WriteByte((byte)litWidth);

            var bw = new BlockWriter(this);
            bw.Setup();
            using (var lzww = new LzwWriter(bw, LzwOrder.Lsb, litWidth))
            {
                int dx = b.Dx();
                if (dx == pm.Stride)
                {
                    try { lzww.Write(pm.Pix.Span.Slice(0, dx * b.Dy()).ToArray()); }
                    catch (Exception ex) { _err = ex; return; }
                }
                else
                {
                    for (int y = b.Min.Y, i = 0; y < b.Max.Y; i += pm.Stride, y++)
                    {
                        try { lzww.Write(pm.Pix.Span.Slice(i, dx).ToArray()); }
                        catch (Exception ex) { _err = ex; return; }
                    }
                }
            }
            bw.CloseBlocks();
        }

        private sealed class BlockWriter(Encoder e) : Stream, IByteSink
        {
            public void Setup() => e._buf[0] = 0;

            void IByteSink.WriteByte(byte c) => WriteBlockByte(c);

            void IByteSink.Flush() { }

            private void WriteBlockByte(byte c)
            {
                if (e._err != null) return;
                e._buf[0]++;
                e._buf[e._buf[0]] = c;
                if (e._buf[0] < 255) return;
                e.Write(e._buf.AsSpan(0, 256));
                e._buf[0] = 0;
            }

            public override void WriteByte(byte c) => WriteBlockByte(c);

            public override void Write(byte[] buffer, int offset, int count)
            {
                for (int i = 0; i < count; i++)
                    WriteBlockByte(buffer[offset + i]);
            }

            public void CloseBlocks()
            {
                if (e._buf[0] == 0)
                    e.WriteByte(0);
                else
                {
                    int n = e._buf[0];
                    e._buf[n + 1] = 0;
                    e.Write(e._buf.AsSpan(0, n + 2));
                }
                e.Flush();
            }

            public override bool CanWrite => true;
            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
        }
    }

    private static int Log2(int x)
    {
        if (x < 2) return 0;
        return BitOperations.Log2((uint)(x - 1));
    }
}
