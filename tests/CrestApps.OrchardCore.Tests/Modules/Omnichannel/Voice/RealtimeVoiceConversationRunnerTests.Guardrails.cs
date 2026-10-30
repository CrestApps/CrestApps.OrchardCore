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
    public async Task ARefusedRequest_StillBeingAnswered_IsTakenOffTheLine_AndAnsweredWithThePlaceholder()
    {
        // Arrange
        var harness = new RealtimeHarness();
        var verdict = RefuseTopics(harness, refused: "crypto");
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;

        var run = harness.RunAsync();
        await WaitUntilAsync(() => harness.Conversation.Unprompted.Count == 1);

        // The caller asks, and the model starts answering before the check comes back.
        harness.Conversation.Queue(new RealtimeConversationEvent { Type = RealtimeConversationEventType.UserTranscript, Text = "Should I put my savings into stocks or crypto?" });
        harness.Conversation.Queue(
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.ResponseStarted },
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.AssistantAudioDelta, Audio = new byte[320] });
        await Task.Delay(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);

        // Act
        verdict.SetResult(Placeholder);
        await WaitUntilAsync(() => harness.Media.Clears >= 1);

        // The rest of the refused reply stays off the line and off the record.
        harness.Conversation.Queue(
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.AssistantAudioDelta, Audio = new byte[320] },
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.AssistantTranscriptDone, Text = "Crypto is risky, but..." },
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.ResponseCompleted });
        await WaitUntilAsync(() => harness.Conversation.Unprompted.Count == 2);

        // Assert
        Assert.DoesNotContain(harness.StoredPrompts, prompt => prompt.Role == ChatRole.Assistant && prompt.Content.StartsWith("Crypto", StringComparison.Ordinal));

        // The placeholder is asked for inside the conversation, with no instructions of its own: asked for as a
        // standalone message, live, the model took it for the start of the call and greeted the caller again.
        Assert.Contains(harness.Conversation.Added, message => message.Role == ChatRole.System && message.Text == RealtimeVoiceConversationRunner.RefusalReplyNote(Placeholder));
        Assert.Equal(string.Empty, harness.Conversation.Unprompted[^1]);
        Assert.Empty(harness.Conversation.Standalone);

        // And it reaches the line and the record.
        QueueResponse(harness, Placeholder);
        await WaitUntilAsync(() => harness.StoredPrompts.Any(prompt => prompt.Role == ChatRole.Assistant && prompt.Content == Placeholder));

        await LetTheCallGoAsync(harness, run);
    }

    [Fact]
    public async Task ARefusedRequest_TheModelHadAlreadyAnswered_IsOnlyNotActedOn()
    {
        // Arrange
        // Live, the check came back after the model had declined in its own words; a placeholder on top of that
        // repeated it.
        var harness = new RealtimeHarness();
        var verdict = RefuseTopics(harness, refused: "crypto");
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;

        var run = harness.RunAsync();
        await WaitUntilAsync(() => harness.Conversation.Unprompted.Count == 1);

        harness.Conversation.Queue(new RealtimeConversationEvent { Type = RealtimeConversationEventType.UserTranscript, Text = "Should I put my savings into stocks or crypto?" });
        QueueResponse(harness, "Not really my turf. I'm all about cars.");
        await WaitUntilAsync(() => harness.StoredPrompts.Any(prompt => prompt.Role == ChatRole.Assistant));

        // Act
        verdict.SetResult(Placeholder);
        await WaitUntilAsync(() => harness.Conversation.Added.Any(message => message.Text == RealtimeVoiceConversationRunner.RefusedRequestNote));
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(harness.Conversation.Unprompted);
        Assert.Equal(0, harness.Media.Clears);

        await LetTheCallGoAsync(harness, run);
    }

    [Fact]
    public async Task ARequestTheGuardrailsAllow_IsLeftToTheModel()
    {
        // Arrange
        var harness = new RealtimeHarness();
        var verdict = RefuseTopics(harness, refused: "crypto");
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;

        var run = harness.RunAsync();
        await WaitUntilAsync(() => harness.Conversation.Unprompted.Count == 1);

        // Act
        harness.Conversation.Queue(new RealtimeConversationEvent { Type = RealtimeConversationEventType.UserTranscript, Text = "When are you open on Saturday?" });
        await WaitUntilAsync(() => harness.StoredPrompts.Any(prompt => prompt.Role == ChatRole.User));
        verdict.SetResult(null);
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(harness.Conversation.Unprompted);
        Assert.Empty(harness.Conversation.Added);

        await LetTheCallGoAsync(harness, run);
    }

    // The profile refuses a topic; the check answers when the test says so, with the placeholder or with nothing.
    private static TaskCompletionSource<string> RefuseTopics(RealtimeHarness harness, string refused)
    {
        var verdict = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        harness.Profile.AlterSettings<AIProfileGuardrailSettings>(settings =>
        {
            settings.CheckCustomer = true;
            settings.OtherTopics = refused;
        });

        harness.Runner.CheckCallerLine = (_, _, _, _) => verdict.Task;

        return verdict;
    }
}
