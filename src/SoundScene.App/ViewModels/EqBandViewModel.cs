using System.Globalization;
using SoundScene.App.Mvvm;
using SoundScene.Core.Audio;

namespace SoundScene.App.ViewModels;

/// <summary>One of the driver's graphic-EQ bands.</summary>
/// <remarks>
/// <see cref="Value"/> is the value a hand sets: it snaps to flat near zero so
/// 0 dB is reachable. <see cref="SetFromDriver"/> skips that, because a curve
/// read back from Dolby must be shown as it really is.
/// </remarks>
public sealed class EqBandViewModel : ObservableObject
{
    private double _value;
    private int _range;

    public EqBandViewModel(EqualizerBand band, int range)
    {
        ArgumentNullException.ThrowIfNull(band);

        Index = band.Index;
        Frequency = band.Frequency;
        FrequencyLabel = band.Label;
        _range = range;
    }

    public int Index { get; }

    /// <summary>Null when the driver's tuning XML could not be read.</summary>
    public double? Frequency { get; }

    /// <summary>Axis label, e.g. <c>1.3k</c>, or <c>B7</c> when unlabelled.</summary>
    public string FrequencyLabel { get; }

    public double Minimum => -_range;

    public double Maximum => _range;

    /// <summary>Raw band value in 1/16 dB, as Dolby stores it.</summary>
    public double Value
    {
        get => _value;
        set
        {
            var raw = GraphicEqualizer.Snap(GraphicEqualizer.Clamp(value, _range));
            if (SetProperty(ref _value, raw))
            {
                RaiseReadouts();
            }
        }
    }

    public double Decibels => GraphicEqualizer.ToDecibels((int)_value);

    /// <summary>Editable dB readout, e.g. <c>+3.0</c>.</summary>
    public string ValueText
    {
        get => GraphicEqualizer.FormatDecibels(Decibels);
        set
        {
            if (!TryParseDecibels(value, out var decibels))
            {
                // Not a number: put the old value back instead of guessing.
                OnPropertyChanged();
                return;
            }

            Value = decibels * 16;
        }
    }

    /// <summary>Value rounded to something Dolby will accept.</summary>
    public int RawValue => (int)_value;

    /// <summary>Applies a value read from Dolby without snapping it.</summary>
    public void SetFromDriver(int raw)
    {
        _value = GraphicEqualizer.Clamp(raw, _range);
        RaiseReadouts();
    }

    public void SetRange(int range)
    {
        if (range == _range || range <= 0)
        {
            return;
        }

        _range = range;
        _value = GraphicEqualizer.Clamp(_value, _range);
        OnPropertyChanged(nameof(Minimum));
        OnPropertyChanged(nameof(Maximum));
    }

    /// <summary>Parses typed dB, accepting both <c>3.5</c> and <c>3,5</c>.</summary>
    public static bool TryParseDecibels(string? text, out double decibels)
    {
        decibels = 0;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim().TrimEnd('d', 'B', 'b');
        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.CurrentCulture, out decibels)
               || double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out decibels);
    }

    private void RaiseReadouts()
    {
        OnPropertyChanged(nameof(Decibels));
        OnPropertyChanged(nameof(ValueText));
    }
}
