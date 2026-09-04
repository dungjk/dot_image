// Ported from Go src/image/image.go (interfaces and Config).


using DotImage.Color;

namespace DotImage;

public readonly struct Config
{
    public IColorModel ColorModel { get; }
    public int Width { get; }
    public int Height { get; }

    public Config(IColorModel colorModel, int width, int height)
    {
        ColorModel = colorModel;
        Width = width;
        Height = height;
    }
}

public interface IImage
{
    IColorModel ColorModel();
    Rect Bounds();
    IColor At(int x, int y);
}

public interface IRgba64Image : IImage
{
    Rgba64 Rgba64At(int x, int y);
}

public interface IPalettedImage : IImage
{
    byte ColorIndexAt(int x, int y);
}

public interface IWritableImage : IImage
{
    bool Opaque();
    void Set(int x, int y, IColor c);
    IImage SubImage(Rect r);
}

public interface ISetRgba64Image
{
    void SetRgba64(int x, int y, Rgba64 c);
}
