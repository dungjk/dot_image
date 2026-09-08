// Ported from Go src/image/jpeg/reader.go


using DotImage.Color;
using DotImage.Util;

namespace DotImage.Jpeg;

public static class JpegReader
{
    public static IImage Decode(Stream r) => new Decoder().Decode(r, configOnly: false)!;

    public static Config DecodeConfig(Stream r)
    {
        var d = new Decoder();
        d.Decode(r, configOnly: true);
        return d.GetConfig();
    }
}

internal sealed partial class Decoder
{
    private Stream _r = Stream.Null;
    private Bits _bits;
    private readonly BytesBuffer _bytes = new();
    private int _width, _height;
    private GrayImage? _img1;
    private YCbCrImage? _img3;
    private byte[]? _blackPix;
    private int _blackStride;
    private bool _flex;
    private int _maxH, _maxV;
    private int _ri;
    private int _nComp;
    private bool _baseline;
    private bool _progressive;
    private bool _jfif;
    private bool _adobeTransformValid;
    private byte _adobeTransform;
    private ushort _eobRun;
    private readonly Component[] _comp = new Component[JpegConstants.MaxComponents];
    private readonly Block[][] _progCoeffs = new Block[JpegConstants.MaxComponents][];
    private readonly HuffmanTable[][] _huff =
    [
        [new(), new(), new(), new()],
        [new(), new(), new(), new()],
    ];
    private readonly Block[] _quant = new Block[JpegConstants.MaxTq + 1];
    private readonly byte[] _tmp = new byte[2 * JpegConstants.BlockSize];

    public Decoder()
    {
        for (int i = 0; i <= JpegConstants.MaxTq; i++)
            _quant[i] = new Block();
    }

    private sealed class BytesBuffer
    {
        public readonly byte[] Buf = new byte[4096];
        public int I, J;
        public int NUnreadable;
    }

    private void Fill()
    {
        if (_bytes.I != _bytes.J)
            throw new InvalidOperationException("jpeg: fill called when unread bytes exist");
        if (_bytes.J > 2)
        {
            _bytes.Buf[0] = _bytes.Buf[_bytes.J - 2];
            _bytes.Buf[1] = _bytes.Buf[_bytes.J - 1];
            _bytes.I = 2;
            _bytes.J = 2;
        }
        int n = _r.Read(_bytes.Buf, _bytes.J, _bytes.Buf.Length - _bytes.J);
        _bytes.J += n;
        if (n > 0) return;
        throw new EndOfStreamException();
    }

    private void UnreadByteStuffedByte()
    {
        _bytes.I -= _bytes.NUnreadable;
        _bytes.NUnreadable = 0;
        if (_bits.N >= 8)
        {
            _bits.A >>= 8;
            _bits.N -= 8;
            _bits.M >>= 8;
        }
    }

    private byte ReadByte()
    {
        while (_bytes.I == _bytes.J)
            Fill();
        byte x = _bytes.Buf[_bytes.I];
        _bytes.I++;
        _bytes.NUnreadable = 0;
        return x;
    }

    private byte ReadByteStuffedByte()
    {
        if (_bytes.I + 2 <= _bytes.J)
        {
            byte x = _bytes.Buf[_bytes.I];
            _bytes.I++;
            _bytes.NUnreadable = 1;
            if (x != 0xff)
                return x;
            if (_bytes.Buf[_bytes.I] != 0x00)
                throw new JpegFormatException(JpegConstants.ErrMissingFF00Msg);
            _bytes.I++;
            _bytes.NUnreadable = 2;
            return 0xff;
        }

        _bytes.NUnreadable = 0;
        byte b = ReadByte();
        _bytes.NUnreadable = 1;
        if (b != 0xff)
            return b;
        b = ReadByte();
        _bytes.NUnreadable = 2;
        if (b != 0x00)
            throw new JpegFormatException(JpegConstants.ErrMissingFF00Msg);
        return 0xff;
    }

    private void ReadFull(Span<byte> p)
    {
        if (_bytes.NUnreadable != 0)
        {
            if (_bits.N >= 8)
                UnreadByteStuffedByte();
            _bytes.NUnreadable = 0;
        }

        while (true)
        {
            int n = Math.Min(p.Length, _bytes.J - _bytes.I);
            _bytes.Buf.AsSpan(_bytes.I, n).CopyTo(p);
            p = p[n..];
            _bytes.I += n;
            if (p.Length == 0)
                break;
            Fill();
        }
    }

    private void Ignore(int n)
    {
        if (_bytes.NUnreadable != 0)
        {
            if (_bits.N >= 8)
                UnreadByteStuffedByte();
            _bytes.NUnreadable = 0;
        }

        while (true)
        {
            int m = _bytes.J - _bytes.I;
            if (m > n) m = n;
            _bytes.I += m;
            n -= m;
            if (n == 0)
                break;
            Fill();
        }
    }

    private void ProcessSOF(int n)
    {
        if (_nComp != 0)
            throw new JpegFormatException("multiple SOF markers");
        switch (n)
        {
            case 6 + 3 * 1: _nComp = 1; break;
            case 6 + 3 * 3: _nComp = 3; break;
            case 6 + 3 * 4: _nComp = 4; break;
            default: throw new JpegUnsupportedException("number of components");
        }
        ReadFull(_tmp.AsSpan(0, n));
        if (_tmp[0] != 8)
            throw new JpegUnsupportedException("precision");
        _height = (_tmp[1] << 8) + _tmp[2];
        _width = (_tmp[3] << 8) + _tmp[4];
        if (_tmp[5] != _nComp)
            throw new JpegFormatException("SOF has wrong length");

        for (int i = 0; i < _nComp; i++)
        {
            _comp[i].C = _tmp[6 + 3 * i];
            for (int j = 0; j < i; j++)
            {
                if (_comp[i].C == _comp[j].C)
                    throw new JpegFormatException("repeated component identifier");
            }

            _comp[i].Tq = _tmp[8 + 3 * i];
            if (_comp[i].Tq > JpegConstants.MaxTq)
                throw new JpegFormatException("bad Tq value");

            byte hv = _tmp[7 + 3 * i];
            int h = hv >> 4, v = hv & 0x0f;
            if (h < 1 || 4 < h || v < 1 || 4 < v)
                throw new JpegFormatException("luma/chroma subsampling ratio");
            if (h == 3 || v == 3)
                throw new JpegUnsupportedException(JpegConstants.ErrUnsupportedSubsamplingRatioMsg);

            switch (_nComp)
            {
                case 1:
                    h = 1; v = 1;
                    break;
                case 4:
                    switch (i)
                    {
                        case 0:
                            if (hv != 0x11 && hv != 0x22)
                                throw new JpegUnsupportedException(JpegConstants.ErrUnsupportedSubsamplingRatioMsg);
                            break;
                        case 1:
                        case 2:
                            if (hv != 0x11)
                                throw new JpegUnsupportedException(JpegConstants.ErrUnsupportedSubsamplingRatioMsg);
                            break;
                        case 3:
                            if (_comp[0].H != h || _comp[0].V != v)
                                throw new JpegUnsupportedException(JpegConstants.ErrUnsupportedSubsamplingRatioMsg);
                            break;
                    }
                    break;
            }

            _maxH = Math.Max(_maxH, h);
            _maxV = Math.Max(_maxV, v);
            _comp[i].H = h;
            _comp[i].V = v;
        }

        if (_nComp == 3)
        {
            for (int i = 0; i < 3; i++)
            {
                if (_maxH % _comp[i].H != 0 || _maxV % _comp[i].V != 0)
                    throw new JpegUnsupportedException(JpegConstants.ErrUnsupportedSubsamplingRatioMsg);
            }
        }

        for (int i = 0; i < _nComp; i++)
        {
            _comp[i].ExpandH = _maxH / _comp[i].H;
            _comp[i].ExpandV = _maxV / _comp[i].V;
        }
    }

    private void ProcessDQT(int n)
    {
        while (n > 0)
        {
            n--;
            byte x = ReadByte();
            int tq = x & 0x0f;
            if (tq > JpegConstants.MaxTq)
                throw new JpegFormatException("bad Tq value");
            switch (x >> 4)
            {
                default:
                    throw new JpegFormatException("bad Pq value");
                case 0:
                    if (n < JpegConstants.BlockSize)
                        goto done;
                    n -= JpegConstants.BlockSize;
                    ReadFull(_tmp.AsSpan(0, JpegConstants.BlockSize));
                    for (int i = 0; i < JpegConstants.BlockSize; i++)
                        _quant[tq][i] = _tmp[i];
                    break;
                case 1:
                    if (n < 2 * JpegConstants.BlockSize)
                        goto done;
                    n -= 2 * JpegConstants.BlockSize;
                    ReadFull(_tmp.AsSpan(0, 2 * JpegConstants.BlockSize));
                    for (int i = 0; i < JpegConstants.BlockSize; i++)
                        _quant[tq][i] = (_tmp[2 * i] << 8) | _tmp[2 * i + 1];
                    break;
            }
        }
    done:
        if (n != 0)
            throw new JpegFormatException("DQT has wrong length");
    }

    private void ProcessDRI(int n)
    {
        if (n != 2)
            throw new JpegFormatException("DRI has wrong length");
        ReadFull(_tmp.AsSpan(0, 2));
        _ri = (_tmp[0] << 8) + _tmp[1];
    }

    private void ProcessApp0Marker(int n)
    {
        if (n < 5)
        {
            Ignore(n);
            return;
        }
        ReadFull(_tmp.AsSpan(0, 5));
        n -= 5;
        _jfif = _tmp[0] == 'J' && _tmp[1] == 'F' && _tmp[2] == 'I' && _tmp[3] == 'F' && _tmp[4] == 0;
        if (n > 0)
            Ignore(n);
    }

    private void ProcessApp14Marker(int n)
    {
        if (n < 12)
        {
            Ignore(n);
            return;
        }
        ReadFull(_tmp.AsSpan(0, 12));
        n -= 12;
        if (_tmp[0] == 'A' && _tmp[1] == 'd' && _tmp[2] == 'o' && _tmp[3] == 'b' && _tmp[4] == 'e')
        {
            _adobeTransformValid = true;
            _adobeTransform = _tmp[11];
        }
        if (n > 0)
            Ignore(n);
    }

    public IImage? Decode(Stream r, bool configOnly)
    {
        _r = r;
        ReadFull(_tmp.AsSpan(0, 2));
        if (_tmp[0] != 0xff || _tmp[1] != JpegConstants.SoiMarker)
            throw new JpegFormatException("missing SOI marker");

        while (true)
        {
            ReadFull(_tmp.AsSpan(0, 2));
            while (_tmp[0] != 0xff)
            {
                _tmp[0] = _tmp[1];
                _tmp[1] = ReadByte();
            }
            byte marker = _tmp[1];
            if (marker == 0)
                continue;
            while (marker == 0xff)
                marker = ReadByte();
            if (marker == JpegConstants.EoiMarker)
                break;
            if (marker >= JpegConstants.Rst0Marker && marker <= JpegConstants.Rst7Marker)
                continue;

            ReadFull(_tmp.AsSpan(0, 2));
            int segLen = (_tmp[0] << 8) + _tmp[1] - 2;
            if (segLen < 0)
                throw new JpegFormatException("short segment length");

            switch (marker)
            {
                case JpegConstants.Sof0Marker:
                case JpegConstants.Sof1Marker:
                case JpegConstants.Sof2Marker:
                    _baseline = marker == JpegConstants.Sof0Marker;
                    _progressive = marker == JpegConstants.Sof2Marker;
                    ProcessSOF(segLen);
                    if (configOnly && _jfif)
                        return null;
                    break;
                case JpegConstants.DhtMarker:
                    if (configOnly) Ignore(segLen);
                    else ProcessDHT(segLen);
                    break;
                case JpegConstants.DqtMarker:
                    if (configOnly) Ignore(segLen);
                    else ProcessDQT(segLen);
                    break;
                case JpegConstants.SosMarker:
                    if (configOnly)
                        return null;
                    ProcessSOS(segLen);
                    break;
                case JpegConstants.DriMarker:
                    if (configOnly) Ignore(segLen);
                    else ProcessDRI(segLen);
                    break;
                case JpegConstants.App0Marker:
                    ProcessApp0Marker(segLen);
                    break;
                case JpegConstants.App14Marker:
                    ProcessApp14Marker(segLen);
                    break;
                default:
                    if ((marker >= JpegConstants.App0Marker && marker <= JpegConstants.App15Marker) ||
                        marker == JpegConstants.ComMarker)
                        Ignore(segLen);
                    else if (marker < 0xc0)
                        throw new JpegFormatException("unknown marker");
                    else
                        throw new JpegUnsupportedException("unknown marker");
                    break;
            }
        }

        if (_progressive)
            ReconstructProgressiveImage();
        if (_img1 != null)
            return _img1;
        if (_img3 != null)
        {
            if (_blackPix != null)
                return ApplyBlack();
            if (IsRGB())
                return ConvertToRGB();
            return _img3;
        }
        throw new JpegFormatException("missing SOS marker");
    }

    private IImage ApplyBlack()
    {
        if (!_adobeTransformValid)
            throw new JpegUnsupportedException("unknown color model: 4-component JPEG doesn't have Adobe APP14 metadata");

        var bounds = _img3!.Bounds();
        if (_adobeTransform != JpegConstants.AdobeTransformUnknown)
        {
            var img = Images.NewRgba(bounds);
            ImageUtil.DrawYCbCr(img, bounds, _img3, bounds.Min);
            var pix = img.Pix.Span;
            for (int y = bounds.Min.Y; y < bounds.Max.Y; y++)
            {
                int iBase = (y - bounds.Min.Y) * img.Stride;
                for (int x = bounds.Min.X; x < bounds.Max.X; x++)
                {
                    int i = iBase + (x - bounds.Min.X) * 4 + 3;
                    pix[i] = (byte)(255 - _blackPix![(y - bounds.Min.Y) * _blackStride + (x - bounds.Min.X)]);
                }
            }
            return new CmykImage { Pix = img.Pix, Stride = img.Stride, Rect = img.Rect };
        }

        var cmyk = Images.NewCmyk(bounds);
        var translations = new (byte[]? Src, int Stride)[]
        {
            (_img3.Y.ToArray(), _img3.YStride),
            (_img3.Cb.ToArray(), _img3.CStride),
            (_img3.Cr.ToArray(), _img3.CStride),
            (_blackPix, _blackStride),
        };
        for (int t = 0; t < 4; t++)
        {
            var (src, stride) = translations[t];
            bool subsample = _comp[t].H != _comp[0].H || _comp[t].V != _comp[0].V;
            var dstPix = cmyk.Pix.Span;
            for (int y = bounds.Min.Y; y < bounds.Max.Y; y++)
            {
                int iBase = (y - bounds.Min.Y) * cmyk.Stride;
                int sy = y - bounds.Min.Y;
                if (subsample) sy /= 2;
                for (int x = bounds.Min.X; x < bounds.Max.X; x++)
                {
                    int i = iBase + (x - bounds.Min.X) * 4 + t;
                    int sx = x - bounds.Min.X;
                    if (subsample) sx /= 2;
                    dstPix[i] = (byte)(255 - src![sy * stride + sx]);
                }
            }
        }
        return cmyk;
    }

    private bool IsRGB()
    {
        if (_jfif) return false;
        if (_adobeTransformValid && _adobeTransform == JpegConstants.AdobeTransformUnknown)
            return true;
        return _comp[0].C == (byte)'R' && _comp[1].C == (byte)'G' && _comp[2].C == (byte)'B';
    }

    private IImage ConvertToRGB()
    {
        int h0 = _comp[0].H, h1 = _comp[1].H, h2 = _comp[2].H;
        int v0 = _comp[0].V, v1 = _comp[1].V, v2 = _comp[2].V;
        if (h1 != h2 || h0 % h1 != 0 || v1 != v2 || v0 % v1 != 0)
            throw new JpegUnsupportedException(JpegConstants.ErrUnsupportedSubsamplingRatioMsg);

        int cScale = h0 / h1;
        var bounds = _img3!.Bounds();
        var img = Images.NewRgba(bounds);
        var ySpan = _img3.Y.Span;
        var cbSpan = _img3.Cb.Span;
        var crSpan = _img3.Cr.Span;
        for (int y = bounds.Min.Y; y < bounds.Max.Y; y++)
        {
            int po = img.PixOffset(bounds.Min.X, y);
            int yo = _img3.YOffset(bounds.Min.X, y);
            int co = _img3.COffset(bounds.Min.X, y);
            for (int i = 0, iMax = bounds.Max.X - bounds.Min.X; i < iMax; i++)
            {
                var pix = img.Pix.Span.Slice(po + 4 * i, 4);
                pix[0] = ySpan[yo + i];
                pix[1] = cbSpan[co + i / cScale];
                pix[2] = crSpan[co + i / cScale];
                pix[3] = 255;
            }
        }
        return img;
    }

    public Config GetConfig()
    {
        switch (_nComp)
        {
            case 1:
                return new Config(ColorModels.Gray, _width, _height);
            case 3:
                var cm = ColorModels.YCbCr;
                if (IsRGB()) cm = ColorModels.Rgba;
                return new Config(cm, _width, _height);
            case 4:
                return new Config(ColorModels.Cmyk, _width, _height);
        }
        throw new JpegFormatException("missing SOF marker");
    }
}
