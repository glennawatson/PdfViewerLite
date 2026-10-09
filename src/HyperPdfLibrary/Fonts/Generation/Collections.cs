// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Generation;

/// <summary>The supported collections and CMap names.</summary>
internal static class Collections
{
    /// <summary>The CJK script value of Adobe-Japan1.</summary>
    private const byte Japan1 = 1;

    /// <summary>The CJK script value of Adobe-GB1.</summary>
    private const byte GB1 = 2;

    /// <summary>The CJK script value of Adobe-CNS1.</summary>
    private const byte CNS1 = 3;

    /// <summary>The CJK script value of Adobe-Korea1.</summary>
    private const byte Korea1 = 4;

    /// <summary>Gets the collections.</summary>
    internal static Collection[] All { get; } =
    [
        new(
            Japan1,
            "Adobe-Japan1-UCS2",
            [
                "83pv-RKSJ-H", "90msp-RKSJ-H", "90msp-RKSJ-V", "90ms-RKSJ-H", "90ms-RKSJ-V", "90pv-RKSJ-H", "Add-RKSJ-H",
                "Add-RKSJ-V", "EUC-H", "EUC-V", "Ext-RKSJ-H", "Ext-RKSJ-V", "H", "UniJIS-UCS2-H", "UniJIS-UCS2-HW-H",
                "UniJIS-UCS2-HW-V", "UniJIS-UCS2-V", "UniJIS-UTF16-H", "UniJIS-UTF16-V", "UniJIS-UTF32-H", "UniJIS-UTF32-V", "V",
            ]),
        new(
            GB1,
            "Adobe-GB1-UCS2",
            [
                "GB-EUC-H", "GB-EUC-V", "GBK2K-H", "GBK2K-V", "GBK-EUC-H", "GBK-EUC-V", "GBKp-EUC-H", "GBKp-EUC-V",
                "GBpc-EUC-H", "GBpc-EUC-V", "UniGB-UCS2-H", "UniGB-UCS2-V", "UniGB-UTF16-H", "UniGB-UTF16-V",
                "UniGB-UTF32-H", "UniGB-UTF32-V",
            ]),
        new(
            CNS1,
            "Adobe-CNS1-UCS2",
            [
                "B5pc-H", "B5pc-V", "CNS-EUC-H", "CNS-EUC-V", "ETen-B5-H", "ETen-B5-V", "ETenms-B5-H", "ETenms-B5-V",
                "HKscs-B5-H", "HKscs-B5-V", "UniCNS-UCS2-H", "UniCNS-UCS2-V", "UniCNS-UTF16-H", "UniCNS-UTF16-V",
                "UniCNS-UTF32-H", "UniCNS-UTF32-V",
            ]),
        new(
            Korea1,
            "Adobe-Korea1-UCS2",
            [
                "KSC-EUC-H", "KSC-EUC-V", "KSCms-UHC-H", "KSCms-UHC-HW-H", "KSCms-UHC-HW-V", "KSCms-UHC-V", "KSCpc-EUC-H",
                "UniKS-UCS2-H", "UniKS-UCS2-V", "UniKS-UTF16-H", "UniKS-UTF16-V", "UniKS-UTF32-H", "UniKS-UTF32-V",
            ]),
    ];
}
