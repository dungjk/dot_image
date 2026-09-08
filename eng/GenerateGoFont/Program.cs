// Generates C# byte array data files from Go font TTF files.
// Usage: dotnet run
// Reads from ../../image-master/font/gofont/ttfs/
// Writes to ../../src/DotImage.Extended/Font/GoFont/Data/

using System.Text;

var fonts = new (string ClassName, string TtfFile)[]
{
    ("GoregularData",           "Go-Regular.ttf"),
    ("GoitalicData",            "Go-Italic.ttf"),
    ("GoboldData",              "Go-Bold.ttf"),
    ("GobolditalicData",        "Go-Bold-Italic.ttf"),
    ("GomediumData",            "Go-Medium.ttf"),
    ("GomediumitalicData",      "Go-Medium-Italic.ttf"),
    ("GomonoData",              "Go-Mono.ttf"),
    ("GomonoitalicData",        "Go-Mono-Italic.ttf"),
    ("GomonoboldData",          "Go-Mono-Bold.ttf"),
    ("GomonobolditalicData",    "Go-Mono-Bold-Italic.ttf"),
    ("GosmallcapsData",         "Go-Smallcaps.ttf"),
    ("GosmallcapsitalicData",   "Go-Smallcaps-Italic.ttf"),
};

string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
string ttfsDir = Path.Combine(root, "eng", "GenerateGoFont", "ttfs");
string outDir = Path.Combine(root, "src", "DotImage.Extended", "Font", "GoFont", "Data");

Directory.CreateDirectory(outDir);

int count = 0;
foreach (var (className, ttfFile) in fonts)
{
    string ttfPath = Path.Combine(ttfsDir, ttfFile);
    if (!File.Exists(ttfPath))
    {
        Console.Error.WriteLine($"File not found: {ttfPath}");
        continue;
    }

    byte[] data = File.ReadAllBytes(ttfPath);
    var sb = new StringBuilder();
    sb.AppendLine("namespace DotImage.Extended.Font.GoFont.Data;");
    sb.AppendLine();
    sb.AppendLine($"// Ported from golang.org/x/image/font/gofont. Generated from {ttfFile}.");
    sb.AppendLine($"internal static class {className}");
    sb.AppendLine("{");
    sb.AppendLine($"    internal static readonly byte[] TTF =");
    sb.AppendLine("    [");
    for (int i = 0; i < data.Length; i += 16)
    {
        int end = Math.Min(i + 16, data.Length);
        var line = new StringBuilder("    ");
        for (int j = i; j < end; j++)
        {
            if (j > i) line.Append(", ");
            line.Append($"0x{data[j]:X2}");
        }
        line.Append(',');
        sb.AppendLine(line.ToString());
    }
    sb.AppendLine("    ];");
    sb.AppendLine("}");
    sb.AppendLine();

    string outPath = Path.Combine(outDir, $"{className}.cs");
    File.WriteAllText(outPath, sb.ToString());
    Console.WriteLine($"Generated {className}.cs ({data.Length} bytes)");
    count++;
}

Console.WriteLine($"Done: {count} files generated.");
