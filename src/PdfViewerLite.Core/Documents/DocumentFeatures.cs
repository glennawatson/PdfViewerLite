// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PdfViewerLite.Core.Documents;

/// <summary>Resolves optional features from document implementations.</summary>
public static class DocumentFeatures
{
    /// <summary>Resolves a feature with the same null and failure behavior as an explicit cast.</summary>
    /// <param name="document">The document, or <see langword="null"/>.</param>
    /// <param name="featureType">The feature interface type.</param>
    /// <returns>The feature, or <see langword="null"/> for a null document.</returns>
    /// <exception cref="InvalidCastException">The document does not support the feature.</exception>
    public static object? CastFeature(IDocument? document, Type featureType)
    {
        ArgumentNullException.ThrowIfNull(featureType);
        return document is null ? null : document.GetFeature(featureType) ?? throw new InvalidCastException("The document does not support the requested feature.");
    }
}
