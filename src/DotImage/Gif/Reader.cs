// Ported from Go src/image/gif/reader.go


using DotImage.Color;
using DotImage.Compress;

namespace DotImage.Gif;

public sealed class GifAnimation
{
    public List<PalettedImage> Image { get; set; } = [];
    public List<int> Delay { get; set; } = [];
    public int LoopCount { get; set; }
    public byte[]? Disposal { get; set; }
    public Config Config { get; set; }
    public byte BackgroundIndex { get; set; }
}

public static class GifReader
{
  public const byte DisposalNone = 0x01;
  public const byte DisposalBackground = 0x02;
  public const byte DisposalPrevious = 0x03;

  private const byte FColorTable = 1 << 7;
  private const byte FInterlace = 1 << 6;
  private const byte FColorTableBitsMask = 7;

  private const byte GcTransparentColorSet = 1 << 0;
  private const byte GcDisposalMethodMask = 7 << 2;

  private const byte SExtension = 0x21;
  private const byte SImageDescriptor = 0x2C;
  private const byte STrailer = 0x3B;

  private const byte EText = 0x01;
  private const byte EGraphicControl = 0xF9;
  private const byte EComment = 0xFE;
  private const byte EApplication = 0xFF;

  public static IImage Decode(Stream r) => new Decoder(r).Decode(false, false).Image[0];

  public static GifAnimation DecodeAll(Stream r) => new Decoder(r).Decode(false, true);

  public static Config DecodeConfig(Stream r) => new Decoder(r).DecodeConfig();

  private sealed class Decoder
  {
    private readonly Stream _r;
    private string _vers = "";
    private int _width;
    private int _height;
    private int _loopCount = -1;
    private int _delayTime;
    private byte _backgroundIndex;
    private byte _disposalMethod;
    private byte _imageFields;
    private byte _transparentIndex;
    private bool _hasTransparentIndex;
    private Palette? _globalColorTable;
    private readonly List<int> _delay = [];
    private readonly List<byte> _disposal = [];
    private readonly List<PalettedImage> _image = [];
    private readonly byte[] _tmp = new byte[1024];

    public Decoder(Stream r) => _r = r;

    public Config DecodeConfig()
    {
      Decode(true, false);
      return new Config(_globalColorTable ?? new Palette([]), _width, _height);
    }

    public GifAnimation Decode(bool configOnly, bool keepAllFrames)
    {
      _loopCount = -1;
      ReadHeaderAndScreenDescriptor();

      if (configOnly)
        return ToGif();

      while (true)
      {
        int c = ReadByte();
        switch (c)
        {
          case SExtension:
            ReadExtension();
            break;
          case SImageDescriptor:
            ReadImageDescriptor(keepAllFrames);
            if (!keepAllFrames && _image.Count == 1)
              return ToGif();
            break;
          case STrailer:
            if (_image.Count == 0)
              throw new InvalidDataException("gif: missing image data");
            return ToGif();
          default:
            throw new InvalidDataException($"gif: unknown block type: 0x{c:x2}");
        }
      }
    }

    private GifAnimation ToGif() => new()
    {
      Image = _image,
      Delay = _delay,
      LoopCount = _loopCount,
      Disposal = _disposal.Count > 0 ? [.._disposal] : null,
      Config = new Config(_globalColorTable ?? new Palette([]), _width, _height),
      BackgroundIndex = _backgroundIndex,
    };

    private void ReadHeaderAndScreenDescriptor()
    {
      ReadFull(_tmp.AsSpan(0, 13));
      _vers = System.Text.Encoding.ASCII.GetString(_tmp, 0, 6);
      if (_vers != "GIF87a" && _vers != "GIF89a")
        throw new InvalidDataException($"gif: can't recognize format \"{_vers}\"");

      _width = _tmp[6] | (_tmp[7] << 8);
      _height = _tmp[8] | (_tmp[9] << 8);
      if ((_tmp[10] & FColorTable) != 0)
      {
        _backgroundIndex = _tmp[11];
        _globalColorTable = ReadColorTable(_tmp[10]);
      }
    }

    private Palette ReadColorTable(byte fields)
    {
      int n = 1 << (1 + (fields & FColorTableBitsMask));
      ReadFull(_tmp.AsSpan(0, 3 * n));
      var colors = new IColor[n];
      int j = 0;
      for (int i = 0; i < n; i++)
      {
        colors[i] = new Rgba(_tmp[j], _tmp[j + 1], _tmp[j + 2], 0xff);
        j += 3;
      }
      return new Palette(colors);
    }

    private void ReadExtension()
    {
      int extension = ReadByte();
      int size = 0;
      switch (extension)
      {
        case EText:
          size = 13;
          break;
        case EGraphicControl:
          ReadGraphicControl();
          return;
        case EComment:
          break;
        case EApplication:
          size = ReadByte();
          break;
        default:
          throw new InvalidDataException($"gif: unknown extension 0x{extension:x2}");
      }

      if (size > 0)
        ReadFull(_tmp.AsSpan(0, size));

      if (extension == EApplication && System.Text.Encoding.ASCII.GetString(_tmp, 0, size) == "NETSCAPE2.0")
      {
        int n = ReadBlock();
        if (n == 0) return;
        if (n == 3 && _tmp[0] == 1)
          _loopCount = _tmp[1] | (_tmp[2] << 8);
      }

      while (true)
      {
        int n = ReadBlock();
        if (n == 0) return;
      }
    }

    private void ReadGraphicControl()
    {
      ReadFull(_tmp.AsSpan(0, 6));
      if (_tmp[0] != 4)
        throw new InvalidDataException($"gif: invalid graphic control extension block size: {_tmp[0]}");

      byte flags = _tmp[1];
      _disposalMethod = (byte)((flags & GcDisposalMethodMask) >> 2);
      _delayTime = _tmp[2] | (_tmp[3] << 8);
      if ((flags & GcTransparentColorSet) != 0)
      {
        _transparentIndex = _tmp[4];
        _hasTransparentIndex = true;
      }
      if (_tmp[5] != 0)
        throw new InvalidDataException($"gif: invalid graphic control extension block terminator: {_tmp[5]}");
    }

    private void ReadImageDescriptor(bool keepAllFrames)
    {
      var m = NewImageFromDescriptor();
      bool useLocalColorTable = (_imageFields & FColorTable) != 0;
      if (useLocalColorTable)
        m.Palette = ReadColorTable(_imageFields);
      else
      {
        if (_globalColorTable == null)
          throw new InvalidDataException("gif: no color table");
        m.Palette = _globalColorTable;
      }

      if (_hasTransparentIndex)
      {
        if (!useLocalColorTable)
        {
          var colors = new IColor[_globalColorTable!.Length];
          for (int i = 0; i < colors.Length; i++)
            colors[i] = _globalColorTable[i];
          m.Palette = new Palette(colors);
        }

        int ti = _transparentIndex;
        if (ti < m.Palette.Length)
        {
          var colors = new IColor[m.Palette.Length];
          for (int i = 0; i < colors.Length; i++)
            colors[i] = m.Palette[i];
          colors[ti] = new Rgba(0, 0, 0, 0);
          m.Palette = new Palette(colors);
        }
        else
        {
          var colors = new IColor[ti + 1];
          for (int i = 0; i < m.Palette.Length; i++)
            colors[i] = m.Palette[i];
          for (int i = m.Palette.Length; i < colors.Length; i++)
            colors[i] = new Rgba(0, 0, 0, 0);
          m.Palette = new Palette(colors);
        }
      }

      int litWidth = ReadByte();
      if (litWidth < 2 || litWidth > 8)
        throw new InvalidDataException($"gif: pixel size in decode out of range: {litWidth}");

      var br = new BlockReader(this);
      using var lzwr = new LzwReader(br, LzwOrder.Lsb, litWidth);
      try
      {
        ReadFull(lzwr, m.Pix.Span);
      }
      catch (EndOfStreamException)
      {
        throw new GifNotEnoughException();
      }
      catch (InvalidDataException ex) when (ex.Message.EndsWith(": unexpected EOF"))
      {
        throw new GifNotEnoughException();
      }

      var extra = new byte[1];
      int n = lzwr.Read(extra, 0, 1);
      if (n != 0)
        throw new GifTooMuchException();

      try
      {
        br.CloseBlocks();
      }
      catch (GifTooMuchException)
      {
        throw;
      }
      catch (Exception ex)
      {
        throw new InvalidDataException($"gif: reading image data: {ex.Message}", ex);
      }

      if (m.Palette.Length < 256)
      {
        foreach (byte pixel in m.Pix.Span)
        {
          if (pixel >= m.Palette.Length)
            throw new GifBadPixelException();
        }
      }

      if ((_imageFields & FInterlace) != 0)
        Uninterlace(m);

      if (keepAllFrames || _image.Count == 0)
      {
        _image.Add(m);
        _delay.Add(_delayTime);
        _disposal.Add(_disposalMethod);
      }

      _delayTime = 0;
      _hasTransparentIndex = false;
    }

    private PalettedImage NewImageFromDescriptor()
    {
      ReadFull(_tmp.AsSpan(0, 9));
      int left = _tmp[0] | (_tmp[1] << 8);
      int top = _tmp[2] | (_tmp[3] << 8);
      int width = _tmp[4] | (_tmp[5] << 8);
      int height = _tmp[6] | (_tmp[7] << 8);
      _imageFields = _tmp[8];

      if (left + width > _width || top + height > _height)
        throw new InvalidDataException("gif: frame bounds larger than image bounds");

      return Images.NewPaletted(
        Geometry.Rect(left, top, left + width, top + height),
        new Palette([]));
    }

    private int ReadBlock()
    {
      int n = ReadByte();
      if (n == 0) return 0;
      ReadFull(_tmp.AsSpan(0, n));
      return n;
    }

    private static readonly (int Skip, int Start)[] Interlacing =
    [
      (8, 0), (8, 4), (4, 2), (2, 1),
    ];

    private static void Uninterlace(PalettedImage m)
    {
      int dx = m.Rect.Dx();
      int dy = m.Rect.Dy();
      var nPix = new byte[dx * dy];
      int offset = 0;
      foreach (var (skip, start) in Interlacing)
      {
        int nOffset = start * dx;
        for (int y = start; y < dy; y += skip)
        {
          m.Pix.Span.Slice(offset, dx).CopyTo(nPix.AsSpan(nOffset, dx));
          offset += dx;
          nOffset += dx * skip;
        }
      }
      m.Pix = nPix;
    }

    private static InvalidDataException GifUnexpectedEof(string context) =>
        new($"gif: {context}: unexpected EOF");

    private int ReadByte()
    {
      int b = _r.ReadByte();
      if (b < 0)
        throw GifUnexpectedEof("reading");
      return b;
    }

    private void ReadFull(Span<byte> b)
    {
      int offset = 0;
      while (offset < b.Length)
      {
        int n = _r.Read(b[offset..]);
        if (n == 0)
          throw GifUnexpectedEof("reading");
        offset += n;
      }
    }

    private static void ReadFull(Stream r, Span<byte> b)
    {
      int offset = 0;
      while (offset < b.Length)
      {
        int n = r.Read(b[offset..]);
        if (n == 0)
          throw GifUnexpectedEof("reading");
        offset += n;
      }
    }

    private sealed class BlockReader(Decoder d) : Stream
    {
      private byte _i;
      private byte _j;
      private Exception? _err;

      private void Fill()
      {
        if (_err != null) return;
        int b = d.ReadByte();
        _j = (byte)b;
        if (_j == 0)
        {
          _err = new EndOfStreamException();
          return;
        }

        _i = 0;
        try
        {
          ReadFull(d._r, d._tmp.AsSpan(0, _j));
        }
        catch (Exception ex)
        {
          _err = ex;
          _j = 0;
        }
      }

      public override int ReadByte()
      {
        if (_i == _j)
        {
          Fill();
          if (_err != null)
            throw _err;
        }

        byte c = d._tmp[_i];
        _i++;
        return c;
      }

      public override int Read(byte[] buffer, int offset, int count)
      {
        if (count == 0 || _err != null)
          return _err != null ? throw _err : 0;
        if (_i == _j)
        {
          Fill();
          if (_err != null)
            throw _err;
        }

        int n = Math.Min(count, _j - _i);
        Buffer.BlockCopy(d._tmp, _i, buffer, offset, n);
        _i += (byte)n;
        return n;
      }

      public void CloseBlocks()
      {
        if (_err is EndOfStreamException)
          return;
        if (_err != null)
          throw _err;

        if (_i == _j)
        {
          Fill();
          if (_err is EndOfStreamException)
            return;
          if (_err != null)
            throw _err;
          if (_j > 1)
            throw new GifTooMuchException();
        }

        Fill();
        if (_err is EndOfStreamException)
          return;
        if (_err != null)
          throw _err;
        throw new GifTooMuchException();
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
  }
}
