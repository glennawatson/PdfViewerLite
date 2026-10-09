// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Platform;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>
/// Checks that a document opened by path, whose file the engine maps rather than loads, can be saved over its own file
/// the way the app saves: write a new file beside it, then move it over the original while the document is still open.
/// </summary>
public sealed class SaveOverOpenFileTests
{
    /// <summary>The pages of the generated document.</summary>
    private const int Pages = 2;

    /// <summary>The highlighted line.</summary>
    private static readonly PageRect Line = new(72, 100, 200, 14);

    /// <summary>Saving over the open file replaces it, the open document keeps reading the old file, and the new file holds the edit.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavingOverTheOpenFileWorks()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"hyperpdf-save-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "document.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.Create(Pages));
        try
        {
            using (var document = new HyperPdfEngine().Open(path, null))
            {
                var editor = (IAnnotationEditor)document;
                _ = editor.AddMarkup(0, AnnotationKind.Highlight, [Line], AnnotationColors.Sage, "Saved over");
                await Assert.That(SaveOver(editor, path)).IsTrue();

                // The open document still reads the file it was opened from.
                await Assert.That(document.PageCount).IsEqualTo(Pages);
                var lastPage = new List<PageAnnotation>();
                editor.GetAnnotations(Pages - 1, lastPage);
                await Assert.That(lastPage).IsEmpty();
            }

            using var reopened = new HyperPdfEngine().Open(path, null);
            var annotations = new List<PageAnnotation>();
            ((IAnnotationEditor)reopened).GetAnnotations(0, annotations);
            await Assert.That(annotations.Count).IsEqualTo(1);
            await Assert.That(Directory.GetFiles(directory).Length).IsEqualTo(1);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>Saves the way the app does: to a file beside the original, then moved over it.</summary>
    /// <param name="editor">The document.</param>
    /// <param name="path">The original file.</param>
    /// <returns><see langword="true"/> when saved.</returns>
    private static bool SaveOver(IAnnotationEditor editor, string path)
    {
        var temporary = $"{path}.saving";
        using (var stream = File.Create(temporary))
        {
            if (!editor.Save(stream))
            {
                return false;
            }
        }

        FileReplacement.Replace(temporary, path);
        return true;
    }
}
