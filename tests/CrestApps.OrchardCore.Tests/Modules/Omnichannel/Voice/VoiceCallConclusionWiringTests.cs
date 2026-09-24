using System.Reflection;
using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Capabilities;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using CrestApps.Core.Templates.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Voice;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Json;
using OrchardCore.Liquid;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// The moment an automated call stops being a call and becomes work for somebody: the outcome the review chose is
/// written on the activity and handed to the subject's workflow, which is what books the callback, the second
/// attempt, or the follow-up task. Both halves of that step are covered elsewhere — the policy that decides the
/// outcome, and the executor that acts on one — and the join between them was covered by nothing. A join that is
/// wrong is invisible from the outside: the call ends normally, the recording is there, the activity reads
/// "Completed", and the follow-up simply never appears, or appears against an outcome the model made up.
/// </summary>
public sealed class VoiceCallConclusionWiringTests
{
    private static readonly DateTime _now = new(2026, 7, 16, 20, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ConcludingACall_RunsTheSubjectsActions_ForTheOutcomeTheCallWasGiven()
    {
        // Arrange
        // The whole point of dispositioning a call is what happens next, and what happens next is the subject's
        // actions. The executor has to be given the call that just ended, the outcome it was given, and the person
        // it was with, because every action it can take -- reschedule, open a new activity, set a contact
        // preference -- is built out of those three.
        var harness = new ConclusionHarness();
        harness.Offers("disposition-interested", "Interested");
        harness.Offers("disposition-callback", "Call back later");
        harness.ModelReturns(dispositionId: "disposition-callback", summary: "The customer asked to be called next week.");

        // Act
        await harness.ConcludeAsync();

        // Assert
        var run = Assert.Single(harness.Executor.Runs);

        Assert.Same(harness.Activity, run.Activity);
        Assert.Equal("disposition-callback", run.Disposition.ItemId);
        Assert.Same(harness.Contact, run.Contact);
    }

    [Fact]
    public async Task AConcludedCall_IsWrittenCompleted_WithTheClocksTimeTheChosenOutcomeAndTheNotes()
    {
        // Arrange
        // The activity is the record of the call. An outcome that reaches the subject's actions but never reaches
        // the row leaves a follow-up scheduled against a call that still reads as in progress, and every report
        // that counts outcomes counts this one as nothing.
        var harness = new ConclusionHarness();
        harness.Offers("disposition-interested", "Interested");
        harness.ModelReturns(dispositionId: "disposition-interested", summary: "The customer wants a quote by Friday.");

        // Act
        await harness.ConcludeAsync();

        // Assert
        Assert.NotNull(harness.WrittenActivity);
        Assert.Equal(ActivityStatus.Completed, harness.WrittenActivity.Status);
        Assert.Equal(_now, harness.WrittenActivity.CompletedUtc);
        Assert.Equal("disposition-interested", harness.WrittenActivity.DispositionId);
        Assert.Equal("The customer wants a quote by Friday.", harness.WrittenActivity.Notes);
    }

    [Fact]
    public async Task ACallNobodySpokeOn_IsWrittenWithThePolicysNote_AndIsNeverSentToTheModel()
    {
        // Arrange
        // Asked to review a call with nothing on it, the model does not answer "nothing happened" -- it writes a
        // fluent account of a conversation that never took place, and that account is read later as fact. The
        // notes on the row have to be the policy's, not the model's, and the model must not be asked at all.
        var harness = new ConclusionHarness();
        harness.NobodySpoke();
        harness.Offers("disposition-no-answer", "No answer");

        // Act
        await harness.ConcludeAsync();

        // Assert
        Assert.Equal(0, harness.Model.Requests);
        Assert.NotNull(harness.WrittenActivity);
        Assert.Equal(VoiceCallConclusionPolicy.NoConversationNote, harness.WrittenActivity.Notes);
        Assert.Equal(ActivityStatus.Completed, harness.WrittenActivity.Status);
    }

    [Fact]
    public async Task ACallNobodySpokeOn_IsGivenTheOutcomeThatTriesAgain_SoTheContactIsAttemptedLater()
    {
        // Arrange
        // With no conversation to judge, the call used to take the first outcome on offer. Live, that was "Done":
        // a call that rang out unanswered was recorded as finished and the contact was never tried again. The
        // subject's own workflow says which outcome schedules another attempt, so an unanswered call takes that.
        var harness = new ConclusionHarness();
        harness.NobodySpoke();
        harness.Offers("disposition-done", "Done");
        harness.Offers("disposition-no-answer", "No answer", OmnichannelConstants.ActionTypes.TryAgain);

        // Act
        await harness.ConcludeAsync();

        // Assert
        Assert.Equal(0, harness.Model.Requests);
        Assert.Equal("disposition-no-answer", harness.WrittenActivity.DispositionId);
        var run = Assert.Single(harness.Executor.Runs);
        Assert.Equal("disposition-no-answer", run.Disposition.ItemId);
    }

    [Fact]
    public async Task ACallVoicemailAnswered_IsNeverReviewed_AndTriesAgainRatherThanOptingTheContactOut()
    {
        // Arrange
        // Live: the greeting was stored as the customer's turn, the review read "when you have finished recording
        // you may hang up" as the customer declining, and chose do-not-call -- opting out somebody who had never
        // picked up. A recording is not a conversation, so it is concluded exactly like a call nobody answered.
        var harness = new ConclusionHarness();
        harness.Says(
            (ChatRole.Assistant, "Hi Amani, this is Alex at Prestige Auto Group. Do you have a quick minute?"),
            (ChatRole.User, "When you have finished recording you may hang up."),
            (ChatRole.Assistant, "Thanks, Amani! Have a great day! [[HANGUP]]"));
        harness.Offers("disposition-do-not-call", "Do Not Call");
        harness.Offers("disposition-no-answer", "No answer", OmnichannelConstants.ActionTypes.TryAgain);
        harness.ModelReturns(dispositionId: "disposition-do-not-call", summary: "The customer declined to engage.");

        // Act
        await harness.ConcludeAsync();

        // Assert
        Assert.Equal(0, harness.Model.Requests);
        Assert.Equal("disposition-no-answer", harness.WrittenActivity.DispositionId);
        Assert.Equal(VoiceCallConclusionPolicy.VoicemailNote, harness.WrittenActivity.Notes);
    }

    [Fact]
    public async Task ACallTheLoopMarkedAsVoicemail_IsConcludedAsUnanswered()
    {
        // Arrange
        // The model can recognise a voicemail the greeting check does not, and the loop records that on the
        // activity; the conclusion has to honour it even though the transcript reads like a conversation.
        var harness = new ConclusionHarness();
        harness.Activity.Put(new VoicemailReached { MessageLeft = true });
        harness.Offers("disposition-interested", "Interested");
        harness.Offers("disposition-no-answer", "No answer", OmnichannelConstants.ActionTypes.TryAgain);
        harness.ModelReturns(dispositionId: "disposition-interested", summary: "The customer wants a call back.");

        // Act
        await harness.ConcludeAsync();

        // Assert
        Assert.Equal(0, harness.Model.Requests);
        Assert.Equal("disposition-no-answer", harness.WrittenActivity.DispositionId);
    }

    [Fact]
    public async Task ACallTheProviderTookForAMachine_ThatHeldAConversation_IsReviewedAsOne()
    {
        // Arrange
        // Detection can mistake a person for a machine. A customer who went on to answer the assistant, more
        // than once and in words no greeting uses, had a conversation, and it is concluded as one.
        var harness = new ConclusionHarness();
        harness.Activity.Put(new VoicemailReached { DetectedByProvider = true });
        harness.Says(
            (ChatRole.Assistant, "Hi, this is Ada calling about your enquiry. Is now a good time?"),
            (ChatRole.User, "Sure, what do you have?"),
            (ChatRole.Assistant, "We have a few options in your range."),
            (ChatRole.User, "Great, send me the details."));
        harness.Offers("disposition-interested", "Interested");
        harness.Offers("disposition-no-answer", "No answer", OmnichannelConstants.ActionTypes.TryAgain);
        harness.ModelReturns(dispositionId: "disposition-interested", summary: "The customer asked for details.");

        // Act
        await harness.ConcludeAsync();

        // Assert
        Assert.Equal(1, harness.Model.Requests);
        Assert.Equal("disposition-interested", harness.WrittenActivity.DispositionId);
    }

    [Fact]
    public async Task ACallTheProviderTookForAMachine_WithNoConversation_TriesAgain()
    {
        // Arrange
        var harness = new ConclusionHarness();
        harness.Activity.Put(new VoicemailReached { DetectedByProvider = true });
        harness.Says((ChatRole.Assistant, "Hi, this is Ada calling about your enquiry."));
        harness.Offers("disposition-interested", "Interested");
        harness.Offers("disposition-no-answer", "No answer", OmnichannelConstants.ActionTypes.TryAgain);

        // Act
        await harness.ConcludeAsync();

        // Assert
        Assert.Equal(0, harness.Model.Requests);
        Assert.Equal("disposition-no-answer", harness.WrittenActivity.DispositionId);
    }

    [Fact]
    public async Task AnOutcomeTheModelInvented_NeverReachesTheSubjectsActions()
    {
        // Arrange
        // The model is shown a list and asked to pick from it; sometimes it answers with something that was not on
        // it. That identifier matches no action the subject has, so acting on it would silently do nothing while
        // the call reads as dispositioned -- and it would put an outcome on the record that no report can explain.
        var harness = new ConclusionHarness();
        harness.Offers("disposition-interested", "Interested");
        harness.Offers("disposition-callback", "Call back later");
        harness.ModelReturns(dispositionId: "disposition-the-model-made-up", summary: "The customer was interested.");

        // Act
        await harness.ConcludeAsync();

        // Assert
        var run = Assert.Single(harness.Executor.Runs);

        Assert.NotEqual("disposition-the-model-made-up", run.Disposition.ItemId);
        Assert.Contains(harness.Dispositions, disposition => disposition.ItemId == run.Disposition.ItemId);

        // And the invented identifier is not what the row carries either.
        Assert.Equal(run.Disposition.ItemId, harness.WrittenActivity.DispositionId);
    }

    [Fact]
    public async Task WhenNoOutcomeCanBeChosen_TheSubjectsActionsAreNotRunAtAll()
    {
        // Arrange
        // A tenant with no dispositions configured leaves the review with nothing to pick, and the model returns
        // nothing either. The executor's first act is to demand a disposition -- DefaultSubjectActionExecutor
        // throws on a context without one -- so running it anyway would throw inside the deferred conclusion task,
        // after the call was already written up, where the only trace is a log line. The recording executor here
        // holds the same precondition, so removing the caller's guard fails this test rather than passing it.
        var harness = new ConclusionHarness();
        harness.ModelReturns(summary: "The customer was not interested.");

        // Act
        await harness.ConcludeAsync();

        // Assert
        Assert.Empty(harness.Executor.Runs);

        // The call is still concluded: an unclassifiable call is finished, not left open.
        Assert.NotNull(harness.WrittenActivity);
        Assert.Equal(ActivityStatus.Completed, harness.WrittenActivity.Status);
        Assert.Null(harness.WrittenActivity.DispositionId);
    }

    [Fact]
    public async Task WhenTheActivityAllowsIt_TheEmailTheCustomerGaveIsWrittenBackToTheContact()
    {
        // Arrange
        // An address given on the phone is only worth capturing if it reaches the contact record; left on the
        // activity it is a note nobody reads and the next email goes nowhere.
        var harness = new ConclusionHarness();
        harness.Activity.AllowAIToUpdateContact = true;
        harness.Offers("disposition-interested", "Interested");
        harness.ModelReturns(
            dispositionId: "disposition-interested",
            summary: "The customer gave an address for the quote.",
            contactEmail: "jamie@example.com");

        // Act
        await harness.ConcludeAsync();

        // Assert
        Assert.Equal("jamie@example.com", OmnichannelSubjectWriter.GetContactEmail(harness.Contact));
        harness.ContentManager.Verify(manager => manager.UpdateAsync(harness.Contact), Times.Once);
    }

    [Fact]
    public async Task WhenTheActivityForbidsIt_TheContactIsNotWrittenToAtAll()
    {
        // Arrange
        // The guard is the operator's answer to "may this automation edit my customer records", snapshotted onto
        // the activity when the work was loaded. An address the model reports anyway must go nowhere near the
        // contact -- not written, and not even attempted, because writing a contact publishes a new version of it.
        var harness = new ConclusionHarness();
        harness.Activity.AllowAIToUpdateContact = false;
        harness.Offers("disposition-interested", "Interested");
        harness.ModelReturns(
            dispositionId: "disposition-interested",
            summary: "The customer gave an address for the quote.",
            contactEmail: "jamie@example.com");

        // Act
        await harness.ConcludeAsync();

        // Assert
        Assert.Null(OmnichannelSubjectWriter.GetContactEmail(harness.Contact));
        harness.ContentManager.Verify(manager => manager.UpdateAsync(It.IsAny<ContentItem>()), Times.Never);
    }

    /// <summary>
    /// Records what the subject's workflow was asked to do, while holding the same precondition the real executor
    /// does: a context with no disposition is a programming error, not a no-op.
    /// </summary>
    private sealed class RecordingSubjectActionExecutor : ISubjectActionExecutor
    {
        public List<SubjectActionExecutionContext> Runs { get; } = [];

        public Task ExecuteAsync(SubjectActionExecutionContext context, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(context.Activity);
            ArgumentNullException.ThrowIfNull(context.Disposition);

            Runs.Add(context);

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Stands in for the model that reviews the finished call, answering with whatever the test says it answered
    /// and counting the times it was asked.
    /// </summary>
    private sealed class StubChatClient : IChatClient
    {
        public string ResponseText { get; set; }

        public int Requests { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions options = null,
            CancellationToken cancellationToken = default)
        {
            Requests++;

            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, ResponseText)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions options = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public object GetService(Type serviceType, object serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// The shape the review answers in. It mirrors the loop's own private result type, and is written with the
    /// very serializer options the loop reads it back with, so the test cannot pass by agreeing with itself about
    /// property names.
    /// </summary>
    private sealed class ConclusionAnalysis
    {
        public string Summary { get; set; }

        public string DispositionId { get; set; }

        public string ContactEmail { get; set; }
    }

    /// <summary>
    /// Wires the conclusion up to doubles: an in-memory activity and transcript, a catalog of the dispositions the
    /// subject's actions are wired to, a model that answers on request, and a recording executor standing in for
    /// the subject's workflow.
    /// </summary>
    private sealed class ConclusionHarness
    {
        private readonly List<AIChatSessionPrompt> _prompts;
        private readonly DocumentJsonSerializerOptions _documentJsonOptions = new();
        private readonly VoiceAgentConversationLoop _loop;
        private readonly IServiceProvider _services;

        public ConclusionHarness()
        {
            Activity = new OmnichannelActivity
            {
                ItemId = "activity-1",
                Status = ActivityStatus.InProgress,
                Channel = OmnichannelConstants.Channels.Phone,
                AIProfileId = "profile-1",
                AISessionId = "session-1",
                SubjectContentType = "Opportunity",
                ContactContentItemId = "contact-1",
                ContactResolutionStatus = ContactResolutionStatus.Resolved,

                // The subject write-back has its own tests; turning it off here keeps these about the outcome and
                // the contact, and keeps a content-type definition out of the picture entirely.
                AllowAIToUpdateSubject = false,
            };

            Contact = new ContentItem
            {
                ContentType = "Customer",
                ContentItemId = "contact-1",
                DisplayText = "Jamie Rivera",
            };

            // A call two people spoke on. Tests that need a silent one empty this again.
            _prompts =
            [
                new AIChatSessionPrompt
                {
                    SessionId = "session-1",
                    Role = ChatRole.Assistant,
                    Content = "Hi, this is Ada calling about your enquiry.",
                },
                new AIChatSessionPrompt
                {
                    SessionId = "session-1",
                    Role = ChatRole.User,
                    Content = "Yes, could you call me back next week?",
                },
            ];

            var activityStore = new Mock<IOmnichannelActivityStore>();
            activityStore
                .Setup(store => store.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Activity);
            activityStore
                .Setup(store => store.UpdateAsync(It.IsAny<OmnichannelActivity>(), It.IsAny<CancellationToken>()))
                .Callback<OmnichannelActivity, CancellationToken>((activity, _) => WrittenActivity = activity);

            ContentManager = new Mock<IContentManager>();
            ContentManager
                .Setup(manager => manager.GetAsync(It.IsAny<string>(), It.IsAny<VersionOptions>()))
                .ReturnsAsync(() => Contact);
            ContentManager
                .Setup(manager => manager.NewAsync(It.IsAny<string>()))
                .ReturnsAsync((string contentType) => new ContentItem
                {
                    ContentType = contentType,
                    ContentItemId = Guid.NewGuid().ToString("n"),
                });

            var dispositionCatalog = new Mock<ICatalog<OmnichannelDisposition>>();
            dispositionCatalog
                .Setup(catalog => catalog.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Dispositions);
            dispositionCatalog
                .Setup(catalog => catalog.GetAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IEnumerable<string> ids, CancellationToken _) => Dispositions
                    .Where(disposition => ids.Contains(disposition.ItemId, StringComparer.Ordinal))
                    .ToList());

            var actionCatalog = new Mock<ISourceCatalog<SubjectAction>>();
            actionCatalog
                .Setup(catalog => catalog.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => SubjectActions);

            var promptStore = new Mock<IAIChatSessionPromptStore>();
            promptStore
                .Setup(store => store.GetPromptsAsync(It.IsAny<string>()))
                .ReturnsAsync(() => _prompts);

            var profileManager = new Mock<IAIProfileManager>();
            profileManager
                .Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AIProfile
                {
                    ItemId = "profile-1",
                    Type = AIProfileType.Chat,
                });

            var flowSettingsService = new Mock<ISubjectFlowSettingsService>();
            flowSettingsService
                .Setup(service => service.FindConfiguredFlowSettingsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SubjectFlowSettings
                {
                    SubjectContentType = "Opportunity",
                    SubjectGoal = "Book the customer in for a quote.",
                });

            var contextBuilder = new Mock<IAICompletionContextBuilder>();
            contextBuilder
                .Setup(builder => builder.BuildAsync(
                    It.IsAny<AIProfile>(),
                    It.IsAny<Action<AICompletionContext>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AICompletionContext());

            var deploymentManager = new Mock<IAIDeploymentManager>();
            deploymentManager
                .Setup(manager => manager.ResolveSlotAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyDictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AIDeployment { ItemId = "deployment-1" });

            var clientFactory = new Mock<IAIClientFactory>();
            clientFactory
                .Setup(factory => factory.CreateChatClientAsync(
                    It.IsAny<AIDeployment>(),
                    It.IsAny<Action<ChatClientBuilder>>()))
                .ReturnsAsync(() => Model);

            var templateService = new Mock<ITemplateService>();
            templateService
                .Setup(service => service.RenderAsync(
                    VoiceTemplateIds.ConclusionAnalysis,
                    It.IsAny<IDictionary<string, object>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync("Review the call and choose one of the dispositions offered.");

            // The conclusion runs in a deferred shell scope and resolves everything it needs from that scope's
            // service provider rather than from the loop's own constructor, so this provider -- not the
            // constructor below -- is where the doubles that matter are wired.
            _services = new ServiceCollection()
                .AddSingleton(activityStore.Object)
                .AddSingleton(profileManager.Object)
                .AddSingleton(flowSettingsService.Object)
                .AddSingleton(promptStore.Object)
                .AddSingleton(clientFactory.Object)
                .AddSingleton(deploymentManager.Object)
                .AddSingleton(contextBuilder.Object)
                .AddSingleton(ContentManager.Object)
                .AddSingleton(dispositionCatalog.Object)
                .AddSingleton(actionCatalog.Object)
                .AddSingleton(templateService.Object)
                .AddSingleton<ISubjectActionExecutor>(Executor)
                .AddSingleton(Options.Create(_documentJsonOptions))
                .AddSingleton<IClock>(new StubClock(_now))
                .AddSingleton(Mock.Of<ISession>())

                // Only read when the activity allows the subject to be updated, which these tests turn off; a
                // bare mock here would otherwise hand back a null type definition and silently offer the model no
                // fields to fill in.
                .AddSingleton(Mock.Of<IContentDefinitionManager>())
                .BuildServiceProvider();

            _loop = new VoiceAgentConversationLoop(
                activityStore.Object,
                Mock.Of<IAIChatSessionManager>(),
                promptStore.Object,
                Mock.Of<IAICompletionService>(),
                Mock.Of<IOmnichannelHandoffTurn>(),
                Mock.Of<IVoiceCallEndTurn>(),
                Mock.Of<IRealtimeCallCompletionRunner>(),
                [],
                [],
                deploymentManager.Object,
                Mock.Of<IAIDeploymentCapabilityService>(),
                contextBuilder.Object,
                profileManager.Object,
                flowSettingsService.Object,
                [],
                Mock.Of<IVoiceAgentMediaProviderResolver>(),
                Mock.Of<IRealtimeVoiceConversationRunner>(),
                Mock.Of<ITurnBasedSilenceWatchdog>(),
                Mock.Of<ILiquidTemplateManager>(),
                ContentManager.Object,
                new StubClock(_now),
                NullLogger<VoiceAgentConversationLoop>.Instance);
        }

        public OmnichannelActivity Activity { get; }

        public ContentItem Contact { get; }

        public Mock<IContentManager> ContentManager { get; }

        public List<OmnichannelDisposition> Dispositions { get; } = [];

        public List<SubjectAction> SubjectActions { get; } = [];

        public RecordingSubjectActionExecutor Executor { get; } = new();

        public StubChatClient Model { get; } = new();

        /// <summary>
        /// The activity the conclusion wrote back, which is the same instance the store handed it.
        /// </summary>
        public OmnichannelActivity WrittenActivity { get; private set; }

        /// <summary>
        /// Configures a disposition the subject's workflow acts on, which is what puts it in front of the model.
        /// </summary>
        public void Offers(string dispositionId, string name, string actionType = OmnichannelConstants.ActionTypes.Finish)
        {
            Dispositions.Add(new OmnichannelDisposition
            {
                ItemId = dispositionId,
                Name = name,
            });

            SubjectActions.Add(new SubjectAction
            {
                ItemId = $"action-for-{dispositionId}",
                Source = actionType,
                SubjectContentType = Activity.SubjectContentType,
                DispositionId = dispositionId,
            });
        }

        /// <summary>
        /// A call that rang out, was declined, or was answered in silence: no turns at all.
        /// </summary>
        public void NobodySpoke()
            => _prompts.Clear();

        /// <summary>
        /// Replaces the transcript with the given turns.
        /// </summary>
        public void Says(params (ChatRole Role, string Content)[] turns)
        {
            _prompts.Clear();
            _prompts.AddRange(turns.Select(turn => new AIChatSessionPrompt
            {
                SessionId = "session-1",
                Role = turn.Role,
                Content = turn.Content,
            }));
        }

        /// <summary>
        /// What the review answers with when it is asked.
        /// </summary>
        public void ModelReturns(string dispositionId = null, string summary = null, string contactEmail = null)
        {
            Model.ResponseText = JsonSerializer.Serialize(
                new ConclusionAnalysis
                {
                    Summary = summary,
                    DispositionId = dispositionId,
                    ContactEmail = contactEmail,
                },
                _documentJsonOptions.SerializerOptions);
        }

        /// <summary>
        /// Runs the conclusion the way the deferred shell task does, with the scope's service provider. The method
        /// is private to the loop and reachable no other way: its only caller is inside a deferred task that a
        /// unit test has no shell to run.
        /// </summary>
        public async Task ConcludeAsync()
        {
            var method = typeof(VoiceAgentConversationLoop).GetMethod(
                "ConcludeAsync",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(method);

            var task = method.Invoke(_loop, [_services, Activity.ItemId]) as Task;

            Assert.NotNull(task);

            // The conclusion takes no cancellation token of its own -- it runs as a deferred shell task, detached
            // from the request that started it -- so the test's token is applied to the wait instead, and a
            // conclusion that never finishes fails the run rather than stalling it.
            await task.WaitAsync(TestContext.Current.CancellationToken);
        }
    }
}
