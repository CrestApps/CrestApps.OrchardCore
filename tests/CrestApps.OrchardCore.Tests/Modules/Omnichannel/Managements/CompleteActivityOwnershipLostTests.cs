using System.Security.Claims;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Controllers;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Security;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements;

/// <summary>
/// An agent dispositioning a preview record whose offer had run out met a bare 403 that gave no reason and threw the
/// disposition away. They are now told the record is no longer theirs and sent back; anyone who truly may not complete
/// the activity is still forbidden.
/// </summary>
public sealed class CompleteActivityOwnershipLostTests
{
    [Fact]
    public async Task Complete_WhenAnAgentLostTheDialerRecord_RedirectsWithAReasonInsteadOfForbidding()
    {
        // Arrange
        var harness = new Harness(canCompleteOwn: true);
        var activity = harness.AddActivity(dialerProfileId: "preview-profile");

        // Act
        var result = await harness.Controller.Complete(activity.ItemId, returnUrl: null);

        // Assert
        Assert.IsType<RedirectToActionResult>(result);
        Assert.NotEmpty(harness.Notifier.Invocations);
    }

    [Fact]
    public async Task Complete_WhenTheUserCannotCompleteTheirOwnActivities_StillForbids()
    {
        // Arrange
        var harness = new Harness(canCompleteOwn: false);
        var activity = harness.AddActivity(dialerProfileId: "preview-profile");

        // Act
        var result = await harness.Controller.Complete(activity.ItemId, returnUrl: null);

        // Assert
        Assert.IsType<ForbidResult>(result);
    }

    // Only dialer work is offered and can run out; refusing any other activity is a real refusal.
    [Fact]
    public async Task Complete_WhenTheActivityIsNotDialerWork_StillForbids()
    {
        // Arrange
        var harness = new Harness(canCompleteOwn: true);
        var activity = harness.AddActivity(dialerProfileId: null);

        // Act
        var result = await harness.Controller.Complete(activity.ItemId, returnUrl: null);

        // Assert
        Assert.IsType<ForbidResult>(result);
    }

    private sealed class Harness
    {
        private readonly Mock<IOmnichannelActivityManager> _activityManager = new();

        public Harness(bool canCompleteOwn)
        {
            // The activity is never this user's to complete; whether they may complete their own work is the question.
            var authorization = new Mock<IAuthorizationService>();
            authorization
                .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
                .ReturnsAsync((ClaimsPrincipal _, object _, IEnumerable<IAuthorizationRequirement> requirements) =>
                    requirements.OfType<PermissionRequirement>().Any(requirement => requirement.Permission.Name == OmnichannelConstants.Permissions.CompleteOwnActivity.Name) && canCompleteOwn
                        ? AuthorizationResult.Success()
                        : AuthorizationResult.Failed());

            var htmlLocalizer = new Mock<IHtmlLocalizer<ActivitiesController>>();
            htmlLocalizer
                .Setup(localizer => localizer[It.IsAny<string>()])
                .Returns<string>(name => new LocalizedHtmlString(name, name));

            var httpContext = new DefaultHttpContext
            {
                RequestServices = new ServiceCollection().BuildServiceProvider(),
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "agent-1")], "test")),
            };

            Controller = new ActivitiesController(
                new Mock<global::YesSql.ISession>().Object,
                new Mock<IUpdateModelAccessor>().Object,
                new Mock<IContentManager>().Object,
                new Mock<IDisplayManager<OmnichannelActivity>>().Object,
                _activityManager.Object,
                authorization.Object,
                new Mock<IContentDefinitionManager>().Object,
                new Mock<IContentItemDisplayManager>().Object,
                new Mock<IActivityDispositionService>().Object,
                new Mock<ISubjectFlowSettingsService>().Object,
                new Mock<IClock>().Object,
                new Mock<ILocalClock>().Object,
                Notifier.Object,
                new UserManager<IUser>(new Mock<IUserStore<IUser>>().Object, null, null, null, null, null, null, null, null),
                new Mock<IDisplayNameProvider>().Object,
                [],
                TestContactMethods.Accessor,
                new Mock<IStringLocalizer<ActivitiesController>>().Object,
                htmlLocalizer.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext },
                Url = new Mock<IUrlHelper>().Object,
            };
        }

        public ActivitiesController Controller { get; }

        public Mock<INotifier> Notifier { get; } = new();

        public OmnichannelActivity AddActivity(string dialerProfileId)
        {
            var activity = new OmnichannelActivity
            {
                ItemId = "activity-1",
                Source = dialerProfileId is null ? ActivitySources.Manual : ActivitySources.PreviewDial,
                DialerProfileId = dialerProfileId,
                Status = ActivityStatus.NotStated,
            };

            _activityManager
                .Setup(manager => manager.FindByIdAsync(activity.ItemId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(activity);

            return activity;
        }
    }
}
