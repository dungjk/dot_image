// Ported from Go src/image/jpeg/scan.go


namespace DotImage.Jpeg;

internal sealed partial class Decoder
{
    private void MakeImg(int mxx, int myy)
    {
        if (_nComp == 1)
        {
            var m = Images.NewGray(Geometry.Rect(0, 0, 8 * mxx, 8 * myy));
            _img1 = (GrayImage)m.SubImage(Geometry.Rect(0, 0, _width, _height));
            return;
        }

        YCbCrSubsampleRatio subsampleRatio = YCbCrSubsampleRatio.Ratio444;
        if (_comp[1].H != _comp[2].H || _comp[1].V != _comp[2].V ||
            _maxH != _comp[0].H || _maxV != _comp[0].V)
        {
            _flex = true;
        }
        else
        {
            int hRatio = _maxH / _comp[1].H;
            int vRatio = _maxV / _comp[1].V;
            subsampleRatio = (hRatio << 4 | vRatio) switch
            {
                0x11 => YCbCrSubsampleRatio.Ratio444,
                0x12 => YCbCrSubsampleRatio.Ratio440,
                0x21 => YCbCrSubsampleRatio.Ratio422,
                0x22 => YCbCrSubsampleRatio.Ratio420,
                0x41 => YCbCrSubsampleRatio.Ratio411,
                0x42 => YCbCrSubsampleRatio.Ratio410,
                _ => subsampleRatio,
            };
            if ((hRatio << 4 | vRatio) is not (0x11 or 0x12 or 0x21 or 0x22 or 0x41 or 0x42))
                _flex = true;
        }

        var ycbcr = YCbCrImages.NewYCbCr(Geometry.Rect(0, 0, 8 * _maxH * mxx, 8 * _maxV * myy), subsampleRatio);
        _img3 = (YCbCrImage)ycbcr.SubImage(Geometry.Rect(0, 0, _width, _height));

        if (_nComp == 4)
        {
            int h3 = _comp[3].H, v3 = _comp[3].V;
            _blackPix = new byte[8 * h3 * mxx * 8 * v3 * myy];
            _blackStride = 8 * h3 * mxx;
        }
    }

    private void ProcessSOS(int n)
    {
        if (_nComp == 0)
            throw new JpegFormatException("missing SOF marker");
        if (n < 6 || 4 + 2 * _nComp < n || n % 2 != 0)
            throw new JpegFormatException("SOS has wrong length");
        ReadFull(_tmp.AsSpan(0, n));
        int nComp = _tmp[0];
        if (n != 4 + 2 * nComp)
            throw new JpegFormatException("SOS length inconsistent with number of components");

        var scan = new ScanComponent[JpegConstants.MaxComponents];
        int totalHV = 0;
        for (int i = 0; i < nComp; i++)
        {
            byte cs = _tmp[1 + 2 * i];
            int compIndex = -1;
            for (int j = 0; j < _nComp; j++)
            {
                if (cs == _comp[j].C)
                    compIndex = j;
            }
            if (compIndex < 0)
                throw new JpegFormatException("unknown component selector");
            scan[i].CompIndex = (byte)compIndex;
            for (int j = 0; j < i; j++)
            {
                if (scan[i].CompIndex == scan[j].CompIndex)
                    throw new JpegFormatException("repeated component selector");
            }
            totalHV += _comp[compIndex].H * _comp[compIndex].V;
            scan[i].Td = (byte)(_tmp[2 + 2 * i] >> 4);
            if (scan[i].Td > JpegConstants.MaxTh || (_baseline && scan[i].Td > 1))
                throw new JpegFormatException("bad Td value");
            scan[i].Ta = (byte)(_tmp[2 + 2 * i] & 0x0f);
            if (scan[i].Ta > JpegConstants.MaxTh || (_baseline && scan[i].Ta > 1))
                throw new JpegFormatException("bad Ta value");
        }
        if (_nComp > 1 && totalHV > 10)
            throw new JpegFormatException("total sampling factors too large");

        int zigStart = 0, zigEnd = JpegConstants.BlockSize - 1;
        uint ah = 0, al = 0;
        if (_progressive)
        {
            zigStart = _tmp[1 + 2 * nComp];
            zigEnd = _tmp[2 + 2 * nComp];
            ah = (uint)(_tmp[3 + 2 * nComp] >> 4);
            al = (uint)(_tmp[3 + 2 * nComp] & 0x0f);
            if ((zigStart == 0 && zigEnd != 0) || zigStart > zigEnd || JpegConstants.BlockSize <= zigEnd)
                throw new JpegFormatException("bad spectral selection bounds");
            if (zigStart != 0 && nComp != 1)
                throw new JpegFormatException("progressive AC coefficients for more than one component");
            if (ah != 0 && ah != al + 1)
                throw new JpegFormatException("bad successive approximation values");
        }

        int mxx = (_width + 8 * _maxH - 1) / (8 * _maxH);
        int myy = (_height + 8 * _maxV - 1) / (8 * _maxV);
        if (_img1 == null && _img3 == null)
            MakeImg(mxx, myy);
        if (_progressive)
        {
            for (int i = 0; i < nComp; i++)
            {
                int compIndex = scan[i].CompIndex;
                if (_progCoeffs[compIndex] == null)
                {
                    int count = mxx * myy * _comp[compIndex].H * _comp[compIndex].V;
                    var blocks = new Block[count];
                    for (int k = 0; k < count; k++)
                        blocks[k] = new Block();
                    _progCoeffs[compIndex] = blocks;
                }
            }
        }

        _bits = default;
        int mcu = 0;
        byte expectedRST = JpegConstants.Rst0Marker;
        var dc = new int[JpegConstants.MaxComponents];
        int bx = 0, by = 0, blockCount = 0;

        for (int my = 0; my < myy; my++)
        {
            for (int mx = 0; mx < mxx; mx++)
            {
                for (int i = 0; i < nComp; i++)
                {
                    int compIndex = scan[i].CompIndex;
                    int hi = _comp[compIndex].H;
                    int vi = _comp[compIndex].V;
                    for (int j = 0; j < hi * vi; j++)
                    {
                        if (nComp != 1)
                        {
                            bx = hi * mx + j % hi;
                            by = vi * my + j / hi;
                        }
                        else
                        {
                            int q = mxx * hi;
                            bx = blockCount % q;
                            by = blockCount / q;
                            blockCount++;
                            if (bx * 8 >= _width || by * 8 >= _height)
                                continue;
                        }

                        Block b;
                        if (_progressive)
                            b = _progCoeffs[compIndex]![by * mxx * hi + bx].Clone();
                        else
                            b = new Block();

                        if (ah != 0)
                        {
                            Refine(ref b, _huff[JpegConstants.AcTable][scan[i].Ta], zigStart, zigEnd, 1 << (int)al);
                        }
                        else
                        {
                            int zig = zigStart;
                            if (zig == 0)
                            {
                                zig++;
                                byte value = DecodeHuffman(_huff[JpegConstants.DcTable][scan[i].Td]);
                                if (value > 16)
                                    throw new JpegUnsupportedException("excessive DC component");
                                int dcDelta = ReceiveExtend(value);
                                dc[compIndex] += dcDelta;
                                b[0] = dc[compIndex] << (int)al;
                            }

                            if (zig <= zigEnd && _eobRun > 0)
                            {
                                _eobRun--;
                            }
                            else
                            {
                                var huff = _huff[JpegConstants.AcTable][scan[i].Ta];
                                for (; zig <= zigEnd; zig++)
                                {
                                    byte value = DecodeHuffman(huff);
                                    int val0 = value >> 4;
                                    int val1 = value & 0x0f;
                                    if (val1 != 0)
                                    {
                                        zig += val0;
                                        if (zig > zigEnd)
                                            break;
                                        int ac = ReceiveExtend((byte)val1);
                                        b[JpegConstants.Unzig[zig]] = ac << (int)al;
                                    }
                                    else
                                    {
                                        if (val0 != 0x0f)
                                        {
                                            _eobRun = (ushort)(1 << val0);
                                            if (val0 != 0)
                                                _eobRun |= (ushort)DecodeBits(val0);
                                            _eobRun--;
                                            break;
                                        }
                                        zig += 0x0f;
                                    }
                                }
                            }
                        }

                        if (_progressive)
                        {
                            _progCoeffs[compIndex]![by * mxx * hi + bx] = b;
                            continue;
                        }
                        ReconstructBlock(ref b, bx, by, compIndex);
                    }
                }
                mcu++;
                if (_ri > 0 && mcu % _ri == 0 && mcu < mxx * myy)
                {
                    ReadFull(_tmp.AsSpan(0, 2));
                    if (_tmp[0] != 0xff || _tmp[1] != expectedRST)
                        FindRST(expectedRST);
                    expectedRST++;
                    if (expectedRST == JpegConstants.Rst7Marker + 1)
                        expectedRST = JpegConstants.Rst0Marker;
                    _bits = default;
                    Array.Clear(dc);
                    _eobRun = 0;
                }
            }
        }
    }

    private void Refine(ref Block b, HuffmanTable h, int zigStart, int zigEnd, int delta)
    {
        if (zigStart == 0)
        {
            if (zigEnd != 0)
                throw new InvalidOperationException("unreachable");
            if (DecodeBit())
                b[0] |= delta;
            return;
        }

        int zig = zigStart;
        if (_eobRun == 0)
        {
            for (; zig <= zigEnd; zig++)
            {
                int z = 0;
                byte value = DecodeHuffman(h);
                int val0 = value >> 4;
                int val1 = value & 0x0f;
                bool eobReached = false;
                switch (val1)
                {
                    case 0:
                        if (val0 != 0x0f)
                        {
                            _eobRun = (ushort)(1 << val0);
                            if (val0 != 0)
                                _eobRun |= (ushort)DecodeBits(val0);
                            eobReached = true;
                        }
                        break;
                    case 1:
                        z = delta;
                        if (!DecodeBit())
                            z = -z;
                        break;
                    default:
                        throw new JpegFormatException("unexpected Huffman code");
                }

                if (eobReached)
                    break;

                zig = RefineNonZeroes(ref b, zig, zigEnd, val0, delta);
                if (zig > zigEnd)
                    throw new JpegFormatException("too many coefficients");
                if (z != 0)
                    b[JpegConstants.Unzig[zig]] = z;
            }
        }

        if (_eobRun > 0)
        {
            _eobRun--;
            RefineNonZeroes(ref b, zig, zigEnd, -1, delta);
        }
    }

    private int RefineNonZeroes(ref Block b, int zig, int zigEnd, int nz, int delta)
    {
        for (; zig <= zigEnd; zig++)
        {
            int u = JpegConstants.Unzig[zig];
            if (b[u] == 0)
            {
                if (nz == 0)
                    break;
                nz--;
                continue;
            }
            if (!DecodeBit())
                continue;
            if (b[u] >= 0)
                b[u] += delta;
            else
                b[u] -= delta;
        }
        return zig;
    }

    private void ReconstructProgressiveImage()
    {
        int mxx = (_width + 8 * _maxH - 1) / (8 * _maxH);
        for (int i = 0; i < _nComp; i++)
        {
            if (_progCoeffs[i] == null)
                continue;
            int v = 8 * _maxV / _comp[i].V;
            int h = 8 * _maxH / _comp[i].H;
            int stride = mxx * _comp[i].H;
            for (int by = 0; by * v < _height; by++)
            {
                for (int bx = 0; bx * h < _width; bx++)
                {
                    var b = _progCoeffs[i][by * stride + bx];
                    ReconstructBlock(ref b, bx, by, i);
                }
            }
        }
    }

    private void ReconstructBlock(ref Block b, int bx, int by, int compIndex)
    {
        ref var qt = ref _quant[_comp[compIndex].Tq];
        for (int zig = 0; zig < JpegConstants.BlockSize; zig++)
            b[JpegConstants.Unzig[zig]] *= qt[zig];
        Dct.Idct(ref b);

        int h = 0, v = 0;
        if (_flex)
        {
            h = _comp[compIndex].ExpandH;
            v = _comp[compIndex].ExpandV;
            bx *= h;
            by *= v;
        }

        int stride;
        Span<byte> dst;
        if (_nComp == 1)
        {
            stride = _img1!.Stride;
            dst = _img1.Pix.Span.Slice(8 * (by * stride + bx));
        }
        else
        {
            switch (compIndex)
            {
                case 0:
                    stride = _img3!.YStride;
                    dst = _img3.Y.Span.Slice(8 * (by * stride + bx));
                    break;
                case 1:
                    stride = _img3!.CStride;
                    dst = _img3.Cb.Span.Slice(8 * (by * stride + bx));
                    break;
                case 2:
                    stride = _img3!.CStride;
                    dst = _img3.Cr.Span.Slice(8 * (by * stride + bx));
                    break;
                case 3:
                    stride = _blackStride;
                    dst = _blackPix!.AsSpan(8 * (by * stride + bx));
                    break;
                default:
                    throw new JpegUnsupportedException("too many components");
            }
        }

        if (_flex)
        {
            for (int y = 0; y < 8; y++)
            {
                int y8 = y * 8;
                int yv = y * v;
                for (int x = 0; x < 8; x++)
                {
                    byte val = (byte)Math.Clamp(b[y8 + x] + 128, 0, 255);
                    int xh = x * h;
                    for (int yy = 0; yy < v; yy++)
                    {
                        for (int xx = 0; xx < h; xx++)
                            dst[(yv + yy) * stride + xh + xx] = val;
                    }
                }
            }
            return;
        }

        for (int y = 0; y < 8; y++)
        {
            int y8 = y * 8;
            int yStride = y * stride;
            for (int x = 0; x < 8; x++)
                dst[yStride + x] = (byte)Math.Clamp(b[y8 + x] + 128, 0, 255);
        }
    }

    private void FindRST(byte expectedRST)
    {
        while (true)
        {
            int i = 0;
            if (_tmp[0] == 0xff)
            {
                if (_tmp[1] == expectedRST)
                    return;
                if (_tmp[1] == 0xff)
                    i = 1;
                else if (_tmp[1] != 0x00)
                    throw new JpegFormatException("bad RST marker");
            }
            else if (_tmp[1] == 0xff)
            {
                _tmp[0] = 0xff;
                i = 1;
            }
            ReadFull(_tmp.AsSpan(i, 2 - i));
        }
    }
}
