// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using System.Runtime.InteropServices;

namespace PdfViewerLite.Speech.Audio;

/// <summary>Finds the system's libpulse-simple.</summary>
internal static class PulseLibraryResolver
{
    /// <summary>The file names tried.</summary>
    private static readonly string[] Candidates = ["libpulse-simple.so.0", "libpulse-simple.so"];

    /// <summary>Whether the resolver was installed.</summary>
    private static int _installed;

    /// <summary>Installs the resolver for this assembly once.</summary>
    internal static void Install()
    {
        if (Interlocked.Exchange(ref _installed, 1) != 0)
        {
            return;
        }

        try
        {
            NativeLibrary.SetDllImportResolver(typeof(PulseLibraryResolver).Assembly, Resolve);
        }
        catch (InvalidOperationException)
        {
            // A resolver is already installed for this assembly.
        }
    }

    /// <summary>Determines whether libpulse-simple can be loaded.</summary>
    /// <returns><see langword="true"/> when found.</returns>
    internal static bool TryLoad() => Load() != 0;

    /// <summary>Resolves libpulse-simple; other libraries, such as ONNX Runtime, use the default probing.</summary>
    /// <param name="libraryName">The requested library.</param>
    /// <param name="assembly">The requesting assembly.</param>
    /// <param name="searchPath">The search path flags.</param>
    /// <returns>The library handle, or zero.</returns>
    private static nint Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath) =>
        libraryName == NativeMethods.Library ? Load() : 0;

    /// <summary>Loads the first candidate found.</summary>
    /// <returns>The handle, or zero.</returns>
    private static nint Load()
    {
        foreach (var candidate in Candidates)
        {
            if (NativeLibrary.TryLoad(candidate, out var handle))
            {
                return handle;
            }
        }

        return 0;
    }
}
