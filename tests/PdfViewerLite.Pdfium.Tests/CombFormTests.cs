// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Forms;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Pdfium.Tests;

/// <summary>Tests reading comb fields and character limits, and filling every kind of field.</summary>
public sealed class CombFormTests
{
    /// <summary>The comb's boxes and character limit.</summary>
    private const int CombBoxes = 6;

    /// <summary>The fields in the form.</summary>
    private const int FieldCount = 5;

    /// <summary>A comb field reports its boxes; ordinary fields do not; values and ticks are kept.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsAndFillsCombFields()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pvl-comb-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.CreateCombForm());
        try
        {
            using var document = new PdfiumEngine().Open(path, null);
            var form = (IFormFiller)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(document, typeof(IFormFiller))!;
            var fields = new List<FormField>();
            form.GetFields(0, fields);
            var name = fields.Single(static f => f.Name == "Name");
            var code = fields.Single(static f => f.Name == "Code");
            var large = fields.Last(static f => f.Kind == FormFieldKind.RadioButton);
            var setCode = form.SetText(0, code.Index, "AB1234");
            var setLarge = form.SetChecked(0, large.Index, true);
            fields.Clear();
            form.GetFields(0, fields);

            await Assert.That(fields.Count).IsEqualTo(FieldCount);
            await Assert.That(code.IsComb).IsTrue();
            await Assert.That(code.MaxLength).IsEqualTo(CombBoxes);
            await Assert.That(name.IsComb).IsFalse();
            await Assert.That(name.MaxLength).IsEqualTo(0);
            await Assert.That(setCode && setLarge).IsTrue();
            await Assert.That(fields.Single(static f => f.Name == "Code").Value).IsEqualTo("AB1234");
            await Assert.That(fields.Last(static f => f.Kind == FormFieldKind.RadioButton).IsChecked).IsTrue();
            await Assert.That(fields.First(static f => f.Kind == FormFieldKind.RadioButton).IsChecked).IsFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }
}
