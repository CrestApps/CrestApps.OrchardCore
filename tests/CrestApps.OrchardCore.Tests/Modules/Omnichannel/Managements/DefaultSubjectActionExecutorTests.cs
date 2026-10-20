using System.Linq.Expressions;
using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Modules;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements;

public sealed class DefaultSubjectActionExecutorTests
{
    private static readonly DateTime _now = new(2026, 7, 11, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(SubjectActionOwnerAssignmentType.SameOwner, null, SubjectActionOwnerAssignmentType.SameOwner)]
    [InlineData(SubjectActionOwnerAssignmentType.SameOwner, "LEGACY", SubjectActionOwnerAssignmentType.SpecificOwner)]
    [InlineData(SubjectActionOwnerAssignmentType.SpecificOwner, null, SubjectActionOwnerAssignmentType.SpecificOwner)]
    public void Resolve_AssignmentMetadata_InfersEffectiveLegacyMode(
        SubjectActionOwnerAssignmentType assignmentType,
        string normalizedUserName,
        SubjectActionOwnerAssignmentType expected)
    {
        // Act
        var result = SubjectActionOwnerAssignmentTypeResolver.Resolve(assignmentType, normalizedUserName);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("TryAgain")]
    [InlineData("NewActivity")]
    public async Task ExecuteAsync_SameOwner_AssignsCompletingUser(string actionType)
    {
        // Arrange
        var action = CreateAction(actionType, SubjectActionOwnerAssignmentType.SameOwner);
        var session = new Mock<ISession>();
        OmnichannelActivity savedActivity = null;

        SetupSave(session, activity => savedActivity = activity);

        var executor = CreateExecutor(action, session);

        // Act
        await executor.ExecuteAsync(CreateContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(savedActivity);
        Assert.Equal("completing-user-id", savedActivity.AssignedToId);
        Assert.Equal("Completing User", savedActivity.AssignedToUsername);
        Assert.Equal(_now, savedActivity.AssignedToUtc);
        Assert.Equal(ActivityAssignmentStatus.Assigned, savedActivity.AssignmentStatus);
    }

    [Theory]
    [InlineData("TryAgain")]
    [InlineData("NewActivity")]
    public async Task ExecuteAsync_LegacyUsername_ResolvesSpecificOwner(string actionType)
    {
        // Arrange
        var action = CreateAction(actionType, SubjectActionOwnerAssignmentType.SameOwner, "SPECIFIC");
        var owner = new User
        {
            UserId = "specific-user-id",
            UserName = "Specific User",
            NormalizedUserName = "SPECIFIC",
        };
        var session = CreateSessionWithUser(owner);
        OmnichannelActivity savedActivity = null;

        SetupSave(session, activity => savedActivity = activity);

        var executor = CreateExecutor(action, session);

        // Act
        await executor.ExecuteAsync(CreateContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(savedActivity);
        Assert.Equal(owner.UserId, savedActivity.AssignedToId);
        Assert.Equal(owner.UserName, savedActivity.AssignedToUsername);
        Assert.Equal(_now, savedActivity.AssignedToUtc);
        Assert.Equal(ActivityAssignmentStatus.Assigned, savedActivity.AssignmentStatus);
    }

    [Theory]
    [InlineData("TryAgain")]
    [InlineData("NewActivity")]
    public async Task ExecuteAsync_MissingSpecificOwner_SkipsFollowUp(string actionType)
    {
        // Arrange
        var action = CreateAction(actionType, SubjectActionOwnerAssignmentType.SpecificOwner, "DELETED");
        var session = CreateSessionWithUser(null);
        var executor = CreateExecutor(action, session);

        // Act
        await executor.ExecuteAsync(CreateContext(), TestContext.Current.CancellationToken);

        // Assert
        session.Verify(
            x => x.SaveAsync(It.IsAny<object>(), false, OmnichannelConstants.CollectionName, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_LocalScheduleDate_ConvertsToUtc()
    {
        // Arrange
        var action = CreateAction(OmnichannelConstants.ActionTypes.TryAgain, SubjectActionOwnerAssignmentType.SameOwner);
        var session = new Mock<ISession>();
        var localDate = new DateTime(2026, 7, 15, 9, 30, 0, DateTimeKind.Unspecified);
        var expectedUtc = new DateTime(2026, 7, 15, 16, 30, 0, DateTimeKind.Utc);
        OmnichannelActivity savedActivity = null;

        SetupSave(session, activity => savedActivity = activity);

        var localClock = new Mock<ILocalClock>();
        localClock
            .Setup(x => x.ConvertToUtcAsync(localDate))
            .ReturnsAsync(expectedUtc);

        var executor = CreateExecutor(action, session, localClock.Object);
        var context = CreateContext();
        context.ActionScheduleDates = new Dictionary<string, DateTime?>
        {
            [action.ItemId] = localDate,
        };

        // Act
        await executor.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(savedActivity);
        Assert.Equal(expectedUtc, savedActivity.ScheduledUtc);
    }

    [Theory]
    [InlineData(null, false, false)]
    [InlineData(24, false, true)]
    [InlineData(null, true, true)]
    public async Task ExecuteAsync_TryAgain_SaysWhetherTheScheduleWasChosen(int? defaultScheduleHours, bool datePicked, bool expectedChosen)
    {
        // Arrange
        var action = CreateAction(OmnichannelConstants.ActionTypes.TryAgain, SubjectActionOwnerAssignmentType.SameOwner);
        action.Put(new TryAgainActionMetadata
        {
            AssignmentType = SubjectActionOwnerAssignmentType.SameOwner,
            DefaultScheduleHours = defaultScheduleHours,
        });

        var session = new Mock<ISession>();
        SetupSave(session, _ => { });

        FollowUpActivityContext followUp = null;
        var handler = new Mock<IFollowUpActivityHandler>();
        handler
            .Setup(x => x.CreatingAsync(It.IsAny<FollowUpActivityContext>(), It.IsAny<CancellationToken>()))
            .Callback<FollowUpActivityContext, CancellationToken>((value, _) => followUp = value)
            .Returns(Task.CompletedTask);

        var executor = CreateExecutor(action, session, followUpHandlers: [handler.Object]);
        var context = CreateContext();

        if (datePicked)
        {
            context.ActionScheduleDates = new Dictionary<string, DateTime?>
            {
                [action.ItemId] = _now.AddHours(2),
            };
        }

        // Act
        await executor.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert: with neither a picked date nor default hours, the carrier, such as a dialer, may set its own wait.
        Assert.NotNull(followUp);
        Assert.Equal(expectedChosen, followUp.HasChosenSchedule);
    }

    [Theory]
    [InlineData("TryAgain", ContactResolutionStatus.Unknown)]
    [InlineData("TryAgain", ContactResolutionStatus.Unresolved)]
    [InlineData("TryAgain", ContactResolutionStatus.Ambiguous)]
    [InlineData("NewActivity", ContactResolutionStatus.Unresolved)]
    [InlineData("NewActivity", ContactResolutionStatus.Ambiguous)]
    public async Task ExecuteAsync_UnresolvedOrAmbiguousContact_SkipsFollowUpSave(
        string actionType,
        ContactResolutionStatus resolutionStatus)
    {
        // Arrange
        var action = CreateAction(actionType, SubjectActionOwnerAssignmentType.SameOwner);
        var session = new Mock<ISession>();
        var executor = CreateExecutor(action, session);
        var context = CreateContext();
        context.Activity.ContactResolutionStatus = resolutionStatus;
        context.Activity.Source = ActivitySources.Inbound;

        // Act
        await executor.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        session.Verify(
            x => x.SaveAsync(It.IsAny<object>(), false, OmnichannelConstants.CollectionName, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(ContactResolutionStatus.Unknown)]
    [InlineData(ContactResolutionStatus.Unresolved)]
    [InlineData(ContactResolutionStatus.Ambiguous)]
    public async Task ExecuteAsync_UnresolvedOrAmbiguousContact_SkipsContactPreferenceMutation(
        ContactResolutionStatus resolutionStatus)
    {
        // Arrange
        var action = CreateAction(OmnichannelConstants.ActionTypes.TryAgain, SubjectActionOwnerAssignmentType.SameOwner);
        action.SetDoNotCall = true;
        var session = new Mock<ISession>();
        var executor = CreateExecutor(action, session);
        var contact = new ContentItem();
        var context = CreateContext();
        context.Contact = contact;
        context.Activity.ContactResolutionStatus = resolutionStatus;
        context.Activity.Source = ActivitySources.Inbound;

        // Act
        await executor.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(contact.TryGet<OmnichannelContactPart>(out _));
    }

    [Theory]
    [InlineData("TryAgain")]
    [InlineData("NewActivity")]
    public async Task ExecuteAsync_ResolvedContact_ExecutesFollowUpActions(string actionType)
    {
        // Arrange
        var action = CreateAction(actionType, SubjectActionOwnerAssignmentType.SameOwner);
        var session = new Mock<ISession>();
        OmnichannelActivity savedActivity = null;

        SetupSave(session, activity => savedActivity = activity);

        var executor = CreateExecutor(action, session);
        var context = CreateContext();
        context.Activity.ContactResolutionStatus = ContactResolutionStatus.Resolved;

        // Act
        await executor.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(savedActivity);
    }

    [Fact]
    public async Task ExecuteAsync_ResolvedContact_AppliesContactPreferences()
    {
        // Arrange
        var action = CreateAction(OmnichannelConstants.ActionTypes.TryAgain, SubjectActionOwnerAssignmentType.SameOwner);
        action.SetDoNotCall = true;
        var session = new Mock<ISession>();

        SetupSave(session, _ => { });

        var executor = CreateExecutor(action, session);
        var contact = new ContentItem();
        var context = CreateContext();
        context.Contact = contact;
        context.Activity.ContactResolutionStatus = ContactResolutionStatus.Resolved;

        // Act
        await executor.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(contact.TryGet<OmnichannelContactPart>(out var part));
        Assert.True(part.DoNotCall);
    }

    [Theory]
    [InlineData("TryAgain")]
    [InlineData("NewActivity")]
    public async Task ExecuteAsync_WithPreparationNotes_SetsFollowUpInstructions(string actionType)
    {
        // Arrange
        var action = CreateAction(actionType, SubjectActionOwnerAssignmentType.SameOwner);
        var session = new Mock<ISession>();
        OmnichannelActivity savedActivity = null;

        SetupSave(session, activity => savedActivity = activity);

        var executor = CreateExecutor(action, session);
        var context = CreateContext();
        context.ActionPreparationNotes = new Dictionary<string, string>
        {
            [action.ItemId] = "  Call back after lunch.  ",
        };

        // Act
        await executor.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(savedActivity);
        Assert.Equal("Call back after lunch.", savedActivity.Instructions);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutPreparationNotes_TryAgainKeepsOriginalInstructions()
    {
        // Arrange
        var action = CreateAction(OmnichannelConstants.ActionTypes.TryAgain, SubjectActionOwnerAssignmentType.SameOwner);
        var session = new Mock<ISession>();
        OmnichannelActivity savedActivity = null;

        SetupSave(session, activity => savedActivity = activity);

        var executor = CreateExecutor(action, session);
        var context = CreateContext();
        context.Activity.Instructions = "Original instructions.";

        // Act
        await executor.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(savedActivity);
        Assert.Equal("Original instructions.", savedActivity.Instructions);
    }

    [Fact]
    public async Task ExecuteAsync_TryAgainOnAnAutomatedCall_TheRetryIsPlacedTheSameWay()
    {
        // Arrange
        // An automated call that rang out was retried as a manual task with no AI profile, so the processor picked
        // it up with nothing to place the call and the contact was never tried again.
        var action = CreateAction(OmnichannelConstants.ActionTypes.TryAgain, SubjectActionOwnerAssignmentType.SameOwner);
        var session = new Mock<ISession>();
        OmnichannelActivity savedActivity = null;

        SetupSave(session, activity => savedActivity = activity);

        var executor = CreateExecutor(action, session);
        var context = CreateContext();
        var activity = context.Activity;
        activity.Kind = ActivityKind.Call;
        activity.Source = ActivitySources.Automatic;
        activity.InteractionType = ActivityInteractionType.Automated;
        activity.AISessionId = "previous-session-id";
        activity.AIProfileId = "ai-profile-id";
        activity.SpeechToTextDeploymentName = "stt";
        activity.TextToSpeechDeploymentName = "tts";
        activity.TextToSpeechVoiceId = "voice";
        activity.UseCallAmbience = true;
        activity.AllowAIToUpdateContact = true;
        activity.AllowAIToUpdateSubject = false;
        activity.ResponseDelayMode = OmnichannelResponseDelayMode.Fixed;
        activity.ResponseDelaySeconds = 3;
        activity.ResponseDelayJitterSeconds = 2;
        activity.BusinessHoursCalendarId = "calendar-id";
        activity.CadenceId = "cadence-id";
        activity.ReEngagementAttempts = 2;
        activity.LastReEngagementUtc = _now.AddHours(-1);

        // Act
        await executor.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(savedActivity);
        Assert.Equal(ActivityKind.Call, savedActivity.Kind);
        Assert.Equal(ActivitySources.Automatic, savedActivity.Source);
        Assert.Equal("ai-profile-id", savedActivity.AIProfileId);
        Assert.Equal("stt", savedActivity.SpeechToTextDeploymentName);
        Assert.Equal("tts", savedActivity.TextToSpeechDeploymentName);
        Assert.Equal("voice", savedActivity.TextToSpeechVoiceId);
        Assert.True(savedActivity.UseCallAmbience);
        Assert.True(savedActivity.AllowAIToUpdateContact);
        Assert.False(savedActivity.AllowAIToUpdateSubject);
        Assert.Equal(OmnichannelResponseDelayMode.Fixed, savedActivity.ResponseDelayMode);
        Assert.Equal(3, savedActivity.ResponseDelaySeconds);
        Assert.Equal(2, savedActivity.ResponseDelayJitterSeconds);
        Assert.Equal("calendar-id", savedActivity.BusinessHoursCalendarId);
        Assert.Equal("cadence-id", savedActivity.CadenceId);

        // The retry is a new conversation, and its re-engagement count starts over.
        Assert.Null(savedActivity.AISessionId);
        Assert.Equal(0, savedActivity.ReEngagementAttempts);
        Assert.Null(savedActivity.LastReEngagementUtc);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutPreparationNotes_NewActivityHasNoInstructions()
    {
        // Arrange
        var action = CreateAction(OmnichannelConstants.ActionTypes.NewActivity, SubjectActionOwnerAssignmentType.SameOwner);
        var session = new Mock<ISession>();
        OmnichannelActivity savedActivity = null;

        SetupSave(session, activity => savedActivity = activity);

        var executor = CreateExecutor(action, session);
        var context = CreateContext();
        context.Activity.Instructions = "Original instructions.";

        // Act
        await executor.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(savedActivity);
        Assert.Null(savedActivity.Instructions);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAPreferenceIsSet_SavesTheContact()
    {
        // Arrange
        // The preference was applied to the content item in memory and nothing wrote it back, so a customer who
        // asked not to be called was dispositioned correctly and dialled again on the next load.
        var action = CreateAction(OmnichannelConstants.ActionTypes.Finish, SubjectActionOwnerAssignmentType.SameOwner);
        action.SetDoNotCall = true;

        var session = new Mock<ISession>();
        var contentManager = new Mock<IContentManager>();
        var executor = CreateExecutor(action, session, contentManager: contentManager);

        var contact = new ContentItem();
        var context = CreateContext();
        context.Contact = contact;
        context.Activity.ContactResolutionStatus = ContactResolutionStatus.Resolved;

        // Act
        await executor.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        contentManager.Verify(x => x.UpdateAsync(contact), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenThePublishedContactChanges_ThatChangeIsPublished()
    {
        // Arrange
        // The lists that decide who gets dialled query published contacts, so a preference left on the draft
        // suppresses nothing.
        var action = CreateAction(OmnichannelConstants.ActionTypes.Finish, SubjectActionOwnerAssignmentType.SameOwner);
        action.SetDoNotSms = true;

        var session = new Mock<ISession>();
        var contentManager = new Mock<IContentManager>();
        var executor = CreateExecutor(action, session, contentManager: contentManager);

        var contact = new ContentItem { Published = true };
        var context = CreateContext();
        context.Contact = contact;
        context.Activity.ContactResolutionStatus = ContactResolutionStatus.Resolved;

        // Act
        await executor.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        contentManager.Verify(x => x.PublishAsync(contact), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoPreferenceIsSet_LeavesTheContactAlone()
    {
        // Arrange
        // Most dispositions say nothing about how the customer may be contacted, and those must not write a new
        // version of the contact every time an activity is completed.
        var action = CreateAction(OmnichannelConstants.ActionTypes.Finish, SubjectActionOwnerAssignmentType.SameOwner);

        var session = new Mock<ISession>();
        var contentManager = new Mock<IContentManager>();
        var executor = CreateExecutor(action, session, contentManager: contentManager);

        var contact = new ContentItem { Published = true };
        var context = CreateContext();
        context.Contact = contact;
        context.Activity.ContactResolutionStatus = ContactResolutionStatus.Resolved;

        // Act
        await executor.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        contentManager.Verify(x => x.UpdateAsync(It.IsAny<ContentItem>()), Times.Never);
        contentManager.Verify(x => x.PublishAsync(It.IsAny<ContentItem>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_DispositionNamesALeadStatus_MovesTheLeadWithNoActionWired()
    {
        // Arrange
        // The platform completes a call to a dead number on its own, with a disposition no subject wires actions to,
        // so the lead used to stay "Open - Not Contacted" for ever. The status set on the disposition applies anyway.
        var contentManager = new Mock<IContentManager>();
        var otherSubjectAction = CreateAction(OmnichannelConstants.ActionTypes.Finish, SubjectActionOwnerAssignmentType.SameOwner);
        otherSubjectAction.SubjectContentType = "OtherSubject";
        var executor = CreateExecutor(otherSubjectAction, new Mock<ISession>(), contentManager: contentManager);

        var contact = new ContentItem();
        contact.Alter<LeadPart>(part => part.StatusId = "open");
        var context = CreateContext();
        context.Contact = contact;
        context.Disposition.Put(new SetLeadStatusActionMetadata { StatusId = "closed" });
        context.Activity.ContactResolutionStatus = ContactResolutionStatus.Resolved;

        // Act
        await executor.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("closed", GetLeadStatusId(contact));
        contentManager.Verify(x => x.UpdateAsync(contact), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_SubjectActionNamesALeadStatus_TakesPrecedenceOverTheDisposition()
    {
        // Arrange
        var action = CreateAction(OmnichannelConstants.ActionTypes.Finish, SubjectActionOwnerAssignmentType.SameOwner);
        action.Put(new SetLeadStatusActionMetadata { StatusId = "nurturing" });
        var contentManager = new Mock<IContentManager>();
        var executor = CreateExecutor(action, new Mock<ISession>(), contentManager: contentManager);

        var contact = new ContentItem();
        contact.Alter<LeadPart>(part => part.StatusId = "open");
        var context = CreateContext();
        context.Contact = contact;
        context.Disposition.Put(new SetLeadStatusActionMetadata { StatusId = "closed" });
        context.Activity.ContactResolutionStatus = ContactResolutionStatus.Resolved;

        // Act
        await executor.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("nurturing", GetLeadStatusId(contact));
        contentManager.Verify(x => x.UpdateAsync(contact), Times.Once);
    }

    private static string GetLeadStatusId(ContentItem contact)
        => contact.TryGet<LeadPart>(out var part) ? part.StatusId : null;

    private static DefaultSubjectActionExecutor CreateExecutor(
        SubjectAction action,
        Mock<ISession> session,
        ILocalClock localClock = null,
        Mock<IContentManager> contentManager = null,
        IEnumerable<IFollowUpActivityHandler> followUpHandlers = null)
    {
        var actionCatalog = new Mock<ISourceCatalog<SubjectAction>>();
        actionCatalog
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { action });

        contentManager ??= new Mock<IContentManager>();
        contentManager
            .Setup(x => x.NewAsync(It.IsAny<string>()))
            .ReturnsAsync((string contentType) => new ContentItem { ContentType = contentType });

        var clock = new Mock<IClock>();
        clock.SetupGet(x => x.UtcNow).Returns(_now);

        return new DefaultSubjectActionExecutor(
            actionCatalog.Object,
            Mock.Of<ISubjectFlowSettingsService>(),
            contentManager.Object,
            Mock.Of<INotInServiceNumberService>(),
            session.Object,
            clock.Object,
            localClock ?? Mock.Of<ILocalClock>(),
            TestContactMethods.Accessor,
            NullLogger<DefaultSubjectActionExecutor>.Instance,
            followUpHandlers: followUpHandlers);
    }

    private static SubjectAction CreateAction(
        string actionType,
        SubjectActionOwnerAssignmentType assignmentType,
        string normalizedUserName = null)
    {
        var action = new SubjectAction
        {
            ItemId = "action-id",
            Source = actionType,
            SubjectContentType = "Subject",
            DispositionId = "disposition-id",
        };

        if (string.Equals(actionType, OmnichannelConstants.ActionTypes.TryAgain, StringComparison.Ordinal))
        {
            action.Put(new TryAgainActionMetadata
            {
                AssignmentType = assignmentType,
                NormalizedUserName = normalizedUserName,
            });
        }
        else
        {
            action.Put(new NewActivityActionMetadata
            {
                AssignmentType = assignmentType,
                NormalizedUserName = normalizedUserName,
            });
        }

        return action;
    }

    private static SubjectActionExecutionContext CreateContext()
    {
        return new SubjectActionExecutionContext
        {
            Activity = new OmnichannelActivity
            {
                ItemId = "activity-id",
                SubjectContentType = "Subject",
                CompletedById = "completing-user-id",
                CompletedByUsername = "Completing User",
                Attempts = 1,
            },
            Disposition = new OmnichannelDisposition
            {
                ItemId = "disposition-id",
            },
        };
    }

    private static Mock<ISession> CreateSessionWithUser(User user)
    {
        var query = new Mock<IQuery<User, UserIndex>>();
        query
            .Setup(x => x.FirstOrDefaultAsync())
            .ReturnsAsync(user);

        var entityQuery = new Mock<IQuery<User>>();
        entityQuery
            .Setup(x => x.With<UserIndex>(It.IsAny<Expression<Func<UserIndex, bool>>>()))
            .Returns(query.Object);

        var rootQuery = new Mock<IQuery>();
        rootQuery
            .Setup(x => x.For<User>(It.IsAny<bool>()))
            .Returns(entityQuery.Object);

        var session = new Mock<ISession>();
        session
            .Setup(x => x.Query(It.IsAny<string>()))
            .Returns(rootQuery.Object);

        return session;
    }

    private static void SetupSave(
        Mock<ISession> session,
        Action<OmnichannelActivity> callback)
    {
        session
            .Setup(x => x.SaveAsync(It.IsAny<object>(), false, OmnichannelConstants.CollectionName, It.IsAny<CancellationToken>()))
            .Callback<object, bool, string, CancellationToken>((entity, _, _, _) => callback((OmnichannelActivity)entity))
            .Returns(Task.CompletedTask);
    }
}
