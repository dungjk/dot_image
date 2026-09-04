// Ported from Go src/image/names.go


using DotImage.Color;

namespace DotImage;

public sealed class UniformImage : IImage, IRgba64Image, IColorModel, IColor
{
    public IColor C { get; set; }

    public UniformImage(IColor c) => C = c;

    public (uint R, uint G, uint B, uint A) Rgba() => C.Rgba();

    public IColorModel ColorModel() => this;

    public IColor Convert(IColor c) => C;

    public Rect Bounds() => new(new Point(-1_000_000_000, -1_000_000_000), new Point(1_000_000_000, 1_000_000_000));

    public IColor At(int x, int y) => C;

    public Rgba64 Rgba64At(int x, int y)
    {
        var (r, g, b, a) = C.Rgba();
        return new Rgba64((ushort)r, (ushort)g, (ushort)b, (ushort)a);
    }

    public bool Opaque()
    {
        var (_, _, _, a) = C.Rgba();
        return a == 0xffff;
    }
}

public static class UniformImages
{
    public static readonly UniformImage Black = new(Colors.Black);
    public static readonly UniformImage White = new(Colors.White);
    public static readonly UniformImage Transparent = new(Colors.Transparent);
    public static readonly UniformImage Opaque = new(Colors.Opaque);

    public static UniformImage NewUniform(IColor c) => new(c);
}
