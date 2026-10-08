using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Realtime;
using CrestApps.OrchardCore.AI.Chat.Services;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat;

/// <summary>
/// Which realtime deployments would leave the user untranscribed, so the profile editor can say so.
/// </summary>
/// <remarks>
/// Live, an AI phone call ran on a site with no speech-to-text deployment: the model heard the caller, but the
/// transcript that the call's review and notes read held only the assistant's side. Nothing on screen said why.
/// </remarks>
public sealed class RealtimeTranscriptionCoverageTests
{
    [Fact]
    public async Task ARealtimeDeployment_WithNoSpeechToTextOnItsProvider_IsUntranscribed()
    {
        var untranscribed = await RealtimeTranscriptionCoverage.GetUntranscribedAsync(
            [Realtime("voice", "Azure")],
            _ => ValueTask.FromResult<AIDeployment>(null));

        Assert.Equal(["voice"], untranscribed);
    }

    [Fact]
    public async Task ARealtimeDeployment_WithSpeechToTextOnItsProvider_IsTranscribed()
    {
        var untranscribed = await RealtimeTranscriptionCoverage.GetUntranscribedAsync(
            [Realtime("voice", "Azure")],
            provider => ValueTask.FromResult(new AIDeployment { Name = "stt", ModelName = "speech-model", ClientName = provider }));

        Assert.Empty(untranscribed);
    }

    [Fact]
    public async Task SpeechToTextFromAnotherProvider_DoesNotTranscribe()
    {
        // The orchestrator will not send another provider's model name to this provider's session.
        var untranscribed = await RealtimeTranscriptionCoverage.GetUntranscribedAsync(
            [Realtime("voice", "Azure")],
            _ => ValueTask.FromResult(new AIDeployment { Name = "stt", ModelName = "speech-model", ClientName = "OpenAI" }));

        Assert.Equal(["voice"], untranscribed);
    }

    [Fact]
    public async Task ACascadedDeployment_TranscribesWithItsOwnLeg()
    {
        var cascaded = Realtime("cascade", "Azure");
        cascaded.Put(new CascadedRealtimeMetadata
        {
            SpeechToTextDeploymentName = "stt",
            ChatDeploymentName = "chat",
            TextToSpeechDeploymentName = "tts",
        });

        var untranscribed = await RealtimeTranscriptionCoverage.GetUntranscribedAsync(
            [cascaded],
            _ => ValueTask.FromResult<AIDeployment>(null));

        Assert.Empty(untranscribed);
    }

    [Fact]
    public async Task EachProvider_IsResolvedOnce()
    {
        var resolved = 0;

        await RealtimeTranscriptionCoverage.GetUntranscribedAsync(
            [Realtime("a", "Azure"), Realtime("b", "Azure")],
            _ =>
            {
                resolved++;

                return ValueTask.FromResult<AIDeployment>(null);
            });

        Assert.Equal(1, resolved);
    }

    private static AIDeployment Realtime(string name, string provider)
        => new() { Name = name, ModelName = name, ClientName = provider };
}
