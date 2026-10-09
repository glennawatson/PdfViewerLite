// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>A feature a reading report can see in a file.</summary>
public enum PdfConformanceFinding
{
    /// <summary>The file is encrypted. PDF/A forbids encryption in every part.</summary>
    Encryption = 0,

    /// <summary>The file holds JavaScript actions or document scripts. PDF/A forbids them in every part.</summary>
    JavaScript = 1,

    /// <summary>A stream uses the LZW filter. PDF/A parts 1 to 3 forbid it.</summary>
    LzwFilter = 2,

    /// <summary>A font used by a page, form or annotation has no embedded font program (Type 3 fonts excepted).</summary>
    NonEmbeddedFont = 3,

    /// <summary>The file uses a soft mask, constant alpha below 1, a blend mode other than Normal, or a transparency group. PDF/A-1 forbids them.</summary>
    Transparency = 4,

    /// <summary>The file has embedded files. PDF/A-1 forbids them; part 2 allows only PDF/A files.</summary>
    EmbeddedFiles = 5,

    /// <summary>A stream refers to external content (<c>/F</c> external stream, <c>/Ref</c> reference XObject or <c>/OPI</c>). PDF/A forbids it.</summary>
    ExternalContent = 6,

    /// <summary>The pages use device colour and the file has no output intent. PDF/A requires an output intent in that case.</summary>
    MissingOutputIntent = 7,
}
