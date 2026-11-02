using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using SoundScene.App.Mvvm;
using SoundScene.Core.Audio;

namespace SoundScene.App.ViewModels;

/// <summary>
/// Backs the equalizer window: ten grouped sliders by default, all twenty bands
/// in the Advanced tab, and the two Dolby feature switches underneath.
/// </summary>
/// <remarks>
/// There is no Apply button. Every edit is written after a short debounce, so
/// writes never overlap and a drag across twenty bands still costs one write.
/// Dolby stores the curve on Personalize and only plays it there, so the window
/// activates Personalize when it loads - otherwise the user would drag sliders
/// and hear nothing.
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
    private bool _volumeLeveler;
    private bool _surroundVirtualizer;

    public EqViewModel(IAudioBackend backend)
    {
        ArgumentNullException.ThrowIfNull(backend);

        _backend = backend;

        Bands = new ObservableCollection<EqBandViewModel>(
            GraphicEqualizer.Frequencies.Select(
                (frequency, index) => new EqBandViewModel(index, frequency, _range)));

        Groups = new ObservableCollection<EqGroupViewModel>(
            GraphicEqualizer.SimpleGroups.Select(
                group => new EqGroupViewModel(group, Bands[group.LowIndex], Bands[group.HighIndex])));

        foreach (var band in Bands)
        {
            band.PropertyChanged += OnBandChanged;
        }

        ResetCommand = new RelayCommand(Reset);
        ReloadCommand = new AsyncRelayCommand(LoadAsync, ReportError);
    }

    public ObservableCollection<EqBandViewModel> Bands { get; }

    public ObservableCollection<EqGroupViewModel> Groups { get; }

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

            var state = await _backend.GetStateAsync().ConfigureAwait(true);
            SetRange(state.GeqMaximumRange);

            // The curve and the switches belong to Personalize, and Dolby only
            // makes them audible there.
            if (state.Profile != AudioProfile.Personalize)
            {
                await _backend.SetProfileAsync(AudioProfile.Personalize).ConfigureAwait(true);
                state = await _backend.GetStateAsync().ConfigureAwait(true);
            }

            var levels = await _backend.GetGeqAsync().ConfigureAwait(true);
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

            Status = $"Live \u00B7 Personalize is active \u00B7 {RangeLabel}";
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
    }

    private void ReportError(Exception exception)
    {
        Status = exception.Message;
    }
}
