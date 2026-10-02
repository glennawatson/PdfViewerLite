// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Platform;
using PdfViewerLite.Core.Printing;
using PdfViewerLite.Platform.Windows.Printing;

namespace PdfViewerLite.Core.Tests.Platform;

/// <summary>Tests the printer settings passed to Windows.</summary>
public sealed class WindowsPrintServiceTests
{
    /// <summary>The Windows value for single-sided printing.</summary>
    private const short OneSided = 1;

    /// <summary>The Windows value for long-edge duplex printing.</summary>
    private const short LongEdge = 2;

    /// <summary>The Windows value for short-edge duplex printing.</summary>
    private const short ShortEdge = 3;

    /// <summary>Verifies that the chosen edge becomes the correct Windows duplex value.</summary>
    /// <param name="twoSided">Whether duplex printing is enabled.</param>
    /// <param name="binding">The edge used to turn the sheet.</param>
    /// <param name="expected">The Windows duplex value.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false, DuplexBinding.LongEdge, OneSided)]
    [Arguments(false, DuplexBinding.ShortEdge, OneSided)]
    [Arguments(true, DuplexBinding.LongEdge, LongEdge)]
    [Arguments(true, DuplexBinding.ShortEdge, ShortEdge)]
    public async Task AppliesDuplexBinding(bool twoSided, DuplexBinding binding, short expected)
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test("Windows printer settings require Windows.");
            return;
        }

        var options = new PrintJobOptions("printer", 1, true, twoSided, PaperSize.A4) { Binding = binding };

        await Assert.That(WindowsPrintService.GetDuplex(options)).IsEqualTo(expected);
    }
}
