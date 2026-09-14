// Ported from Go src/image/jpeg/huffman.go


namespace DotImage.Jpeg;

internal sealed partial class Decoder
{
    private const int MaxCodeLength = 16;
    private const int MaxNCodes = 256;
    private const int LutSize = 8;

    private void EnsureNBits(int n)
    {
        while (true)
        {
            byte c;
            try
            {
                c = ReadByteStuffedByte();
            }
            catch (EndOfStreamException)
            {
                throw new JpegFormatException(JpegConstants.ErrShortHuffmanDataMsg);
            }
            _bits.A = (_bits.A << 8) | c;
            _bits.N += 8;
            if (_bits.M == 0)
                _bits.M = 1u << 7;
            else
                _bits.M <<= 8;
            if (_bits.N >= n)
                break;
        }
    }

    private int ReceiveExtend(byte t)
    {
        if (_bits.N < t)
            EnsureNBits(t);
        _bits.N -= t;
        _bits.M >>= t;
        int s = 1 << t;
        int x = (int)(_bits.A >> (byte)_bits.N) & (s - 1);
        int sign = (x >> (t - 1)) - 1;
        x += sign & (((-1) << t) + 1);
        return x;
    }

    private void ProcessDHT(int n)
    {
        Span<int> nCodes = stackalloc int[MaxCodeLength];
        while (n > 0)
        {
            if (n < 17)
                throw new JpegFormatException("DHT has wrong length");
            ReadFull(_tmp.AsSpan(0, 17));
            int tc = _tmp[0] >> 4;
            if (tc > JpegConstants.MaxTc)
                throw new JpegFormatException("bad Tc value");
            int th = _tmp[0] & 0x0f;
            if (th > JpegConstants.MaxTh || (_baseline && th > 1))
                throw new JpegFormatException("bad Th value");
            var h = _huff[tc][th];

            h.NCodes = 0;
            for (int i = 0; i < MaxCodeLength; i++)
            {
                nCodes[i] = _tmp[i + 1];
                h.NCodes += nCodes[i];
            }
            if (h.NCodes == 0)
                throw new JpegFormatException("Huffman table has zero length");
            if (h.NCodes > MaxNCodes)
                throw new JpegFormatException("Huffman table has excessive length");
            n -= h.NCodes + 17;
            if (n < 0)
                throw new JpegFormatException("DHT has wrong length");
            ReadFull(h.Vals.AsSpan(0, h.NCodes));

            Array.Clear(h.Lut);
            uint x = 0, code = 0;
            for (int i = 0; i < LutSize; i++)
            {
                code <<= 1;
                for (int j = 0; j < nCodes[i]; j++)
                {
                    byte b = (byte)(code << (7 - i));
                    ushort lutValue = (ushort)((h.Vals[x] << 8) | (2 + i));
                    for (int k = 0; k < 1 << (7 - i); k++)
                        h.Lut[b | k] = lutValue;
                    code++;
                    x++;
                }
            }

            int c = 0, index = 0;
            for (int i = 0; i < MaxCodeLength; i++)
            {
                int nc = nCodes[i];
                if (nc == 0)
                {
                    h.MinCodes[i] = -1;
                    h.MaxCodes[i] = -1;
                    h.ValsIndices[i] = -1;
                }
                else
                {
                    h.MinCodes[i] = c;
                    h.MaxCodes[i] = c + nc - 1;
                    h.ValsIndices[i] = index;
                    c += nc;
                    index += nc;
                }
                c <<= 1;
            }
        }
    }

    private byte DecodeHuffman(HuffmanTable h)
    {
        if (h.NCodes == 0)
            throw new JpegFormatException("uninitialized Huffman table");

        if (_bits.N < 8)
        {
            try
            {
                EnsureNBits(8);
            }
            catch (JpegFormatException ex) when (ex.Message == "invalid JPEG format: " + JpegConstants.ErrMissingFF00Msg || ex.Message == "invalid JPEG format: " + JpegConstants.ErrShortHuffmanDataMsg)
            {
                if (_bytes.NUnreadable != 0)
                    UnreadByteStuffedByte();
                goto slowPath;
            }
        }

        if (_bits.N >= 8)
        {
            ushort v = h.Lut[(int)((_bits.A >> (_bits.N - LutSize)) & 0xff)];
            if (v != 0)
            {
                int nb = (v & 0xff) - 1;
                _bits.N -= nb;
                _bits.M >>= nb;
                return (byte)(v >> 8);
            }
        }

    slowPath:
        for (int i = 0, code = 0; i < MaxCodeLength; i++)
        {
            if (_bits.N == 0)
                EnsureNBits(1);
            if ((_bits.A & _bits.M) != 0)
                code |= 1;
            _bits.N--;
            _bits.M >>= 1;
            if (code <= h.MaxCodes[i])
                return h.Vals[h.ValsIndices[i] + code - h.MinCodes[i]];
            code <<= 1;
        }
        throw new JpegFormatException("bad Huffman code");
    }

    private bool DecodeBit()
    {
        if (_bits.N == 0)
            EnsureNBits(1);
        bool ret = (_bits.A & _bits.M) != 0;
        _bits.N--;
        _bits.M >>= 1;
        return ret;
    }

    private uint DecodeBits(int n)
    {
        if (_bits.N < n)
            EnsureNBits(n);
        uint ret = _bits.A >> (_bits.N - n);
        ret &= (1u << n) - 1;
        _bits.N -= n;
        _bits.M >>= n;
        return ret;
    }
}
