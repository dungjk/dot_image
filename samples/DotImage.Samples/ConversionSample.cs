using DotImage;
using DotImage.Gif;
using DotImage.Jpeg;
using DotImage.Png;
using DotImage.Extended.Bmp;
using DotImage.Extended.Tiff;
using DotImage.Extended.Webp;

static class ConversionSample
{
    private static readonly string[] SupportedExtensions = ["png", "jpeg", "gif", "bmp", "tiff", "webp"];

    public static void Run()
    {
        string imagesDir = FindImagesDir();
        string outputsDir = Path.Combine(Directory.GetParent(imagesDir)!.FullName, "outputs");
        Directory.CreateDirectory(outputsDir);
        foreach (string old in Directory.EnumerateFiles(outputsDir))
            File.Delete(old);

        foreach (string file in Directory.EnumerateFiles(imagesDir).OrderBy(f => f, StringComparer.Ordinal))
        {
            string ext = Path.GetExtension(file).TrimStart('.').ToLowerInvariant();
            string stem = Path.GetFileNameWithoutExtension(file);
            if (ext == "jpg")
            {
                if (File.Exists(Path.Combine(Path.GetDirectoryName(file)!, stem + ".jpeg")))
                    continue;
                ext = "jpeg";
            }
            if (!SupportedExtensions.Contains(ext))
                continue;

            using var input = File.OpenRead(file);
            IImage image = Decode(ext, input);
            Console.WriteLine("Convert: " + Path.GetFileName(file) + " (" + ext + " -> ...)");

            foreach (string target in SupportedExtensions)
            {
                if (target == "webp" || target == ext)
                    continue;
                string name = $"{stem}-{ext}-{target}.{target}";
                using var output = File.Create(Path.Combine(outputsDir, name));
                Encode(target, output, image);
                Console.WriteLine("  -> " + name);
            }
        }
    }

    private static IImage Decode(string format, Stream input)
    {
        return format switch
        {
            "png" => PngReader.Decode(input),
            "jpeg" => JpegReader.Decode(input),
            "gif" => GifReader.Decode(input),
            "bmp" => Bmp.Decode(input),
            "tiff" => Tiff.Decode(input),
            "webp" => Decoder.Decode(input),
            _ => throw new NotSupportedException("decode: " + format),
        };
    }

    private static void Encode(string format, Stream output, IImage image)
    {
        switch (format)
        {
            case "png": PngWriter.Encode(output, image); break;
            case "jpeg": JpegWriter.Encode(output, image); break;
            case "gif": GifWriter.Encode(output, image); break;
            case "bmp": Bmp.Encode(output, image); break;
            case "tiff": TiffWriter.Encode(output, image, null); break;
            default: throw new NotSupportedException("encode: " + format);
        }
    }

    private static string FindImagesDir()
    {
        string[] candidates =
        [
            Path.Combine(Environment.CurrentDirectory, "samples", "DotImage.Samples", "images"),
            Path.Combine(Environment.CurrentDirectory, "images"),
        ];
        foreach (string c in candidates)
        {
            if (Directory.Exists(c))
                return c;
        }
        throw new DirectoryNotFoundException("images directory not found");
    }
}