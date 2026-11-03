using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Drivers;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Conversations;

public sealed class AICallMetricsTests
{
    [Fact]
    public void Percentile_UsesTheNearestRank()
    {
        // Arrange
        var samples = new[] { 100, 900, 300, 500, 700, 200, 400, 600, 800, 1000 };

        // Assert
        Assert.Equal(500, AICallMetrics.Percentile(samples, 50));
        Assert.Equal(900, AICallMetrics.Percentile(samples, 90));
        Assert.Equal(1000, AICallMetrics.Percentile(samples, 95));
        Assert.Null(AICallMetrics.Percentile([], 50));
    }

    [Fact]
    public void ApplyLatencies_PutsThePercentilesOnTheSummary()
    {
        // Arrange
        var summary = new AIVoiceSessionSummary();

        // Act
        AICallMetrics.ApplyLatencies(summary, [800, 1200, 1000], [600, 700, 650], []);

        // Assert
        Assert.Equal(3, summary.LatencySamples);
        Assert.Equal(1000, summary.ResponseLatencyP50Ms);
        Assert.Equal(1200, summary.ResponseLatencyMaxMs);
        Assert.Equal(650, summary.ModelLatencyP50Ms);
        Assert.Null(summary.DetectorWaitP50Ms);
    }

    [Fact]
    public void EstimateCost_AddsTheCarrierMinutesAndTheDeploymentsMinutesAndTokens()
    {
        // Arrange
        var summary = new AIVoiceSessionSummary
        {
            DeploymentName = "realtime",
            SessionDurationMs = 120_000,
            InputAudioTokens = 1_000_000,
            OutputTextTokens = 500_000,
        };
        var settings = new AICostSettings
        {
            TelephonyPerMinute = 0.01m,
            Deployments =
            [
                new AIDeploymentPrice { DeploymentName = "Realtime", AudioInputPerMillion = 32, OutputPerMillion = 16, PerMinute = 0.05m },
            ],
        };

        // Act
        var cost = AICallMetrics.EstimateCost(summary, settings);

        // Assert
        Assert.Equal(0.02m + 0.10m + 32m + 8m, cost);
    }

    [Fact]
    public void EstimateCost_IsUnknownWithoutPrices()
        => Assert.Null(AICallMetrics.EstimateCost(new AIVoiceSessionSummary { SessionDurationMs = 60_000 }, new AICostSettings()));

    [Fact]
    public void Prices_RoundTripAndReportBadLines()
    {
        // Act
        var (prices, errors) = AICostSettingsDisplayDriver.ParsePrices("gpt-realtime | 4 | 16 | 32 | 64 | 0\nbad | x\n\nminute-billed | | | | | 0.06");

        // Assert
        Assert.Equal(2, prices.Count);
        Assert.Equal([2], errors);
        Assert.Equal(64m, prices[0].AudioOutputPerMillion);
        Assert.Equal(0.06m, prices[1].PerMinute);
        Assert.Equal(prices.Count, AICostSettingsDisplayDriver.ParsePrices(AICostSettingsDisplayDriver.FormatPrices(prices)).Prices.Count);
    }

    [Fact]
    public void TheMeter_CountsATurnBasedAnswerWhenItsSpeechStarts()
    {
        // Arrange
        var start = new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc).Ticks;
        var meter = new AIVoiceSessionMeter(measuresCallerSpeech: false);
        meter.Start(start);

        // Act
        meter.CallerLineAnswered(start + TimeSpan.FromSeconds(10).Ticks, modelMs: 700);
        meter.AssistantSpeechStarted(start + TimeSpan.FromSeconds(11.2).Ticks);
        meter.TurnAnswered(900, 600, 250);
        var measured = meter.Measure(start + TimeSpan.FromSeconds(30).Ticks);

        // Assert
        Assert.Equal([1200, 900], measured.ResponseLatenciesMs);
        Assert.Equal([700, 600], measured.ModelLatenciesMs);
        Assert.Equal([250], measured.DetectorWaitsMs);
    }
}
