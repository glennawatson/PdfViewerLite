// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Tagged;

/// <summary>
/// Installs a font factory that loads <see cref="TaggedTestFont"/> for fonts named <c>/TaggedTest</c>, restoring the
/// previous factory on dispose. The factory is process-wide, so tests that use this run one at a time.
/// </summary>
[DebuggerDisplay("TaggedFontScope")]
internal sealed class TaggedFontScope : IDisposable
{
    /// <summary>The factory in force before.</summary>
    private readonly Func<PdfDictionary, PdfFont?>? _previous;

    /// <summary>Initializes a new instance of the <see cref="TaggedFontScope"/> class.</summary>
    internal TaggedFontScope()
    {
        _previous = PdfFont.Factory;
        PdfFont.Factory = static dictionary => IsTestFont(dictionary) ? new TaggedTestFont(dictionary) : null;
    }

    /// <inheritdoc/>
    public void Dispose() => PdfFont.Factory = _previous;

    /// <summary>Determines whether a font dictionary names the test font.</summary>
    /// <param name="dictionary">The font dictionary.</param>
    /// <returns><see langword="true"/> for <c>/BaseFont /TaggedTest</c>.</returns>
    private static bool IsTestFont(PdfDictionary dictionary) =>
        dictionary.Owner?.Names.NameEquals(dictionary.GetName(KnownName.BaseFont), "TaggedTest"u8) == true;
}
