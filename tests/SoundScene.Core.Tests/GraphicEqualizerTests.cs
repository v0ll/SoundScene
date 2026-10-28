using SoundScene.Core.Audio;

namespace SoundScene.Core.Tests;

public class GraphicEqualizerTests
{
    [Fact]
    public void Frequencies_MatchDolbyBandCount()
    {
        Assert.Equal(GraphicEqualizer.BandCount, GraphicEqualizer.Frequencies.Count);
        Assert.Equal(20, GraphicEqualizer.Frequencies.Count);
    }

    [Fact]
    public void Frequencies_AreStrictlyAscending()
    {
        var frequencies = GraphicEqualizer.Frequencies;

        for (var i = 1; i < frequencies.Count; i++)
        {
            Assert.True(
                frequencies[i] > frequencies[i - 1],
                $"Band {i} ({frequencies[i]}) is not above band {i - 1} ({frequencies[i - 1]}).");
        }
    }

    [Fact]
    public void Frequencies_CoverTheAudibleRange()
    {
        Assert.InRange(GraphicEqualizer.Frequencies[0], 20, 60);
        Assert.InRange(GraphicEqualizer.Frequencies[^1], 18000, 22000);
    }

    [Theory]
    [InlineData(47, "47")]
    [InlineData(234, "234")]
    [InlineData(1031, "1k")]
    [InlineData(1313, "1.3k")]
    [InlineData(3000, "3k")]
    [InlineData(11250, "11k")]
    [InlineData(19688, "20k")]
    public void FormatFrequency_ProducesShortAxisLabels(double hertz, string expected)
    {
        Assert.Equal(expected, GraphicEqualizer.FormatFrequency(hertz));
    }

    [Fact]
    public void ToDecibels_MapsDriverUnits()
    {
        Assert.Equal(12.0, GraphicEqualizer.ToDecibels(GraphicEqualizer.DefaultRange));
        Assert.Equal(-12.0, GraphicEqualizer.ToDecibels(-GraphicEqualizer.DefaultRange));
        Assert.Equal(3.0, GraphicEqualizer.ToDecibels(48));
    }

    [Theory]
    [InlineData(500, 192)]
    [InlineData(-500, -192)]
    [InlineData(47.4, 47)]
    [InlineData(47.6, 48)]
    [InlineData(-0.4, 0)]
    public void Clamp_KeepsValuesInsideTheDriverRange(double input, int expected)
    {
        Assert.Equal(expected, GraphicEqualizer.Clamp(input, GraphicEqualizer.DefaultRange));
    }

    [Fact]
    public void Flat_IsTwentyZeros()
    {
        var flat = GraphicEqualizer.Flat();

        Assert.Equal(GraphicEqualizer.BandCount, flat.Length);
        Assert.All(flat, value => Assert.Equal(0, value));
    }
}
