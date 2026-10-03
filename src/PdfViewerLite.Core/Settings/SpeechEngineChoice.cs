// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PdfViewerLite.Core.Settings;

/// <summary>Which voice reads documents aloud.</summary>
public enum SpeechEngineChoice
{
    /// <summary>The natural sounding voice that runs on this computer, downloaded once. Nothing leaves the computer.</summary>
    OnDevice = 0,

    /// <summary>Azure AI Speech with the person's own key; each sentence is sent to their Azure resource.</summary>
    Azure = 1,
}
