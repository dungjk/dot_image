// Ported from golang.org/x/image/tiff/reader.go.
// Copyright 2011 The Go Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license.

using System.Buffers.Binary;
using System.IO.Compression;
using DotImage;
using DotImage.Color;
using DotImage.Extended.Internal;
using DotImage.Extended.Tiff.Lzw;

namespace DotImage.Extended.Tiff;

/// <summary>
/// Package tiff implements a TIFF image decoder and encoder.
/// The TIFF specification is at http://partners.adobe.com/public/developer/en/tiff/TIFF6.pdf
/// </summary>
public static class Tiff
{
    private const int MaxChunkSize = 10 << 20; // 10M
    internal const int MaxBytesPerPixel = 8;

    /// <summary>Reads a TIFF image from r and returns it as an IImage. The type of IImage returned depends on the contents of the TIFF.</summary>
    public static IImage Decode(Stream r)
    {
        TiffDecoder d = new(r);

        bool blockPadding = false;
        int blockWidth = d.ConfigWidth;
        int blockHeight = d.ConfigHeight;
        int blocksAcross = 1;
        int blocksDown = 1;

        if (d.ConfigWidth == 0)
        {
            blocksAcross = 0;
        }
        if (d.ConfigHeight == 0)
        {
            blocksDown = 0;
        }

        uint[] blockOffsets = Array.Empty<uint>();
        uint[] blockCounts = Array.Empty<uint>();

        if (d.FirstVal(TiffTags.TileWidth) != 0)
        {
            blockPadding = true;

            blockWidth = d.FirstIntVal(TiffTags.TileWidth);
            blockHeight = d.FirstIntVal(TiffTags.TileLength);

            // The specification says that tile widths and lengths must be a multiple of 16.
            // We currently permit invalid sizes, but reject anything too small to limit the
            // amount of work a malicious input can force us to perform.
            if (blockWidth < 8 || blockHeight < 8)
            {
                throw TiffFormatError("tile size is too small");
            }
            // Same conservative assumption on bytes-per-pixel as for the image dimensions.
            if (!SafeMath.Mul3(blockWidth, blockHeight, MaxBytesPerPixel).Ok)
            {
                throw TiffFormatError("tile size is too large");
            }
            if (blockWidth - d.ConfigWidth > 16 || blockHeight - d.ConfigHeight > 16)
            {
                // Tiles may be padded to the nearest multiple of 16, but one of
                // the dimensions of the tile exceeds the image dimension by more
                // than padding would require.
                if (blockWidth > 1024 || blockHeight > 1024)
                {
                    throw TiffFormatError("tile size exceeds image size");
                }
            }
            if (blockWidth != 0)
            {
                blocksAcross = (int)((d.ConfigWidth + blockWidth - 1) / (uint)blockWidth);
            }
            if (blockHeight != 0)
            {
                blocksDown = (int)((d.ConfigHeight + blockHeight - 1) / (uint)blockHeight);
            }

            blockOffsets = d.ParseIfdOffsets(TiffTags.TileOffsets, blocksAcross * blocksDown);
            blockCounts = d.ParseIfdOffsets(TiffTags.TileByteCounts, blocksAcross * blocksDown);
        }
        else
        {
            uint v = d.FirstVal(TiffTags.RowsPerStrip);
            if (v > 0 && v < (uint)blockHeight)
            {
                blockHeight = (int)v;
            }

            if (blockHeight != 0)
            {
                blocksDown = (int)((d.ConfigHeight + blockHeight - 1) / (uint)blockHeight);
            }

            blockOffsets = d.ParseIfdOffsets(TiffTags.StripOffsets, blocksDown);
            blockCounts = d.ParseIfdOffsets(TiffTags.StripByteCounts, blocksDown);
        }

        // Check if we have the right number of strips/tiles, offsets and counts.
        int total = blocksAcross * blocksDown;
        if (blockOffsets.Length < total || blockCounts.Length < total)
        {
            throw TiffFormatError("inconsistent header");
        }

        Rect imgRect = Geometry.Rect(0, 0, d.ConfigWidth, d.ConfigHeight);
        IImage? img = null;
        switch (d.Mode)
        {
            case TiffImageMode.Gray:
            case TiffImageMode.GrayInvert:
                img = d.Bpp == 16 ? Images.NewGray16(imgRect) : Images.NewGray(imgRect);
                break;
            case TiffImageMode.Paletted:
                img = Images.NewPaletted(imgRect, new Palette(d.Palette));
                break;
            case TiffImageMode.Nrgba:
                img = d.Bpp == 16 ? Images.NewNrgba64(imgRect) : Images.NewNrgba(imgRect);
                break;
            case TiffImageMode.Rgb:
            case TiffImageMode.Rgba:
                img = d.Bpp == 16 ? Images.NewRgba64(imgRect) : Images.NewRgba(imgRect);
                break;
            default:
                throw new NotSupportedException("tiff: unsupported feature: color model");
        }

        if (blocksAcross == 0 || blocksDown == 0)
        {
            return img;
        }
        // Maximum data per pixel is 8 bytes (RGBA64).
        long blockMaxDataSize = (long)blockWidth * blockHeight * MaxBytesPerPixel;
        for (int i = 0; i < blocksAcross; i++)
        {
            int blkW = blockWidth;
            if (!blockPadding && i == blocksAcross - 1 && d.ConfigWidth % blockWidth != 0)
            {
                blkW = d.ConfigWidth % blockWidth;
            }
            for (int j = 0; j < blocksDown; j++)
            {
                int blkH = blockHeight;
                if (!blockPadding && j == blocksDown - 1 && d.ConfigHeight % blockHeight != 0)
                {
                    blkH = d.ConfigHeight % blockHeight;
                }
                long offset = blockOffsets[j * blocksAcross + i];
                long n = blockCounts[j * blocksAcross + i];
                switch (d.FirstVal(TiffTags.Compression))
                {
                    // According to the spec, Compression does not have a default value,
                    // but some tools interpret a missing Compression value as none, so we do
                    // the same.
                    case (uint)TiffCompressionTypes.None:
                    case 0:
                        if (n > blockMaxDataSize)
                        {
                            throw TiffFormatError("block data size too large");
                        }
                        if (d.R.CanSlice)
                        {
                            d.Buf = d.R.Slice(offset, n);
                        }
                        else
                        {
                            d.Buf = SafeReadAt(d.R, n, offset);
                        }
                        break;
                    case (uint)TiffCompressionTypes.G3:
                        // The Go source uses the (skipped) golang.org/x/image/ccitt
                        // package for Group 3 Fax decoding.
                        throw new NotSupportedException("tiff: unsupported feature: compression value 3");
                    case (uint)TiffCompressionTypes.G4:
                        // The Go source uses the (skipped) golang.org/x/image/ccitt
                        // package for Group 4 Fax decoding.
                        throw new NotSupportedException("tiff: unsupported feature: compression value 4");
                    case (uint)TiffCompressionTypes.Lzw:
                        {
                            Stream section = new TiffSectionStream(d.R, offset, n);
                            using LzwInputStream lzwStream = new(LzwDecoder.NewReader(section, LzwOrder.Msb, 8));
                            d.Buf = TiffCompressor.ReadBuf(lzwStream, d.Buf, blockMaxDataSize);
                            break;
                        }
                    case (uint)TiffCompressionTypes.Deflate:
                    case (uint)TiffCompressionTypes.DeflateOld:
                        {
                            Stream section = new TiffSectionStream(d.R, offset, n);
                            using var zlib = new ZLibStream(section, CompressionMode.Decompress);
                            d.Buf = TiffCompressor.ReadBuf(zlib, d.Buf, blockMaxDataSize);
                            break;
                        }
                    case (uint)TiffCompressionTypes.PackBits:
                        d.Buf = TiffCompressor.UnpackBits(new TiffSectionStream(d.R, offset, n), blockMaxDataSize);
                        break;
                    default:
                        throw new NotSupportedException($"tiff: unsupported feature: compression value {d.FirstVal(TiffTags.Compression)}");
                }

                int xmin = i * blockWidth;
                int ymin = j * blockHeight;
                int xmax = xmin + blkW;
                int ymax = ymin + blkH;
                d.Decode(img, xmin, ymin, xmax, ymax);
            }
        }
        return img;
    }

    /// <summary>Returns the color model and dimensions of a TIFF image without decoding the entire image.</summary>
    public static Config DecodeConfig(Stream r)
    {
        TiffDecoder d = new(r);
        return new Config(d.ConfigModel, d.ConfigWidth, d.ConfigHeight);
    }

    /// <summary>Registers the tiff format. Call this to mimic Go's package-level init() registration.</summary>
    public static class FormatRegistration
    {
        public static void Register()
        {
            DotImage.FormatRegistry.RegisterFormat(
                "tiff",
                "II*\x00",
                Decode,
                DecodeConfig);
            DotImage.FormatRegistry.RegisterFormat(
                "tiff",
                "MM\x00*",
                Decode,
                DecodeConfig);
        }
    }

    private static InvalidDataException TiffFormatError(string detail) =>
        new("tiff: invalid format: " + detail);

    private static NotSupportedException TiffUnsupportedError(string detail) =>
        new("tiff: unsupported feature: " + detail);

    // SafeReadAt is a verbatim copy of internal/saferio.ReadDataAt from the
    // standard library, which is used to read data from a reader using a length
    // provided by untrusted data, without allocating the entire slice ahead of
    // time if it is large (>maxChunkSize).
    internal static byte[] SafeReadAt(ITiffReaderAt r, long n, long off)
    {
        if (n < 0 || n > int.MaxValue)
        {
            // n is too large to fit in int, so we can't allocate
            // a buffer large enough. Treat this as a read failure.
            throw new EndOfStreamException();
        }

        if (n < MaxChunkSize)
        {
            byte[] buf = new byte[n];
            r.ReadAt(buf, off);
            return buf;
        }

        byte[] result = new byte[n];
        byte[] buf1 = new byte[MaxChunkSize];
        long remaining = n;
        long offset = off;
        int dst = 0;
        while (remaining > 0)
        {
            int next = (int)System.Math.Min(remaining, MaxChunkSize);
            r.ReadAt(buf1.AsSpan(0, next), offset);
            Array.Copy(buf1, 0, result, dst, next);
            dst += next;
            remaining -= next;
            offset += next;
        }
        return result;
    }
}

/// <summary>
/// A minimal byte-order helper, mirroring Go's encoding/binary.ByteOrder.
/// </summary>
internal readonly struct TiffEndian
{
    public static readonly TiffEndian LittleEndian = new(true);
    public static readonly TiffEndian BigEndian = new(false);

    private readonly bool _little;

    private TiffEndian(bool little)
    {
        _little = little;
    }

    public ushort Uint16(ReadOnlySpan<byte> b) =>
        _little ? BinaryPrimitives.ReadUInt16LittleEndian(b) : BinaryPrimitives.ReadUInt16BigEndian(b);

    public uint Uint32(ReadOnlySpan<byte> b) =>
        _little ? BinaryPrimitives.ReadUInt32LittleEndian(b) : BinaryPrimitives.ReadUInt32BigEndian(b);

    public void PutUint16(Span<byte> b, ushort v)
    {
        if (_little) BinaryPrimitives.WriteUInt16LittleEndian(b, v);
        else BinaryPrimitives.WriteUInt16BigEndian(b, v);
    }

    public void PutUint32(Span<byte> b, uint v)
    {
        if (_little) BinaryPrimitives.WriteUInt32LittleEndian(b, v);
        else BinaryPrimitives.WriteUInt32BigEndian(b, v);
    }
}

internal sealed class TiffDecoder
{
    public ITiffReaderAt R;
    public TiffEndian ByteOrder;
    public int ConfigWidth;
    public int ConfigHeight;
    public IColorModel ConfigModel = ColorModels.Rgba;
    public TiffImageMode Mode;
    public uint Bpp;
    public readonly Dictionary<int, uint[]> Features = new();
    public readonly Dictionary<int, byte[]> Ifd = new();
    public IColor[] Palette = Array.Empty<IColor>();

    public byte[] Buf = Array.Empty<byte>();
    public int Off;      // Current offset in Buf.
    public uint V;       // Buffer value for reading with arbitrary bit depths.
    public uint Nbits;   // Remaining number of bits in V.

    public TiffDecoder(Stream r)
    {
        R = TiffReaderAtFactory.NewReaderAt(r);
        byte[] p = new byte[8];
        R.ReadAt(p, 0);

        if (p[0] == TiffConsts.LeHeader0 && p[1] == TiffConsts.LeHeader1 &&
            p[2] == TiffConsts.LeHeader2 && p[3] == TiffConsts.LeHeader3)
        {
            ByteOrder = TiffEndian.LittleEndian;
        }
        else if (p[0] == TiffConsts.BeHeader0 && p[1] == TiffConsts.BeHeader1 &&
                 p[2] == TiffConsts.BeHeader2 && p[3] == TiffConsts.BeHeader3)
        {
            ByteOrder = TiffEndian.BigEndian;
        }
        else
        {
            throw new InvalidDataException("tiff: invalid format: malformed header");
        }

        long ifdOffset = ByteOrder.Uint32(p.AsSpan(4, 4));

        // The first two bytes contain the number of entries (12 bytes each).
        byte[] countBuf = new byte[2];
        R.ReadAt(countBuf, ifdOffset);
        int numItems = ByteOrder.Uint16(countBuf);

        // All IFD entries are read in one chunk.
        byte[] ifdData = Tiff.SafeReadAt(R, TiffConsts.IfdLen * (long)numItems, ifdOffset + 2);

        int prevTag = -1;
        for (int i = 0; i < ifdData.Length; i += TiffConsts.IfdLen)
        {
            int tag = ParseIfd(ifdData.AsSpan(i, TiffConsts.IfdLen));
            if (tag <= prevTag)
            {
                throw new InvalidDataException("tiff: invalid format: tags are not sorted in ascending order");
            }
            prevTag = tag;
        }

        ConfigWidth = FirstIntVal(TiffTags.ImageWidth);
        ConfigHeight = FirstIntVal(TiffTags.ImageLength);
        if (ConfigWidth == 0 || ConfigHeight == 0)
        {
            throw new InvalidDataException("tiff: invalid format: zero-size image");
        }
        // Check that the image fits in memory.
        // This conservatively assumes 8 bytes per pixel,
        // rather than using the actual pixel size.
        if (!SafeMath.Mul3(ConfigWidth, ConfigHeight, Tiff.MaxBytesPerPixel).Ok)
        {
            throw new InvalidDataException("tiff: invalid format: image too large");
        }

        if (!Features.ContainsKey(TiffTags.BitsPerSample))
        {
            // Default is 1 per specification.
            Features[TiffTags.BitsPerSample] = new uint[] { 1 };
        }
        Bpp = FirstVal(TiffTags.BitsPerSample);
        switch (Bpp)
        {
            case 0:
                throw new InvalidDataException("tiff: invalid format: BitsPerSample must not be 0");
            case 1:
            case 8:
            case 16:
                // Nothing to do, these are accepted by this implementation.
                break;
            default:
                throw new NotSupportedException($"tiff: unsupported feature: BitsPerSample of {Bpp}");
        }

        // Determine the image mode.
        switch (FirstVal(TiffTags.PhotometricInterpretation))
        {
            case (uint)TiffPhotometricInterpretations.Rgb:
                if (Bpp == 16)
                {
                    foreach (uint b in Features[TiffTags.BitsPerSample])
                    {
                        if (b != 16)
                        {
                            throw new InvalidDataException("tiff: invalid format: wrong number of samples for 16bit RGB");
                        }
                    }
                }
                else
                {
                    foreach (uint b in Features[TiffTags.BitsPerSample])
                    {
                        if (b != 8)
                        {
                            throw new InvalidDataException("tiff: invalid format: wrong number of samples for 8bit RGB");
                        }
                    }
                }
                // RGB images normally have 3 samples per pixel.
                // If there are more, ExtraSamples (p. 31-32 of the spec)
                // gives their meaning (usually an alpha channel).
                switch (Features[TiffTags.BitsPerSample].Length)
                {
                    case 3:
                        Mode = TiffImageMode.Rgb;
                        ConfigModel = Bpp == 16 ? ColorModels.Rgba64 : ColorModels.Rgba;
                        break;
                    case 4:
                        switch (FirstVal(TiffTags.ExtraSamples))
                        {
                            case 1:
                                Mode = TiffImageMode.Rgba;
                                ConfigModel = Bpp == 16 ? ColorModels.Rgba64 : ColorModels.Rgba;
                                break;
                            case 2:
                                Mode = TiffImageMode.Nrgba;
                                ConfigModel = Bpp == 16 ? ColorModels.Nrgba64 : ColorModels.Nrgba;
                                break;
                            default:
                                throw new InvalidDataException("tiff: invalid format: wrong number of samples for RGB");
                        }
                        break;
                    default:
                        throw new InvalidDataException("tiff: invalid format: wrong number of samples for RGB");
                }
                break;
            case (uint)TiffPhotometricInterpretations.Paletted:
                Mode = TiffImageMode.Paletted;
                ConfigModel = new Palette(Palette);
                break;
            case (uint)TiffPhotometricInterpretations.WhiteIsZero:
                Mode = TiffImageMode.GrayInvert;
                ConfigModel = Bpp == 16 ? ColorModels.Gray16 : ColorModels.Gray;
                break;
            case (uint)TiffPhotometricInterpretations.BlackIsZero:
                Mode = TiffImageMode.Gray;
                ConfigModel = Bpp == 16 ? ColorModels.Gray16 : ColorModels.Gray;
                break;
            default:
                throw new NotSupportedException("tiff: unsupported feature: color model");
        }
        if (FirstVal(TiffTags.PhotometricInterpretation) != (uint)TiffPhotometricInterpretations.Rgb)
        {
            if (Features[TiffTags.BitsPerSample].Length != 1)
            {
                throw new NotSupportedException("tiff: unsupported feature: extra samples");
            }
        }
    }

    // FirstVal returns the first uint of the features entry with the given tag,
    // or 0 if the tag does not exist.
    public uint FirstVal(int tag)
    {
        if (!Features.TryGetValue(tag, out uint[]? f) || f.Length == 0)
        {
            return 0;
        }
        return f[0];
    }

    // FirstIntVal returns the first int of the features entry with the given tag,
    // or 0 if the tag does not exist.
    // If the tag overflows an int, it returns an error.
    public int FirstIntVal(int tag)
    {
        uint v = FirstVal(tag);
        if (v > int.MaxValue)
        {
            throw new InvalidDataException("tiff: invalid format: IFD value too large");
        }
        return (int)v;
    }

    // IfdUint decodes the IFD entry in p, which must be of the Byte, Short
    // or Long type, and returns the decoded uint values.
    //
    // maxCount limits the number of values.
    // If the entry contains more than maxCount values, only the first maxCount are parsed.
    public uint[] IfdUint(ReadOnlySpan<byte> p, int maxCount)
    {
        if (p.Length < TiffConsts.IfdLen)
        {
            throw new InvalidDataException("tiff: invalid format: bad IFD entry");
        }

        ushort datatype = ByteOrder.Uint16(p.Slice(2, 2));
        int dt = datatype;
        if (dt <= 0 || dt >= TiffDataTypes.Lengths.Length)
        {
            throw new NotSupportedException("tiff: unsupported feature: IFD entry datatype");
        }

        uint count = ByteOrder.Uint32(p.Slice(4, 4));
        if (count > (uint)int.MaxValue / TiffDataTypes.Lengths[dt])
        {
            throw new InvalidDataException("tiff: invalid format: IFD data too large");
        }

        int truncatedCount = (int)System.Math.Min(count, (uint)maxCount);
        uint datalen = TiffDataTypes.Lengths[dt] * count;
        byte[] raw;
        if (datalen > 4)
        {
            // The IFD contains a pointer to the real value.
            long truncatedLen = (long)TiffDataTypes.Lengths[dt] * truncatedCount;
            long ptr = ByteOrder.Uint32(p.Slice(8, 4));
            raw = Tiff.SafeReadAt(R, truncatedLen, ptr);
        }
        else
        {
            raw = p.Slice(8, (int)datalen).ToArray();
        }

        uint[] u = new uint[truncatedCount];
        switch (dt)
        {
            case TiffDataTypes.Byte:
                for (int i = 0; i < u.Length; i++)
                {
                    u[i] = raw[i];
                }
                break;
            case TiffDataTypes.Short:
                for (int i = 0; i < u.Length; i++)
                {
                    u[i] = ByteOrder.Uint16(raw.AsSpan(2 * i, 2));
                }
                break;
            case TiffDataTypes.Long:
                for (int i = 0; i < u.Length; i++)
                {
                    u[i] = ByteOrder.Uint32(raw.AsSpan(4 * i, 4));
                }
                break;
            default:
                throw new NotSupportedException("tiff: unsupported feature: data type");
        }
        return u;
    }

    // ParseIfdOffsets parses an IFD entry stored in Ifd using IfdUint.
    // For IFD entries which can be large, we delay reading and parsing the entry
    // until we know the image size, which lets us set reasonable bounds on the IFD entry.
    public uint[] ParseIfdOffsets(int tag, int maxCount)
    {
        if (!Ifd.TryGetValue(tag, out byte[]? p))
        {
            return Array.Empty<uint>();
        }
        return IfdUint(p, maxCount);
    }

    // ParseIfd decides whether the IFD entry in p is "interesting" and
    // stows away the data in the decoder. It returns the tag number of the
    // entry.
    private int ParseIfd(ReadOnlySpan<byte> p)
    {
        // smallEntryMaxCount is the limit to use for parsed IFD entries that
        // don't scale with the image size.
        const int smallEntryMaxCount = 16;

        ushort tagValue = ByteOrder.Uint16(p.Slice(0, 2));
        int tag = tagValue;
        switch (tag)
        {
            case TiffTags.BitsPerSample:
            case TiffTags.ExtraSamples:
            case TiffTags.PhotometricInterpretation:
            case TiffTags.Compression:
            case TiffTags.Predictor:
            case TiffTags.RowsPerStrip:
            case TiffTags.TileWidth:
            case TiffTags.TileLength:
            case TiffTags.ImageLength:
            case TiffTags.ImageWidth:
            case TiffTags.FillOrder:
            case TiffTags.T4Options:
            case TiffTags.T6Options:
                Features[tag] = IfdUint(p, smallEntryMaxCount);
                break;
            case TiffTags.StripOffsets:
            case TiffTags.StripByteCounts:
            case TiffTags.TileOffsets:
            case TiffTags.TileByteCounts:
                // These keys may contain many values.
                // Stash the IFD entry for later parsing.
                Ifd[tag] = p.ToArray();
                break;
            case TiffTags.ColorMap:
                {
                    const int maxColors = 256;
                    uint[] val = IfdUint(p, (3 * maxColors) + 1);
                    int numcolors = val.Length / 3;
                    if (val.Length % 3 != 0 || numcolors <= 0 || numcolors > maxColors)
                    {
                        throw new InvalidDataException("tiff: invalid format: bad ColorMap length");
                    }
                    Palette = new IColor[numcolors];
                    for (int i = 0; i < numcolors; i++)
                    {
                        Palette[i] = new Rgba64(
                            (ushort)val[i],
                            (ushort)val[i + numcolors],
                            (ushort)val[i + 2 * numcolors],
                            0xffff);
                    }
                    break;
                }
            case TiffTags.SampleFormat:
                {
                    // Page 27 of the spec: If the SampleFormat is present and
                    // the value is not 1 [= unsigned integer data], a Baseline
                    // TIFF reader that cannot handle the SampleFormat value
                    // must terminate the import process gracefully.
                    uint[] val = IfdUint(p, smallEntryMaxCount);
                    foreach (uint v in val)
                    {
                        if (v != 1)
                        {
                            throw new NotSupportedException("tiff: unsupported feature: sample format");
                        }
                    }
                    break;
                }
        }
        return tag;
    }

    // ReadBits reads n bits from the internal buffer starting at the current offset.
    public uint ReadBits(uint n)
    {
        while (Nbits < n)
        {
            V <<= 8;
            if (Off >= Buf.Length)
            {
                throw new InvalidDataException("tiff: invalid format: not enough pixel data");
            }
            V |= Buf[Off];
            Off++;
            Nbits += 8;
        }
        Nbits -= n;
        uint rv = V >> (int)Nbits;
        V &= unchecked((uint)~(rv << (int)Nbits));
        return rv;
    }

    // FlushBits discards the unread bits in the buffer used by ReadBits.
    // It is used at the end of a line.
    public void FlushBits()
    {
        V = 0;
        Nbits = 0;
    }

    // Decode decodes the raw data of an image.
    // It reads from Buf and writes the strip or tile into dst.
    public void Decode(IImage dst, int xmin, int ymin, int xmax, int ymax)
    {
        Off = 0;

        // Apply horizontal predictor if necessary.
        // In this case, p contains the color difference to the preceding pixel.
        // See page 64-65 of the spec.
        if (FirstVal(TiffTags.Predictor) == (uint)TiffPredictors.Horizontal)
        {
            switch (Bpp)
            {
                case 16:
                    {
                        int off = 0;
                        int n = 2 * Features[TiffTags.BitsPerSample].Length; // bytes per sample times samples per pixel
                        for (int y = ymin; y < ymax; y++)
                        {
                            off += n;
                            for (int x = 0; x < (xmax - xmin - 1) * n; x += 2)
                            {
                                if (off + 2 > Buf.Length)
                                {
                                    throw new InvalidDataException("tiff: invalid format: not enough pixel data");
                                }
                                ushort v0 = ByteOrder.Uint16(Buf.AsSpan(off - n, 2));
                                ushort v1 = ByteOrder.Uint16(Buf.AsSpan(off, 2));
                                ByteOrder.PutUint16(Buf.AsSpan(off, 2), (ushort)(v1 + v0));
                                off += 2;
                            }
                        }
                        break;
                    }
                case 8:
                    {
                        int off = 0;
                        int n = 1 * Features[TiffTags.BitsPerSample].Length; // bytes per sample times samples per pixel
                        for (int y = ymin; y < ymax; y++)
                        {
                            off += n;
                            for (int x = 0; x < (xmax - xmin - 1) * n; x++)
                            {
                                if (off >= Buf.Length)
                                {
                                    throw new InvalidDataException("tiff: invalid format: not enough pixel data");
                                }
                                Buf[off] = (byte)(Buf[off] + Buf[off - n]);
                                off++;
                            }
                        }
                        break;
                    }
                case 1:
                    throw new NotSupportedException("tiff: unsupported feature: horizontal predictor with 1 BitsPerSample");
            }
        }

        // CalcRowBytes returns the number of bytes in a row with numSamples
        // samples per pixel and bitsPerSample bytes per sample.
        static int CalcRowBytes(int xmin, int xmax, int numSamples, uint bitsPerSample) =>
            (int)(((long)(xmax - xmin) * numSamples * bitsPerSample + 7) / 8);
        // CalcRowOff returns the offset of the next row.
        static int CalcRowOff(int y, int ymin, int rowBytes) => (y - ymin) * rowBytes;

        int rMaxX = System.Math.Min(xmax, dst.Bounds().Max.X);
        int rMaxY = System.Math.Min(ymax, dst.Bounds().Max.Y);
        switch (Mode)
        {
            case TiffImageMode.Gray:
            case TiffImageMode.GrayInvert:
                {
                    int rowBytes = CalcRowBytes(xmin, xmax, 1, Bpp);
                    if (Bpp == 16)
                    {
                        Gray16Image gray16 = (Gray16Image)dst;
                        for (int y = ymin; y < rMaxY; y++)
                        {
                            Off = CalcRowOff(y, ymin, rowBytes);
                            for (int x = xmin; x < rMaxX; x++)
                            {
                                if (Off + 2 > Buf.Length)
                                {
                                    throw new InvalidDataException("tiff: invalid format: not enough pixel data");
                                }
                                ushort v = ByteOrder.Uint16(Buf.AsSpan(Off, 2));
                                Off += 2;
                                if (Mode == TiffImageMode.GrayInvert)
                                {
                                    v = (ushort)(0xffff - v);
                                }
                                gray16.SetGray16(x, y, new Gray16(v));
                            }
                        }
                    }
                    else
                    {
                        GrayImage gray = (GrayImage)dst;
                        uint max = (1u << (int)Bpp) - 1;
                        for (int y = ymin; y < rMaxY; y++)
                        {
                            Off = CalcRowOff(y, ymin, rowBytes);
                            FlushBits();
                            for (int x = xmin; x < rMaxX; x++)
                            {
                                uint v = ReadBits(Bpp);
                                v = v * 0xff / max;
                                if (Mode == TiffImageMode.GrayInvert)
                                {
                                    v = 0xff - v;
                                }
                                gray.SetGray(x, y, new Gray((byte)v));
                            }
                        }
                    }
                    break;
                }
            case TiffImageMode.Paletted:
                {
                    PalettedImage paletted = (PalettedImage)dst;
                    int pLen = Palette.Length;
                    int rowBytes = CalcRowBytes(xmin, xmax, 1, Bpp);
                    for (int y = ymin; y < rMaxY; y++)
                    {
                        Off = CalcRowOff(y, ymin, rowBytes);
                        FlushBits();
                        for (int x = xmin; x < rMaxX; x++)
                        {
                            uint v = ReadBits(Bpp);
                            byte idx = (byte)v;
                            if (idx >= pLen)
                            {
                                throw new InvalidDataException("tiff: invalid format: invalid color index");
                            }
                            paletted.SetColorIndex(x, y, idx);
                        }
                    }
                    break;
                }
            case TiffImageMode.Rgb:
                {
                    int rowBytes = CalcRowBytes(xmin, xmax, 3, Bpp);
                    if (Bpp == 16)
                    {
                        Rgba64Image rgba64 = (Rgba64Image)dst;
                        for (int y = ymin; y < rMaxY; y++)
                        {
                            Off = CalcRowOff(y, ymin, rowBytes);
                            for (int x = xmin; x < rMaxX; x++)
                            {
                                if (Off + 6 > Buf.Length)
                                {
                                    throw new InvalidDataException("tiff: invalid format: not enough pixel data");
                                }
                                ushort rr = ByteOrder.Uint16(Buf.AsSpan(Off + 0, 2));
                                ushort g = ByteOrder.Uint16(Buf.AsSpan(Off + 2, 2));
                                ushort b = ByteOrder.Uint16(Buf.AsSpan(Off + 4, 2));
                                Off += 6;
                                rgba64.SetRgba64(x, y, new Rgba64(rr, g, b, 0xffff));
                            }
                        }
                    }
                    else
                    {
                        RgbaImage rgba = (RgbaImage)dst;
                        for (int y = ymin; y < rMaxY; y++)
                        {
                            Off = CalcRowOff(y, ymin, rowBytes);
                            int min = rgba.PixOffset(xmin, y);
                            int maxI = rgba.PixOffset(rMaxX, y);
                            int off = (y - ymin) * (xmax - xmin) * 3;
                            for (int i = min; i < maxI; i += 4)
                            {
                                if (off + 3 > Buf.Length)
                                {
                                    throw new InvalidDataException("tiff: invalid format: not enough pixel data");
                                }
                                rgba.Pix.Span[i + 0] = Buf[off + 0];
                                rgba.Pix.Span[i + 1] = Buf[off + 1];
                                rgba.Pix.Span[i + 2] = Buf[off + 2];
                                rgba.Pix.Span[i + 3] = 0xff;
                                off += 3;
                            }
                        }
                    }
                    break;
                }
            case TiffImageMode.Nrgba:
                {
                    int rowBytes = CalcRowBytes(xmin, xmax, 4, Bpp);
                    if (Bpp == 16)
                    {
                        Nrgba64Image nrgba64 = (Nrgba64Image)dst;
                        for (int y = ymin; y < rMaxY; y++)
                        {
                            Off = CalcRowOff(y, ymin, rowBytes);
                            for (int x = xmin; x < rMaxX; x++)
                            {
                                if (Off + 8 > Buf.Length)
                                {
                                    throw new InvalidDataException("tiff: invalid format: not enough pixel data");
                                }
                                ushort rr = ByteOrder.Uint16(Buf.AsSpan(Off + 0, 2));
                                ushort g = ByteOrder.Uint16(Buf.AsSpan(Off + 2, 2));
                                ushort b = ByteOrder.Uint16(Buf.AsSpan(Off + 4, 2));
                                ushort a = ByteOrder.Uint16(Buf.AsSpan(Off + 6, 2));
                                Off += 8;
                                nrgba64.SetNrgba64(x, y, new Nrgba64(rr, g, b, a));
                            }
                        }
                    }
                    else
                    {
                        NrgbaImage nrgba = (NrgbaImage)dst;
                        for (int y = ymin; y < rMaxY; y++)
                        {
                            int min = nrgba.PixOffset(xmin, y);
                            int maxI = nrgba.PixOffset(rMaxX, y);
                            int i0 = (y - ymin) * (xmax - xmin) * 4;
                            int i1 = (y - ymin + 1) * (xmax - xmin) * 4;
                            if (i1 > Buf.Length)
                            {
                                throw new InvalidDataException("tiff: invalid format: not enough pixel data");
                            }
                            int n = System.Math.Min(i1 - i0, maxI - min);
                            Buf.AsSpan(i0, n).CopyTo(nrgba.Pix.Span.Slice(min, n));
                        }
                    }
                    break;
                }
            case TiffImageMode.Rgba:
                {
                    int rowBytes = CalcRowBytes(xmin, xmax, 4, Bpp);
                    if (Bpp == 16)
                    {
                        Rgba64Image rgba64 = (Rgba64Image)dst;
                        for (int y = ymin; y < rMaxY; y++)
                        {
                            Off = CalcRowOff(y, ymin, rowBytes);
                            for (int x = xmin; x < rMaxX; x++)
                            {
                                if (Off + 8 > Buf.Length)
                                {
                                    throw new InvalidDataException("tiff: invalid format: not enough pixel data");
                                }
                                ushort rr = ByteOrder.Uint16(Buf.AsSpan(Off + 0, 2));
                                ushort g = ByteOrder.Uint16(Buf.AsSpan(Off + 2, 2));
                                ushort b = ByteOrder.Uint16(Buf.AsSpan(Off + 4, 2));
                                ushort a = ByteOrder.Uint16(Buf.AsSpan(Off + 6, 2));
                                Off += 8;
                                rgba64.SetRgba64(x, y, new Rgba64(rr, g, b, a));
                            }
                        }
                    }
                    else
                    {
                        RgbaImage rgba = (RgbaImage)dst;
                        for (int y = ymin; y < rMaxY; y++)
                        {
                            int min = rgba.PixOffset(xmin, y);
                            int maxI = rgba.PixOffset(rMaxX, y);
                            int i0 = (y - ymin) * (xmax - xmin) * 4;
                            int i1 = (y - ymin + 1) * (xmax - xmin) * 4;
                            if (i1 > Buf.Length)
                            {
                                throw new InvalidDataException("tiff: invalid format: not enough pixel data");
                            }
                            int n = System.Math.Min(i1 - i0, maxI - min);
                            Buf.AsSpan(i0, n).CopyTo(rgba.Pix.Span.Slice(min, n));
                        }
                    }
                    break;
                }
        }
    }
}

/// <summary>
/// Stream adapter over an <see cref="LzwDecoder"/>.
/// </summary>
internal sealed class LzwInputStream : Stream
{
    private readonly LzwDecoder _decoder;

    public LzwInputStream(LzwDecoder decoder)
    {
        _decoder = decoder;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override int Read(Span<byte> buffer) => _decoder.Read(buffer);

    public override int Read(byte[] buffer, int offset, int count) =>
        Read(buffer.AsSpan(offset, count));

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}