using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Handlers;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Moq;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Validation;

/// <summary>
/// Pins the subject action rules to the handler, so an action written by a recipe is held to the same set the
/// editor applies.
/// </summary>
public class SubjectActionHandlerValidationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidatingAsync_WhenTheDispositionIsMissing_Fails(string dispositionId)
    {
        // Arrange
        var action = new SubjectAction
        {
            DispositionId = dispositionId,
        };

        // Act
        var context = await ValidateAsync(action, configuredSubject: null);

        // Assert
        AssertFailedFor(context, nameof(SubjectAction.DispositionId));
    }

    /// <remarks>
    /// An unnamed subject is a supported configuration: the new activity keeps the subject type of the activity that
    /// raised the action, so requiring one here would refuse entries the editor offers and older recipes carry.
    /// </remarks>
    [Fact]
    public async Task ValidatingAsync_WhenANewActivityActionNamesNoSubject_Succeeds()
    {
        // Arrange
        var action = CreateNewActivityAction(subjectContentType: null);

        // Act
        var context = await ValidateAsync(action, configuredSubject: null);

        // Assert
        Assert.True(context.Result.Succeeded);
    }

    [Fact]
    public async Task ValidatingAsync_WhenANewActivityActionNamesAnUnconfiguredSubject_Fails()
    {
        // Arrange
        var action = CreateNewActivityAction("SupportCase");

        // Act
        var context = await ValidateAsync(action, configuredSubject: "Invoice");

        // Assert
        AssertFailedFor(context, nameof(NewActivityActionMetadata.SubjectContentType));
    }

    [Fact]
    public async Task ValidatingAsync_WhenANewActivityActionNamesAConfiguredSubject_Succeeds()
    {
        // Arrange
        var action = CreateNewActivityAction("SupportCase");

        // Act
        var context = await ValidateAsync(action, configuredSubject: "SupportCase");

        // Assert
        Assert.True(context.Result.Succeeded);
    }

    [Fact]
    public async Task ValidatingAsync_WhenANewActivityActionAssignsASpecificOwnerWithoutNamingOne_Fails()
    {
        // Arrange
        var action = CreateNewActivityAction("SupportCase");
        var metadata = action.GetOrCreate<NewActivityActionMetadata>();

        metadata.AssignmentType = SubjectActionOwnerAssignmentType.SpecificOwner;
        metadata.NormalizedUserName = "   ";
        action.Put(metadata);

        // Act
        var context = await ValidateAsync(action, configuredSubject: "SupportCase");

        // Assert
        AssertFailedFor(context, nameof(NewActivityActionMetadata.NormalizedUserName));
    }

    [Fact]
    public async Task ValidatingAsync_WhenATryAgainActionAssignsASpecificOwnerWithoutNamingOne_Fails()
    {
        // Arrange
        var action = new SubjectAction
        {
            DispositionId = "disposition-1",
            Source = OmnichannelConstants.ActionTypes.TryAgain,
        };

        action.Put(new TryAgainActionMetadata
        {
            AssignmentType = SubjectActionOwnerAssignmentType.SpecificOwner,
            NormalizedUserName = null,
        });

        // Act
        var context = await ValidateAsync(action, configuredSubject: null);

        // Assert
        AssertFailedFor(context, nameof(NewActivityActionMetadata.NormalizedUserName));
    }

    [Fact]
    public async Task ValidatingAsync_WhenATryAgainActionDoesNotAssignASpecificOwner_Succeeds()
    {
        // Arrange
        var action = new SubjectAction
        {
            DispositionId = "disposition-1",
            Source = OmnichannelConstants.ActionTypes.TryAgain,
        };

        action.Put(new TryAgainActionMetadata
        {
            AssignmentType = SubjectActionOwnerAssignmentType.SameOwner,
        });

        // Act
        var context = await ValidateAsync(action, configuredSubject: null);

        // Assert
        Assert.True(context.Result.Succeeded);
    }

    private static async Task<ValidatingContext<SubjectAction>> ValidateAsync(SubjectAction action, string configuredSubject)
    {
        var flowSettingsService = new Mock<ISubjectFlowSettingsService>();

        flowSettingsService
            .Setup(x => x.FindConfiguredFlowSettingsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string subjectContentType, CancellationToken _) => Task.FromResult(
                configuredSubject is not null && string.Equals(configuredSubject, subjectContentType, StringComparison.OrdinalIgnoreCase)
                    ? new SubjectFlowSettings { SubjectContentType = subjectContentType }
                    : null));

        var handler = new SubjectActionHandler(
            flowSettingsService.Object,
            new Mock<ISession>().Object,
            new PassThroughStringLocalizer<SubjectActionHandler>());

        var context = new ValidatingContext<SubjectAction>(action);

        await handler.ValidatingAsync(context, TestContext.Current.CancellationToken);

        return context;
    }

    private static void AssertFailedFor(ValidatingContext<SubjectAction> context, string memberName)
    {
        Assert.False(context.Result.Succeeded);
        Assert.Contains(context.Result.Errors, error => error.MemberNames.Contains(memberName));
    }

    private static SubjectAction CreateNewActivityAction(string subjectContentType)
    {
        var action = new SubjectAction
        {
            DispositionId = "disposition-1",
            Source = OmnichannelConstants.ActionTypes.NewActivity,
        };

        action.Put(new NewActivityActionMetadata
        {
            SubjectContentType = subjectContentType,
            AssignmentType = SubjectActionOwnerAssignmentType.SameOwner,
        });

        return action;
    }
}
