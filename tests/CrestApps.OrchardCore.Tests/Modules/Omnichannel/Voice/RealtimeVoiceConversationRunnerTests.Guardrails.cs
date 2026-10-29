using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Realtime;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// A live call whose profile refuses topics: what the caller says is checked as it is transcribed.
/// </summary>
/// <remarks>
/// Until this, the guardrails covered web chat, texts and turn-based calls, and a realtime call answered whatever it
/// was asked.
/// </remarks>
public sealed partial class RealtimeVoiceConversationRunnerTests
{
    private const string Placeholder = "I'm sorry, I can't help with that.";

    [Fact]
    public async Task ARefusedRequest_IsTakenOffTheLine_AndAnsweredWithThePlaceholder()
    {
        // Arrange
        var harness = new RealtimeHarness();
        RefuseTopics(harness, refused: "a competitor");
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;

        var run = harness.RunAsync();
        await WaitUntilAsync(() => harness.Conversation.Unprompted.Count == 1);

        // Act
        harness.Conversation.Queue(new RealtimeConversationEvent { Type = RealtimeConversationEventType.UserTranscript, Text = "What do you think of a competitor's prices?" });
        await WaitUntilAsync(() => harness.Conversation.Standalone.Count == 1);

        // Assert
        // The model is told not to act on it, and asked for the placeholder under the session's own instructions.
        Assert.Contains(harness.Conversation.Added, message => message.Role == ChatRole.System && message.Text == RealtimeVoiceConversationRunner.RefusedRequestNote);
        Assert.EndsWith(RealtimeVoiceConversationRunner.RefusalInstructions(Placeholder), harness.Conversation.Standalone[0], StringComparison.Ordinal);

        // What the model had started saying stops reaching the caller.
        Assert.True(harness.Media.Clears >= 1);

        // The placeholder itself is let through and kept on the record.
        QueueResponse(harness, Placeholder);
        await WaitUntilAsync(() => harness.StoredPrompts.Any(prompt => prompt.Role == ChatRole.Assistant && prompt.Content == Placeholder));

        await LetTheCallGoAsync(harness, run);
    }

    [Fact]
    public async Task ARequestTheGuardrailsAllow_IsLeftToTheModel()
    {
        // Arrange
        var harness = new RealtimeHarness();
        RefuseTopics(harness, refused: "a competitor");
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;

        var run = harness.RunAsync();
        await WaitUntilAsync(() => harness.Conversation.Unprompted.Count == 1);

        // Act
        harness.Conversation.Queue(new RealtimeConversationEvent { Type = RealtimeConversationEventType.UserTranscript, Text = "When are you open on Saturday?" });
        await WaitUntilAsync(() => harness.StoredPrompts.Any(prompt => prompt.Role == ChatRole.User));
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(harness.Conversation.Standalone);
        Assert.DoesNotContain(harness.Conversation.Added, message => message.Text == RealtimeVoiceConversationRunner.RefusedRequestNote);

        await LetTheCallGoAsync(harness, run);
    }

    // The profile refuses a topic, and the check refuses any line that mentions it.
    private static void RefuseTopics(RealtimeHarness harness, string refused)
    {
        harness.Profile.AlterSettings<AIProfileGuardrailSettings>(settings =>
        {
            settings.CheckCustomer = true;
            settings.OtherTopics = refused;
        });

        harness.Runner.CheckCallerLine = (_, line, _, _) =>
            Task.FromResult(line.Contains(refused, StringComparison.OrdinalIgnoreCase) ? Placeholder : null);
    }
}
