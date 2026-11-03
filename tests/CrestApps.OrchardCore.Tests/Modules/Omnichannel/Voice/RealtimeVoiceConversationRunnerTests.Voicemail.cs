using CrestApps.Core.AI.Realtime;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// A live call answered by a voicemail or answering machine, as the provider reports it.
/// </summary>
/// <remarks>
/// Live, the message a customer's voicemail kept was the end of the assistant's opening line. The provider had said
/// a machine answered, the activity was marked, and the session holding the call in another request never heard of
/// it: the model treated the greeting as a person, and the call was still concluded as having left a message.
/// </remarks>
public sealed partial class RealtimeVoiceConversationRunnerTests
{
    private const string VoicemailMessage = "Hi Amani, this is Alex from Contoso Motors. Sorry we missed you; we will try you again soon.";

    [Fact]
    public void VoicemailPrompt_QuotesTheOpeningLine_SoTheMessageKeepsTheCallsLanguage()
    {
        // The message is asked for without the conversation; live, a Greek call's message came in English.
        var prompt = RealtimeVoiceConversationRunner.VoicemailPrompt(" Καλησπέρα σας, καλώ από το Studio Athena. ");

        Assert.StartsWith(VoiceCallGuidance.LeavingAVoicemail, prompt, StringComparison.Ordinal);
        Assert.EndsWith("\"Καλησπέρα σας, καλώ από το Studio Athena.\"", prompt, StringComparison.Ordinal);
        Assert.Equal(VoiceCallGuidance.LeavingAVoicemail, RealtimeVoiceConversationRunner.VoicemailPrompt(null));

        // Quoted for its language only: the first line can be the model's reply to the greeting, which the message
        // must not carry on from.
        Assert.Contains("not as something to answer or carry on from", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheVoicemailMessage_QuotesTheFirstLineWithWords_SaidBeforeTheMachineWasReported()
    {
        // Arrange
        var harness = new RealtimeHarness();
        harness.UseEndCallTurn();
        using var machine = new CancellationTokenSource();
        using var greetingEnded = new CancellationTokenSource();
        harness.AnsweredByMachine = machine.Token;
        harness.MachineGreetingEnded = greetingEnded.Token;
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;

        var run = harness.RunAsync();
        await WaitUntilAsync(() => harness.Conversation.Unprompted.Count == 1);

        // The opening response is cut off before it has any words, and the next one is the call's first real line.
        QueueResponse(harness, " ");
        QueueResponse(harness, "Καλησπέρα σας, καλώ από το Studio Athena.");
        await WaitUntilAsync(() => harness.StoredPrompts.Any(prompt => prompt.Role == ChatRole.Assistant));

        // Act
        await machine.CancelAsync();
        await WaitUntilAsync(() => harness.Media.Clears == 1);
        await greetingEnded.CancelAsync();
        await WaitUntilAsync(() => harness.Conversation.Standalone.Count == 1);

        // Assert
        // The blank line did not take the place of the one that tells the message which language to leave it in.
        Assert.EndsWith("\"Καλησπέρα σας, καλώ από το Studio Athena.\"", harness.Conversation.Standalone[0], StringComparison.Ordinal);

        await LetTheCallGoAsync(harness, run);
    }

    [Fact]
    public async Task WhenTheProviderReportsAMachine_TheModelsRepliesToTheGreetingAreKeptOffTheLine()
    {
        // Arrange
        var harness = new RealtimeHarness();
        harness.UseEndCallTurn();
        using var machine = new CancellationTokenSource();
        harness.AnsweredByMachine = machine.Token;
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;

        var run = harness.RunAsync();
        await WaitUntilAsync(() => harness.Conversation.Unprompted.Count == 1);

        // Act
        await machine.CancelAsync();
        await WaitUntilAsync(() => harness.Media.Clears == 1);

        // The model answers the greeting as though a person had said it.
        QueueResponse(harness, "Hi, is this Amani? Is now a good time to talk?");
        await Task.Delay(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);

        // Assert
        // Nothing of it reaches the recording, and nothing of it is on the record of the call.
        Assert.Empty(harness.Media.WrittenAudio);
        Assert.DoesNotContain(harness.StoredPrompts, prompt => prompt.Role == ChatRole.Assistant);

        // And nothing on the line may interrupt the message that is to come.
        Assert.False(harness.Conversation.TurnDetectionUpdates[^1].AllowInterruption);

        await LetTheCallGoAsync(harness, run);
    }

    [Fact]
    public async Task OnceTheGreetingEnds_OneMessageIsLeft_AndTheCallIsEndedOnVoicemail()
    {
        // Arrange
        var harness = new RealtimeHarness();
        var endCall = harness.UseEndCallTurn();
        using var machine = new CancellationTokenSource();
        using var greetingEnded = new CancellationTokenSource();
        harness.AnsweredByMachine = machine.Token;
        harness.MachineGreetingEnded = greetingEnded.Token;
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;

        var run = harness.RunAsync();
        await WaitUntilAsync(() => harness.Conversation.Unprompted.Count == 1);
        await machine.CancelAsync();
        await WaitUntilAsync(() => harness.Media.Clears == 1);

        // Act
        await greetingEnded.CancelAsync();
        await WaitUntilAsync(() => harness.Conversation.Unprompted.Count == 2);

        // Assert
        // Asked for with the instruction both paths share, under the session's own instructions so it comes in the
        // persona, voice and language the call has been held in.
        Assert.Contains(VoiceCallGuidance.LeavingAVoicemail, harness.Conversation.Unprompted[^1], StringComparison.Ordinal);
        Assert.Contains("## Right now", harness.Conversation.Unprompted[^1], StringComparison.Ordinal);
        Assert.False(endCall.EndCallRequested);

        // As a standalone message: on live calls, asked for inside the conversation, the model answered the greeting
        // it had just heard, or called a tool and said nothing.
        Assert.Equal(harness.Conversation.Unprompted[^1], Assert.Single(harness.Conversation.Standalone));

        // The model says it, and it is the one thing the machine hears.
        QueueResponse(harness, VoicemailMessage);
        await WaitUntilAsync(() => endCall.EndCallRequested);

        Assert.True(endCall.ReachedVoicemail);
        Assert.NotEmpty(harness.Media.WrittenAudio);
        Assert.Equal(VoicemailMessage, Assert.Single(harness.StoredPrompts, prompt => prompt.Role == ChatRole.Assistant).Content);

        // The line is closed once the message has played, with no person's moment to answer it, and the message
        // was asked for once.
        var completed = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));

        Assert.Same(run, completed);
        Assert.True(await run);
        Assert.Equal(1, harness.Conversation.Unprompted.Count(instructions => instructions.Contains(VoiceCallGuidance.LeavingAVoicemail, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task WhenTheProviderNeverHearsTheGreetingEnd_TheMessageIsLeftOnceTheLineHasBeenQuiet()
    {
        // Arrange
        // Standard detection reports the machine and nothing more. The greeting is then given until the line has
        // been quiet long enough for the tone to have sounded, measured from the last the machine was heard.
        var harness = new RealtimeHarness();
        harness.UseEndCallTurn();
        using var machine = new CancellationTokenSource();
        harness.AnsweredByMachine = machine.Token;
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;

        var run = harness.RunAsync();
        await WaitUntilAsync(() => harness.Conversation.Unprompted.Count == 1);
        await machine.CancelAsync();
        await WaitUntilAsync(() => harness.Media.Clears == 1);

        // The greeting is still going: the provider hears it speak two seconds in, so two seconds after that the
        // line has not been quiet for long enough.
        harness.UtcNow = harness.UtcNow.AddSeconds(2);
        harness.Conversation.Queue(new RealtimeConversationEvent { Type = RealtimeConversationEventType.UserSpeechStarted });
        await Task.Delay(TimeSpan.FromMilliseconds(400), TestContext.Current.CancellationToken);
        harness.UtcNow = harness.UtcNow.AddSeconds(2);
        await Task.Delay(TimeSpan.FromMilliseconds(400), TestContext.Current.CancellationToken);

        Assert.Single(harness.Conversation.Unprompted);

        // Act
        // The tone has sounded and the recording is running.
        harness.UtcNow = harness.UtcNow.Add(RealtimeVoiceConversationRunner.VoicemailGreetingQuietWait);
        await WaitUntilAsync(() => harness.Conversation.Unprompted.Count == 2);

        // Assert
        Assert.Contains(VoiceCallGuidance.LeavingAVoicemail, harness.Conversation.Unprompted[^1], StringComparison.Ordinal);

        await LetTheCallGoAsync(harness, run);
    }

    [Fact]
    public async Task TheQuietBeforeTheMessage_IsMeasuredFromTheGreetingHeardOnTheLine()
    {
        // Arrange
        // The provider does not report every word of a greeting; the line carries all of it.
        var harness = new RealtimeHarness();
        harness.UseEndCallTurn();
        using var machine = new CancellationTokenSource();
        harness.AnsweredByMachine = machine.Token;
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;

        var run = harness.RunAsync();
        await WaitUntilAsync(() => harness.Conversation.Unprompted.Count == 1);
        await machine.CancelAsync();
        await WaitUntilAsync(() => harness.Media.Clears == 1);

        // Two seconds in, the greeting is heard on the line; two seconds after that it has not been quiet for long.
        harness.UtcNow = harness.UtcNow.AddSeconds(2);

        for (var i = 0; i < 10; i++)
        {
            harness.Media.QueueCallerAudio(LineTone(-20, i));
        }

        await WaitUntilAsync(() => harness.Conversation.SentAudio.Count >= 10);
        harness.UtcNow = harness.UtcNow.AddSeconds(2);
        await Task.Delay(TimeSpan.FromMilliseconds(400), TestContext.Current.CancellationToken);

        Assert.Single(harness.Conversation.Unprompted);

        // Act
        harness.UtcNow = harness.UtcNow.Add(RealtimeVoiceConversationRunner.VoicemailGreetingQuietWait);
        await WaitUntilAsync(() => harness.Conversation.Unprompted.Count == 2);

        // Assert
        Assert.Contains(VoiceCallGuidance.LeavingAVoicemail, harness.Conversation.Unprompted[^1], StringComparison.Ordinal);

        await LetTheCallGoAsync(harness, run);
    }

    [Fact]
    public async Task WhenNoMessageComes_TheCallIsStillEnded_RatherThanLeftRecording()
    {
        // Arrange
        var harness = new RealtimeHarness();
        var endCall = harness.UseEndCallTurn();
        using var machine = new CancellationTokenSource();
        using var greetingEnded = new CancellationTokenSource();
        harness.AnsweredByMachine = machine.Token;
        harness.MachineGreetingEnded = greetingEnded.Token;
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;

        var run = harness.RunAsync();
        await WaitUntilAsync(() => harness.Conversation.Unprompted.Count == 1);
        await machine.CancelAsync();
        await WaitUntilAsync(() => harness.Media.Clears == 1);
        await greetingEnded.CancelAsync();
        await WaitUntilAsync(() => harness.Conversation.Unprompted.Count == 2);

        // Act
        // The model never begins the message, however long it is given: it is asked once more, then given up on.
        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        harness.UtcNow = harness.UtcNow.Add(RealtimeVoiceConversationRunner.VoicemailMessageStartWait + TimeSpan.FromMilliseconds(100));
        await WaitUntilAsync(() => harness.Conversation.Unprompted.Count == 3);
        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        harness.UtcNow = harness.UtcNow.Add(RealtimeVoiceConversationRunner.VoicemailMessageStartWait + TimeSpan.FromMilliseconds(100));
        await WaitUntilAsync(() => endCall.EndCallRequested);

        // Assert
        Assert.True(endCall.ReachedVoicemail);
        Assert.Empty(harness.Media.WrittenAudio);

        var completed = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));

        Assert.Same(run, completed);
        Assert.True(await run);
    }

    [Fact]
    public async Task NothingAfterTheMessage_ReachesTheRecording_OrTakesTheCallBackFromAMachine()
    {
        // Arrange
        // A person who speaks after the goodbye gets the call back. A machine's tone, or the start of its recording,
        // is reported as speech too, and must not: the recording would run until the machine gave up.
        var harness = new RealtimeHarness();
        var endCall = harness.UseEndCallTurn();
        using var machine = new CancellationTokenSource();
        using var greetingEnded = new CancellationTokenSource();
        harness.AnsweredByMachine = machine.Token;
        harness.MachineGreetingEnded = greetingEnded.Token;
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;

        var run = harness.RunAsync();
        await WaitUntilAsync(() => harness.Conversation.Unprompted.Count == 1);
        await machine.CancelAsync();
        await WaitUntilAsync(() => harness.Media.Clears == 1);
        await greetingEnded.CancelAsync();
        await WaitUntilAsync(() => harness.Conversation.Unprompted.Count == 2);
        QueueResponse(harness, VoicemailMessage);
        await WaitUntilAsync(() => endCall.EndCallRequested);
        var heard = harness.Media.WrittenAudio.Count;

        // Act
        harness.Conversation.Queue(new RealtimeConversationEvent { Type = RealtimeConversationEventType.UserSpeechStarted });
        QueueResponse(harness, "Thanks, goodbye.");
        await Task.Delay(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(heard, harness.Media.WrittenAudio.Count);
        Assert.Single(harness.StoredPrompts, prompt => prompt.Role == ChatRole.Assistant);

        var completed = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));

        Assert.Same(run, completed);
        Assert.True(await run);
    }

    [Fact]
    public async Task AMachineTheAssistantHasPressedKeysOn_IsAPhoneMenu_AndIsLeftNoMessage()
    {
        // Arrange
        // A business's menu is a machine to the provider. Live, the assistant pressed the key for the person it was
        // calling, the call was then reported as a machine, and a message was left on the menu and the call hung up.
        var harness = new RealtimeHarness();
        var endCall = harness.UseEndCallTurn();
        using var machine = new CancellationTokenSource();
        using var menu = new CancellationTokenSource();
        harness.AnsweredByMachine = machine.Token;
        harness.PhoneMenuReached = menu.Token;
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;

        var run = harness.RunAsync();
        await WaitUntilAsync(() => harness.Conversation.Unprompted.Count == 1);
        await menu.CancelAsync();

        // Act
        await machine.CancelAsync();
        await Task.Delay(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
        QueueResponse(harness, "Hi, is this Amani?");
        await WaitUntilAsync(() => harness.Media.WrittenAudio.Count > 0);

        // Assert
        // The line was never taken from the model, and no message was asked for.
        Assert.Equal(0, harness.Media.Clears);
        Assert.Single(harness.Conversation.Unprompted);
        Assert.False(endCall.EndCallRequested);

        await LetTheCallGoAsync(harness, run);
    }

    [Fact]
    public async Task KeysPressedWhileTheGreetingPlays_TurnTheMachineIntoAPhoneMenu_AndTheConversationCarriesOn()
    {
        // Arrange
        var harness = new RealtimeHarness();
        var endCall = harness.UseEndCallTurn();
        using var machine = new CancellationTokenSource();
        using var greetingEnded = new CancellationTokenSource();
        using var menu = new CancellationTokenSource();
        harness.AnsweredByMachine = machine.Token;
        harness.MachineGreetingEnded = greetingEnded.Token;
        harness.PhoneMenuReached = menu.Token;
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;

        var run = harness.RunAsync();
        await WaitUntilAsync(() => harness.Conversation.Unprompted.Count == 1);
        await machine.CancelAsync();
        await WaitUntilAsync(() => harness.Media.Clears == 1);

        // Act
        await menu.CancelAsync();
        await WaitUntilAsync(() => harness.Conversation.TurnDetectionUpdates[^1].AllowInterruption);
        await greetingEnded.CancelAsync();
        QueueResponse(harness, "Hi, is this Amani?");
        await WaitUntilAsync(() => harness.Media.WrittenAudio.Count > 0);

        // Assert
        // What the model says reaches the line again, and the end of the menu's greeting asks for no message.
        Assert.Single(harness.Conversation.Unprompted);
        Assert.False(endCall.EndCallRequested);

        await LetTheCallGoAsync(harness, run);
    }

    // One complete response from the model: it starts, speaks, its transcript lands, and it completes.
    private static void QueueResponse(RealtimeHarness harness, string text)
        => harness.Conversation.Queue(
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.ResponseStarted },
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.AssistantAudioDelta, Audio = new byte[320] },
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.AssistantTranscriptDone, Text = text },
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.ResponseCompleted });
}
