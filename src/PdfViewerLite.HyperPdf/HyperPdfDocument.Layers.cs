// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Documents;

namespace PdfViewerLite.HyperPdf;

/// <content>Layers, read and shown or hidden by the managed library without changing the file.</content>
public sealed partial class HyperPdfDocument : ILayerSource
{
    /// <inheritdoc/>
    public IReadOnlyList<DocumentLayer> GetLayers()
    {
        if (IsDisposed)
        {
            return [];
        }

        var layers = _document.OptionalContent.Layers;
        var result = new DocumentLayer[layers.Count];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = new(layers[i].Id, layers[i].Name, layers[i].IsVisible);
        }

        return result;
    }

    /// <inheritdoc/>
    public bool SetLayerVisible(int id, bool visible) => !IsDisposed && _document.OptionalContent.SetVisible(id, visible);
}
