// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Text;

/// <summary>Builds text pages with one reusable state for each thread.</summary>
internal static class TextPageBuild
{
    /// <summary>The reusable state of the current thread.</summary>
    [ThreadStatic]
    private static TextPageBuildState? _current;

    /// <summary>Gets the reusable state of the current thread.</summary>
    internal static TextPageBuildState Current => _current ??= new();

    /// <summary>Builds a text page from the collected runs, then clears the state.</summary>
    /// <param name="state">The reusable build state.</param>
    /// <param name="page">The page.</param>
    /// <param name="rightToLeft">Whether the document asks for right-to-left reading order.</param>
    /// <returns>The text page.</returns>
    internal static PdfTextPage Build(TextPageBuildState state, PdfPage page, bool rightToLeft)
    {
        state.Display = page.ViewerTransform;
        state.PageWidth = page.Width;
        state.PageHeight = page.Height;
        state.RightToLeft = rightToLeft;
        try
        {
            TextRunCollection.ProcessRuns(state);
            return TextPageAssembly.CreatePage(state, page);
        }
        finally
        {
            TextPageAssembly.Clear(state);
        }
    }
}
