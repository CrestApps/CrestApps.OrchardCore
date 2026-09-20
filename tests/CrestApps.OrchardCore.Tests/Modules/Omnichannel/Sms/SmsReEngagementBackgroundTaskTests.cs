using CrestApps.Core.Omnichannel.Services;
using CrestApps.Core.Omnichannel.Models;
using CrestApps.OrchardCore.Omnichannel.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Services;
using Microsoft.Extensions.Time.Testing;
using System.Collections.Concurrent;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Indexes;
using CrestApps.OrchardCore.Omnichannel.Sms.BackgroundTasks;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Infrastructure;
using OrchardCore.Locking;
using OrchardCore.Modules;
using OrchardCore.Sms;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Sms;

/// <summary>
/// A contact who stopped replying wakes up to a text at two in the morning their time, or to a fifth follow-up
/// after ignoring four, or to a nudge that talks over the answer they sent a minute ago. Nothing between this
/// task and the carrier asks any of those questions again — the only thing standing between a quiet contact and
/// an unwanted message is a run of early returns in one method, and every one of them can be deleted without
/// breaking a compile. These tests are what breaks instead.
/// </summary>
public sealed class SmsReEngagementBackgroundTaskTests
{
    private static readonly DateTime _now = new(2026, 7, 16, 20, 0, 0, DateTimeKind.Utc);

    private const string _contactDestination = "+15555550100";
    private const string _endpointNumber = "+15555550111";

    // Fixed offsets the stub gate resolves these identifiers to, so the test does not depend on the machine's
    // time-zone database. At the 20:00 UTC test clock these put one contact at 13:00 and the other at 22:00.
    private const string _afternoonTimeZoneId = "America/Los_Angeles";
    private const string _eveningTimeZoneId = "Europe/Berlin";

    [Fact]
    public async Task DoWorkAsync_WhenTheStepIsDueAndBusinessHoursAreOpen_SendsTheNudgeAndRecordsTheAttempt()
    {
        // Arrange
        // The positive control. Every other test here asserts that nothing was sent, and those assertions are
        // worthless unless this one proves the same harness does produce a send when the conversation is due.
        var databasePath = DatabasePath("sends");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var scenario = new ReEngagementScenario
            {
                Cadence = DefinedMessageCadence(("Still interested?", 30)),
            };

            var activity = await SeedAwaitingConversationAsync(store, scenario, _contactDestination, _now.AddMinutes(-45));

            // Act
            await RunAsync(store, scenario);

            // Assert
            var message = Assert.Single(scenario.Sms.Sent);

            Assert.Equal(_contactDestination, message.To);
            Assert.Equal(_endpointNumber, message.From);
            Assert.Equal("Still interested?", message.Body);

            Assert.Equal(1, activity.ReEngagementAttempts);
            Assert.Equal(_now, activity.LastReEngagementUtc);
            Assert.Contains(activity.ItemId, scenario.UpdatedActivityIds);

            // The nudge restarts the silence window, which is what spaces the next step out from this one.
            Assert.Equal(_now, scenario.Sessions[activity.AISessionId].LastActivityUtc);

            var storedPrompt = Assert.Single(scenario.Prompts[activity.AISessionId], prompt => prompt.Content == "Still interested?");

            Assert.Equal(ChatRole.Assistant, storedPrompt.Role);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task DoWorkAsync_WhenTheBusinessHoursGateReportsClosed_SendsNoNudge()
    {
        // Arrange
        // This task is the only quiet-hours enforcement there is for a background-initiated text. Nothing further
        // down the path — not the AI, not the SMS provider — will decline to deliver a message at midnight.
        var databasePath = DatabasePath("closed");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var scenario = new ReEngagementScenario
            {
                Cadence = DefinedMessageCadence(("Still interested?", 30)),
            };

            scenario.BusinessHours.ForcedAnswer = false;

            var activity = await SeedAwaitingConversationAsync(store, scenario, _contactDestination, _now.AddMinutes(-45));

            // Act
            await RunAsync(store, scenario);

            // Assert
            Assert.Empty(scenario.Sms.Sent);
            Assert.Equal(0, activity.ReEngagementAttempts);
            Assert.Null(activity.LastReEngagementUtc);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task DoWorkAsync_EvaluatesBusinessHoursInTheContactsTimeZone_NotAgainstTheServerClock()
    {
        // Arrange
        // Business hours only mean anything where the contact actually is. At the 20:00 UTC test clock a nine-to-six
        // window is shut on the server's own clock, so a gate handed the server's time would nudge nobody; a gate
        // handed nothing at all would nudge everybody. Only a gate told each contact's zone sends to the contact
        // whose local time is 13:00 and stays quiet for the one whose local time is 22:00.
        var databasePath = DatabasePath("time-zones");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var scenario = new ReEngagementScenario
            {
                Cadence = DefinedMessageCadence(("Still interested?", 30)),
            };

            // Evaluate the window rather than forcing an answer, so the zone actually decides the outcome.
            scenario.BusinessHours.ForcedAnswer = null;

            var afternoonActivity = await SeedAwaitingConversationAsync(store, scenario, "+15555550120", _now.AddMinutes(-45));
            SetContactTimeZone(scenario, afternoonActivity, _afternoonTimeZoneId);

            var eveningActivity = await SeedAwaitingConversationAsync(store, scenario, "+15555550130", _now.AddMinutes(-45));
            SetContactTimeZone(scenario, eveningActivity, _eveningTimeZoneId);

            // Act
            await RunAsync(store, scenario);

            // Assert
            var message = Assert.Single(scenario.Sms.Sent);

            Assert.Equal("+15555550120", message.To);
            Assert.Equal(1, afternoonActivity.ReEngagementAttempts);
            Assert.Equal(0, eveningActivity.ReEngagementAttempts);

            // Both conversations reached the gate, and each carried its own contact's zone rather than the server's.
            Assert.Contains(scenario.BusinessHours.Evaluations, evaluation => evaluation.TimeZoneId == _afternoonTimeZoneId);
            Assert.Contains(scenario.BusinessHours.Evaluations, evaluation => evaluation.TimeZoneId == _eveningTimeZoneId);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task DoWorkAsync_WhenTheNamedBusinessHoursCalendarCannotBeEvaluated_FailsClosedAndSendsNoNudge()
    {
        // Arrange
        // A calendar can vanish from under an activity: the business-hours feature is turned off, or the calendar is
        // deleted while conversations that snapshotted its id are still running. The gate then has nothing to answer
        // with, and answering "open" would send after-hours messages on the strength of a calendar nobody can read.
        // The gate here is wired to say open, so only the missing-calendar check can stop the send.
        var databasePath = DatabasePath("missing-calendar");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var scenario = new ReEngagementScenario
            {
                Cadence = DefinedMessageCadence(("Still interested?", 30)),
            };

            scenario.BusinessHours.ForcedAnswer = true;
            scenario.BusinessHours.Calendars.Add(new BusinessHoursCalendarOption("some-other-calendar", "Support hours"));

            var activity = await SeedAwaitingConversationAsync(
                store,
                scenario,
                _contactDestination,
                _now.AddMinutes(-45),
                configure: seeded => seeded.BusinessHoursCalendarId = "calendar-that-no-longer-exists");

            // Act
            await RunAsync(store, scenario);

            // Assert
            Assert.Empty(scenario.Sms.Sent);
            Assert.Equal(0, activity.ReEngagementAttempts);

            // Declined before the gate was ever asked to open: an unreadable calendar is not a question worth asking.
            Assert.Empty(scenario.BusinessHours.Evaluations);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task DoWorkAsync_WhenTheNamedBusinessHoursCalendarIsAvailableAndOpen_SendsTheNudge()
    {
        // Arrange
        // The counterpart to failing closed: a conversation that names a calendar the gate can still resolve must
        // go through, otherwise "fail closed" would quietly mean "never nudge anything that names a calendar".
        var databasePath = DatabasePath("known-calendar");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var scenario = new ReEngagementScenario
            {
                Cadence = DefinedMessageCadence(("Still interested?", 30)),
            };

            scenario.BusinessHours.ForcedAnswer = true;
            scenario.BusinessHours.Calendars.Add(new BusinessHoursCalendarOption("sales-hours", "Sales hours"));

            var activity = await SeedAwaitingConversationAsync(
                store,
                scenario,
                _contactDestination,
                _now.AddMinutes(-45),
                configure: seeded => seeded.BusinessHoursCalendarId = "sales-hours");

            // Act
            await RunAsync(store, scenario);

            // Assert
            Assert.Single(scenario.Sms.Sent);
            Assert.Equal(1, activity.ReEngagementAttempts);

            var evaluation = Assert.Single(scenario.BusinessHours.Evaluations);

            Assert.Equal("sales-hours", evaluation.CalendarId);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task DoWorkAsync_WhenEveryCadenceStepHasAlreadyBeenSent_SendsNoFurtherNudge()
    {
        // Arrange
        // The step count is the only cap on how many follow-ups a contact ever receives. Without it the same
        // conversation keeps qualifying every five minutes for as long as it stays unanswered.
        var databasePath = DatabasePath("exhausted");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var scenario = new ReEngagementScenario
            {
                Cadence = DefinedMessageCadence(("First nudge", 30), ("Second nudge", 180)),
            };

            var activity = await SeedAwaitingConversationAsync(
                store,
                scenario,
                _contactDestination,
                _now.AddDays(-2),
                configure: seeded => seeded.ReEngagementAttempts = 2);

            // Act
            await RunAsync(store, scenario);

            // Assert
            Assert.Empty(scenario.Sms.Sent);
            Assert.Equal(2, activity.ReEngagementAttempts);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task DoWorkAsync_WhenTheCadenceIsDisabled_SendsNoNudge()
    {
        // Arrange
        // Turning a cadence off is how an operator stops follow-ups going out across every campaign using it, and
        // it has to take effect without editing or re-loading the conversations already pointing at it.
        var databasePath = DatabasePath("disabled-cadence");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var cadence = DefinedMessageCadence(("Still interested?", 30));
            cadence.Enabled = false;

            var scenario = new ReEngagementScenario
            {
                Cadence = cadence,
            };

            var activity = await SeedAwaitingConversationAsync(store, scenario, _contactDestination, _now.AddDays(-2));

            // Act
            await RunAsync(store, scenario);

            // Assert
            Assert.Empty(scenario.Sms.Sent);
            Assert.Equal(0, activity.ReEngagementAttempts);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task DoWorkAsync_WhenTheCadenceHasNoSteps_SendsNoNudge()
    {
        // Arrange
        // A cadence saved with its steps cleared has nothing to say. Reaching for a step index on an empty list
        // would throw once per conversation, per run, forever.
        var databasePath = DatabasePath("stepless-cadence");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var scenario = new ReEngagementScenario
            {
                Cadence = new Cadence
                {
                    ItemId = "cadence-1",
                    DisplayText = "Empty",
                    Enabled = true,
                    Steps = [],
                },
            };

            var activity = await SeedAwaitingConversationAsync(store, scenario, _contactDestination, _now.AddDays(-2));

            // Act
            await RunAsync(store, scenario);

            // Assert
            Assert.Empty(scenario.Sms.Sent);
            Assert.Equal(0, activity.ReEngagementAttempts);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task DoWorkAsync_WhenTheStepDelayHasNotElapsedSinceTheLastConversationActivity_SendsNoNudge()
    {
        // Arrange
        // The silence is measured from our own last message, not from when the activity was created, so a contact
        // who was written to twenty-nine minutes ago has not been quiet for thirty.
        var databasePath = DatabasePath("too-soon");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var scenario = new ReEngagementScenario
            {
                Cadence = DefinedMessageCadence(("Still interested?", 30)),
            };

            var activity = await SeedAwaitingConversationAsync(store, scenario, _contactDestination, _now.AddMinutes(-29));

            // Act
            await RunAsync(store, scenario);

            // Assert
            Assert.Empty(scenario.Sms.Sent);
            Assert.Equal(0, activity.ReEngagementAttempts);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task DoWorkAsync_MeasuresEachStepsDelayFromTheLastConversationActivity_SoSuccessiveStepsSpaceOut()
    {
        // Arrange
        // The step that applies is the one at the attempt count, and its delay is counted from the conversation's
        // last activity — which the previous nudge moved. Both conversations below have already had one nudge and
        // are far past the first step's thirty minutes; only the one that is also past the second step's three
        // hours may be written to again. Measuring from the first step's delay, or from the activity's creation,
        // would collapse the schedule and send both follow-ups almost back to back.
        var databasePath = DatabasePath("step-spacing");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var scenario = new ReEngagementScenario
            {
                Cadence = DefinedMessageCadence(("First nudge", 30), ("Second nudge", 180)),
            };

            var stillWaitingActivity = await SeedAwaitingConversationAsync(
                store,
                scenario,
                "+15555550140",
                _now.AddMinutes(-90),
                configure: seeded => seeded.ReEngagementAttempts = 1);

            var dueActivity = await SeedAwaitingConversationAsync(
                store,
                scenario,
                "+15555550150",
                _now.AddMinutes(-200),
                configure: seeded => seeded.ReEngagementAttempts = 1);

            // Act
            await RunAsync(store, scenario);

            // Assert
            var message = Assert.Single(scenario.Sms.Sent);

            Assert.Equal("+15555550150", message.To);
            Assert.Equal("Second nudge", message.Body);

            Assert.Equal(1, stillWaitingActivity.ReEngagementAttempts);
            Assert.Equal(2, dueActivity.ReEngagementAttempts);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task DoWorkAsync_WhenTheStepDelayIsNotPositive_SendsNoNudge()
    {
        // Arrange
        // A step saved with no delay would otherwise be due the instant the conversation goes quiet — the contact
        // would get a follow-up in the same breath as the message they have not answered yet.
        var databasePath = DatabasePath("zero-delay");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var scenario = new ReEngagementScenario
            {
                Cadence = DefinedMessageCadence(("Still interested?", 0)),
            };

            var activity = await SeedAwaitingConversationAsync(store, scenario, _contactDestination, _now.AddDays(-7));

            // Act
            await RunAsync(store, scenario);

            // Assert
            Assert.Empty(scenario.Sms.Sent);
            Assert.Equal(0, activity.ReEngagementAttempts);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task DoWorkAsync_WhenTheCustomersLastMessageIsUnanswered_SendsNoNudgeBecauseAReplyIsOwed()
    {
        // Arrange
        // A nudge only makes sense when we are the ones waiting. If the customer's message is the last real turn,
        // they are waiting on us, and asking "are you still there?" on top of an unanswered question reads as a
        // machine that is not listening. The generated prompt sitting after their message is bookkeeping, not a
        // reply, so it must not be mistaken for one.
        var databasePath = DatabasePath("owed-reply");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var scenario = new ReEngagementScenario
            {
                Cadence = DefinedMessageCadence(("Still interested?", 30)),
            };

            var activity = await SeedAwaitingConversationAsync(store, scenario, _contactDestination, _now.AddHours(-3));

            scenario.Prompts[activity.AISessionId] =
            [
                Prompt(activity.AISessionId, ChatRole.Assistant, "Hi there, are you still interested?", _now.AddHours(-3)),
                Prompt(activity.AISessionId, ChatRole.User, "What was the price again?", _now.AddMinutes(-40)),
                GeneratedPrompt(activity.AISessionId, ChatRole.Assistant, "[internal summary]", _now.AddMinutes(-39)),
            ];

            // Act
            await RunAsync(store, scenario);

            // Assert
            Assert.Empty(scenario.Sms.Sent);
            Assert.Equal(0, activity.ReEngagementAttempts);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task DoWorkAsync_WhenAReplyIsBeingComposedForTheConversation_SendsNoNudge()
    {
        // Arrange
        // A live inbound is mid-generation for this conversation: the customer is actively messaging right now.
        // Nudging across that produces two outbound texts seconds apart, one of them answering a question and the
        // other asking whether anyone is there.
        var databasePath = DatabasePath("generating");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var scenario = new ReEngagementScenario
            {
                Cadence = DefinedMessageCadence(("Still interested?", 30)),
            };

            var activity = await SeedAwaitingConversationAsync(store, scenario, _contactDestination, _now.AddHours(-3));

            using var generation = scenario.ConversationGate.Begin(activity.AISessionId, TestContext.Current.CancellationToken);

            // Act
            await RunAsync(store, scenario);

            // Assert
            Assert.Empty(scenario.Sms.Sent);
            Assert.Equal(0, activity.ReEngagementAttempts);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task DoWorkAsync_WhenTheContactHasAskedToStopReceivingSms_SendsNoNudge()
    {
        // Arrange
        // A conversation can be loaded, go quiet, and only then have the contact reply STOP or be marked
        // do-not-SMS by an agent. The activity still holds their number, and this task is the only thing left that
        // reads the contact before the next message goes out. Texting someone who has opted out is not a rough
        // edge — it is the carrier-visible violation the preference exists to prevent.
        //
        // This test currently FAILS: the task reads the contact only to pull their time zone and never looks at
        // DoNotSms. It asserts the intended behaviour, not today's.
        var databasePath = DatabasePath("opted-out");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var scenario = new ReEngagementScenario
            {
                Cadence = DefinedMessageCadence(("Still interested?", 30)),
            };

            var activity = await SeedAwaitingConversationAsync(store, scenario, _contactDestination, _now.AddMinutes(-45));

            scenario.Contacts[activity.ContactContentItemId]
                .Alter<OmnichannelContactPart>(part => part.SetDoNotSms(true, _now.AddMinutes(-10)));

            // Act
            await RunAsync(store, scenario);

            // Assert
            Assert.Empty(scenario.Sms.Sent);
            Assert.Equal(0, activity.ReEngagementAttempts);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task DoWorkAsync_WhenTheStepIsAiComposed_SendsTheModelsMessageAndPassesTheStepGuidance()
    {
        // Arrange
        // An AI-composed step hands the model the campaign's guidance and the conversation so far, and must send
        // what comes back rather than the guidance itself. Tools stay off: a follow-up is one sentence of prose,
        // and a nudge is not a turn where the model should be updating records or transferring anybody.
        var databasePath = DatabasePath("ai-step");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var scenario = new ReEngagementScenario
            {
                Cadence = new Cadence
                {
                    ItemId = "cadence-1",
                    DisplayText = "Follow up",
                    Enabled = true,
                    Steps =
                    [
                        new CadenceStep
                        {
                            DelayMinutes = 30,
                            IsAiGenerated = true,
                            Message = "Mention the free trial.",
                        },
                    ],
                },
                AssistantReply = "  Happy to set up that free trial whenever you are ready.  ",
            };

            var activity = await SeedAwaitingConversationAsync(store, scenario, _contactDestination, _now.AddMinutes(-45));

            // Act
            await RunAsync(store, scenario);

            // Assert
            var message = Assert.Single(scenario.Sms.Sent);

            Assert.Equal("Happy to set up that free trial whenever you are ready.", message.Body);

            var context = Assert.Single(scenario.CompletionContexts);

            Assert.Contains("Mention the free trial.", context.SystemMessage);
            Assert.True(context.DisableTools);
            Assert.Same(scenario.Sessions[activity.AISessionId], Assert.IsType<AIChatSession>(context.AdditionalProperties["Session"]));

            var transcript = Assert.Single(scenario.CompletionTranscripts);

            Assert.Equal("Hi there, are you still interested?", Assert.Single(transcript).Text);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static async Task RunAsync(IStore store, ReEngagementScenario scenario)
    {
        await using var workSession = store.CreateSession();
        var serviceProvider = BuildServiceProvider(workSession, scenario);

        await new SmsReEngagementBackgroundTask().DoWorkAsync(serviceProvider, TestContext.Current.CancellationToken);
    }

    private static ServiceProvider BuildServiceProvider(ISession session, ReEngagementScenario scenario)
    {
        var services = new ServiceCollection();

        services.AddSingleton(session);
        services.AddSingleton<TimeProvider>(new FakeTimeProvider(_now));
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        services.AddSingleton<ISmsService>(scenario.Sms);
        services.AddSingleton<IBusinessHoursGate>(scenario.BusinessHours);
        services.AddSingleton<IAutomatedConversationGate>(scenario.ConversationGate);
        services.AddSingleton<ILocalLock>(new InProcessLocalLock());
        services.AddSingleton<IAICompletionContextBuilder>(new PassThroughCompletionContextBuilder());
        services.AddSingleton<ISubjectFlowSettingsService>(new NoTimeoutSubjectFlowSettingsService());

        services.AddSingleton(PromptStore(scenario));
        services.AddSingleton(ChatSessionManager(scenario));
        services.AddSingleton(ProfileManager(scenario));
        services.AddSingleton(DeploymentManager());
        services.AddSingleton(CompletionService(scenario));
        services.AddSingleton(EndpointCatalog(scenario));
        services.AddSingleton(CadenceCatalog(scenario));
        services.AddSingleton(ActivityStore(scenario));
        services.AddSingleton(ContentManager(scenario));

        services.AddScoped<ISmsReEngagementCycle, SmsReEngagementCycle>();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// A prompt store backed by the scenario's per-session lists, so a nudge this task stores is visible to the
    /// next read the way the real store would make it.
    /// </summary>
    private static IAIChatSessionPromptStore PromptStore(ReEngagementScenario scenario)
    {
        var promptStore = new Mock<IAIChatSessionPromptStore>();

        promptStore
            .Setup(x => x.GetPromptsAsync(It.IsAny<string>()))
            .Returns((string sessionId) =>
                Task.FromResult<IReadOnlyList<AIChatSessionPrompt>>([.. scenario.PromptsFor(sessionId)]));

        promptStore
            .Setup(x => x.CreateAsync(It.IsAny<AIChatSessionPrompt>(), It.IsAny<CancellationToken>()))
            .Callback((AIChatSessionPrompt prompt, CancellationToken _) => scenario.PromptsFor(prompt.SessionId).Add(prompt))
            .Returns(ValueTask.CompletedTask);

        return promptStore.Object;
    }

    private static IAIChatSessionManager ChatSessionManager(ReEngagementScenario scenario)
    {
        var chatSessionManager = new Mock<IAIChatSessionManager>();

        chatSessionManager
            .Setup(x => x.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string sessionId, CancellationToken _) =>
                Task.FromResult(scenario.Sessions.TryGetValue(sessionId ?? string.Empty, out var chatSession) ? chatSession : null));

        chatSessionManager
            .Setup(x => x.SaveAsync(It.IsAny<AIChatSession>(), It.IsAny<CancellationToken>()))
            .Callback((AIChatSession chatSession, CancellationToken _) => scenario.SavedSessionIds.Add(chatSession.SessionId))
            .Returns(Task.CompletedTask);

        return chatSessionManager.Object;
    }

    private static IAIProfileManager ProfileManager(ReEngagementScenario scenario)
    {
        var profileManager = new Mock<IAIProfileManager>();

        profileManager
            .Setup(x => x.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<AIProfile>(scenario.Profile));

        return profileManager.Object;
    }

    private static IAIDeploymentManager DeploymentManager()
    {
        var deploymentManager = new Mock<IAIDeploymentManager>();

        deploymentManager
            .Setup(x => x.ResolveSlotAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<AIDeployment>(new AIDeployment { Name = "chat-deployment" }));

        return deploymentManager.Object;
    }

    private static IAICompletionService CompletionService(ReEngagementScenario scenario)
    {
        var completionService = new Mock<IAICompletionService>();

        completionService
            .Setup(x => x.CompleteAsync(
                It.IsAny<AIDeployment>(),
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<AICompletionContext>(),
                It.IsAny<CancellationToken>()))
            .Callback((AIDeployment deployment, IEnumerable<ChatMessage> messages, AICompletionContext context, CancellationToken token) =>
            {
                scenario.CompletionTranscripts.Add([.. messages]);
                scenario.CompletionContexts.Add(context);
            })
            .ReturnsAsync(() => new ChatResponse(new ChatMessage(ChatRole.Assistant, scenario.AssistantReply)));

        return completionService.Object;
    }

    private static ICatalog<OmnichannelChannelEndpoint> EndpointCatalog(ReEngagementScenario scenario)
    {
        var catalog = new Mock<ICatalog<OmnichannelChannelEndpoint>>();

        catalog
            .Setup(x => x.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string itemId, CancellationToken _) => new ValueTask<OmnichannelChannelEndpoint>(
                string.Equals(scenario.Endpoint.ItemId, itemId, StringComparison.Ordinal) ? scenario.Endpoint : null));

        return catalog.Object;
    }

    private static ICatalog<Cadence> CadenceCatalog(ReEngagementScenario scenario)
    {
        var catalog = new Mock<ICatalog<Cadence>>();

        catalog
            .Setup(x => x.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string itemId, CancellationToken _) => new ValueTask<Cadence>(
                scenario.Cadence is not null && string.Equals(scenario.Cadence.ItemId, itemId, StringComparison.Ordinal)
                    ? scenario.Cadence
                    : null));

        return catalog.Object;
    }

    /// <summary>
    /// An activity store that hands back the very instances the test seeded, so the counters the task writes under
    /// its lock are the ones the assertions read.
    /// </summary>
    private static IOmnichannelActivityStore ActivityStore(ReEngagementScenario scenario)
    {
        var activityStore = new Mock<IOmnichannelActivityStore>();

        activityStore
            .Setup(x => x.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string itemId, CancellationToken _) => new ValueTask<OmnichannelActivity>(
                scenario.Activities.TryGetValue(itemId ?? string.Empty, out var activity) ? activity : null));

        activityStore
            .Setup(x => x.UpdateAsync(It.IsAny<OmnichannelActivity>(), It.IsAny<CancellationToken>()))
            .Callback((OmnichannelActivity activity, CancellationToken _) => scenario.UpdatedActivityIds.Add(activity.ItemId))
            .Returns(ValueTask.CompletedTask);

        return activityStore.Object;
    }

    private static IContentManager ContentManager(ReEngagementScenario scenario)
    {
        var contentManager = new Mock<IContentManager>();

        contentManager
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<VersionOptions>()))
            .ReturnsAsync((string contentItemId, VersionOptions _) =>
                scenario.Contacts.TryGetValue(contentItemId ?? string.Empty, out var contact) ? contact : null);

        return contentManager.Object;
    }

    private static Cadence DefinedMessageCadence(params (string Message, int DelayMinutes)[] steps)
    {
        return new Cadence
        {
            ItemId = "cadence-1",
            DisplayText = "Follow up",
            Enabled = true,
            Steps = [.. steps.Select(step => new CadenceStep
            {
                DelayMinutes = step.DelayMinutes,
                IsAiGenerated = false,
                Message = step.Message,
            })],
        };
    }

    private static async Task<OmnichannelActivity> SeedAwaitingConversationAsync(
        IStore store,
        ReEngagementScenario scenario,
        string preferredDestination,
        DateTime lastActivityUtc,
        Action<OmnichannelActivity> configure = null)
    {
        var sessionId = IdGenerator.GenerateId();
        var contactContentItemId = IdGenerator.GenerateId();

        var activity = new OmnichannelActivity
        {
            ItemId = IdGenerator.GenerateId(),
            Channel = OmnichannelConstants.Channels.Sms,
            ChannelEndpointId = scenario.Endpoint.ItemId,
            ContactContentItemId = contactContentItemId,
            ContactContentType = "Lead",
            SubjectContentType = "LeadFollowUp",
            PreferredDestination = preferredDestination,
            ScheduledUtc = _now.AddDays(-1),
            CreatedUtc = _now.AddDays(-1),
            InteractionType = ActivityInteractionType.Automated,
            Status = ActivityStatus.AwaitingCustomerAnswer,
            AISessionId = sessionId,
            AIProfileId = "profile-1",
            CadenceId = scenario.Cadence?.ItemId,
        };

        configure?.Invoke(activity);

        await using (var seedSession = store.CreateSession())
        {
            await seedSession.SaveAsync(
                activity,
                collection: OmnichannelConstants.CollectionName,
                cancellationToken: TestContext.Current.CancellationToken);

            await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        scenario.Activities[activity.ItemId] = activity;

        scenario.Sessions[sessionId] = new AIChatSession
        {
            SessionId = sessionId,
            ProfileId = "profile-1",
            LastActivityUtc = lastActivityUtc,
        };

        // The conversation opened with our message and the contact never answered: the state every nudge starts from.
        scenario.Prompts[sessionId] =
        [
            Prompt(sessionId, ChatRole.Assistant, "Hi there, are you still interested?", lastActivityUtc),
        ];

        scenario.Contacts[contactContentItemId] = new ContentItem
        {
            ContentItemId = contactContentItemId,
            ContentType = "Lead",
        };

        return activity;
    }

    private static void SetContactTimeZone(ReEngagementScenario scenario, OmnichannelActivity activity, string timeZoneId)
    {
        scenario.Contacts[activity.ContactContentItemId]
            .Alter<OmnichannelContactPart>(part => part.TimeZoneId = timeZoneId);
    }

    private static AIChatSessionPrompt Prompt(string sessionId, ChatRole role, string content, DateTime createdUtc)
    {
        return new AIChatSessionPrompt
        {
            ItemId = IdGenerator.GenerateId(),
            SessionId = sessionId,
            Role = role,
            Content = content,
            CreatedUtc = createdUtc,
        };
    }

    private static AIChatSessionPrompt GeneratedPrompt(string sessionId, ChatRole role, string content, DateTime createdUtc)
    {
        var prompt = Prompt(sessionId, role, content, createdUtc);
        prompt.IsGeneratedPrompt = true;

        return prompt;
    }

    private static string DatabasePath(string purpose)
        => Path.Combine(Path.GetTempPath(), $"sms-re-engagement-{purpose}-{Guid.NewGuid():N}.db");

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new OmnichannelActivityIndexProvider()]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(
            OmnichannelConstants.CollectionName,
            TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
        await schemaBuilder.CreateMapIndexTableAsync<OmnichannelActivityIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<ActivityKind>("Kind")
            .Column<string>("Source", column => column.WithLength(50))
            .Column<string>("Channel", column => column.WithLength(50))
            .Column<string>("ChannelEndpointId", column => column.WithLength(26))
            .Column<string>("PreferredDestination", column => column.WithLength(255))
            .Column<string>("ContactContentItemId", column => column.WithLength(26))
            .Column<string>("ContactContentType", column => column.WithLength(255))
            .Column<string>("CampaignId", column => column.WithLength(26))
            .Column<string>("SubjectContentType", column => column.WithLength(26))
            .Column<DateTime>("ScheduledUtc", column => column.NotNull())
            .Column<DateTime>("CompletedUtc")
            .Column<int>("Attempts", column => column.NotNull())
            .Column<string>("AssignedToId", column => column.WithLength(26))
            .Column<DateTime>("AssignedToUtc")
            .Column<ActivityAssignmentStatus>("AssignmentStatus")
            .Column<string>("ReservationId", column => column.WithLength(26))
            .Column<string>("ReservedById", column => column.WithLength(26))
            .Column<DateTime>("ReservedUtc")
            .Column<DateTime>("ReservationExpiresUtc")
            .Column<string>("CreatedById", column => column.WithLength(26))
            .Column<string>("DispositionId", column => column.WithLength(26))
            .Column<DateTime>("CreatedUtc", column => column.NotNull())
            .Column<ActivityUrgencyLevel>("UrgencyLevel")
            .Column<ActivityStatus>("Status")
            .Column<ActivityInteractionType>("InteractionType")
            .Column<bool>("AiEscalated"),
            collection: OmnichannelConstants.CollectionName);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }

    /// <summary>
    /// The state one run of the task reads and writes: the conversations, the contacts behind them, and the
    /// collaborators that record what the task tried to do with them.
    /// </summary>
    private sealed class ReEngagementScenario
    {
        public Cadence Cadence { get; set; }

        public OmnichannelChannelEndpoint Endpoint { get; } = new()
        {
            ItemId = "endpoint-1",
            Channel = OmnichannelConstants.Channels.Sms,
            Value = _endpointNumber,
        };

        public AIProfile Profile { get; } = new()
        {
            Name = "sales-agent",
            Type = AIProfileType.Chat,
        };

        public string AssistantReply { get; set; } = "Just checking in.";

        public Dictionary<string, OmnichannelActivity> Activities { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, AIChatSession> Sessions { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, List<AIChatSessionPrompt>> Prompts { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, ContentItem> Contacts { get; } = new(StringComparer.Ordinal);

        public List<string> UpdatedActivityIds { get; } = [];

        public List<string> SavedSessionIds { get; } = [];

        public List<AICompletionContext> CompletionContexts { get; } = [];

        public List<List<ChatMessage>> CompletionTranscripts { get; } = [];

        public RecordingSmsService Sms { get; } = new();

        public StubBusinessHoursGate BusinessHours { get; } = new();

        public InMemoryAutomatedConversationGate ConversationGate { get; } = new(TimeProvider.System);

        public List<AIChatSessionPrompt> PromptsFor(string sessionId)
        {
            if (!Prompts.TryGetValue(sessionId ?? string.Empty, out var prompts))
            {
                prompts = [];
                Prompts[sessionId ?? string.Empty] = prompts;
            }

            return prompts;
        }
    }

    /// <summary>
    /// An SMS service that records what would have gone to the carrier instead of sending it.
    /// </summary>
    private sealed class RecordingSmsService : ISmsService
    {
        public List<SmsMessage> Sent { get; } = [];

        public Task<Result> SendAsync(SmsMessage message, CancellationToken cancellationToken = default)
        {
            Sent.Add(message);

            return Task.FromResult(Result.Success());
        }
    }

    /// <summary>
    /// A business-hours gate that either answers as told or evaluates a nine-to-six window in the contact's zone,
    /// resolved from fixed offsets so the verdict does not depend on the machine's time-zone database.
    /// </summary>
    private sealed class StubBusinessHoursGate : IBusinessHoursGate
    {
        private static readonly Dictionary<string, TimeSpan> _offsets = new(StringComparer.OrdinalIgnoreCase)
        {
            [_afternoonTimeZoneId] = TimeSpan.FromHours(-7),
            [_eveningTimeZoneId] = TimeSpan.FromHours(2),
        };

        /// <summary>
        /// Gets or sets the answer every evaluation returns; <see langword="null"/> evaluates the local window.
        /// </summary>
        public bool? ForcedAnswer { get; set; } = true;

        public List<BusinessHoursCalendarOption> Calendars { get; } = [];

        public List<(string CalendarId, string TimeZoneId)> Evaluations { get; } = [];

        public Task<bool> IsOpenAsync(string calendarId, DateTime utcInstant, string timeZoneId, CancellationToken cancellationToken = default)
        {
            Evaluations.Add((calendarId, timeZoneId));

            if (ForcedAnswer.HasValue)
            {
                return Task.FromResult(ForcedAnswer.Value);
            }

            var offset = timeZoneId is not null && _offsets.TryGetValue(timeZoneId, out var found)
                ? found
                : TimeSpan.Zero;

            var localHour = utcInstant.Add(offset).Hour;

            return Task.FromResult(localHour >= 9 && localHour < 18);
        }

        public Task<IReadOnlyList<BusinessHoursCalendarOption>> GetCalendarOptionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<BusinessHoursCalendarOption>>(Calendars);
    }

    /// <summary>
    /// A real in-process lock. A mock would report the lock as not acquired and the task would silently do nothing,
    /// which is the same observable result as the gate declining — and would make every assertion here meaningless.
    /// </summary>
    private sealed class InProcessLocalLock : ILocalLock
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new(StringComparer.Ordinal);

        public async Task<ILocker> AcquireLockAsync(string key, TimeSpan? expiration = null)
        {
            var semaphore = _semaphores.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();

            return new Locker(semaphore);
        }

        public async Task<(ILocker locker, bool locked)> TryAcquireLockAsync(string key, TimeSpan timeout, TimeSpan? expiration = null)
        {
            var semaphore = _semaphores.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            var locked = await semaphore.WaitAsync(timeout);

            return locked
                ? (new Locker(semaphore), true)
                : (null, false);
        }

        public Task<bool> IsLockAcquiredAsync(string key)
            => Task.FromResult(_semaphores.TryGetValue(key, out var semaphore) && semaphore.CurrentCount == 0);

        private sealed class Locker : ILocker
        {
            private readonly SemaphoreSlim _semaphore;
            private bool _released;

            public Locker(SemaphoreSlim semaphore)
            {
                _semaphore = semaphore;
            }

            public void Dispose()
                => Release();

            public ValueTask DisposeAsync()
            {
                Release();

                return ValueTask.CompletedTask;
            }

            private void Release()
            {
                if (_released)
                {
                    return;
                }

                _released = true;
                _semaphore.Release();
            }
        }
    }

    /// <summary>
    /// A completion-context builder that applies the caller's configuration to a fresh context, so the system
    /// message and tool switch the task sets are the ones the assertions see.
    /// </summary>
    private sealed class PassThroughCompletionContextBuilder : IAICompletionContextBuilder
    {
        public ValueTask<AICompletionContext> BuildAsync(object resource, Action<AICompletionContext> configure, CancellationToken cancellationToken = default)
        {
            var context = new AICompletionContext();
            configure?.Invoke(context);

            return ValueTask.FromResult(context);
        }
    }

    /// <summary>
    /// A flow-settings service for subjects that define no no-response timeout, so the task's deadline refresh is
    /// skipped and nothing here depends on it.
    /// </summary>
    private sealed class NoTimeoutSubjectFlowSettingsService : ISubjectFlowSettingsService
    {
        public Task<IReadOnlyList<SubjectFlowSettings>> GetConfiguredFlowSettingsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SubjectFlowSettings>>([]);

        public Task<SubjectFlowSettings> FindConfiguredFlowSettingsAsync(string subjectContentType, CancellationToken cancellationToken = default)
            => Task.FromResult<SubjectFlowSettings>(null);

        public Task<IReadOnlyList<SubjectDefinition>> GetConfiguredSubjectDefinitionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SubjectDefinition>>([]);

        public Task<IReadOnlyList<SubjectDefinition>> GetConfiguredSubjectDefinitionsAsync(SubjectDirection direction, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SubjectDefinition>>([]);

        public bool IsConfigured(SubjectFlowSettings flowSettings)
            => false;
    }
}
