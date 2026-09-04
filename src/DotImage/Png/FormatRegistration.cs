// PNG format registration


namespace DotImage.Png;

internal static class FormatRegistration
{
    private const string PngMagic = "\x89PNG\r\n\x1a\n";

    internal static void Register()
    {
        FormatRegistry.RegisterFormat(
            "png",
            PngMagic,
            PngReader.Decode,
            PngReader.DecodeConfig);
    }
}
