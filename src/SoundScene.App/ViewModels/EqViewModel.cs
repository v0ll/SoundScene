using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using SoundScene.App.Mvvm;
using SoundScene.Core.Audio;

namespace SoundScene.App.ViewModels;

/// <summary>
/// Backs the graphic-equalizer window.
/// </summary>
/// <remarks>
/// EQ lives on the Personalize profile and Dolby only exposes it while that
/// profile is active, so applying writes the curve and then makes Personalize
/// active. Otherwise the user would drag sliders and hear nothing.
/// </remarks>
public sealed class EqViewModel : ObservableObject
{
    private readonly IAudioBackend _backend;
    private string _status = "Reading the current curve\u2026";
    private bool _isBusy;
    private int _range = GraphicEqualizer.DefaultRange;

    public EqViewModel(IAudioBackend backend)
    {
        ArgumentNullException.ThrowIfNull(backend);

        _backend = backend;

        Bands = new ObservableCollection<EqBandViewModel>(
            GraphicEqualizer.Frequencies.Select(
                (frequency, index) => new EqBandViewModel(index, frequency, _range)));

        ApplyCommand = new AsyncRelayCommand(ApplyAsync, ReportError);
        RevertCommand = new AsyncRelayCommand(LoadAsync, ReportError);
        ResetCommand = new RelayCommand(Reset);
    }

    public ObservableCollection<EqBandViewModel> Bands { get; }

    public ICommand ApplyCommand { get; }

    public ICommand RevertCommand { get; }

    public ICommand ResetCommand { get; }

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

    public async Task LoadAsync()
    {
        try
        {
            IsBusy = true;

            var state = await _backend.GetStateAsync().ConfigureAwait(true);
            SetRange(state.GeqMaximumRange);

            var levels = await _backend.GetGeqAsync().ConfigureAwait(true);
            for (var i = 0; i < Bands.Count && i < levels.Count; i++)
            {
                Bands[i].SetValue(levels[i]);
            }

            Status = $"Loaded the Personalize curve \u00B7 {RangeLabel}";
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ApplyAsync()
    {
        try
        {
            IsBusy = true;

            var levels = Bands.Select(band => band.RawValue).ToArray();
            await _backend.SetGeqAsync(levels).ConfigureAwait(true);
            await _backend.SetProfileAsync(AudioProfile.Personalize).ConfigureAwait(true);

            Status = "Applied. Personalize is active so you can hear it.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Reset()
    {
        foreach (var band in Bands)
        {
            band.SetValue(0);
        }

        Status = "Flat. Press Apply to write it to Dolby.";
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
