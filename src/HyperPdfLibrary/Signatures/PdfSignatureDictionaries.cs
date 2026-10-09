// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Signatures;

/// <summary>Recognises signature dictionaries and reads their modification-detection entries.</summary>
internal static class PdfSignatureDictionaries
{
    /// <summary>The lowest valid DocMDP permission.</summary>
    private const int MinPermission = 1;

    /// <summary>The highest valid DocMDP permission.</summary>
    private const int MaxPermission = 3;

    /// <summary>The DocMDP permission when /P is missing.</summary>
    private const int DefaultPermission = 2;

    /// <summary>Gets the /Type of a document timestamp's dictionary.</summary>
    internal static ReadOnlySpan<byte> DocTimeStamp => "DocTimeStamp"u8;

    /// <summary>Gets the sub-filter of a document timestamp.</summary>
    internal static ReadOnlySpan<byte> Rfc3161 => "ETSI.RFC3161"u8;

    /// <summary>Gets the FieldMDP transform method.</summary>
    private static ReadOnlySpan<byte> FieldMdpMethod => "FieldMDP"u8;

    /// <summary>Gets the UR3 transform method.</summary>
    private static ReadOnlySpan<byte> Ur3Method => "UR3"u8;

    /// <summary>Gets the /Action key of a lock.</summary>
    private static ReadOnlySpan<byte> ActionKey => "Action"u8;

    /// <summary>Gets the Include action.</summary>
    private static ReadOnlySpan<byte> IncludeAction => "Include"u8;

    /// <summary>Gets the Exclude action.</summary>
    private static ReadOnlySpan<byte> ExcludeAction => "Exclude"u8;

    /// <summary>Determines whether a dictionary is a signature value: /Type /Sig or /DocTimeStamp, or a byte range with contents.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="names">Its name table.</param>
    /// <returns><see langword="true"/> for a signature value.</returns>
    internal static bool IsSignatureValue(PdfDictionary dictionary, PdfNameTable names)
    {
        var type = dictionary.GetName(KnownName.Type);
        if (type.Is(KnownName.Sig) || (!type.IsNone && names.NameEquals(type, DocTimeStamp)))
        {
            return true;
        }

        return dictionary.ContainsKey(KnownName.ByteRange) && dictionary.ContainsKey(KnownName.Contents) && dictionary.ContainsKey(KnownName.Filter);
    }

    /// <summary>Determines whether a signature value is a document timestamp.</summary>
    /// <param name="dictionary">The signature value.</param>
    /// <param name="names">Its name table.</param>
    /// <returns><see langword="true"/> for /Type /DocTimeStamp or /SubFilter /ETSI.RFC3161.</returns>
    internal static bool IsDocumentTimestamp(PdfDictionary dictionary, PdfNameTable names)
    {
        var type = dictionary.GetName(KnownName.Type);
        var subFilter = dictionary.GetName(KnownName.SubFilter);
        return (!type.IsNone && names.NameEquals(type, DocTimeStamp)) || (!subFilter.IsNone && names.NameEquals(subFilter, Rfc3161));
    }

    /// <summary>Reads the transforms in a signature's /Reference array.</summary>
    /// <param name="references">The array, or <see langword="null"/>.</param>
    /// <param name="names">The name table.</param>
    /// <returns>The DocMDP permission, FieldMDP lock and usage rights found.</returns>
    internal static SignatureReferences ReadReferences(PdfArray? references, PdfNameTable names)
    {
        var result = new SignatureReferences(PdfMdpPermission.None, null, null);
        for (var i = 0; references is not null && i < references.Count; i++)
        {
            var reference = references.GetDictionary(i);
            var parameters = reference?.GetDictionary(KnownName.TransformParams);
            if (parameters is null)
            {
                continue;
            }

            result = Apply(result, reference!.GetName(KnownName.TransformMethod), parameters, names);
        }

        return result;
    }

    /// <summary>Reads a /Lock dictionary or FieldMDP transform parameters.</summary>
    /// <param name="dictionary">The dictionary, or <see langword="null"/>.</param>
    /// <param name="names">The name table.</param>
    /// <returns>The lock, or <see langword="null"/>.</returns>
    internal static PdfFieldLock? ReadLock(PdfDictionary? dictionary, PdfNameTable names)
    {
        if (dictionary is null)
        {
            return null;
        }

        var fields = dictionary.GetArray(KnownName.Fields);
        var list = new string[fields?.Count ?? 0];
        for (var i = 0; i < list.Length; i++)
        {
            list[i] = PdfText.Decode(fields!.Get(i).AsStringBytes());
        }

        var permission = dictionary.ContainsKey(KnownName.P) ? ReadPermission(dictionary, 0) : PdfMdpPermission.None;
        return new(ReadAction(dictionary.GetName(names.Intern(ActionKey)), names), list, permission);
    }

    /// <summary>Adds one transform to the transforms read so far; the first DocMDP transform wins, as in PDFium.</summary>
    /// <param name="result">The transforms so far.</param>
    /// <param name="method">The /TransformMethod.</param>
    /// <param name="parameters">The /TransformParams.</param>
    /// <param name="names">The name table.</param>
    /// <returns>The transforms.</returns>
    private static SignatureReferences Apply(SignatureReferences result, PdfName method, PdfDictionary parameters, PdfNameTable names)
    {
        if (method.Is(KnownName.DocMDP))
        {
            return result.DocMdp == PdfMdpPermission.None ? result with { DocMdp = ReadPermission(parameters, DefaultPermission) } : result;
        }

        if (method.IsNone)
        {
            return result;
        }

        if (names.NameEquals(method, FieldMdpMethod))
        {
            return result with { FieldMdp = ReadLock(parameters, names) };
        }

        return names.NameEquals(method, Ur3Method) ? result with { UsageRights = PdfUsageRightsReader.Read(parameters, names) } : result;
    }

    /// <summary>Reads a DocMDP permission, treating values other than 1 to 3 as none, as PDFium does.</summary>
    /// <param name="dictionary">The dictionary holding /P.</param>
    /// <param name="fallback">The value when /P is missing.</param>
    /// <returns>The permission.</returns>
    private static PdfMdpPermission ReadPermission(PdfDictionary dictionary, int fallback)
    {
        var value = dictionary.GetInt32(KnownName.P, fallback);
        return value is >= MinPermission and <= MaxPermission ? (PdfMdpPermission)value : PdfMdpPermission.None;
    }

    /// <summary>Reads a lock's /Action.</summary>
    /// <param name="action">The action name.</param>
    /// <param name="names">The name table.</param>
    /// <returns>The action.</returns>
    private static PdfFieldLockAction ReadAction(PdfName action, PdfNameTable names)
    {
        if (action.Is(KnownName.All))
        {
            return PdfFieldLockAction.All;
        }

        if (action.IsNone)
        {
            return PdfFieldLockAction.None;
        }

        if (names.NameEquals(action, IncludeAction))
        {
            return PdfFieldLockAction.Include;
        }

        return names.NameEquals(action, ExcludeAction) ? PdfFieldLockAction.Exclude : PdfFieldLockAction.None;
    }
}
