#!/usr/bin/env -S dotnet run --file
#:property IsAotCompatible=false
#:property PublishAot=false

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// Extracts PDFium's bundled Foxit fonts (BSD-3-Clause) from the C++ byte arrays in core/fxge/fontdata/chromefontdata
// into binary .cff files for the HyperPdfLibrary embedded resources.
// Usage: dotnet run --file scripts/ExtractFoxitFonts.cs -- <chromefontdata folder> <output folder>
using System.Globalization;

return PdfViewerLite.Scripts.ExtractFoxitFonts.Run(args);

namespace PdfViewerLite.Scripts
{
    /// <summary>Converts the PDFium font data sources to binary font files.</summary>
    internal static class ExtractFoxitFonts
    {
        /// <summary>The number of command line arguments.</summary>
        private const int ArgumentCount = 2;

        /// <summary>The exit code for wrong arguments.</summary>
        private const int UsageError = 2;

        /// <summary>The length of the opening and closing braces.</summary>
        private const int BraceLength = 2;

        /// <summary>The length of the 0x prefix.</summary>
        private const int HexPrefixLength = 2;

        /// <summary>The faces the library bundles; the multiple master fonts are not used.</summary>
        private static readonly string[] Faces =
        [
            "FoxitSans", "FoxitSansBold", "FoxitSansItalic", "FoxitSansBoldItalic",
            "FoxitSerif", "FoxitSerifBold", "FoxitSerifItalic", "FoxitSerifBoldItalic",
            "FoxitFixed", "FoxitFixedBold", "FoxitFixedItalic", "FoxitFixedBoldItalic",
            "FoxitSymbol", "FoxitDingbats",
        ];

        /// <summary>Runs the tool.</summary>
        /// <param name="args">The source and output folders.</param>
        /// <returns>The exit code.</returns>
        internal static int Run(string[] args)
        {
            if (args.Length != ArgumentCount)
            {
                Console.Error.WriteLine("Usage: ExtractFoxitFonts <chromefontdata folder> <output folder>");
                return UsageError;
            }

            _ = Directory.CreateDirectory(args[1]);
            foreach (var face in Faces)
            {
                var bytes = Extract(File.ReadAllText(Path.Combine(args[0], $"{face}.cpp")));
                File.WriteAllBytes(Path.Combine(args[1], $"{face}.cff"), bytes);
                Console.WriteLine($"{face}.cff {bytes.Length}");
            }

            return 0;
        }

        /// <summary>Reads the hexadecimal values between the braces of the array initialiser.</summary>
        /// <param name="source">The C++ source.</param>
        /// <returns>The bytes.</returns>
        private static byte[] Extract(string source)
        {
            var start = source.IndexOf("{{", StringComparison.Ordinal) + BraceLength;
            var end = source.IndexOf("}}", start, StringComparison.Ordinal);
            var bytes = new List<byte>();
            foreach (var item in source.AsSpan(start, end - start).ToString().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                bytes.Add(byte.Parse(item.AsSpan(HexPrefixLength), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            }

            return [.. bytes];
        }
    }
}
