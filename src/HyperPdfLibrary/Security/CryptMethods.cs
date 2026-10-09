// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Security;

/// <summary>How a document's strings and streams are encrypted.</summary>
/// <param name="Strings">The method for strings.</param>
/// <param name="Streams">The method for streams.</param>
[DebuggerDisplay("CryptMethods: strings {Strings}, streams {Streams}")]
internal readonly record struct CryptMethods(CryptMethod Strings, CryptMethod Streams);
