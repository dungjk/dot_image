// Ported from Go golang.org/x/image/vp8/decode.go

using System;
using System.IO;
using DotImage;

namespace DotImage.Extended.Webp.Vp8;

// FrameHeader is a frame header, as specified in section 9.1.
public readonly struct FrameHeader
{
    public FrameHeader(bool keyFrame, byte versionNumber, bool showFrame, uint firstPartitionLen, int width, int height, byte xScale, byte yScale)
    {
        KeyFrame = keyFrame;
        VersionNumber = versionNumber;
        ShowFrame = showFrame;
        FirstPartitionLen = firstPartitionLen;
        Width = width;
        Height = height;
        XScale = xScale;
        YScale = yScale;
    }

    public bool KeyFrame { get; }
    public byte VersionNumber { get; }
    public bool ShowFrame { get; }
    public uint FirstPartitionLen { get; }
    public int Width { get; }
    public int Height { get; }
    public byte XScale { get; }
    public byte YScale { get; }
}

public sealed class Vp8FormatException : Exception
{
    public Vp8FormatException(string message) : base("invalid VP8 format: " + message) { }
}

public sealed class Vp8UnsupportedException : Exception
{
    public Vp8UnsupportedException(string message) : base("unsupported VP8 feature: " + message) { }
}

// LimitReader wraps a Stream to read at most n bytes from it.
internal sealed class LimitReader
{
    private readonly Stream _r;
    private int _n;

    public LimitReader(Stream r, int n)
    {
        _r = r;
        _n = n;
    }

    // N is how many bytes remain to be read.
    public int N => _n;

    // ReadFull reads exactly len(p) bytes into p.
    public void ReadFull(Span<byte> p)
    {
        if (p.Length > _n)
        {
            throw new EndOfStreamException();
        }
        int total = 0;
        while (total < p.Length)
        {
            int nr = _r.Read(p[total..]);
            if (nr <= 0)
            {
                throw new EndOfStreamException();
            }
            total += nr;
        }
        _n -= total;
    }
}

// SegmentHeader holds segment-related header information.
internal struct SegmentHeader
{
    internal bool UseSegment;
    internal bool UpdateMap;
    internal bool RelativeDelta;
    internal sbyte[] Quantizer;
    internal sbyte[] FilterStrength;
    internal byte[] Prob;

    public SegmentHeader()
    {
        Quantizer = new sbyte[Decoder.NSegment];
        FilterStrength = new sbyte[Decoder.NSegment];
        Prob = new byte[3] { 0xff, 0xff, 0xff };
    }
}

// FilterHeader holds filter-related header information.
internal struct FilterHeader
{
    internal bool Simple;
    internal sbyte Level;
    internal byte Sharpness;
    internal bool UseLFDelta;
    internal sbyte[] RefLFDelta;
    internal sbyte[] ModeLFDelta;
    internal sbyte[] PerSegmentLevel;

    public FilterHeader()
    {
        RefLFDelta = new sbyte[Decoder.NRefLFDelta];
        ModeLFDelta = new sbyte[Decoder.NModeLFDelta];
        PerSegmentLevel = new sbyte[Decoder.NSegment];
    }
}

// Mb is the per-macroblock decode state. A decoder maintains mbw+1 of these
// as it is decoding macroblocks left-to-right and top-to-bottom: mbw for the
// macroblocks in the row above, and one for the macroblock to the left.
internal sealed class Mb
{
    // Pred is the predictor mode for the 4 bottom or right 4x4 luma regions.
    internal readonly byte[] Pred = new byte[4];
    // NzMask is a mask of 8 bits: 4 for the bottom or right 4x4 luma regions,
    // and 2 + 2 for the bottom or right 4x4 chroma regions. A 1 bit indicates
    // that region has non-zero coefficients.
    internal byte NzMask;
    // NzY16 is a 0/1 value that is 1 if the macroblock used Y16 prediction and
    // had non-zero coefficients.
    internal byte NzY16;
}

// Decoder decodes VP8 bitstreams into frames. Decoding one frame consists of
// calling Init, DecodeFrameHeader and then DecodeFrame in that order.
// A Decoder can be re-used to decode multiple frames.
public sealed partial class Decoder
{
    internal const int NSegment = 4;
    internal const int NSegmentProb = 3;

    internal const int NRefLFDelta = 4;
    internal const int NModeLFDelta = 4;

    // The planes into which an image is divided. A separate plane is used for
    // the "24" y tokens, as those are only decoded when Y16 prediction is used.
    internal const int PlaneY1WithY2 = 0;
    internal const int PlaneY2 = 1;
    internal const int PlaneUv = 2;
    internal const int PlaneY1SansY2 = 3;

    internal const int NPlane = 4;
    internal const int NBand = 8;
    internal const int NContext = 3;
    internal const int NProb = 11;

    private const int NOP = 8;

    // Rr is the input bitsream.
    private LimitReader? _rr;
    // Scratch is a scratch buffer.
    private readonly byte[] _scratch = new byte[8];
    // Img is the YCbCr image to decode into.
    private YCbCrImage? _img;
    // Mbw and Mbh are the number of 16x16 macroblocks wide and high the image is.
    private int _mbw, _mbh;
    // FrameHeader is the frame header. When decoding multiple frames,
    // frames that aren't key frames will inherit the Width, Height,
    // XScale and YScale of the most recent key frame.
    private FrameHeader _frameHeader;
    // Other headers.
    private SegmentHeader _segmentHeader;
    private FilterHeader _filterHeader;
    // The image data is divided into a number of independent partitions.
    // There is 1 "first partition" and between 1 and 8 "other partitions"
    // for coefficient data.
    private readonly Partition _fp = new();
    private readonly Partition[] _op;
    private int _nOP;
    // Quantization factors.
    private readonly Quant[] _quant;
    // DCT/WHT coefficient decoding probabilities.
    private byte[][][][] _tokenProb = null!;
    private bool _useSkipProb;
    private byte _skipProb;
    // Loop filter parameters.
    private readonly FilterParam[,] _filterParams = new FilterParam[NSegment, 2];
    private FilterParam[] _perMBFilterParams = [];

    // The fields below relate to the current macroblock being decoded.
    //
    // Segment-based adjustments.
    private int _segment;
    // Per-macroblock state for the macroblock immediately left of and those
    // macroblocks immediately above the current macroblock.
    private Mb _leftMB = new();
    private Mb[] _upMB = [];
    // Bitmasks for which 4x4 regions of coeff contain non-zero coefficients.
    private uint _nzDCMask, _nzACMask;
    // Predictor modes.
    private bool _usePredY16; // The libwebp C code calls this !is_i4x4_.
    private byte _predY16;
    private byte _predC8;
    private readonly byte[,] _predY4 = new byte[4, 4];

    // The fields below form a workspace for reconstructing a macroblock.
    // Their specific sizes are documented in reconstruct.go.
    private readonly short[] _coeff = new short[1 * 16 * 16 + 2 * 8 * 8 + 1 * 4 * 4];
    private readonly byte[][] _ybr;

    public Decoder()
    {
        _segmentHeader = new SegmentHeader();
        _filterHeader = new FilterHeader();
        _quant = new Quant[NSegment];
        for (int i = 0; i < NSegment; i++)
        {
            _quant[i] = new Quant(new ushort[2], new ushort[2], new ushort[2]);
        }
        _op = new Partition[NOP];
        for (int i = 0; i < NOP; i++)
        {
            _op[i] = new Partition();
        }
        _ybr = new byte[26][];
        for (int i = 0; i < 26; i++)
        {
            _ybr[i] = new byte[32];
        }
    }

    internal static byte BoolToU8(bool b) => b ? (byte)1 : (byte)0;

    // Init initializes the decoder to read at most n bytes from r.
    public void Init(Stream r, int n)
    {
        _rr = new LimitReader(r, n);
    }

    // DecodeFrameHeader decodes the frame header.
    public FrameHeader DecodeFrameHeader()
    {
        if (_rr is null)
        {
            throw new InvalidOperationException("vp8: Init must be called before DecodeFrameHeader");
        }
        // All frame headers are at least 3 bytes long.
        _rr.ReadFull(_scratch.AsSpan(0, 3));
        bool keyFrame = (_scratch[0] & 1) == 0;
        byte versionNumber = (byte)((_scratch[0] >> 1) & 7);
        bool showFrame = ((_scratch[0] >> 4) & 1) == 1;
        uint firstPartitionLen = (uint)_scratch[0] >> 5 | (uint)_scratch[1] << 3 | (uint)_scratch[2] << 11;
        if (!keyFrame)
        {
            _frameHeader = new FrameHeader(keyFrame, versionNumber, showFrame, firstPartitionLen,
                _frameHeader.Width, _frameHeader.Height, _frameHeader.XScale, _frameHeader.YScale);
            return _frameHeader;
        }
        // Frame headers for key frames are an additional 7 bytes long.
        _rr.ReadFull(_scratch.AsSpan(0, 7));
        // Check the magic sync code.
        if (_scratch[0] != 0x9d || _scratch[1] != 0x01 || _scratch[2] != 0x2a)
        {
            throw new Vp8FormatException("invalid format");
        }
        int width = (_scratch[4] & 0x3f) << 8 | _scratch[3];
        int height = (_scratch[6] & 0x3f) << 8 | _scratch[5];
        byte xScale = (byte)(_scratch[4] >> 6);
        byte yScale = (byte)(_scratch[6] >> 6);
        _mbw = (width + 0x0f) >> 4;
        _mbh = (height + 0x0f) >> 4;
        _segmentHeader = new SegmentHeader();
        _tokenProb = DeepCopyTokenProb(DefaultTokenProb);
        _segment = 0;
        _frameHeader = new FrameHeader(keyFrame, versionNumber, showFrame, firstPartitionLen, width, height, xScale, yScale);
        return _frameHeader;
    }

    // EnsureImg ensures that d.img is large enough to hold the decoded frame.
    private void EnsureImg()
    {
        Guards.EnsureDecodeSize(_frameHeader.Width, _frameHeader.Height, 4);
        if (_img != null)
        {
            Point p0 = _img.Rect.Min;
            Point p1 = _img.Rect.Max;
            if (p0.X == 0 && p0.Y == 0 && p1.X >= 16 * _mbw && p1.Y >= 16 * _mbh)
            {
                return;
            }
        }
        var m = YCbCrImages.NewYCbCr(Geometry.Rect(0, 0, 16 * _mbw, 16 * _mbh), YCbCrSubsampleRatio.Ratio420);
        _img = (YCbCrImage)m.SubImage(Geometry.Rect(0, 0, _frameHeader.Width, _frameHeader.Height));
        _perMBFilterParams = new FilterParam[_mbw * _mbh];
        _upMB = new Mb[_mbw];
        for (int i = 0; i < _mbw; i++)
        {
            _upMB[i] = new Mb();
        }
    }

    // ParseSegmentHeader parses the segment header, as specified in section 9.3.
    private void ParseSegmentHeader()
    {
        _segmentHeader.UseSegment = _fp.ReadBit(Partition.UniformProb);
        if (!_segmentHeader.UseSegment)
        {
            _segmentHeader.UpdateMap = false;
            return;
        }
        _segmentHeader.UpdateMap = _fp.ReadBit(Partition.UniformProb);
        if (_fp.ReadBit(Partition.UniformProb))
        {
            _segmentHeader.RelativeDelta = !_fp.ReadBit(Partition.UniformProb);
            for (int i = 0; i < NSegment; i++)
            {
                _segmentHeader.Quantizer[i] = (sbyte)_fp.ReadOptionalInt(Partition.UniformProb, 7);
            }
            for (int i = 0; i < NSegment; i++)
            {
                _segmentHeader.FilterStrength[i] = (sbyte)_fp.ReadOptionalInt(Partition.UniformProb, 6);
            }
        }
        if (!_segmentHeader.UpdateMap)
        {
            return;
        }
        for (int i = 0; i < NSegmentProb; i++)
        {
            if (_fp.ReadBit(Partition.UniformProb))
            {
                _segmentHeader.Prob[i] = (byte)_fp.ReadUint(Partition.UniformProb, 8);
            }
            else
            {
                _segmentHeader.Prob[i] = 0xff;
            }
        }
    }

    // ParseFilterHeader parses the filter header, as specified in section 9.4.
    private void ParseFilterHeader()
    {
        _filterHeader.Simple = _fp.ReadBit(Partition.UniformProb);
        _filterHeader.Level = (sbyte)_fp.ReadUint(Partition.UniformProb, 6);
        _filterHeader.Sharpness = (byte)_fp.ReadUint(Partition.UniformProb, 3);
        _filterHeader.UseLFDelta = _fp.ReadBit(Partition.UniformProb);
        if (_filterHeader.UseLFDelta && _fp.ReadBit(Partition.UniformProb))
        {
            for (int i = 0; i < NRefLFDelta; i++)
            {
                _filterHeader.RefLFDelta[i] = (sbyte)_fp.ReadOptionalInt(Partition.UniformProb, 6);
            }
            for (int i = 0; i < NModeLFDelta; i++)
            {
                _filterHeader.ModeLFDelta[i] = (sbyte)_fp.ReadOptionalInt(Partition.UniformProb, 6);
            }
        }
        if (_filterHeader.Level == 0)
        {
            return;
        }
        if (_segmentHeader.UseSegment)
        {
            for (int i = 0; i < NSegment; i++)
            {
                int strength = _segmentHeader.FilterStrength[i];
                if (_segmentHeader.RelativeDelta)
                {
                    strength += _filterHeader.Level;
                }
                _filterHeader.PerSegmentLevel[i] = (sbyte)strength;
            }
        }
        else
        {
            _filterHeader.PerSegmentLevel[0] = _filterHeader.Level;
        }
        ComputeFilterParams();
    }

    // ParseOtherPartitions parses the other partitions, as specified in section 9.5.
    private void ParseOtherPartitions()
    {
        const int maxNOP = 1 << 3;
        var partLens = new int[maxNOP];
        _nOP = 1 << (int)_fp.ReadUint(Partition.UniformProb, 2);

        // The final partition length is implied by the remaining chunk data
        // (d.r.n) and the other d.nOP-1 partition lengths. Those d.nOP-1 partition
        // lengths are stored as 24-bit uints, i.e. up to 16 MiB per partition.
        int n = 3 * (_nOP - 1);
        partLens[_nOP - 1] = _rr!.N - n;
        if (partLens[_nOP - 1] < 0)
        {
            throw new EndOfStreamException();
        }
        if (n > 0)
        {
            var buf = new byte[n];
            _rr.ReadFull(buf);
            for (int i = 0; i < _nOP - 1; i++)
            {
                int pl = buf[3 * i + 0] | buf[3 * i + 1] << 8 | buf[3 * i + 2] << 16;
                if (pl > partLens[_nOP - 1])
                {
                    throw new EndOfStreamException();
                }
                partLens[i] = pl;
                partLens[_nOP - 1] -= pl;
            }
        }

        // We check if the final partition length can also fit into a 24-bit uint.
        // Strictly speaking, this isn't part of the spec, but it guards against a
        // malicious WEBP image that is too large to ReadFull the encoded DCT
        // coefficients into memory, whether that's because the actual WEBP file is
        // too large, or whether its RIFF metadata lists too large a chunk.
        if (1 << 24 <= partLens[_nOP - 1])
        {
            throw new Vp8FormatException("too much data to decode");
        }

        var buf2 = new byte[_rr.N];
        _rr.ReadFull(buf2);
        for (int i = 0, offset = 0; i < _nOP; i++)
        {
            int pl = partLens[i];
            _op[i].Init(buf2.AsSpan(offset, pl).ToArray());
            offset += pl;
        }
    }

    // ParseOtherHeaders parses header information other than the frame header.
    private void ParseOtherHeaders()
    {
        // Initialize and parse the first partition.
        var firstPartition = new byte[(int)_frameHeader.FirstPartitionLen];
        _rr!.ReadFull(firstPartition);
        _fp.Init(firstPartition);
        if (_frameHeader.KeyFrame)
        {
            // Read and ignore the color space and pixel clamp values. They are
            // specified in section 9.2, but are unimplemented.
            _fp.ReadBit(Partition.UniformProb);
            _fp.ReadBit(Partition.UniformProb);
        }
        ParseSegmentHeader();
        ParseFilterHeader();
        ParseOtherPartitions();
        ParseQuant();
        if (!_frameHeader.KeyFrame)
        {
            // Golden and AltRef frames are specified in section 9.7.
            // Note that they are only used for video, not still images.
            throw new Vp8UnsupportedException("Golden / AltRef frames are not implemented");
        }
        // Read and ignore the refreshLastFrameBuffer bit, specified in section 9.8.
        // It applies only to video, and not still images.
        _fp.ReadBit(Partition.UniformProb);
        ParseTokenProb();
        _useSkipProb = _fp.ReadBit(Partition.UniformProb);
        if (_useSkipProb)
        {
            _skipProb = (byte)_fp.ReadUint(Partition.UniformProb, 8);
        }
        if (_fp.UnexpectedEof)
        {
            throw new EndOfStreamException();
        }
    }

    // DecodeFrame decodes the frame and returns it as an YCbCr image.
    // The image's contents are valid up until the next call to Decoder.Init.
    public YCbCrImage DecodeFrame()
    {
        EnsureImg();
        ParseOtherHeaders();
        // Reconstruct the rows.
        for (int mbx = 0; mbx < _mbw; mbx++)
        {
            _upMB[mbx] = new Mb();
        }
        for (int mby = 0; mby < _mbh; mby++)
        {
            _leftMB = new Mb();
            for (int mbx = 0; mbx < _mbw; mbx++)
            {
                bool skip = Reconstruct(mbx, mby);
                FilterParam fs = _filterParams[_segment, BoolToU8(!_usePredY16)];
                fs.Inner = fs.Inner || !skip;
                _perMBFilterParams[_mbw * mby + mbx] = fs;
            }
        }
        if (_fp.UnexpectedEof)
        {
            throw new EndOfStreamException();
        }
        for (int i = 0; i < _nOP; i++)
        {
            if (_op[i].UnexpectedEof)
            {
                throw new EndOfStreamException();
            }
        }
        // Apply the loop filter.
        //
        // Even if we are using per-segment levels, section 15 says that "loop
        // filtering must be skipped entirely if loop_filter_level at either the
        // frame header level or macroblock override level is 0".
        if (_filterHeader.Level != 0)
        {
            if (_filterHeader.Simple)
            {
                SimpleFilter();
            }
            else
            {
                NormalFilter();
            }
        }
        return _img!;
    }
}