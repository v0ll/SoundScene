using System.Globalization;
using SoundScene.App.Mvvm;
using SoundScene.Core.Audio;

namespace SoundScene.App.ViewModels;

/// <summary>One slider in the 20-band equalizer.</summary>
public sealed class EqBandViewModel : ObservableObject
{
    private double _value;
    private int _range;

    public EqBandViewModel(int index, double frequency, int range)
    {
        Index = index;
        Frequency = frequency;
        FrequencyLabel = GraphicEqualizer.FormatFrequency(frequency);
        _range = range;
    }

    public int Index { get; }

    public double Frequency { get; }

    /// <summary>Axis label, e.g. <c>1.3k</c>.</summary>
    public string FrequencyLabel { get; }

    public double Minimum => -_range;

    public double Maximum => _range;

    /// <summary>Raw band value in 1/16 dB, as Dolby stores it.</summary>
    public double Value
    {
        get => _value;
        set
        {
            if (SetProperty(ref _value, value))
            {
                OnPropertyChanged(nameof(Decibels));
                OnPropertyChanged(nameof(ValueLabel));
            }
        }
    }

    public double Decibels => GraphicEqualizer.ToDecibels(GraphicEqualizer.Clamp(_value, _range));

    /// <summary>Signed dB readout above the slider.</summary>
    public string ValueLabel =>
        Decibels.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture);

    /// <summary>Value rounded to something Dolby will accept.</summary>
    public int RawValue => GraphicEqualizer.Clamp(_value, _range);

    public void SetValue(int raw) => Value = raw;

    public void SetRange(int range)
    {
        if (range == _range || range <= 0)
        {
            return;
        }

        _range = range;
        OnPropertyChanged(nameof(Minimum));
        OnPropertyChanged(nameof(Maximum));
    }
}
