// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.PageObjects;
using HyperPdfLibrary.Redaction;

namespace HyperPdfLibrary.Tests.Redaction;

/// <summary>Redacts text in real files from the cached corpus (the tests do nothing when the corpus is not cached).</summary>
[NotInParallel]
public sealed class RedactionCorpusTests
{
    /// <summary>The largest corpus file redacted, so the suite stays quick.</summary>
    private const long MaxFileLength = 3L * 1024 * 1024;

    /// <summary>The render mode of invisible text.</summary>
    private const int InvisibleMode = 3;

    /// <summary>The fewest glyphs a text object needs to be worth redacting.</summary>
    private const int MinimumGlyphs = 6;

    /// <summary>The first text object with some letters on the first page of each small file is redacted: extraction no longer finds it, and the other text stays.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RealTextIsRemoved()
    {
        var redacted = 0;
        foreach (var path in CorpusFiles())
        {
            var bytes = await File.ReadAllBytesAsync(path);
            string secret;
            string before;
            byte[] saved;
            using (var document = PdfDocumentReader.Open(bytes, null))
            {
                var target = FindTarget(PdfDocumentPageContent.GetPageContent(document, 0));
                if (target is null)
                {
                    continue;
                }

                secret = target.Text;
                before = PdfDocumentText.GetTextPage(document, 0).Text;
                _ = PdfRedactions.Add(document, 0, [target.Bounds], PdfRedactionAppearance.Black);
                await using var output = new MemoryStream();
                _ = PdfRedactor.ApplyAndSave(document, output, PdfRedactionOptions.Default);
                saved = output.ToArray();
            }

            var after = RedactionSamples.TextOf(saved);
            redacted++;

            await Assert.That(after).DoesNotContain(secret);
            await Assert.That(after.Length).IsLessThan(before.Length);
        }

        if (redacted == 0)
        {
            Skip.Test("No cached corpus file has text to redact.");
        }
    }

    /// <summary>Lists the small cached corpus files.</summary>
    /// <returns>The paths.</returns>
    private static List<string> CorpusFiles()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "pdfviewerlite", "corpus");
        var files = new List<string>();
        foreach (var path in Directory.Exists(folder) ? Directory.EnumerateFiles(folder, "*.pdf") : [])
        {
            if (new FileInfo(path).Length <= MaxFileLength)
            {
                files.Add(path);
            }
        }

        files.Sort(StringComparer.Ordinal);
        return files;
    }

    /// <summary>Finds a visible text object with enough letters to be a word or more.</summary>
    /// <param name="content">The content.</param>
    /// <returns>The object, or <see langword="null"/>.</returns>
    private static PdfTextObject? FindTarget(PdfPageContent content)
    {
        foreach (var item in content.Objects)
        {
            if (item is PdfTextObject { RenderMode: not InvisibleMode } text && text.GlyphCount >= MinimumGlyphs && text.Text.Any(char.IsLetter))
            {
                return text;
            }
        }

        return null;
    }
}
