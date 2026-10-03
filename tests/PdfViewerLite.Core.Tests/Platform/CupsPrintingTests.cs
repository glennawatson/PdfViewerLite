// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Printing;
using PdfViewerLite.Platform.Linux.Cups;

namespace PdfViewerLite.Core.Tests.Platform;

/// <summary>Tests for printing through CUPS.</summary>
public sealed class CupsPrintingTests
{
    /// <summary>The copies asked for.</summary>
    private const int Copies = 2;

    /// <summary>More copies than allowed.</summary>
    private const int TooMany = 5000;

    /// <summary>Verifies the settings become the standard CUPS job options.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BuildsJobOptions()
    {
        var options = CupsPrinting.BuildOptions(new("office", Copies, false, true, PaperSize.Letter)).ToDictionary(static o => o.Name, static o => o.Value);

        await Assert.That(options["copies"]).IsEqualTo("2");
        await Assert.That(options["media"]).IsEqualTo("na_letter_8.5x11in");
        await Assert.That(options["sides"]).IsEqualTo("two-sided-long-edge");
        await Assert.That(options["print-color-mode"]).IsEqualTo("monochrome");
    }

    /// <summary>Verifies copies are kept to a sensible number and one-sided colour A4 is the plain case.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepsCopiesSensible()
    {
        var options = CupsPrinting.BuildOptions(new("office", TooMany, true, false, PaperSize.A4)).ToDictionary(static o => o.Name, static o => o.Value);

        await Assert.That(options["copies"]).IsEqualTo("999");
        await Assert.That(options["media"]).IsEqualTo("iso_a4_210x297mm");
        await Assert.That(options["sides"]).IsEqualTo("one-sided");
        await Assert.That(options["print-color-mode"]).IsEqualTo("color");
    }

    /// <summary>Verifies listing printers works, returning none rather than failing when no print server is running.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ListsPrintersWithoutFailing()
    {
        var printers = CupsPrinting.GetPrinters();

        await Assert.That(printers).IsNotNull();
        await Assert.That(printers.All(static p => !string.IsNullOrEmpty(p.Name))).IsTrue();
    }
}
