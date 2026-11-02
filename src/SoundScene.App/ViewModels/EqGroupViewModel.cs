using System.ComponentModel;
using SoundScene.App.Mvvm;
using SoundScene.Core.Audio;

namespace SoundScene.App.ViewModels;

/// <summary>
/// One of the ten sliders in the simple equalizer view. It drives two adjacent
/// driver bands at once, so the common case stays readable while the Advanced
/// tab still exposes everything.
/// </summary>
public sealed class EqGroupViewModel : ObservableObject
{
    private readonly EqBandViewModel _low;
    private readonly EqBandViewModel _high;
    private bool _syncing;

    public EqGroupViewModel(EqualizerGroup group, EqBandViewModel low, EqBandViewModel high)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(low);
        ArgumentNullException.ThrowIfNull(high);

        Group = group;
        _low = low;
        _high = high;

        _low.PropertyChanged += OnBandChanged;
        _high.PropertyChanged += OnBandChanged;
    }

    public EqualizerGroup Group { get; }

    /// <summary>Axis label, e.g. <c>555</c>.</summary>
    public string Label => Group.Label;

    /// <summary>What the slider really moves, e.g. <c>47 Hz + 141 Hz</c>.</summary>
    public string Members => Group.Members;

    public double Minimum => _low.Minimum;

    public double Maximum => _low.Maximum;

    /// <summary>Average of the two bands, in 1/16 dB.</summary>
    public double Value
    {
        get => (_low.Value + _high.Value) / 2;
        set
        {
            if (_syncing)
            {
                return;
            }

            _syncing = true;
            try
            {
                _low.Value = value;
                _high.Value = value;
            }
            finally
            {
                _syncing = false;
            }

            RaiseReadouts();
        }
    }

    public string ValueText
    {
        get => GraphicEqualizer.FormatDecibels(Value / 16);
        set
        {
            if (EqBandViewModel.TryParseDecibels(value, out var decibels))
            {
                Value = decibels * 16;
            }
            else
            {
                OnPropertyChanged();
            }
        }
    }

    private void OnBandChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EqBandViewModel.Value))
        {
            RaiseReadouts();
        }
        else if (e.PropertyName is nameof(EqBandViewModel.Minimum) or nameof(EqBandViewModel.Maximum))
        {
            OnPropertyChanged(nameof(Minimum));
            OnPropertyChanged(nameof(Maximum));
        }
    }

    private void RaiseReadouts()
    {
        OnPropertyChanged(nameof(Value));
        OnPropertyChanged(nameof(ValueText));
    }
}
