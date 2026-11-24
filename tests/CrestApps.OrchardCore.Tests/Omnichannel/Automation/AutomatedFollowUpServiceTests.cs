using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Automation;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Data;
using OrchardCore.Locking;
using YesSql;
using static CrestApps.OrchardCore.Tests.Omnichannel.Automation.AutomatedActivityScanHarness;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Automation;

/// <summary>
/// The follow-up pass reads the conversations awaiting a customer's answer a batch at a time, keyed on the document id,
/// and keeps where it stopped so the next pass goes on from there and wraps to the first conversation at the end. These
/// run the real index query against SQLite, a new session and service per pass, and one cursor shared by every pass, as
/// the background task's scopes and the singleton cursor do.
/// </summary>
public sealed class AutomatedFollowUpServiceTests
{
    private static readonly DateTime _deadline = Now.AddHours(1);

    [Fact]
    public async Task SendDueAsync_WhenMoreConversationsAwaitThanOnePassReads_TheNextPassGoesOnFromWhereTheLastStopped()
    {
        await WithStoreAsync("follow-up-advance", async store =>
        {
            // Arrange
            var candidates = await SeedCandidatesAsync(store, 300);
            var scenario = new FollowUpScenario();

            // Act
            var first = await scenario.RunPassAsync(store);
            var cursorAfterFirst = scenario.Cursors.FollowUp;
            var second = await scenario.RunPassAsync(store);

            // Assert
            // A pass reads at most two batches of 100; the first stops at the 200th conversation and keeps its id.
            Assert.Equal(SessionIds(1, 200), first);
            Assert.Equal(candidates[199].Id, cursorAfterFirst);

            // The second goes on with the 201st instead of reading the first 200 again.
            Assert.Equal(SessionIds(201, 300), second.Take(100));

            // The recovery pass keeps its own cursor.
            Assert.Equal(0L, scenario.Cursors.OwedReply);
        });
    }

    [Fact]
    public async Task SendDueAsync_WhenAPassReachesTheLastConversation_ItGoesRoundToTheFirstAndTheNextPassGoesOnFromThere()
    {
        await WithStoreAsync("follow-up-wrap", async store =>
        {
            // Arrange
            var candidates = await SeedCandidatesAsync(store, 300);
            var scenario = new FollowUpScenario();

            // Where a pass that read the first 200 left the cursor.
            scenario.Cursors.FollowUp = candidates[199].Id;

            // Act
            var second = await scenario.RunPassAsync(store);
            var cursorAfterSecond = scenario.Cursors.FollowUp;
            var third = await scenario.RunPassAsync(store);

            // Assert
            Assert.Equal(SessionIds(201, 300).Concat(SessionIds(1, 100)), second);
            Assert.Equal(candidates[99].Id, cursorAfterSecond);

            Assert.Equal(SessionIds(101, 300), third);
            Assert.Equal(candidates[299].Id, scenario.Cursors.FollowUp);
        });
    }

    [Fact]
    public async Task SendDueAsync_WhenAPassFromTheFirstConversationReachesTheLast_TheNextPassStartsFromTheFirstAgain()
    {
        await WithStoreAsync("follow-up-restart", async store =>
        {
            // Arrange
            await SeedCandidatesAsync(store, 150);
            var scenario = new FollowUpScenario();

            // Act
            var first = await scenario.RunPassAsync(store);
            var cursorAfterFirst = scenario.Cursors.FollowUp;
            var second = await scenario.RunPassAsync(store);

            // Assert
            Assert.Equal(SessionIds(1, 150), first);
            Assert.Equal(0L, cursorAfterFirst);
            Assert.Equal(SessionIds(1, 150), second);
        });
    }

    [Fact]
    public async Task SendDueAsync_WhenAConversationFails_ThePassStopsAndTheNextGoesOnAfterIt()
    {
        await WithStoreAsync("follow-up-failure", async store =>
        {
            // Arrange
            var candidates = await SeedCandidatesAsync(store, 30);
            var scenario = new FollowUpScenario();

            // Act
            var first = await scenario.RunPassAsync(store, gate => gate.FailFor = SessionId(12));
            var cursorAfterFirst = scenario.Cursors.FollowUp;
            var second = await scenario.RunPassAsync(store);

            // Assert
            Assert.Equal(SessionIds(1, 12), first);
            Assert.Equal(candidates[11].Id, cursorAfterFirst);
            Assert.Equal(SessionIds(13, 30), second.Take(18));
        });
    }

    [Fact]
    public async Task SendDueAsync_WhenAPassGoesRoundPastWhereItStarted_SendsOneFollowUpPerConversation()
    {
        await WithStoreAsync("follow-up-once", async store =>
        {
            // Arrange
            var candidates = await SeedCandidatesAsync(store, 12);
            var scenario = new FollowUpScenario();

            // Every conversation is due its first step, and the cadence has a second, so the step count is not what
            // stops a second follow-up. The pass starts after the ninth, reads to the end, and goes round to the first.
            scenario.AwaitCustomers(candidates, Now.AddMinutes(-45));
            scenario.Cursors.FollowUp = candidates[8].Id;

            // Act
            var sent = 0;
            await scenario.RunPassAsync(store, gate => gate.ReportsGenerating = false, async service => sent = await service.SendDueAsync(_deadline, TestContext.Current.CancellationToken));

            // Assert
            Assert.Equal(12, sent);
            Assert.Equal(
                candidates.Select(candidate => candidate.PreferredDestination).Order(StringComparer.Ordinal),
                scenario.Sent.Select(message => message.ContactAddress).Order(StringComparer.Ordinal));
            Assert.All(scenario.Sent, message => Assert.Equal("Still interested?", message.Body));

            var stored = await LoadCandidatesAsync(store);

            Assert.Equal(12, stored.Count);
            Assert.All(stored, activity =>
            {
                Assert.Equal(1, activity.ReEngagementAttempts);
                Assert.Equal(Now, activity.LastReEngagementUtc);
            });
        });
    }

    [Fact]
    public async Task SendDueAsync_WhenThePassRunsAgainRightAway_SendsNoSecondFollowUp()
    {
        await WithStoreAsync("follow-up-again", async store =>
        {
            // Arrange
            var candidates = await SeedCandidatesAsync(store, 12);
            var scenario = new FollowUpScenario();

            scenario.AwaitCustomers(candidates, Now.AddMinutes(-45));
            await scenario.RunPassAsync(store, gate => gate.ReportsGenerating = false);
            scenario.Sent.Clear();

            // Act
            var sent = -1;
            await scenario.RunPassAsync(store, gate => gate.ReportsGenerating = false, async service => sent = await service.SendDueAsync(_deadline, TestContext.Current.CancellationToken));

            // Assert
            // The second step waits its delay after the first follow-up, which went out a moment ago.
            Assert.Equal(0, sent);
            Assert.Empty(scenario.Sent);
            Assert.All(await LoadCandidatesAsync(store), activity => Assert.Equal(1, activity.ReEngagementAttempts));
        });
    }

    [Fact]
    public async Task SendDueAsync_WhenAPassGoesRoundPastWhereItStarted_ExaminesEachConversationOnce()
    {
        await WithStoreAsync("follow-up-examine-once", async store =>
        {
            // Arrange
            var candidates = await SeedCandidatesAsync(store, 12);
            var scenario = new FollowUpScenario();

            // A previous pass stopped after the ninth (its deadline passed, or the ninth failed).
            scenario.Cursors.FollowUp = candidates[8].Id;

            // Act
            var asked = await scenario.RunPassAsync(store);

            // Assert
            // The pass goes round "to reach the conversations before where it started": the tenth to the twelfth, then
            // the first to the ninth, each once.
            Assert.Equal(SessionIds(10, 12).Concat(SessionIds(1, 9)), asked);
        });
    }

    private static async Task<IReadOnlyList<OmnichannelActivity>> LoadCandidatesAsync(IStore store)
    {
        await using var session = store.CreateSession();

        var activities = await session
            .Query<OmnichannelActivity, OmnichannelActivityIndex>(index =>
                index.Status == ActivityStatus.AwaitingCustomerAnswer &&
                index.InteractionType == ActivityInteractionType.Automated &&
                index.Channel == Channel,
                collection: OmnichannelConstants.CollectionName)
            .ListAsync(TestContext.Current.CancellationToken);

        return [.. activities];
    }

    /// <summary>
    /// The collaborators of one follow-up pass: the conversations' transcripts, the cadence, the address they are sent
    /// from, and the channel that records what would have been sent.
    /// </summary>
    private sealed class FollowUpScenario
    {
        public AutomatedActivityScanCursors Cursors { get; } = new();

        public Cadence Cadence { get; } = new()
        {
            ItemId = CadenceId,
            DisplayText = "Follow up",
            Enabled = true,
            Steps =
            [
                new CadenceStep { DelayMinutes = 30, IsAiGenerated = false, Message = "Still interested?" },
                new CadenceStep { DelayMinutes = 30, IsAiGenerated = false, Message = "One last check." },
            ],
        };

        public OmnichannelChannelEndpoint Endpoint { get; } = new()
        {
            ItemId = EndpointId,
            Channel = Channel,
            Value = EndpointAddress,
        };

        public Dictionary<string, AIChatSession> Sessions { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, List<AIChatSessionPrompt>> Prompts { get; } = new(StringComparer.Ordinal);

        public List<MessagingOutboundMessage> Sent { get; } = [];

        /// <summary>
        /// Opens each conversation with the agent's message, which the customer never answered.
        /// </summary>
        public void AwaitCustomers(IEnumerable<OmnichannelActivity> activities, DateTime lastMessageUtc)
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
                    new AIChatSessionPrompt
                    {
                        ItemId = $"prompt-{activity.AISessionId}",
                        SessionId = activity.AISessionId,
                        Role = ChatRole.Assistant,
                        Content = "Hi, are you still interested?",
                        CreatedUtc = lastMessageUtc,
                    },
                ];
            }
        }

        /// <summary>
        /// Runs one pass in its own session and service, as one run of the background task does.
        /// </summary>
        /// <returns>The AI chat sessions the pass examined, in order.</returns>
        public async Task<IReadOnlyList<string>> RunPassAsync(
            IStore store,
            Action<RecordingConversationGate> configureGate = null,
            Func<AutomatedFollowUpService, Task> run = null)
        {
            var gate = new RecordingConversationGate();
            configureGate?.Invoke(gate);

            await using var session = store.CreateSession();
            var service = CreateService(store, session, gate);

            if (run is null)
            {
                await service.SendDueAsync(_deadline, TestContext.Current.CancellationToken);
            }
            else
            {
                await run(service);
            }

            return gate.Asked;
        }

        private AutomatedFollowUpService CreateService(IStore store, ISession session, IAutomatedConversationGate gate)
        {
            var messagingChannel = new Mock<IMessagingChannel>();
            messagingChannel
                .Setup(x => x.SendAsync(It.IsAny<MessagingOutboundMessage>(), It.IsAny<CancellationToken>()))
                .Callback((MessagingOutboundMessage message, CancellationToken _) => Sent.Add(message))
                .ReturnsAsync(MessageDispatchResult.Success());

            var channelResolver = new Mock<IMessagingChannelResolver>();
            channelResolver.Setup(x => x.Get(Channel)).Returns(messagingChannel.Object);

            var chatSessionManager = new Mock<IAIChatSessionManager>();
            chatSessionManager
                .Setup(x => x.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((string sessionId, CancellationToken _) => Task.FromResult(Sessions.GetValueOrDefault(sessionId ?? string.Empty)));
            chatSessionManager
                .Setup(x => x.SaveAsync(It.IsAny<AIChatSession>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var promptStore = new Mock<IAIChatSessionPromptStore>();
            promptStore
                .Setup(x => x.GetPromptsAsync(It.IsAny<string>()))
                .Returns((string sessionId) => Task.FromResult<IReadOnlyList<AIChatSessionPrompt>>([.. Prompts.GetValueOrDefault(sessionId ?? string.Empty) ?? []]));
            promptStore
                .Setup(x => x.CreateAsync(It.IsAny<AIChatSessionPrompt>(), It.IsAny<CancellationToken>()))
                .Callback((AIChatSessionPrompt prompt, CancellationToken _) => Prompts[prompt.SessionId].Add(prompt))
                .Returns(ValueTask.CompletedTask);

            var profileManager = new Mock<IAIProfileManager>();
            profileManager
                .Setup(x => x.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(new ValueTask<AIProfile>(new AIProfile { Name = "agent", Type = AIProfileType.Chat }));

            var endpointManager = new Mock<IOmnichannelChannelEndpointManager>();
            endpointManager
                .Setup(x => x.FindByIdAsync(EndpointId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Endpoint);

            var cadenceCatalog = new Mock<ICatalog<Cadence>>();
            cadenceCatalog
                .Setup(x => x.FindByIdAsync(CadenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Cadence);

            var businessHoursGate = new Mock<IBusinessHoursGate>();
            businessHoursGate
                .Setup(x => x.IsOpenAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var localLock = new Mock<ILocalLock>();
            localLock
                .Setup(x => x.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
                .ReturnsAsync((Mock.Of<ILocker>(), true));

            return new AutomatedFollowUpService(
                [CreateChannel()],
                channelResolver.Object,
                chatSessionManager.Object,
                promptStore.Object,
                profileManager.Object,
                Mock.Of<IAIDeploymentManager>(),
                Mock.Of<IAICompletionContextBuilder>(),
                Mock.Of<IAICompletionService>(),
                endpointManager.Object,
                cadenceCatalog.Object,
                Mock.Of<IContentManager>(),
                Mock.Of<IContactOptOutResolver>(),
                [],
                new OmnichannelActivityStore(session, [], [], store, Mock.Of<IDbConnectionAccessor>()),
                Mock.Of<ISubjectFlowSettingsService>(),
                businessHoursGate.Object,
                gate,
                localLock.Object,
                session,
                new StubClock(Now),
                Cursors,
                NullLogger<AutomatedFollowUpService>.Instance);
        }
    }
}
