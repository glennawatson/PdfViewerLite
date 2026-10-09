// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Interchange;

/// <content>The <c>/Annots</c> array.</content>
internal static partial class FdfDictionaryReader
{
    /// <summary>The prefix of the names given to annotations that replies refer to but that have none.</summary>
    private const string GeneratedPrefix = "fdf-";

    /// <summary>Reads the annotations.</summary>
    /// <param name="annotations">The array.</param>
    /// <param name="data">The data.</param>
    private static void ReadAnnotations(PdfArray annotations, PdfInterchangeData data)
    {
        var names = NameRepliedTo(annotations);
        for (var i = 0; i < annotations.Count; i++)
        {
            if (annotations.GetDictionary(i) is not { } dictionary)
            {
                continue;
            }

            var page = dictionary.GetInt32(KnownName.Page);
            if (InterchangeAnnotationReader.Read(dictionary, annotations.GetRaw(i).AsReference(), page, names) is { } annotation)
            {
                data.Annotations.Add(annotation);
            }
        }
    }

    /// <summary>Gives a name to every annotation that is answered but has no name of its own.</summary>
    /// <param name="annotations">The array.</param>
    /// <returns>The names by object number.</returns>
    private static Dictionary<int, string> NameRepliedTo(PdfArray annotations)
    {
        var names = new Dictionary<int, string>();
        for (var i = 0; i < annotations.Count; i++)
        {
            if (annotations.GetDictionary(i) is not { } dictionary || !dictionary.GetRaw(KnownName.IRT).IsReference)
            {
                continue;
            }

            var target = dictionary.GetRaw(KnownName.IRT).AsReference().Number;
            _ = names.TryAdd(target, GeneratedPrefix + target.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return names;
    }
}
