// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>The arithmetic integer decoders of symbol dictionaries and text regions (T.88 tables 31 and 33).</summary>
internal enum Jbig2IntegerKind
{
    /// <summary>IADT: the strip T delta.</summary>
    StripDelta = 0,

    /// <summary>IAFS: the first S of a strip.</summary>
    FirstS = 1,

    /// <summary>IADS: the S delta between instances.</summary>
    SDelta = 2,

    /// <summary>IAIT: the T offset within a strip.</summary>
    InstanceT = 3,

    /// <summary>IARI: whether an instance is refined.</summary>
    Refine = 4,

    /// <summary>IARDW: the refinement width delta.</summary>
    RefineWidth = 5,

    /// <summary>IARDH: the refinement height delta.</summary>
    RefineHeight = 6,

    /// <summary>IARDX: the refinement X offset.</summary>
    RefineX = 7,

    /// <summary>IARDY: the refinement Y offset.</summary>
    RefineY = 8,

    /// <summary>IADH: the height class delta.</summary>
    HeightDelta = 9,

    /// <summary>IADW: the symbol width delta.</summary>
    WidthDelta = 10,

    /// <summary>IAAI: the number of aggregated instances.</summary>
    AggregateCount = 11,

    /// <summary>IAEX: the export run length.</summary>
    ExportRun = 12,
}
