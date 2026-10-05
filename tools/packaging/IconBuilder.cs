// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using SkiaSharp;

namespace PdfViewerLite.Tools.Packaging;

/// <summary>Creates the platform icon files from one source image with SkiaSharp.</summary>
internal static class IconBuilder
{
    /// <summary>The PNG encoder quality; PNG is lossless so this only trades size for time.</summary>
    private const int PngQuality = 100;

    /// <summary>The icns file magic.</summary>
    private const string IcnsMagic = "icns";

    /// <summary>The length of an icns chunk type code.</summary>
    private const int TypeLength = 4;

    /// <summary>The length of the type and length fields that start every icns chunk.</summary>
    private const int ChunkHeaderLength = TypeLength + sizeof(uint);

    /// <summary>The 16 pixel icon size.</summary>
    private const int Pixels16 = 16;

    /// <summary>The 24 pixel icon size.</summary>
    private const int Pixels24 = 24;

    /// <summary>The 32 pixel icon size.</summary>
    private const int Pixels32 = 32;

    /// <summary>The 48 pixel icon size.</summary>
    private const int Pixels48 = 48;

    /// <summary>The 64 pixel icon size.</summary>
    private const int Pixels64 = 64;

    /// <summary>The 128 pixel icon size.</summary>
    private const int Pixels128 = 128;

    /// <summary>The 256 pixel icon size.</summary>
    private const int Pixels256 = 256;

    /// <summary>The pixel sizes of the Linux hicolor PNG icons.</summary>
    private static readonly int[] LinuxSizes = [Pixels16, Pixels24, Pixels32, Pixels48, Pixels64, Pixels128, Pixels256];

    /// <summary>The icns chunks: type code and pixel size, PNG encoded.</summary>
    private static readonly (string Type, int Size)[] IcnsChunks =
    [
        ("icp4", Pixels16),
        ("icp5", Pixels32),
        ("icp6", Pixels64),
        ("ic07", Pixels128),
        ("ic08", Pixels256),
        ("ic11", Pixels32),
        ("ic12", Pixels64),
        ("ic13", Pixels256),
    ];

    /// <summary>Writes a square PNG logo.</summary>
    /// <param name="sourcePath">The source image.</param>
    /// <param name="outputPath">The output PNG path.</param>
    /// <param name="size">The width and height in pixels.</param>
    internal static void WritePng(string sourcePath, string outputPath, int size)
    {
        using var source = SKBitmap.Decode(sourcePath);
        File.WriteAllBytes(outputPath, EncodePng(source, size));
    }

    /// <summary>Writes freedesktop hicolor PNG icons at every standard size.</summary>
    /// <param name="sourcePath">The square source image.</param>
    /// <param name="directory">The icon theme root, for example packaging/linux/icons/hicolor.</param>
    /// <param name="applicationId">The icon name without an extension.</param>
    /// <returns>The files written.</returns>
    internal static List<string> WriteLinuxIcons(string sourcePath, string directory, string applicationId)
    {
        using var source = SKBitmap.Decode(sourcePath);
        List<string> written = [];
        foreach (var size in LinuxSizes)
        {
            var folder = Path.Combine(directory, $"{size}x{size}", "apps");
            _ = Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"{applicationId}.png");
            File.WriteAllBytes(path, EncodePng(source, size));
            written.Add(path);
        }

        return written;
    }

    /// <summary>Writes a macOS icns file of PNG encoded chunks.</summary>
    /// <param name="sourcePath">The square source image.</param>
    /// <param name="outputPath">The icns file to create.</param>
    internal static void WriteIcns(string sourcePath, string outputPath)
    {
        using var source = SKBitmap.Decode(sourcePath);
        using var body = new MemoryStream();
        Span<byte> header = stackalloc byte[ChunkHeaderLength];
        foreach (var (type, size) in IcnsChunks)
        {
            var png = EncodePng(source, size);
            _ = System.Text.Encoding.ASCII.GetBytes(type, header);
            BinaryPrimitives.WriteUInt32BigEndian(header[TypeLength..], (uint)(ChunkHeaderLength + png.Length));
            body.Write(header);
            body.Write(png);
        }

        using var output = File.Create(outputPath);
        _ = System.Text.Encoding.ASCII.GetBytes(IcnsMagic, header);
        BinaryPrimitives.WriteUInt32BigEndian(header[TypeLength..], (uint)(ChunkHeaderLength + body.Length));
        output.Write(header);
        body.Position = 0;
        body.CopyTo(output);
    }

    /// <summary>Scales the image to a square and encodes it as PNG.</summary>
    /// <param name="source">The source bitmap.</param>
    /// <param name="size">The width and height in pixels.</param>
    /// <returns>The PNG bytes.</returns>
    /// <exception cref="InvalidOperationException">The image could not be scaled or encoded.</exception>
    private static byte[] EncodePng(SKBitmap source, int size)
    {
        SKImageInfo info = new(size, size, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var scaled = source.Resize(info, new(SKCubicResampler.Mitchell))
            ?? throw new InvalidOperationException($"The icon could not be scaled to {size}x{size}.");
        using var image = SKImage.FromBitmap(scaled);
        using var data = image.Encode(SKEncodedImageFormat.Png, PngQuality)
            ?? throw new InvalidOperationException($"The {size}x{size} icon could not be encoded.");
        return data.ToArray();
    }
}
