using System.Linq.Expressions;
using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore;
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

    private static DefaultSubjectActionExecutor CreateExecutor(
        SubjectAction action,
        Mock<ISession> session,
        ILocalClock localClock = null)
    {
        var actionCatalog = new Mock<ISourceCatalog<SubjectAction>>();
        actionCatalog
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { action });

        var contentManager = new Mock<IContentManager>();
        contentManager
            .Setup(x => x.NewAsync(It.IsAny<string>()))
            .ReturnsAsync((string contentType) => new ContentItem { ContentType = contentType });

        var clock = new Mock<IClock>();
        clock.SetupGet(x => x.UtcNow).Returns(_now);

        return new DefaultSubjectActionExecutor(
            actionCatalog.Object,
            Mock.Of<ISubjectFlowSettingsService>(),
            contentManager.Object,
            session.Object,
            clock.Object,
            localClock ?? Mock.Of<ILocalClock>(),
            NullLogger<DefaultSubjectActionExecutor>.Instance);
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
