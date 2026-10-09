// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// Turns the codestream components into image channels as PDFium does: the palette applies only with a
/// component mapping, and the channel definitions put colour channels in their association order.
/// </summary>
internal static class JpxChannels
{
    /// <summary>The association value that also means the whole image.</summary>
    private const int WholeImage = 65_535;

    /// <summary>Builds the channels.</summary>
    /// <param name="file">The JP2 boxes.</param>
    /// <param name="geometry">The codestream geometry.</param>
    /// <param name="raw">Whether to ignore the palette, mapping and definitions, as PDFium asks for Indexed images.</param>
    /// <returns>The channels, or <see langword="null"/> when the mapping names missing components.</returns>
    internal static JpxChannel[]? Build(JpxFileInfo file, JpxGeometry geometry, bool raw)
    {
        var components = geometry.Components;
        if (raw)
        {
            return Direct(components);
        }

        var channels = file is { Palette: { } palette, Mappings: { } mappings } ? Mapped(components, palette, mappings) : Direct(components);
        if (channels is not null && file.Definitions is { } definitions)
        {
            Reorder(channels, definitions);
        }

        return channels;
    }

    /// <summary>Makes one channel per component.</summary>
    /// <param name="components">The components.</param>
    /// <returns>The channels.</returns>
    private static JpxChannel[] Direct(JpxComponentInfo[] components)
    {
        var channels = new JpxChannel[components.Length];
        for (var c = 0; c < channels.Length; c++)
        {
            channels[c] = new(c, -1, components[c].Precision, components[c].Signed);
        }

        return channels;
    }

    /// <summary>Makes one channel per palette column through the component mapping.</summary>
    /// <param name="components">The components.</param>
    /// <param name="palette">The palette.</param>
    /// <param name="mappings">The component mapping.</param>
    /// <returns>The channels, or <see langword="null"/> when the mapping is invalid.</returns>
    private static JpxChannel[]? Mapped(JpxComponentInfo[] components, JpxPalette palette, JpxChannelMapping[] mappings)
    {
        if (mappings.Length < palette.Columns)
        {
            return null;
        }

        var channels = new JpxChannel[palette.Columns];
        for (var i = 0; i < channels.Length; i++)
        {
            var (component, throughPalette, column) = mappings[i];
            if (component >= components.Length || column >= palette.Columns)
            {
                return null;
            }

            channels[i] = throughPalette
                ? new(component, column, palette.Precisions[i], palette.Signed[i])
                : new(component, -1, palette.Precisions[i], palette.Signed[i]);
        }

        return channels;
    }

    /// <summary>Swaps colour channels into their association order, as PDFium's does.</summary>
    /// <param name="channels">The channels, reordered in place.</param>
    /// <param name="definitions">The channel definitions.</param>
    private static void Reorder(JpxChannel[] channels, JpxChannelDefinition[] definitions)
    {
        Span<int> indices = definitions.Length <= byte.MaxValue ? stackalloc int[definitions.Length] : new int[definitions.Length];
        for (var i = 0; i < definitions.Length; i++)
        {
            indices[i] = definitions[i].Channel;
        }

        for (var i = 0; i < definitions.Length; i++)
        {
            var channel = indices[i];
            var association = definitions[i].Association;
            if (channel >= channels.Length || association is 0 or WholeImage || association - 1 >= channels.Length)
            {
                continue;
            }

            var target = association - 1;
            if (channel == target || definitions[i].Type != 0)
            {
                continue;
            }

            var saved = channels[channel];
            channels[channel] = channels[target];
            channels[target] = saved;
            RenameLater(indices, i + 1, channel, target);
        }
    }

    /// <summary>Updates the channel numbers of later definitions after two channels swap.</summary>
    /// <param name="indices">The definitions' channel numbers.</param>
    /// <param name="start">The first later definition.</param>
    /// <param name="first">One swapped channel.</param>
    /// <param name="second">The other swapped channel.</param>
    private static void RenameLater(Span<int> indices, int start, int first, int second)
    {
        for (var j = start; j < indices.Length; j++)
        {
            if (indices[j] == first)
            {
                indices[j] = second;
            }
            else if (indices[j] == second)
            {
                indices[j] = first;
            }
        }
    }
}
