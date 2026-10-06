using System.Net;
using System.Text.Json;
using CrestApps.OrchardCore.Telnyx.Models;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Telnyx names a suppression direction from its own side of the leg: <c>inbound</c> is what it receives from the party
/// the leg reaches and <c>outbound</c> is what it plays to them. A live call with only "clean the agent's voice" chosen
/// sent <c>outbound</c> on the agent's leg and changed the caller's voice instead, so these pin the corrected mapping and
/// the strength sent with it.
/// </summary>
public sealed class TelnyxNoiseSuppressionServiceTests
{
    [Theory]
    [InlineData(TelnyxNoiseSuppressionLeg.Agent, true, false, "inbound")]
    [InlineData(TelnyxNoiseSuppressionLeg.Agent, false, true, "outbound")]
    [InlineData(TelnyxNoiseSuppressionLeg.Agent, true, true, "both")]
    [InlineData(TelnyxNoiseSuppressionLeg.Agent, false, false, null)]
    [InlineData(TelnyxNoiseSuppressionLeg.Customer, true, false, "outbound")]
    [InlineData(TelnyxNoiseSuppressionLeg.Customer, false, true, "inbound")]
    [InlineData(TelnyxNoiseSuppressionLeg.Customer, true, true, "both")]
    [InlineData(TelnyxNoiseSuppressionLeg.InternalAgent, true, false, "inbound")]
    [InlineData(TelnyxNoiseSuppressionLeg.InternalAgent, true, true, "inbound")]
    [InlineData(TelnyxNoiseSuppressionLeg.InternalAgent, false, true, null)]
    public void GetDirection_CleansTheVoiceEachOptionNames(TelnyxNoiseSuppressionLeg leg, bool agentVoice, bool callerVoice, string expected)
    {
        // Act
        var direction = TelnyxNoiseSuppressionService.GetDirection(leg, agentVoice, callerVoice);

        // Assert
        Assert.Equal(expected, direction);
    }

    [Theory]
    [InlineData(TelnyxNoiseSuppressionEngine.Krisp, TelnyxNoiseSuppressionStrength.Light, "suppression_level", 50.0)]
    [InlineData(TelnyxNoiseSuppressionEngine.Krisp, TelnyxNoiseSuppressionStrength.Balanced, "suppression_level", 70.0)]
    [InlineData(TelnyxNoiseSuppressionEngine.Krisp, TelnyxNoiseSuppressionStrength.Strong, "suppression_level", 100.0)]
    [InlineData(TelnyxNoiseSuppressionEngine.DeepFilterNet, TelnyxNoiseSuppressionStrength.Light, "attenuation_limit", 12)]
    [InlineData(TelnyxNoiseSuppressionEngine.DeepFilterNet, TelnyxNoiseSuppressionStrength.Balanced, "attenuation_limit", 24)]
    [InlineData(TelnyxNoiseSuppressionEngine.DeepFilterNet, TelnyxNoiseSuppressionStrength.Strong, "attenuation_limit", 100)]
    [InlineData(TelnyxNoiseSuppressionEngine.AiCoustics, TelnyxNoiseSuppressionStrength.Light, "enhancement_level", 0.5)]
    [InlineData(TelnyxNoiseSuppressionEngine.AiCoustics, TelnyxNoiseSuppressionStrength.Balanced, "enhancement_level", 0.7)]
    [InlineData(TelnyxNoiseSuppressionEngine.AiCoustics, TelnyxNoiseSuppressionStrength.Strong, "enhancement_level", 1.0)]
    public void BuildEngineConfig_SetsEachEnginesOwnStrength(TelnyxNoiseSuppressionEngine engine, TelnyxNoiseSuppressionStrength strength, string key, object expected)
    {
        // Act
        var config = TelnyxNoiseSuppressionService.BuildEngineConfig(engine, strength);

        // Assert
        var setting = Assert.Single(config);
        Assert.Equal(key, setting.Key);
        Assert.Equal(expected, setting.Value);
    }

    [Theory]
    [InlineData(TelnyxNoiseSuppressionEngine.Denoiser)]
    [InlineData(TelnyxNoiseSuppressionEngine.Off)]
    public void BuildEngineConfig_SendsNoneForAnEngineWithoutAStrength(TelnyxNoiseSuppressionEngine engine)
    {
        // Act
        var config = TelnyxNoiseSuppressionService.BuildEngineConfig(engine, TelnyxNoiseSuppressionStrength.Strong);

        // Assert
        Assert.Null(config);
    }

    [Fact]
    public void BuildEngineConfig_TreatsAnUnknownStrengthAsBalanced()
    {
        // Act
        var config = TelnyxNoiseSuppressionService.BuildEngineConfig(TelnyxNoiseSuppressionEngine.Krisp, (TelnyxNoiseSuppressionStrength)42);

        // Assert
        Assert.Equal(70.0, config["suppression_level"]);
    }

    [Theory]
    [InlineData("Light", TelnyxNoiseSuppressionStrength.Light)]
    [InlineData(" strong ", TelnyxNoiseSuppressionStrength.Strong)]
    [InlineData("Balanced", TelnyxNoiseSuppressionStrength.Balanced)]
    [InlineData("Maximum", TelnyxNoiseSuppressionStrength.Balanced)]
    [InlineData("42", TelnyxNoiseSuppressionStrength.Balanced)]
    [InlineData("", TelnyxNoiseSuppressionStrength.Balanced)]
    [InlineData(null, TelnyxNoiseSuppressionStrength.Balanced)]
    public void NormalizeStrength_ReadsUnknownValuesAsBalanced(string value, TelnyxNoiseSuppressionStrength expected)
    {
        // Act
        var strength = TelnyxNoiseSuppressionService.NormalizeStrength(value);

        // Assert
        Assert.Equal(expected, strength);
    }

    [Fact]
    public async Task ApplyAsync_CleansOnlyTheAgentsVoiceOnTheAgentLegWithTheChosenStrength()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK, """{ "data": { "result": "ok" } }""");
        var service = CreateService(handler, new TelnyxOptions
        {
            NoiseSuppressionEngine = TelnyxNoiseSuppressionEngine.Krisp,
            NoiseSuppressionAgentVoice = true,
            NoiseSuppressionCallerVoice = false,
        });

        // Act
        await service.ApplyAsync("v3:agent-leg", TelnyxNoiseSuppressionLeg.Agent, TestContext.Current.CancellationToken);

        // Assert
        var request = Assert.Single(handler.Requests);
        Assert.EndsWith("/actions/suppression_start", request.Path, StringComparison.Ordinal);

        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("inbound", body.RootElement.GetProperty("direction").GetString());
        Assert.Equal("Krisp", body.RootElement.GetProperty("noise_suppression_engine").GetString());
        Assert.Equal(70, body.RootElement.GetProperty("noise_suppression_engine_config").GetProperty("suppression_level").GetDouble());
    }

    [Fact]
    public async Task ApplyAsync_SendsNoEngineConfigForTheDenoiser()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK, """{ "data": { "result": "ok" } }""");
        var service = CreateService(handler, new TelnyxOptions
        {
            NoiseSuppressionEngine = TelnyxNoiseSuppressionEngine.Denoiser,
            NoiseSuppressionAgentVoice = false,
            NoiseSuppressionCallerVoice = true,
            NoiseSuppressionStrength = TelnyxNoiseSuppressionStrength.Strong,
        });

        // Act
        await service.ApplyAsync("v3:agent-leg", TelnyxNoiseSuppressionLeg.Agent, TestContext.Current.CancellationToken);

        // Assert
        var request = Assert.Single(handler.Requests);

        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("outbound", body.RootElement.GetProperty("direction").GetString());
        Assert.False(body.RootElement.TryGetProperty("noise_suppression_engine_config", out _));
    }

    private static TelnyxNoiseSuppressionService CreateService(HttpMessageHandler handler, TelnyxOptions options)
    {
        options.IsEnabled = true;
        options.ApiKey = "test-api-key";
        options.ConnectionId = "connection-1";
        options.ApiBaseUrl = "https://api.telnyx.com/v2/";

        var monitor = new TestOptionsMonitor<TelnyxOptions>(options);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.telnyx.com/v2/"),
        };
        var apiClient = new TelnyxApiClient(httpClient, monitor, new TelnyxApiRetryPolicy(TimeSpan.Zero), NullLogger<TelnyxApiClient>.Instance);

        return new TelnyxNoiseSuppressionService(apiClient, monitor, NullLogger<TelnyxNoiseSuppressionService>.Instance);
    }
}
