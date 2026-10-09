// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Content;

/// <summary>One operator spelling and the operator it names.</summary>
/// <param name="Spelling">The operator as written in a content stream.</param>
/// <param name="Operator">The operator it names.</param>
[DebuggerDisplay("{Spelling} = {Operator}")]
internal readonly record struct OperatorSpelling(string Spelling, ContentOperator Operator);
