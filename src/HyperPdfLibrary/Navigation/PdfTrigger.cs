// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Navigation;

/// <summary>An additional action (<c>/AA</c> entry): an event and what it runs.</summary>
/// <param name="Event">The event key, for example E (cursor enters), WC (will close) or O (page opened).</param>
/// <param name="Action">The action and its chain.</param>
[DebuggerDisplay("PdfTrigger: {Event}")]
public sealed record PdfTrigger(string Event, PdfActionNode Action);
