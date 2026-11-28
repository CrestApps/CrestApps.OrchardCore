using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Templates.Services;
using CrestApps.OrchardCore.Omnichannel.Automation;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Json;
using OrchardCore.Locking;
using YesSql;
using static CrestApps.OrchardCore.Tests.Omnichannel.Automation.AutomatedActivityScanHarness;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Automation;

/// <summary>
/// The owed-reply recovery pass reads the conversations awaiting a customer's answer a batch at a time, keyed on the
/// document id, and keeps where it stopped in its own cursor so the next pass goes on from there and wraps to the first
/// conversation at the end. These run the real index query against SQLite, a new session and service per pass, and one
/// cursor shared by every pass, as the background task's scopes and the singleton cursor do.
/// </summary>
public sealed class AutomatedOwedReplyRecoveryTests
{
    private static readonly DateTime _deadline = Now.AddHours(1);

    [Fact]
    public async Task RecoverAsync_WhenMoreConversationsAwaitThanOnePassReads_TheNextPassGoesOnFromWhereTheLastStopped()
    {
        await WithStoreAsync("recovery-advance", async store =>
        {
            // Arrange
            var candidates = await SeedCandidatesAsync(store, 300);
            var scenario = new RecoveryScenario();

            // Act
            var first = await scenario.RunPassAsync(store);
            var cursorAfterFirst = scenario.Cursors.OwedReply;
            var second = await scenario.RunPassAsync(store);

            // Assert
            // A pass reads at most two batches of 100; the first stops at the 200th conversation and keeps its id.
            Assert.Equal(SessionIds(1, 200), first);
            Assert.Equal(candidates[199].Id, cursorAfterFirst);

            // The second goes on with the 201st instead of reading the first 200 again.
            Assert.Equal(SessionIds(201, 300), second.Take(100));

            // The follow-up pass keeps its own cursor.
            Assert.Equal(0L, scenario.Cursors.FollowUp);
        });
    }

    [Fact]
    public async Task RecoverAsync_WhenAPassReachesTheLastConversation_ItGoesRoundToTheFirstAndTheNextPassGoesOnFromThere()
    {
        await WithStoreAsync("recovery-wrap", async store =>
        {
            // Arrange
            var candidates = await SeedCandidatesAsync(store, 300);
            var scenario = new RecoveryScenario();

            // Where a pass that read the first 200 left the cursor.
            scenario.Cursors.OwedReply = candidates[199].Id;

            // Act
            var second = await scenario.RunPassAsync(store);
            var cursorAfterSecond = scenario.Cursors.OwedReply;
            var third = await scenario.RunPassAsync(store);

            // Assert
            Assert.Equal(SessionIds(201, 300).Concat(SessionIds(1, 100)), second);
            Assert.Equal(candidates[99].Id, cursorAfterSecond);

            Assert.Equal(SessionIds(101, 300), third);
            Assert.Equal(candidates[299].Id, scenario.Cursors.OwedReply);
        });
    }

    [Fact]
    public async Task RecoverAsync_WhenAPassFromTheFirstConversationReachesTheLast_TheNextPassStartsFromTheFirstAgain()
    {
        await WithStoreAsync("recovery-restart", async store =>
        {
            // Arrange
            await SeedCandidatesAsync(store, 150);
            var scenario = new RecoveryScenario();

            // Act
            var first = await scenario.RunPassAsync(store);
            var cursorAfterFirst = scenario.Cursors.OwedReply;
            var second = await scenario.RunPassAsync(store);

            // Assert
            Assert.Equal(SessionIds(1, 150), first);
            Assert.Equal(0L, cursorAfterFirst);
            Assert.Equal(SessionIds(1, 150), second);
        });
    }

    [Fact]
    public async Task RecoverAsync_WhenAConversationFails_ThePassStopsAndTheNextGoesOnAfterIt()
    {
        await WithStoreAsync("recovery-failure", async store =>
        {
            // Arrange
            var candidates = await SeedCandidatesAsync(store, 30);
            var scenario = new RecoveryScenario();

            // Act
            var first = await scenario.RunPassAsync(store, gate => gate.FailFor = SessionId(12));
            var cursorAfterFirst = scenario.Cursors.OwedReply;
            var second = await scenario.RunPassAsync(store);

            // Assert
            Assert.Equal(SessionIds(1, 12), first);
            Assert.Equal(candidates[11].Id, cursorAfterFirst);
            Assert.Equal(SessionIds(13, 30), second.Take(18));
        });
    }

    [Fact]
    public async Task RecoverAsync_RedrivesOnlyTheConversationsWhoseRecentLastMessageIsTheCustomers()
    {
        await WithStoreAsync("recovery-owed", async store =>
        {
            // Arrange
            var candidates = await SeedCandidatesAsync(store, 12);
            var scenario = new RecoveryScenario();

            scenario.OweReplies(candidates, Now.AddMinutes(-5));

            // The agent spoke last: nothing is owed.
            scenario.Prompts[SessionId(3)].Add(Prompt(SessionId(3), ChatRole.Assistant, "Here is the answer.", Now.AddMinutes(-4)));

            // The customer wrote longer ago than a late reply is still sent.
            scenario.Prompts[SessionId(5)][^1].CreatedUtc = Now - AutomatedOwedReplyRecovery.MaxOwedReplyAge - TimeSpan.FromMinutes(1);

            // No transcript at all.
            scenario.Sessions.Remove(SessionId(7));

            // Act
            var recovered = await scenario.RecoverAsync(store);

            // Assert
            Assert.Equal(9, recovered);
            Assert.Equal(9, scenario.HandledTurns);
        });
    }

    [Fact]
    public async Task RecoverAsync_WhenAPassGoesRoundPastWhereItStarted_RedrivesEachOwedConversationOnce()
    {
        await WithStoreAsync("recovery-once", async store =>
        {
            // Arrange
            var candidates = await SeedCandidatesAsync(store, 12);
            var scenario = new RecoveryScenario();

            // Every conversation owes a reply. The handler leaves each turn unanswered (as it does when it cannot send),
            // so the transcript alone cannot stop a second re-drive. A previous pass stopped after the ninth.
            scenario.OweReplies(candidates, Now.AddMinutes(-5));
            scenario.Cursors.OwedReply = candidates[8].Id;

            // Act
            var recovered = await scenario.RecoverAsync(store);

            // Assert
            // The pass goes round "to reach the conversations before where it started": the tenth to the twelfth, then
            // the first to the ninth, each re-driven once.
            Assert.Equal(12, recovered);
            Assert.Equal(12, scenario.HandledTurns);
        });
    }

    private static AIChatSessionPrompt Prompt(string sessionId, ChatRole role, string content, DateTime createdUtc)
        => new()
        {
            ItemId = $"prompt-{sessionId}-{createdUtc.Ticks}",
            SessionId = sessionId,
            Role = role,
            Content = content,
            CreatedUtc = createdUtc,
        };

    /// <summary>
    /// The collaborators of one recovery pass: the conversations' transcripts, the address they run on, and a real
    /// conversation handler whose channel resolver records each owed turn raised to it and then declines to send.
    /// </summary>
    private sealed class RecoveryScenario
    {
        public AutomatedActivityScanCursors Cursors { get; } = new();

        public OmnichannelChannelEndpoint Endpoint { get; } = new()
        {
            ItemId = EndpointId,
            Channel = Channel,
            Value = EndpointAddress,
        };

        public Dictionary<string, AIChatSession> Sessions { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, List<AIChatSessionPrompt>> Prompts { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Gets how many owed turns reached the conversation handler.
        /// </summary>
        public int HandledTurns { get; private set; }

        /// <summary>
        /// Ends each conversation with the customer's message, written at <paramref name="lastMessageUtc"/> and never
        /// answered.
        /// </summary>
        public void OweReplies(IEnumerable<OmnichannelActivity> activities, DateTime lastMessageUtc)
        {
            foreach (var activity in activities)
            {
                Sessions[activity.AISessionId] = new AIChatSession
                {
                    SessionId = activity.AISessionId,
                    ProfileId = ProfileId,
                    LastActivityUtc = lastMessageUtc,
                };

                Prompts[activity.AISessionId] =
                [
                    Prompt(activity.AISessionId, ChatRole.Assistant, "Hi, how can I help?", lastMessageUtc.AddMinutes(-10)),
                    Prompt(activity.AISessionId, ChatRole.User, "I have a question about my order.", lastMessageUtc),
                ];
            }
        }

        /// <summary>
        /// Runs one pass, with the gate letting every conversation through.
        /// </summary>
        /// <returns>How many conversations the pass re-drove.</returns>
        public async Task<int> RecoverAsync(IStore store)
        {
            var recovered = -1;

            await RunPassAsync(
                store,
                gate => gate.ReportsGenerating = false,
                async recovery => recovered = await recovery.RecoverAsync(_deadline, TestContext.Current.CancellationToken));

            return recovered;
        }

        /// <summary>
        /// Runs one pass in its own session and service, as one run of the background task does.
        /// </summary>
        /// <returns>The AI chat sessions the pass examined, in order.</returns>
        public async Task<IReadOnlyList<string>> RunPassAsync(
            IStore store,
            Action<RecordingConversationGate> configureGate = null,
            Func<AutomatedOwedReplyRecovery, Task> run = null)
        {
            var gate = new RecordingConversationGate();
            configureGate?.Invoke(gate);

            await using var session = store.CreateSession();
            var recovery = CreateRecovery(session, gate);

            if (run is null)
            {
                await recovery.RecoverAsync(_deadline, TestContext.Current.CancellationToken);
            }
            else
            {
                await run(recovery);
            }

            return gate.Asked;
        }

        private AutomatedOwedReplyRecovery CreateRecovery(ISession session, IAutomatedConversationGate gate)
        {
            var channel = CreateChannel();
            var clock = new StubClock(Now);

            var chatSessionManager = new Mock<IAIChatSessionManager>();
            chatSessionManager
                .Setup(x => x.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((string sessionId, CancellationToken _) => Task.FromResult(Sessions.GetValueOrDefault(sessionId ?? string.Empty)));

            var promptStore = new Mock<IAIChatSessionPromptStore>();
            promptStore
                .Setup(x => x.GetPromptsAsync(It.IsAny<string>()))
                .Returns((string sessionId) => Task.FromResult<IReadOnlyList<AIChatSessionPrompt>>([.. Prompts.GetValueOrDefault(sessionId ?? string.Empty) ?? []]));

            var endpointManager = new Mock<IOmnichannelChannelEndpointManager>();
            endpointManager
                .Setup(x => x.FindByIdAsync(EndpointId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Endpoint);

            // The handler resolves the messaging channel once it has recognised the owed turn as an inbound message on an
            // automated channel; resolving none, it stops there without sending or recording anything.
            var handlerChannelResolver = new Mock<IMessagingChannelResolver>();
            handlerChannelResolver
                .Setup(x => x.Get(Channel))
                .Callback(() => HandledTurns++)
                .Returns((IMessagingChannel)null);

            var handler = new AutomatedConversationHandler(
                [channel],
                handlerChannelResolver.Object,
                Mock.Of<IAIChatSessionManager>(),
                Mock.Of<IAIChatSessionPromptStore>(),
                Mock.Of<IAICompletionService>(),
                Mock.Of<IOmnichannelHandoffTurn>(),
                Mock.Of<IAutomatedConversationGate>(),
                Mock.Of<IAIClientFactory>(),
                Mock.Of<IAIDeploymentManager>(),
                Mock.Of<IAICompletionContextBuilder>(),
                Mock.Of<IAIProfileManager>(),
                Mock.Of<ITemplateService>(),
                Mock.Of<IOmnichannelChannelEndpointManager>(),
                Mock.Of<ISubjectFlowSettingsService>(),
                Mock.Of<IContentManager>(),
                Mock.Of<IOmnichannelActivityStore>(),
                [],
                [],
                Mock.Of<ILocalLock>(),
                session,
                clock,
                Options.Create(new DocumentJsonSerializerOptions()),
                NullLogger<AutomatedConversationHandler>.Instance,
                Mock.Of<IStringLocalizer<AutomatedConversationHandler>>());

            return new AutomatedOwedReplyRecovery(
                [channel],
                handler,
                gate,
                chatSessionManager.Object,
                promptStore.Object,
                endpointManager.Object,
                session,
                clock,
                Cursors,
                NullLogger<AutomatedOwedReplyRecovery>.Instance);
        }
    }
}
