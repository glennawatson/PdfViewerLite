// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Headless;
using Avalonia.Threading;

namespace PdfViewerLite.App.Tests;

/// <summary>Pumps the headless dispatcher and renderer until a condition holds.</summary>
internal static class UiWait
{
    /// <summary>The default timeout.</summary>
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    /// <summary>How long to sleep between pumps.</summary>
    private static readonly TimeSpan PumpInterval = TimeSpan.FromMilliseconds(10);

    /// <summary>Runs dispatcher jobs until <paramref name="condition"/> is true or the timeout expires.</summary>
    /// <param name="condition">The condition.</param>
    /// <returns><see langword="true"/> when the condition held.</returns>
    internal static async Task<bool> UntilAsync(Func<bool> condition)
    {
        var start = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(start) < DefaultTimeout)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            if (condition())
            {
                return true;
            }

            await Task.Delay(PumpInterval);
        }

        return condition();
    }
}
