// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Interchange;

/// <summary>A node of the field hierarchy that XFDF and FDF write: one partial name, perhaps a value, and child fields.</summary>
[DebuggerDisplay("InterchangeFieldTree: {PartialName} ({Children.Count} kids)")]
internal sealed class InterchangeFieldTree
{
    /// <summary>The children by partial name.</summary>
    private readonly Dictionary<string, InterchangeFieldTree> _index = [with(StringComparer.Ordinal)];

    /// <summary>Initializes a new instance of the <see cref="InterchangeFieldTree"/> class.</summary>
    /// <param name="partialName">The partial name; empty at the root.</param>
    internal InterchangeFieldTree(string partialName) => PartialName = partialName;

    /// <summary>Gets the partial name.</summary>
    internal string PartialName { get; }

    /// <summary>Gets or sets the field whose full name ends at this node, or <see langword="null"/>.</summary>
    internal PdfInterchangeField? Field { get; set; }

    /// <summary>Gets the children in the order their first field appeared.</summary>
    internal List<InterchangeFieldTree> Children { get; } = [];

    /// <summary>Joins a parent field's fully qualified name and a partial name.</summary>
    /// <param name="prefix">The parent's name; empty at the top.</param>
    /// <param name="partial">The partial name.</param>
    /// <returns>The fully qualified name.</returns>
    internal static string Join(string prefix, string partial)
    {
        if (prefix.Length == 0)
        {
            return partial;
        }

        return partial.Length == 0 ? prefix : $"{prefix}.{partial}";
    }

    /// <summary>Builds the hierarchy of a list of fields by splitting their names at dots.</summary>
    /// <param name="fields">The fields.</param>
    /// <returns>The root; its children are the top level fields.</returns>
    internal static InterchangeFieldTree Build(List<PdfInterchangeField> fields)
    {
        var root = new InterchangeFieldTree(string.Empty);
        foreach (var field in fields)
        {
            var node = root;
            foreach (var part in field.Name.Split('.'))
            {
                node = node.GetOrAddChild(part);
            }

            node.Field = field;
        }

        return root;
    }

    /// <summary>Finds or adds a child.</summary>
    /// <param name="partialName">The child's partial name.</param>
    /// <returns>The child.</returns>
    private InterchangeFieldTree GetOrAddChild(string partialName)
    {
        if (_index.TryGetValue(partialName, out var child))
        {
            return child;
        }

        child = new(partialName);
        _index.Add(partialName, child);
        Children.Add(child);
        return child;
    }
}
