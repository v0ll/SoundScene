using SoundScene.Core.Backends.Dolby;

namespace SoundScene.Core.Tests;

public class DaxRequirementsTests
{
    [Fact]
    public void IsReady_RequiresEveryItem()
    {
        var requirements = new DaxRequirements(
        [
            new DaxRequirement("Dolby Access", true, "Version 3"),
            new DaxRequirement("Dolby DAX service", false, "Not installed"),
        ]);

        Assert.False(requirements.IsReady);
    }

    [Fact]
    public void Summary_NamesTheFirstMissingItem()
    {
        var requirements = new DaxRequirements(
        [
            new DaxRequirement("Dolby Access", false, "Not installed"),
            new DaxRequirement("Dolby DAX service", false, "Not installed"),
        ]);

        Assert.Equal("Dolby Access is missing", requirements.Summary);
    }

    [Fact]
    public void Summary_IsReadyWhenEverythingPasses()
    {
        var requirements = new DaxRequirements(
        [
            new DaxRequirement("Dolby Access", true, "Version 3"),
        ]);

        Assert.True(requirements.IsReady);
        Assert.Equal("Dolby is ready", requirements.Summary);
    }

    [Fact]
    public void ToText_MarksEachItem()
    {
        var requirements = new DaxRequirements(
        [
            new DaxRequirement("Dolby Access", true, "Version 3"),
            new DaxRequirement("Dolby DAX service", false, "Not installed"),
        ]);

        var text = requirements.ToText();

        Assert.Contains("[ok] Dolby Access - Version 3", text);
        Assert.Contains("[!!] Dolby DAX service - Not installed", text);
    }
}
