// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Programs;

/// <summary>Limits shared by the CFF DICT reader and the charstring interpreters.</summary>
internal static class CharstringLimits
{
    /// <summary>The deepest argument stack: 48 in Type 2 and CFF DICTs, which also covers Type 1's 24.</summary>
    internal const int StackDepth = 48;

    /// <summary>The size of the Type 2 transient array.</summary>
    internal const int TransientSize = 32;

    /// <summary>The scratch space an interpreter needs: the stack and the transient array.</summary>
    internal const int ScratchSize = StackDepth + TransientSize;

    /// <summary>The deepest subroutine nesting.</summary>
    internal const int MaxSubrDepth = 10;
}
