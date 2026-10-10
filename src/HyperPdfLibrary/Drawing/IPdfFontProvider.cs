// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Drawing;

/// <summary>Resolves platform fonts without exposing native font objects to the PDF engine.</summary>
public interface IPdfFontProvider
{
    /// <summary>Gets the shared default face.</summary>
    IPdfFontFace DefaultFace { get; }

    /// <summary>Creates a face in the requested family and style, applying missing bold or slant when needed.</summary>
    /// <param name="family">The requested family name.</param>
    /// <param name="weight">The requested weight from 100 to 900.</param>
    /// <param name="italic">Whether the requested face slants.</param>
    /// <returns>An owned face, or null when no usable face matches.</returns>
    IPdfFontFace? Match(string family, int weight, bool italic);

    /// <summary>Creates a face containing a Unicode scalar.</summary>
    /// <param name="unicode">The Unicode scalar.</param>
    /// <returns>An owned face, or null when no system face has the character.</returns>
    IPdfFontFace? MatchCharacter(int unicode);
}
