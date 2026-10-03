// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using PdfViewerLite.App.Services;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Reading;
using PdfViewerLite.Core.Settings;
using PdfViewerLite.Core.Speech;
using ReactiveUI;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.ViewModels;

/// <summary>
/// Reads a tab's text aloud a sentence at a time, preparing the next sentence while the current one plays so there are
/// no gaps. The sentence being read is softly marked on the page and the view follows it from page to page. Pausing
/// keeps the place; Previous and Next move a sentence at a time.
/// </summary>
[DebuggerDisplay("Open={IsOpen}, Playing={IsPlaying}, Page={SpokenPage}")]
public sealed class ReadAloudViewModel : ReactiveObject, IDisposable
{
    /// <summary>Bytes in a megabyte.</summary>
    private const double BytesPerMegabyte = 1024 * 1024;

    /// <summary>The index of normal speed in <see cref="SpeedValues"/>.</summary>
    private const int NormalSpeedIndex = 2;

    /// <summary>The speeds offered.</summary>
    private static readonly double[] SpeedValues = [0.8, 0.9, 1, 1.1, 1.25, 1.5];

    /// <summary>The owning tab.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>The application services.</summary>
    private readonly AppServices _services;

    /// <summary>The sentences of the page being read.</summary>
    private readonly List<SpeechSentence> _sentences = [];

    /// <summary>The text of the page being read, in reading order.</summary>
    private string _pageText = string.Empty;

    /// <summary>The page character index of each character of <see cref="_pageText"/>; -1 for added spaces.</summary>
    private int[] _map = [];

    /// <summary>The page whose sentences are loaded, or -1.</summary>
    private int _loadedPage = -1;

    /// <summary>The sentence being read on <see cref="SpokenPage"/>.</summary>
    private int _sentence;

    /// <summary>Whether the voices or speed are being set up, rather than chosen.</summary>
    private bool _listing;

    /// <summary>The voices listed in <see cref="VoiceNames"/>.</summary>
    private IReadOnlyList<SpeechVoice>? _listedVoices;

    /// <summary>The page reading should start from, or -1 for the current page.</summary>
    private int _startPage = -1;

    /// <summary>The character reading should start from once the page is loaded; <see cref="int.MaxValue"/> means the last sentence.</summary>
    private int _startChar;

    /// <summary>Stops the reading or download in progress.</summary>
    private CancellationTokenSource? _work;

    /// <summary>Initializes a new instance of the <see cref="ReadAloudViewModel"/> class.</summary>
    /// <param name="owner">The owning tab.</param>
    /// <param name="services">The application services.</param>
    public ReadAloudViewModel(DocumentTabViewModel owner, AppServices services)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(services);
        _owner = owner;
        _services = services;
        SpeedNames = BuildSpeedNames();
        RefreshVoices();
        _listing = true;
        SpeedIndex = NearestSpeed(services.Settings.SpeechSpeed);
        _listing = false;

        PlayPauseCommand = ReactiveCommand.Create(PlayPause);
        NextCommand = ReactiveCommand.Create(() => Step(1));
        PreviousCommand = ReactiveCommand.Create(() => Step(-1));
        CloseCommand = ReactiveCommand.Create(Close);
        ToggleCommand = ReactiveCommand.Create(() => IsOpen = !IsOpen);
        DownloadVoiceCommand = ReactiveCommand.CreateFromTask(DownloadVoiceAsync, this.WhenAnyValue(static vm => vm.IsDownloading).Select(static busy => !busy));
    }

    /// <summary>Gets or sets a value indicating whether the Read Aloud bar is shown. Opening starts reading from the current page.</summary>
    public bool IsOpen
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            _ = this.RaiseAndSetIfChanged(ref field, value);
            if (value)
            {
                Begin();
            }
            else
            {
                Stop();
            }
        }
    }

    /// <summary>Gets a value indicating whether audio is being read.</summary>
    public bool IsPlaying
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets a value indicating whether the voice must be downloaded or set up before reading.</summary>
    public bool NeedsVoice
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets a value indicating whether the on-device voice can be downloaded from the bar.</summary>
    public bool CanDownloadVoice
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets a value indicating whether the voice is downloading.</summary>
    public bool IsDownloading
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets the download progress from 0 to 1.</summary>
    public double DownloadProgress
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets the steady status, for example "Reading page 3 of 12" or "Paused".</summary>
    public string StatusText
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    /// <summary>Gets the label of the play button: "Pause" while reading, otherwise "Play".</summary>
    public string PlayPauseText
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = "Play";

    /// <summary>Gets the voices' names and descriptions, for example "Heart, American English, warm".</summary>
    public IReadOnlyList<string> VoiceNames
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = [];

    /// <summary>Gets or sets the chosen voice's index in <see cref="VoiceNames"/>.</summary>
    public int VoiceIndex
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            _ = this.RaiseAndSetIfChanged(ref field, value);
            OnChoiceChanged();
        }
    }

    /// <summary>Gets the speeds' names.</summary>
    public IReadOnlyList<string> SpeedNames { get; }

    /// <summary>Gets or sets the chosen speed's index in <see cref="SpeedNames"/>.</summary>
    public int SpeedIndex
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            _ = this.RaiseAndSetIfChanged(ref field, value);
            OnChoiceChanged();
        }
    }

    /// <summary>Gets the page of the sentence being read, or -1.</summary>
    public int SpokenPage
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = -1;

    /// <summary>Gets the rectangles of the sentence being read, in page space.</summary>
    public IReadOnlyList<PageRect> SpokenBounds
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = [];

    /// <summary>Gets the command that pauses or carries on reading.</summary>
    public ReactiveCommand<RxVoid, RxVoid> PlayPauseCommand { get; }

    /// <summary>Gets the command that moves to the next sentence.</summary>
    public ReactiveCommand<RxVoid, RxVoid> NextCommand { get; }

    /// <summary>Gets the command that moves back a sentence.</summary>
    public ReactiveCommand<RxVoid, RxVoid> PreviousCommand { get; }

    /// <summary>Gets the command that stops reading and closes the bar.</summary>
    public ReactiveCommand<RxVoid, RxVoid> CloseCommand { get; }

    /// <summary>Gets the command that opens or closes Read Aloud (Ctrl+Shift+Y).</summary>
    public ReactiveCommand<RxVoid, bool> ToggleCommand { get; }

    /// <summary>Gets the command that downloads the on-device voice, once, with the person's go-ahead.</summary>
    public ReactiveCommand<RxVoid, RxVoid> DownloadVoiceCommand { get; }

    /// <summary>Gets the speed chosen.</summary>
    private float Speed => (float)SpeedValues[Math.Clamp(SpeedIndex, 0, SpeedValues.Length - 1)];

    /// <summary>Starts reading at a character on a page, opening the bar.</summary>
    /// <param name="page">The page.</param>
    /// <param name="charIndex">The character to start from; reading begins with the sentence holding it.</param>
    public void StartAt(int page, int charIndex)
    {
        _startPage = Math.Max(0, page);
        _startChar = Math.Max(0, charIndex);
        if (IsOpen)
        {
            Begin();
        }
        else
        {
            IsOpen = true;
        }
    }

    /// <summary>Pauses reading, keeping the place.</summary>
    public void Pause()
    {
        CancelWork();
        IsPlaying = false;
        PlayPauseText = "Play";
        if (IsOpen && !NeedsVoice)
        {
            StatusText = "Paused";
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _work?.Cancel();
        _work?.Dispose();
        _work = null;
    }

    /// <summary>Describes a size in megabytes.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>For example "95 MB".</returns>
    internal static string Megabytes(long bytes) => $"{Math.Max(1, Math.Round(bytes / BytesPerMegabyte)):0} MB";

    /// <summary>Finds the offered speed nearest a remembered one.</summary>
    /// <param name="speed">The speed.</param>
    /// <returns>The index in <see cref="SpeedValues"/>.</returns>
    internal static int NearestSpeed(double speed)
    {
        var nearest = NormalSpeedIndex;
        for (var i = 0; i < SpeedValues.Length; i++)
        {
            if (Math.Abs(SpeedValues[i] - speed) < Math.Abs(SpeedValues[nearest] - speed))
            {
                nearest = i;
            }
        }

        return nearest;
    }

    /// <summary>Gets a page's text in reading order, or as the engine gives it when the layout is unknown.</summary>
    /// <param name="document">The document.</param>
    /// <param name="reading">The reading order, if available.</param>
    /// <param name="page">The page.</param>
    /// <returns>The text and the page character index of each of its characters.</returns>
    internal static (string Text, int[] Map) LoadText(IDocument document, ReadingDocument? reading, int page)
    {
        if (reading is not null)
        {
            var flattened = ReadingDocument.Flatten(reading.GetPage(page), out var map);
            return (flattened, map);
        }

        var text = document.GetText(page, 0, document.GetCharacterCount(page));
        var identity = new int[text.Length];
        for (var i = 0; i < identity.Length; i++)
        {
            identity[i] = i;
        }

        return (text, identity);
    }

    /// <summary>Finds where a page character is in the reading text: the same character, or the nearest one read.</summary>
    /// <param name="map">The reading text's page character indices.</param>
    /// <param name="charIndex">The page character.</param>
    /// <returns>The offset in the reading text.</returns>
    internal static int ReadingOffset(int[] map, int charIndex)
    {
        var best = 0;
        var bestDistance = int.MaxValue;
        for (var i = 0; i < map.Length && bestDistance > 0; i++)
        {
            var distance = map[i] < 0 ? int.MaxValue : Math.Abs(map[i] - charIndex);
            if (distance >= bestDistance)
            {
                continue;
            }

            best = i;
            bestDistance = distance;
        }

        return best;
    }

    /// <summary>Finds the sentence holding a character.</summary>
    /// <param name="sentences">The sentences.</param>
    /// <param name="charIndex">The character.</param>
    /// <returns>The sentence index; the last sentence starting before the character.</returns>
    internal static int SentenceAt(List<SpeechSentence> sentences, int charIndex)
    {
        var found = 0;
        for (var i = 0; i < sentences.Count && sentences[i].Start <= charIndex; i++)
        {
            found = i;
        }

        return found;
    }

    /// <summary>Names the speeds offered.</summary>
    /// <returns>For example "Normal speed" and "1.25× speed".</returns>
    private static string[] BuildSpeedNames()
    {
        var names = new string[SpeedValues.Length];
        for (var i = 0; i < names.Length; i++)
        {
            names[i] = i == NormalSpeedIndex ? "Normal speed" : $"{SpeedValues[i]:0.##}× speed";
        }

        return names;
    }

    /// <summary>Observes a sentence prepared but not read, so a failure in it is not left unobserved.</summary>
    /// <param name="prepared">The prepared sentence.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ObservePrepared(Task<SpeechAudio>? prepared) =>
        prepared?.ContinueWith(static task => _ = task.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);

    /// <summary>Starts reading from the requested place, or the top of the current page.</summary>
    private void Begin()
    {
        CancelWork();
        RefreshVoices();
        SpokenPage = _startPage >= 0 ? _startPage : Math.Max(0, _owner.CurrentPageIndex);
        _startPage = -1;
        _loadedPage = -1;
        _sentence = 0;
        Play();
    }

    /// <summary>Plays or pauses.</summary>
    private void PlayPause()
    {
        if (IsPlaying)
        {
            Pause();
        }
        else
        {
            Play();
        }
    }

    /// <summary>Moves a sentence forwards or back, carrying on reading if it was.</summary>
    /// <param name="delta">+1 or -1.</param>
    private void Step(int delta)
    {
        var wasPlaying = IsPlaying;
        CancelWork();
        if (_loadedPage >= 0)
        {
            var target = _sentence + delta;
            if (target < 0 && SpokenPage > 0)
            {
                SpokenPage--;
                _loadedPage = -1;
                _startChar = int.MaxValue;
            }
            else if (target >= _sentences.Count && SpokenPage < _owner.PageCount - 1)
            {
                SpokenPage++;
                _loadedPage = -1;
                _startChar = 0;
            }
            else
            {
                _sentence = Math.Clamp(target, 0, Math.Max(0, _sentences.Count - 1));
                Highlight();
            }
        }

        if (wasPlaying)
        {
            Play();
        }
    }

    /// <summary>Stops reading and clears the mark.</summary>
    private void Stop()
    {
        CancelWork();
        IsPlaying = false;
        PlayPauseText = "Play";
        SpokenBounds = [];
        SpokenPage = -1;
        _loadedPage = -1;
        StatusText = string.Empty;
    }

    /// <summary>Closes the bar.</summary>
    private void Close() => IsOpen = false;

    /// <summary>Starts the reading loop from the current place.</summary>
    private void Play()
    {
        CancelWork();
        var work = new CancellationTokenSource();
        _work = work;
        _services.ClaimSpeech(this);
        _ = ReadAsync(work);
    }

    /// <summary>Cancels the reading or download in progress.</summary>
    private void CancelWork()
    {
        var work = _work;
        _work = null;
        work?.Cancel();
    }

    /// <summary>Restarts the current sentence with the new voice or speed and remembers the choice.</summary>
    private void OnChoiceChanged()
    {
        if (_listing)
        {
            return;
        }

        var settings = _services.Settings;
        settings.SpeechSpeed = SpeedValues[Math.Clamp(SpeedIndex, 0, SpeedValues.Length - 1)];
        var voices = _services.GetSpeechEngine().Voices;
        if (VoiceIndex >= 0 && VoiceIndex < voices.Count)
        {
            settings.SpeechVoice = voices[VoiceIndex].Id;
        }

        _services.SaveSettings();
        if (IsPlaying)
        {
            Play();
        }
    }

    /// <summary>Lists the voices of the chosen engine and selects the remembered one, when the engine changed.</summary>
    private void RefreshVoices()
    {
        var voices = _services.GetSpeechEngine().Voices;
        if (ReferenceEquals(voices, _listedVoices))
        {
            return;
        }

        _listedVoices = voices;
        var names = new string[voices.Count];
        var selected = 0;
        for (var i = 0; i < voices.Count; i++)
        {
            names[i] = $"{voices[i].Name}, {voices[i].Description}";
            if (voices[i].Id == _services.Settings.SpeechVoice)
            {
                selected = i;
            }
        }

        _listing = true;
        VoiceNames = names;
        VoiceIndex = selected;
        _listing = false;
    }

    /// <summary>Checks that the voice and sound output are ready, explaining what is needed when they are not.</summary>
    /// <param name="engine">The engine.</param>
    /// <returns><see langword="true"/> when reading can start.</returns>
    private bool CheckReady(ISpeechEngine engine)
    {
        if (!engine.IsReady)
        {
            var onDevice = _services.Settings.SpeechEngine == SpeechEngineChoice.OnDevice;
            NeedsVoice = true;
            CanDownloadVoice = onDevice;
            StatusText = onDevice
                ? $"The natural voice runs on this computer and needs a one-time download of about {Megabytes(_services.Speech.MissingBytes)}. What you read is never sent anywhere."
                : "Enter your Azure Speech key and region in Preferences, or choose the voice on this computer.";
            return false;
        }

        NeedsVoice = false;
        CanDownloadVoice = false;
        if (!_services.Audio.IsAvailable)
        {
            StatusText = "No sound output was found. Reading aloud needs PipeWire or PulseAudio.";
            return false;
        }

        return true;
    }

    /// <summary>Reads until the end of the document, a pause or a stop, explaining any failure in the bar.</summary>
    /// <param name="work">Stops the reading.</param>
    /// <returns>A task.</returns>
    private async Task ReadAsync(CancellationTokenSource work)
    {
        var engine = _services.GetSpeechEngine();
        if (!CheckReady(engine) || _owner.TryGetDocument() is not { } document)
        {
            SetPlaying(false);
            return;
        }

        SetPlaying(true);
        try
        {
            await ReadSentencesAsync(document, engine, work.Token).ConfigureAwait(true);
            StatusText = "Finished reading.";
            SpokenBounds = [];
        }
        catch (OperationCanceledException)
        {
            // Paused or stopped: the place is kept.
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or IOException)
        {
            StatusText = $"Reading aloud stopped: {ex.Message}";
        }
        finally
        {
            if (ReferenceEquals(_work, work))
            {
                _work = null;
                SetPlaying(false);
            }
            else if (_work is null)
            {
                SetPlaying(false);
            }

            work.Dispose();
        }
    }

    /// <summary>Reads sentence after sentence, preparing the next while the current one plays.</summary>
    /// <param name="document">The document.</param>
    /// <param name="engine">The engine.</param>
    /// <param name="token">Stops the reading.</param>
    /// <returns>A task that completes at the end of the document.</returns>
    private async Task ReadSentencesAsync(IDocument document, ISpeechEngine engine, CancellationToken token)
    {
        var voice = engine.Voices[Math.Clamp(VoiceIndex, 0, engine.Voices.Count - 1)].Id;
        var speed = Speed;
        Task<SpeechAudio>? prepared = null;
        try
        {
            while (await LoadPageAsync(document).ConfigureAwait(true))
            {
                token.ThrowIfCancellationRequested();
                StatusText = $"Reading page {SpokenPage + 1} of {document.PageCount}";
                var clip = await (prepared ?? engine.SynthesizeAsync(SentenceText(_sentence), voice, speed, token)).ConfigureAwait(true);
                prepared = _sentence + 1 < _sentences.Count ? engine.SynthesizeAsync(SentenceText(_sentence + 1), voice, speed, token) : null;
                token.ThrowIfCancellationRequested();
                Highlight();
                await _services.Audio.PlayAsync(clip, token).ConfigureAwait(true);
                token.ThrowIfCancellationRequested();
                _sentence++;
            }
        }
        finally
        {
            ObservePrepared(prepared);
        }
    }

    /// <summary>Shows whether reading is under way.</summary>
    /// <param name="playing">Whether reading.</param>
    private void SetPlaying(bool playing)
    {
        IsPlaying = playing;
        PlayPauseText = playing ? "Pause" : "Play";
    }

    /// <summary>Loads the sentences of <see cref="SpokenPage"/>, moving past pages without text.</summary>
    /// <param name="document">The document.</param>
    /// <returns><see langword="false"/> at the end of the document.</returns>
    private async Task<bool> LoadPageAsync(IDocument document)
    {
        while (_loadedPage != SpokenPage || _sentence >= _sentences.Count)
        {
            if (_loadedPage == SpokenPage)
            {
                // Finished this page.
                SpokenPage++;
                _startChar = 0;
            }

            if (SpokenPage >= document.PageCount)
            {
                SpokenPage = document.PageCount - 1;
                _loadedPage = -1;
                return false;
            }

            var page = SpokenPage;
            var reading = _owner.GetReadingDocument();
            (_pageText, _map) = await Task.Run(() => LoadText(document, reading, page)).ConfigureAwait(true);
            _sentences.Clear();
            SentenceSplitter.Split(_pageText, _sentences);
            _loadedPage = page;
            _sentence = _startChar == int.MaxValue ? Math.Max(0, _sentences.Count - 1) : SentenceAt(_sentences, ReadingOffset(_map, _startChar));
            _startChar = 0;
            if (_sentences.Count > 0 && page != _owner.CurrentPageIndex)
            {
                _owner.GoToPage(page);
            }
        }

        return true;
    }

    /// <summary>Gets a sentence's text, prepared for the voice.</summary>
    /// <param name="index">The sentence.</param>
    /// <returns>The text.</returns>
    private string SentenceText(int index)
    {
        var sentence = _sentences[index];
        return SentenceSplitter.ToSpeech(_pageText.AsSpan(sentence.Start, sentence.Length));
    }

    /// <summary>Marks the sentence being read.</summary>
    private void Highlight()
    {
        if (_owner.TryGetDocument() is not { } document || _sentence >= _sentences.Count)
        {
            SpokenBounds = [];
            return;
        }

        var sentence = _sentences[_sentence];
        var rects = new List<PageRect>();
        var runs = new List<(int Start, int Count)>();
        ReadingDocument.GetRuns(_map, sentence.Start, sentence.Length, runs);
        foreach (var (start, count) in runs)
        {
            document.GetTextBounds(SpokenPage, start, count, rects);
        }

        SpokenBounds = rects;
    }

    /// <summary>Downloads the on-device voice, then starts reading.</summary>
    /// <returns>A task.</returns>
    private async Task DownloadVoiceAsync()
    {
        CancelWork();
        using var work = new CancellationTokenSource();
        _work = work;
        IsDownloading = true;
        DownloadProgress = 0;
        try
        {
            StatusText = "Downloading the voice…";
            var progress = new Progress<double>(fraction => DownloadProgress = fraction);
            await _services.Speech.DownloadVoice(progress, work.Token).ConfigureAwait(true);
            IsDownloading = false;
            NeedsVoice = false;
            CanDownloadVoice = false;
            if (IsOpen)
            {
                Play();
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "The download stopped. Download Voice carries on where it left off.";
        }
        catch (HttpRequestException ex)
        {
            StatusText = $"The voice could not be downloaded: {ex.Message}";
        }
        catch (IOException ex)
        {
            StatusText = $"The voice could not be saved: {ex.Message}";
        }
        finally
        {
            IsDownloading = false;
            if (ReferenceEquals(_work, work))
            {
                _work = null;
            }
        }
    }
}
