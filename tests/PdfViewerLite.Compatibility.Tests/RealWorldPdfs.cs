// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PdfViewerLite.Compatibility.Tests;

/// <summary>Test data sources over the corpus.</summary>
public static class RealWorldPdfs
{
    /// <summary>Gets every document.</summary>
    /// <returns>The documents.</returns>
    public static IEnumerable<Func<RealWorldPdf>> All() => From(static _ => true);

    /// <summary>Gets the documents that open.</summary>
    /// <returns>The documents.</returns>
    public static IEnumerable<Func<RealWorldPdf>> Readable() => From(static d => !d.Unreadable);

    /// <summary>Gets the documents with forms.</summary>
    /// <returns>The documents.</returns>
    public static IEnumerable<Func<RealWorldPdf>> Forms() => From(static d => !d.Unreadable && d.Has("forms"));

    /// <summary>Gets the documents that arrive signed.</summary>
    /// <returns>The documents.</returns>
    public static IEnumerable<Func<RealWorldPdf>> Signed() => From(static d => d.Has("signatures"));

    /// <summary>Gets the documents with a reading order transcript.</summary>
    /// <returns>The documents.</returns>
    public static IEnumerable<Func<RealWorldPdf>> Transcribed() => From(static d => d.GroundTruthUrl is not null);

    /// <summary>Gets the documents with a password.</summary>
    /// <returns>The documents.</returns>
    public static IEnumerable<Func<RealWorldPdf>> Protected() => From(static d => d.Password is not null);

    /// <summary>Wraps the matching documents for TUnit.</summary>
    /// <param name="filter">Which documents to use.</param>
    /// <returns>The documents.</returns>
    private static IEnumerable<Func<RealWorldPdf>> From(Func<RealWorldPdf, bool> filter)
    {
        foreach (var document in RealWorldPdfCache.Documents)
        {
            if (filter(document))
            {
                yield return () => document;
            }
        }
    }
}
