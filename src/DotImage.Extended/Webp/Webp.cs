// Ported from Go golang.org/x/image/webp/decode.go

using System;
using System.IO;
using DotImage;
using DotImage.Color;
using DotImage.Extended.Webp.Riff;
using DotImage.Extended.Webp.Vp8;
using DotImage.Extended.Webp.Vp8l;

namespace DotImage.Extended.Webp;

public sealed class WebpFormatException : Exception
{
    public WebpFormatException() : base("webp: invalid format") { }
}

/// <summary>Decodes WebP images.</summary>
public static class Decoder
{
    private static readonly FourCC FccAlpha = new('A', 'L', 'P', 'H');
    private static readonly FourCC FccVP8 = new('V', 'P', '8', ' ');
    private static readonly FourCC FccVP8L = new('V', 'P', '8', 'L');
    private static readonly FourCC FccVP8X = new('V', 'P', '8', 'X');
    private static readonly FourCC FccWebp = new('W', 'E', 'B', 'P');

    /// <summary>Decodes a WebP image from r.</summary>
    public static IImage Decode(Stream r) => DecodeImpl(r, false).Image!;

    /// <summary>Decodes a WebP image from a byte array.</summary>
    public static IImage Decode(byte[] data) => Decode(new MemoryStream(data));

    /// <summary>Returns the color model and dimensions of a WebP image without decoding the entire image.</summary>
    public static Config DecodeConfig(Stream r) => DecodeImpl(r, true).Config;

    private static (IImage? Image, Config Config) DecodeImpl(Stream r, bool configOnly)
    {
        var (formType, riffReader) = RiffReader.NewReader(r);
        if (formType != FccWebp)
            throw new WebpFormatException();

        byte[]? alpha = null;
        int alphaStride = 0;
        bool wantAlpha = false;
        bool seenVP8X = false;
        uint widthMinusOne = 0;
        uint heightMinusOne = 0;
        var buf = new byte[10];

        while (true)
        {
            var (chunkID, chunkLen, chunkData) = riffReader.Next();
            if (chunkData == null)
                throw new WebpFormatException();

            if (chunkID == FccAlpha)
            {
                if (!wantAlpha)
                    throw new WebpFormatException();
                wantAlpha = false;
                if (ReadFull(chunkData, buf, 0, 1) < 1)
                    throw new WebpFormatException();
                (alpha, alphaStride) = ReadAlpha(chunkData, widthMinusOne, heightMinusOne, (byte)(buf[0] & 0x03));
                UnfilterAlpha(alpha, alphaStride, (byte)((buf[0] >> 2) & 0x03));
            }
            else if (chunkID == FccVP8)
            {
                if (wantAlpha || chunkLen > int.MaxValue)
                    throw new WebpFormatException();
                var d = new Vp8.Decoder();
                d.Init(chunkData, (int)chunkLen);
                var fh = d.DecodeFrameHeader();
                if (seenVP8X && (fh.Width != (int)widthMinusOne + 1 || fh.Height != (int)heightMinusOne + 1))
                    throw new WebpFormatException();
                if (configOnly)
                {
                    return (null, new Config(ColorModels.YCbCr, fh.Width, fh.Height));
                }
                var m = d.DecodeFrame();
                if (alpha != null)
                {
                    var ama = new NYCbCrAImage
                    {
                        YCbCrImage = m,
                        A = new Memory<byte>(alpha),
                        AStride = alphaStride,
                    };
                    return (ama, default);
                }
                return (m, default);
            }
            else if (chunkID == FccVP8L)
            {
                if (alpha != null)
                    throw new WebpFormatException();
                if (configOnly)
                {
                    return (null, Vp8l.Decoder.DecodeConfig(chunkData));
                }
                Stream data = chunkData;
                if (seenVP8X)
                {
                    var vp8lHeader = new byte[5];
                    if (ReadFull(chunkData, vp8lHeader) < 5)
                        throw new WebpFormatException();
                    var hdrReader = new BitReader(new MemoryStream(vp8lHeader));
                    Vp8l.Decoder.DecodeHeader(hdrReader, out int vlw, out int vlh);
                    if (vlw != (int)widthMinusOne + 1 || vlh != (int)heightMinusOne + 1)
                        throw new WebpFormatException();
                    data = new MultiStream(new MemoryStream(vp8lHeader), chunkData);
                }
                var img = Vp8l.Decoder.Decode(data);
                return (img, default);
            }
            else if (chunkID == FccVP8X)
            {
                if (seenVP8X)
                    throw new WebpFormatException();
                seenVP8X = true;
                if (chunkLen != 10)
                    throw new WebpFormatException();
                if (ReadFull(chunkData, buf) < 10)
                    throw new WebpFormatException();
                const byte alphaBit = 1 << 4;
                wantAlpha = (buf[0] & alphaBit) != 0;
                widthMinusOne = (uint)buf[4] | (uint)buf[5] << 8 | (uint)buf[6] << 16;
                heightMinusOne = (uint)buf[7] | (uint)buf[8] << 8 | (uint)buf[9] << 16;
                ulong w = (ulong)widthMinusOne + 1;
                ulong h = (ulong)heightMinusOne + 1;
                if (w * h > int.MaxValue)
                    throw new WebpFormatException();
                if (configOnly)
                {
                    return (null, new Config(
                        wantAlpha ? ColorModels.NYCbCrA : ColorModels.YCbCr,
                        (int)widthMinusOne + 1,
                        (int)heightMinusOne + 1));
                }
            }
            else
            {
                // Unsupported chunk; skip its data.
            }
        }
    }

    private static (byte[] alpha, int alphaStride) ReadAlpha(Stream chunkData, uint widthMinusOne, uint heightMinusOne, byte compression)
    {
        switch (compression)
        {
            case 0:
            {
                int w = (int)widthMinusOne + 1;
                int h = (int)heightMinusOne + 1;
                Guards.EnsureDecodeSize(w, h, 1);
                var alpha = new byte[w * h];
                if (ReadFull(chunkData, alpha) < alpha.Length)
                    throw new EndOfStreamException();
                return (alpha, w);
            }
            case 1:
            {
                if (widthMinusOne > 0x3fff || heightMinusOne > 0x3fff)
                    throw new WebpFormatException();
                var header = new byte[]
                {
                    0x2f,
                    (byte)widthMinusOne,
                    (byte)(widthMinusOne >> 8 | heightMinusOne << 6),
                    (byte)(heightMinusOne >> 2),
                    (byte)(heightMinusOne >> 10),
                };
                var alphaImage = Vp8l.Decoder.Decode(new MultiStream(new MemoryStream(header), chunkData));
                var pix = alphaImage.Pix.Span;
                var alpha = new byte[pix.Length / 4];
                for (int i = 0; i < alpha.Length; i++)
                    alpha[i] = pix[4 * i + 1];
                return (alpha, (int)widthMinusOne + 1);
            }
            default:
                throw new WebpFormatException();
        }
    }

    private static void UnfilterAlpha(byte[] alpha, int alphaStride, byte filter)
    {
        if (alpha.Length == 0 || alphaStride == 0)
            return;
        switch (filter)
        {
            case 1: // Horizontal.
                for (int i = 1; i < alphaStride; i++)
                    alpha[i] += alpha[i - 1];
                for (int i = alphaStride; i < alpha.Length; i += alphaStride)
                {
                    alpha[i] += alpha[i - alphaStride];
                    for (int j = 1; j < alphaStride; j++)
                        alpha[i + j] += alpha[i + j - 1];
                }
                break;
            case 2: // Vertical.
                for (int i = 1; i < alphaStride; i++)
                    alpha[i] += alpha[i - 1];
                for (int i = alphaStride; i < alpha.Length; i++)
                    alpha[i] += alpha[i - alphaStride];
                break;
            case 3: // Gradient.
                for (int i = 1; i < alphaStride; i++)
                    alpha[i] += alpha[i - 1];
                for (int i = alphaStride; i < alpha.Length; i += alphaStride)
                {
                    alpha[i] += alpha[i - alphaStride];
                    for (int j = 1; j < alphaStride; j++)
                    {
                        int c = alpha[i + j - alphaStride - 1];
                        int b = alpha[i + j - alphaStride];
                        int a = alpha[i + j - 1];
                        int x = a + b - c;
                        x = x < 0 ? 0 : x > 255 ? 255 : x;
                        alpha[i + j] += (byte)x;
                    }
                }
                break;
        }
    }

    private static int ReadFull(Stream s, byte[] buf, int offset, int count)
    {
        int total = 0;
        while (total < count)
        {
            int n = s.Read(buf, offset + total, count - total);
            if (n == 0) break;
            total += n;
        }
        return total;
    }

    private static int ReadFull(Stream s, byte[] buf) => ReadFull(s, buf, 0, buf.Length);

    private sealed class MultiStream : Stream
    {
        private readonly Stream[] _streams;
        private int _index;

        public MultiStream(params Stream[] streams) => _streams = streams;

        public override int Read(byte[] buffer, int offset, int count)
        {
            int total = 0;
            while (_index < _streams.Length && total < count)
            {
                int n = _streams[_index].Read(buffer, offset + total, count - total);
                if (n == 0) _index++;
                else total += n;
            }
            return total;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
    }
}

/// <summary>Registers the WebP format with the <see cref="FormatRegistry"/>.</summary>
public static class WebpFormatRegistration
{
    private static int _registered;

    public static void Register()
    {
        if (Interlocked.CompareExchange(ref _registered, 1, 0) != 0)
            return;
        FormatRegistry.RegisterFormat(
            "webp",
            "RIFF????WEBPVP8",
            Decoder.Decode,
            Decoder.DecodeConfig);
    }
}