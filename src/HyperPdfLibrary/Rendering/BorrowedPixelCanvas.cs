// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>A reusable canvas wrapper whose transient native handle stays thread confined.</summary>
internal sealed class BorrowedPixelCanvas : SKCanvas
{
    /// <summary>Initializes a new instance of the <see cref="BorrowedPixelCanvas"/> class.</summary>
    /// <param name="bitmap">The holder's empty bitmap.</param>
    internal BorrowedPixelCanvas(SKBitmap bitmap)
        : base(bitmap) => Release();

    /// <inheritdoc/>
    public override nint Handle
    {
        get => PixelHandle;
        protected set => PixelHandle = value;
    }

    /// <summary>Gets or sets the transient canvas handle without registering a new wrapper.</summary>
    internal nint PixelHandle { get; set; }

    /// <summary>Releases the transient canvas while keeping this managed wrapper reusable.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Release() => DisposeNative();

    /// <inheritdoc/>
    protected override void DisposeNative()
    {
        if (Handle == 0)
        {
            return;
        }

        try
        {
            base.DisposeNative();
        }
        finally
        {
            Handle = 0;
        }
    }
}
