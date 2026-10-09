// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Navigation;

/// <summary>Goes to a page in this document.</summary>
/// <param name="Destination">Where it goes.</param>
[DebuggerDisplay("GoToAction: page {Destination.PageIndex}")]
public sealed record GoToAction(PdfDestination Destination);
