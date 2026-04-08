using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.Tests.Core.Models;

public sealed class DefaultAIOptionsTests
{
    [Fact]
    public void Normalize_WhenAbsoluteMaximumIsNotPositive_ResetsToDefaultAndClampsMaximum()
    {
        var options = new DefaultAIOptions
        {
            MaximumIterationsPerRequest = 150,
            AbsoluteMaximumIterationsPerRequest = 0,
        };

        options.Normalize();

        Assert.Equal(100, options.AbsoluteMaximumIterationsPerRequest);
        Assert.Equal(100, options.MaximumIterationsPerRequest);
    }

    [Fact]
    public void Normalize_WhenMaximumIterationsIsNotPositive_ResetsToDefault()
    {
        var options = new DefaultAIOptions
        {
            MaximumIterationsPerRequest = 0,
            AbsoluteMaximumIterationsPerRequest = 25,
        };

        options.Normalize();

        Assert.Equal(10, options.MaximumIterationsPerRequest);
        Assert.Equal(25, options.AbsoluteMaximumIterationsPerRequest);
    }

    [Fact]
    public void ApplySiteOverrides_WhenOverrideValuesExceedAbsoluteMaximum_ClampsResult()
    {
        var options = new DefaultAIOptions
        {
            MaximumIterationsPerRequest = 8,
            AbsoluteMaximumIterationsPerRequest = 12,
            EnableDistributedCaching = true,
            EnableOpenTelemetry = false,
        };

        var result = options.ApplySiteOverrides(new GeneralAIOptions
        {
            OverrideMaximumIterationsPerRequest = true,
            MaximumIterationsPerRequest = 50,
            OverrideEnableDistributedCaching = true,
            EnableDistributedCaching = false,
            OverrideEnableOpenTelemetry = true,
            EnableOpenTelemetry = true,
        });

        Assert.Equal(12, result.MaximumIterationsPerRequest);
        Assert.Equal(12, result.AbsoluteMaximumIterationsPerRequest);
        Assert.False(result.EnableDistributedCaching);
        Assert.True(result.EnableOpenTelemetry);
    }

    [Fact]
    public void ApplySiteOverrides_WhenOverridesAreDisabled_PreservesAppSettingsValues()
    {
        var options = new DefaultAIOptions
        {
            MaximumIterationsPerRequest = 7,
            AbsoluteMaximumIterationsPerRequest = 20,
            EnableDistributedCaching = false,
            EnableOpenTelemetry = true,
        };

        var result = options.ApplySiteOverrides(new GeneralAIOptions
        {
            OverrideMaximumIterationsPerRequest = false,
            MaximumIterationsPerRequest = 15,
            OverrideEnableDistributedCaching = false,
            EnableDistributedCaching = true,
            OverrideEnableOpenTelemetry = false,
            EnableOpenTelemetry = false,
        });

        Assert.Equal(7, result.MaximumIterationsPerRequest);
        Assert.Equal(20, result.AbsoluteMaximumIterationsPerRequest);
        Assert.False(result.EnableDistributedCaching);
        Assert.True(result.EnableOpenTelemetry);
    }
}
