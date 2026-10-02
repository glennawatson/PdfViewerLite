// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Documents;
using PdfViewerLite.Pdfium.Native;

namespace PdfViewerLite.Pdfium;

/// <summary>Showing and hiding layers.</summary>
public sealed partial class PdfiumDocument : ILayerSource
{
    /// <summary>The layers with their current visibility, read on first use.</summary>
    private DocumentLayer[]? _layers;

    /// <summary>The layers as the document sets them, to tell when the view matches.</summary>
    private DocumentLayer[]? _defaultLayers;

    /// <summary>The copy pages are drawn from while layers differ from the document's setting.</summary>
    private PdfiumLayerView? _layerView;

    /// <summary>Whether the copy is out of date after an edit.</summary>
    private bool _layerViewStale;

    /// <inheritdoc/>
    public IReadOnlyList<DocumentLayer> GetLayers()
    {
        using var scope = PdfiumLibrary.EnterScope();
        return EnsureLayers();
    }

    /// <inheritdoc/>
    public bool SetLayerVisible(int id, bool visible)
    {
        using var scope = PdfiumLibrary.EnterScope();
        var layers = EnsureLayers();
        var index = -1;
        for (var i = 0; i < layers.Length && index < 0; i++)
        {
            index = layers[i].Id == id ? i : -1;
        }

        if (IsDisposed || index < 0)
        {
            return false;
        }

        layers[index] = layers[index] with { IsVisible = visible };
        return RebuildLayerView();
    }

    /// <summary>Marks the layer view out of date after an edit, so the next render rebuilds it. Callers hold the PDFium lock.</summary>
    private void InvalidateLayerView() => _layerViewStale = _layerView is not null;

    /// <summary>Reads the layers once. Callers hold the PDFium lock.</summary>
    /// <returns>The layers.</returns>
    private DocumentLayer[] EnsureLayers()
    {
        if (_layers is not null)
        {
            return _layers;
        }

        _defaultLayers = IsDisposed || !File.Exists(FilePath) ? [] : [.. PdfLayers.Read(File.ReadAllBytes(FilePath))];
        _layers = [.. _defaultLayers];
        return _layers;
    }

    /// <summary>Gets the layer view to draw from, rebuilding it after edits. Callers hold the PDFium lock.</summary>
    /// <returns>The view, or <see langword="null"/> to draw the document itself.</returns>
    private PdfiumLayerView? CurrentLayerView()
    {
        if (_layerViewStale)
        {
            _ = RebuildLayerView();
        }

        return _layerView;
    }

    /// <summary>Builds the copy showing the chosen layers, or drops it when they match the document. Callers hold the PDFium lock.</summary>
    /// <returns><see langword="true"/> when the view now shows the chosen layers.</returns>
    private bool RebuildLayerView()
    {
        _layerViewStale = false;
        _layerView?.Dispose();
        _layerView = null;
        if (_layers is null || _defaultLayers is null || _layers.AsSpan().SequenceEqual(_defaultLayers))
        {
            return true;
        }

        var hidden = new List<int>();
        foreach (var layer in _layers)
        {
            if (!layer.IsVisible)
            {
                hidden.Add(layer.Id);
            }
        }

        try
        {
            _layerView = PdfiumLayerView.Open(PdfLayers.WithHidden(CurrentBytes(), hidden));
            return _layerView is not null;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    /// <summary>Gets the document as it is now, with unsaved edits written in. Callers hold the PDFium lock.</summary>
    /// <returns>The bytes.</returns>
    private byte[] CurrentBytes()
    {
        if (!HasUnsavedChanges)
        {
            return File.ReadAllBytes(FilePath);
        }

        using var stream = new MemoryStream();
        _ = WriteDocument(_handle, stream, NativeMethods.FPDF_GetSignatureCount(_handle) > 0 ? SaveIncremental : SaveFull);
        return stream.ToArray();
    }
}
