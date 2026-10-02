// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using System.Runtime.InteropServices;

namespace PdfViewerLite.Ocr.Native;

/// <summary>Finds Tesseract: next to the app first (for the AppImage), then the system's versioned library.</summary>
internal static class TesseractLibraryResolver
{
    /// <summary>The file names tried, most specific first.</summary>
    private static readonly string[] Candidates = GetCandidates();

    /// <summary>Installs the resolver for this assembly. Safe to call more than once.</summary>
    internal static void Install()
    {
        try
        {
            NativeLibrary.SetDllImportResolver(typeof(TesseractLibraryResolver).Assembly, Resolve);
        }
        catch (InvalidOperationException)
        {
            // A resolver is already installed for this assembly.
        }
    }

    /// <summary>Determines whether Tesseract can be loaded.</summary>
    /// <returns><see langword="true"/> when it was found.</returns>
    internal static bool TryLoad() => Load() != 0;

    /// <summary>Gets the library file names for this operating system.</summary>
    /// <returns>The names, most specific first.</returns>
    private static string[] GetCandidates()
    {
        if (OperatingSystem.IsWindows())
        {
            // The UB Mannheim installer, the usual Windows build, puts libtesseract-5.dll in its program folder.
            return ["libtesseract-5.dll", "tesseract53.dll", "tesseract50.dll", @"C:\Program Files\Tesseract-OCR\libtesseract-5.dll"];
        }

        // Homebrew installs outside the default library path: /opt/homebrew on Apple silicon, /usr/local on Intel.
        return OperatingSystem.IsMacOS()
            ? ["libtesseract.5.dylib", "libtesseract.dylib", "/opt/homebrew/lib/libtesseract.5.dylib", "/usr/local/lib/libtesseract.5.dylib"]
            : ["libtesseract.so.5", "libtesseract.so"];
    }

    /// <summary>Resolves the Tesseract library.</summary>
    /// <param name="libraryName">The requested library.</param>
    /// <param name="assembly">The requesting assembly.</param>
    /// <param name="searchPath">The search path flags.</param>
    /// <returns>The library handle, or zero to fall back to the default probing.</returns>
    private static nint Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath) =>
        libraryName == NativeMethods.Library ? Load() : 0;

    /// <summary>Loads the first candidate found.</summary>
    /// <returns>The handle, or zero.</returns>
    private static nint Load()
    {
        foreach (var candidate in Candidates)
        {
            if (NativeLibrary.TryLoad(Path.Combine(AppContext.BaseDirectory, candidate), out var local))
            {
                return local;
            }

            if (NativeLibrary.TryLoad(candidate, out var system))
            {
                return system;
            }
        }

        return 0;
    }
}
