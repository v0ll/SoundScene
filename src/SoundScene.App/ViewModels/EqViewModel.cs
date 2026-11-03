using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using SoundScene.App.Mvvm;
using SoundScene.Core.Audio;

namespace SoundScene.App.ViewModels;

/// <summary>
/// Backs the equalizer window: paired sliders by default, every driver band in
/// the Advanced tab, and the two Dolby feature switches underneath.
/// </summary>
/// <remarks>
/// The band count and range are read from the backend once the window opens,
/// because not every DAX build exposes 20 bands. There is no Apply button.
/// Every edit is written after a short debounce, so writes never overlap and a
/// drag across many bands still costs one write. Dolby stores the curve on
/// Personalize and only plays it there, so the window activates Personalize when
/// it loads - otherwise the user would drag sliders and hear nothing.
/// </remarks>
public sealed class EqViewModel : ObservableObject
{
    private static readonly TimeSpan WriteDelay = TimeSpan.FromMilliseconds(80);

    private readonly IAudioBackend _backend;
    private string _status = "Reading the current curve\u2026";
    private bool _isBusy;
    private bool _isLoading;
    private bool _writing;
    private bool _writePending;
    private int _writeVersion;
    private int _range = GraphicEqualizer.DefaultRange;
    private bool _hasFrequencies;
    private bool _volumeLeveler;
    private bool _surroundVirtualizer;

    public EqViewModel(IAudioBackend backend)
    {
        ArgumentNullException.ThrowIfNull(backend);

        _backend = backend;

        BuildBands(GraphicEqualizer.DefaultLayout);

        ResetCommand = new RelayCommand(Reset);
        ReloadCommand = new AsyncRelayCommand(LoadAsync, ReportError);
    }

    public ObservableCollection<EqBandViewModel> Bands { get; } = [];

    public ObservableCollection<EqGroupViewModel> Groups { get; } = [];

    public ICommand ResetCommand { get; }

    public ICommand ReloadCommand { get; }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    /// <summary>e.g. <c>±12 dB</c>, taken from what the driver reports.</summary>
    public string RangeLabel =>
        "\u00B1" + GraphicEqualizer.ToDecibels(_range).ToString("0.#", CultureInfo.InvariantCulture) + " dB";

    /// <summary>
    /// Axis labels for the simple view, derived from the driver's range.
    /// </summary>
    /// <remarks>
    /// The slider geometry assumes the endpoints sit inside the track, so only
    /// the text changes here; a driver that reports a different range still gets
    /// correct labels instead of a hardcoded ±12.
    /// </remarks>
    public string AxisTopLabel => FormatAxis(GraphicEqualizer.ToDecibels(_range));

    public string AxisUpperMidLabel => FormatAxis(GraphicEqualizer.ToDecibels(_range) / 2);

    public string AxisLowerMidLabel => FormatAxis(-GraphicEqualizer.ToDecibels(_range) / 2);

    public string AxisBottomLabel => FormatAxis(-GraphicEqualizer.ToDecibels(_range));

    /// <summary>Number of columns for the paired sliders.</summary>
    public int GroupColumns => Math.Max(Groups.Count, 1);

    /// <summary>Number of columns for the Advanced tab, one per driver band.</summary>
    public int BandColumns => Math.Max(Bands.Count, 1);

    /// <summary>Explains the pairing without assuming a specific band count.</summary>
    public string SimpleHint =>
        $"Each slider moves a pair of neighbouring bands. The Advanced tab has all {Bands.Count} individually.";

    /// <summary>Describes what the Advanced tab shows on this driver.</summary>
    public string AdvancedHint => _hasFrequencies
        ? $"All {Bands.Count} driver bands at Dolby's own frequencies. Type a level or drag a slider - no Apply needed."
        : $"All {Bands.Count} driver bands. The driver did not expose band frequencies, so they are numbered B1 to B{Bands.Count}.";

    public bool VolumeLeveler
    {
        get => _volumeLeveler;
        set
        {
            if (!SetProperty(ref _volumeLeveler, value))
            {
                return;
            }

            OnPropertyChanged(nameof(VolumeLevelerText));

            if (!_isLoading)
            {
                _ = ApplyFeatureAsync(value, static (backend, enabled, token) => backend.SetVolumeLevelerAsync(enabled, token));
            }
        }
    }

    public bool SurroundVirtualizer
    {
        get => _surroundVirtualizer;
        set
        {
            if (!SetProperty(ref _surroundVirtualizer, value))
            {
                return;
            }

            OnPropertyChanged(nameof(SurroundVirtualizerText));

            if (!_isLoading)
            {
                _ = ApplyFeatureAsync(value, static (backend, enabled, token) => backend.SetSurroundVirtualizerAsync(enabled, token));
            }
        }
    }

    public string VolumeLevelerText => _volumeLeveler ? "On" : "Off";

    public string SurroundVirtualizerText => _surroundVirtualizer ? "On" : "Off";

    public async Task LoadAsync()
    {
        _isLoading = true;
        try
        {
            IsBusy = true;
            Status = "Reading the current curve\u2026";

            var capabilities = await _backend.GetCapabilitiesAsync().ConfigureAwait(true);
            SetRange(capabilities.GeqMaximumRange);

            var layout = GraphicEqualizer.CreateLayout(
                capabilities.BandCount,
                capabilities.BandFrequencies);

            if (layout.BandCount != Bands.Count)
            {
                BuildBands(layout);
            }

            var state = await _backend.GetStateAsync().ConfigureAwait(true);
            SetRange(state.GeqMaximumRange);

            // The curve and the switches belong to Personalize, and Dolby only
            // makes them audible there.
            if (state.ProfileIndex != capabilities.PersonalizeIndex)
            {
                await _backend.SetProfileIndexAsync(capabilities.PersonalizeIndex).ConfigureAwait(true);
                state = await _backend.GetStateAsync().ConfigureAwait(true);
            }

            var levels = await _backend.GetGeqAsync().ConfigureAwait(true);

            // Defensive: if the two probes disagree, believe the actual curve
            // and rebuild to its width so a write cannot be rejected.
            if (levels.Count > 0 && levels.Count != Bands.Count)
            {
                BuildBands(GraphicEqualizer.CreateLayout(levels.Count, capabilities.BandFrequencies));
            }

            for (var i = 0; i < Bands.Count && i < levels.Count; i++)
            {
                Bands[i].SetFromDriver(levels[i]);
            }

            _volumeLeveler = state.VolumeLevelerEnabled;
            _surroundVirtualizer = state.SurroundVirtualizerEnabled;
            OnPropertyChanged(nameof(VolumeLeveler));
            OnPropertyChanged(nameof(SurroundVirtualizer));
            OnPropertyChanged(nameof(VolumeLevelerText));
            OnPropertyChanged(nameof(SurroundVirtualizerText));

            Status = $"Live \u00B7 Personalize is active \u00B7 {Bands.Count} bands \u00B7 {RangeLabel}";
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
        finally
        {
            _isLoading = false;
            IsBusy = false;
        }
    }

    private void BuildBands(EqualizerLayout layout)
    {
        foreach (var band in Bands)
        {
            band.PropertyChanged -= OnBandChanged;
        }

        Bands.Clear();
        Groups.Clear();

        foreach (var band in layout.Bands)
        {
            var viewModel = new EqBandViewModel(band, _range);
            viewModel.PropertyChanged += OnBandChanged;
            Bands.Add(viewModel);
        }

        foreach (var group in layout.Groups)
        {
            Groups.Add(new EqGroupViewModel(group, Bands[group.LowIndex], Bands[group.HighIndex]));
        }

        _hasFrequencies = layout.HasFrequencies;

        OnPropertyChanged(nameof(BandColumns));
        OnPropertyChanged(nameof(GroupColumns));
        OnPropertyChanged(nameof(SimpleHint));
        OnPropertyChanged(nameof(AdvancedHint));
    }

    private void OnBandChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EqBandViewModel.Value))
        {
            QueueWrite();
        }
    }

    private void QueueWrite()
    {
        if (_isLoading)
        {
            return;
        }

        var version = ++_writeVersion;
        _ = WriteAfterDelayAsync(version);
    }

    private async Task WriteAfterDelayAsync(int version)
    {
        await Task.Delay(WriteDelay).ConfigureAwait(true);

        // A newer edit superseded this one, so the newest writes the whole curve.
        if (version != _writeVersion)
        {
            return;
        }

        await WriteAsync().ConfigureAwait(true);
    }

    private async Task WriteAsync()
    {
        if (_writing)
        {
            // The loop below picks this up instead of overlapping two writes.
            _writePending = true;
            return;
        }

        _writing = true;
        try
        {
            do
            {
                _writePending = false;
                var levels = Bands.Select(band => band.RawValue).ToArray();
                await _backend.SetGeqAsync(levels).ConfigureAwait(true);
            }
            while (_writePending);
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
        finally
        {
            _writing = false;
        }
    }

    private async Task ApplyFeatureAsync(
        bool enabled,
        Func<IAudioBackend, bool, CancellationToken, Task> apply)
    {
        try
        {
            await apply(_backend, enabled, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
    }

    private void Reset()
    {
        foreach (var band in Bands)
        {
            band.Value = 0;
        }

        Status = "Flat \u00B7 written to Dolby";
    }

    private void SetRange(int range)
    {
        _range = range > 0 ? range : GraphicEqualizer.DefaultRange;

        foreach (var band in Bands)
        {
            band.SetRange(_range);
        }

        OnPropertyChanged(nameof(RangeLabel));
        OnPropertyChanged(nameof(AxisTopLabel));
        OnPropertyChanged(nameof(AxisUpperMidLabel));
        OnPropertyChanged(nameof(AxisLowerMidLabel));
        OnPropertyChanged(nameof(AxisBottomLabel));
    }

    private static string FormatAxis(double decibels)
    {
        var rounded = Math.Round(decibels, 1, MidpointRounding.AwayFromZero);
        return rounded.ToString("+0.#;-0.#;0", CultureInfo.InvariantCulture) + " dB";
    }

    private void ReportError(Exception exception)
    {
        Status = exception.Message;
    }
}
