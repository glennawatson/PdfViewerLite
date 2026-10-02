// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PdfViewerLite.Pdfium.Tests;

/// <summary>Tests for <see cref="PdfDate"/>.</summary>
public sealed class PdfDateTests
{
    /// <summary>Verifies full dates with offsets parse.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ParsesFullDate() =>
        await Assert.That(PdfDate.Parse("D:20240229235958-05'30'")).IsEqualTo(new DateTimeOffset(2024, 2, 29, 23, 59, 58, new TimeSpan(-5, -30, 0)));

    /// <summary>Verifies partial dates default the missing fields.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ParsesPartialDate() =>
        await Assert.That(PdfDate.Parse("D:2023")).IsEqualTo(new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero));

    /// <summary>Verifies invalid values return null.</summary>
    /// <param name="value">The value.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("yesterday")]
    [Arguments("D:20231399")]
    public async Task RejectsInvalidDates(string? value) => await Assert.That(PdfDate.Parse(value)).IsNull();
}
