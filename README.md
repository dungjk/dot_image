# DotImage

A C# port of Go's standard library [`image`](https://pkg.go.dev/image) package for **.NET 8** and **.NET 10**.

DotImage provides 2D image types, color models, Porter–Duff compositing, and PNG, GIF, and JPEG codecs. The implementation follows Go's logic closely while exposing an idiomatic C# API (`IImage`, `Memory<byte>`, exceptions).

## Features

- **Core image types** — `RgbaImage`, `NrgbaImage`, `GrayImage`, `CmykImage`, `PalettedImage`, `YCbCrImage`, and more
- **Color** — `IColor`, color models, RGB↔YCbCr, RGB↔CMYK, Plan9 and WebSafe palettes
- **Drawing** — `DrawOps.Draw`, `DrawMask`, `Clip`, Floyd–Steinberg dithering
- **Codecs** — PNG (all color types, interlaced, 16-bit), GIF (animation), JPEG (baseline + progressive decode)
- **Auto-detect decode** — `FormatRegistry.Decode` sniffs magic bytes and dispatches to the registered codec

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download) or [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Build & Test

```bash
dotnet build
dotnet test
```

Tests run on both `net8.0` and `net10.0` (209 tests).

## Quick Start

### Decode any supported format

PNG, GIF, and JPEG formats register automatically on the first call to `FormatRegistry.Decode` or `DecodeConfig`.

```csharp
using DotImage;

using var stream = File.OpenRead("photo.png");
var (image, format) = FormatRegistry.Decode(stream);

Console.WriteLine($"{format}: {image.Bounds().Dx()}×{image.Bounds().Dy()}");

var (r, g, b, a) = image.At(0, 0).Rgba();
```

### Decode config only (check dimensions before allocating)

```csharp
using var stream = File.OpenRead("photo.jpg");
var (config, format) = FormatRegistry.DecodeConfig(stream);

Console.WriteLine($"{format}: {config.Width}×{config.Height}");
```

### Create and draw on an image

```csharp
using DotImage;
using DotImage.Color;
using DotImage.Draw;

var rect = Geometry.Rect(0, 0, 256, 256);
var dst = Images.NewRgba(rect);

DrawOps.Draw(dst, rect, UniformImages.White, Point.Origin, Op.Src);
DrawOps.Draw(dst, new Rect(new Point(64, 64), new Point(192, 192)), UniformImages.Black, Point.Origin, Op.Over);
```

### Encode PNG

```csharp
using DotImage.Png;

using var output = File.Create("output.png");
PngWriter.Encode(output, image);
```

### Encode JPEG

```csharp
using DotImage.Jpeg;

using var output = File.Create("output.jpg");
JpegWriter.Encode(output, image, new JpegOptions { Quality = 90 });
```

### Decode GIF animation

```csharp
using DotImage.Gif;

using var stream = File.OpenRead("animation.gif");
var gif = GifReader.DecodeAll(stream);

foreach (var frame in gif.Image)
{
    // process each frame
}
```

## Project Structure

```
DotImage/
├── DotImage.slnx
├── src/DotImage/
│   ├── Color/           # color models, palettes, YCbCr/CMYK
│   ├── Draw/            # compositing
│   ├── Png/             # PNG codec
│   ├── Gif/             # GIF codec
│   ├── Jpeg/            # JPEG codec
│   ├── Compress/        # GIF LZW
│   ├── Util/            # YCbCr→RGBA fast paths
│   ├── Image.cs         # concrete image buffer types
│   ├── Geom.cs          # Point, Rect
│   ├── Format.cs        # FormatRegistry
│   └── ...
└── tests/DotImage.Tests/
```

## API Mapping (Go → C#)

| Go | DotImage |
|---|---|
| `image.Image` | `IImage` |
| `image.NewRGBA(r)` | `Images.NewRgba(r)` |
| `image.Decode(r)` | `FormatRegistry.Decode(stream)` |
| `color.Color` | `IColor` |
| `image.Rectangle` | `Rect` |
| `draw.Draw(...)` | `DrawOps.Draw(...)` |
| `png.Decode(r)` | `PngReader.Decode(stream)` |
| `jpeg.Encode(w, m, &opts)` | `JpegWriter.Encode(stream, image, options)` |
| `gif.DecodeAll(r)` | `GifReader.DecodeAll(stream)` |

## Relationship to Go

This library is a faithful port of Go's `image` tree:

| Go package | DotImage namespace |
|---|---|
| `image` | `DotImage` |
| `image/color` | `DotImage.Color` |
| `image/draw` | `DotImage.Draw` |
| `image/png` | `DotImage.Png` |
| `image/gif` | `DotImage.Gif` |
| `image/jpeg` | `DotImage.Jpeg` |

Key differences from Go:

- Errors are thrown as exceptions instead of returned as `error` values
- Pixel buffers use `byte[]` (slice via `Memory<byte>` where applicable)
- Format registration is lazy (on first decode) instead of Go blank imports

## License

The original Go source code is Copyright © The Go Authors and distributed under a [BSD-style license](https://go.dev/LICENSE).

This C# port retains the same license terms. See `LICENSE` and `NOTICE` for details.
