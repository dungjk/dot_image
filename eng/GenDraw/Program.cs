using System.Text;

namespace DotImage.GenDraw;

public static class Program
{
    public static int Main(string[] args)
    {
        string dir = Environment.CurrentDirectory;
        string outPath = Path.Combine(dir, "src", "DotImage.Extended", "Draw", "ScaleLeaf.Generated.cs");
        bool check = false;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--out":
                    outPath = args[++i];
                    break;
                case "--check":
                    check = true;
                    break;
            }
        }

        string generated = Generator.Generate();

        if (check)
        {
            string current = File.ReadAllText(outPath);
            if (current != generated)
            {
                Console.Error.WriteLine($"ScaleLeaf.Generated.cs is out of date; run 'dotnet run --project eng/GenDraw' to regenerate.");
                return 1;
            }
            return 0;
        }

        File.WriteAllText(outPath, generated);
        Console.WriteLine($"Wrote {outPath}");
        return 0;
    }
}