// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if !NET11_0_OR_GREATER
namespace System.Runtime.CompilerServices;

/// <summary>The contract of a C# 15 union type: the case value it holds. Provided by .NET 11; polyfilled here for .NET 10.</summary>
internal interface IUnion
{
    /// <summary>Gets the case value, or <see langword="null"/>.</summary>
    object? Value { get; }
}
#endif
