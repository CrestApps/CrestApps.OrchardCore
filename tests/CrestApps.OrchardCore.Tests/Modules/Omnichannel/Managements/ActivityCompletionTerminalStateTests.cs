using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Users;
using CrestApps.OrchardCore.Users;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements;

/// <summary>
/// Wrapping up an activity that is already finished.
/// </summary>
/// <remarks>
/// This is ordinary traffic rather than a stale link: an automated call that hands off to a live agent concludes
/// its own activity, so the agent who wraps up afterwards is dispositioning something the automation has already
/// closed. It used to render the completion form anyway and then answer the submit with a bare 404, so the agent
/// saw an error page and lost the disposition, the notes and the scheduled actions they had just filled in. It
/// happened on a live call: the automation closed the activity while the agent was still talking, ninety seconds
/// before they wrapped up.
/// </remarks>
public sealed class ActivityCompletionTerminalStateTests
{
    [Theory]
    [InlineData(ActivityStatus.Completed)]
    [InlineData(ActivityStatus.Cancelled)]
    [InlineData(ActivityStatus.Purged)]
    public async Task SubmittingADispositionForAFinishedActivity_SaysSoInsteadOfAnsweringNotFound(ActivityStatus status)
    {
        // Arrange
        var harness = new Harness(status);

        // Act
        var result = await harness.Controller.CompleteAsync("activity-1", returnUrl: null, [], []);

        // Assert
        // "Already done" is not "never existed", and the agent has to be told which one it was.
        Assert.IsNotType<NotFoundResult>(result);
        Assert.True(harness.AgentWasTold, "The agent was redirected without being told why their disposition did not stick.");
        Harness.AssertRedirected(result);
    }

    [Theory]
    [InlineData(ActivityStatus.Completed)]
    [InlineData(ActivityStatus.Cancelled)]
    [InlineData(ActivityStatus.Purged)]
    public async Task OpeningTheWrapUpFormForAFinishedActivity_TurnsTheAgentAwayBeforeTheyType(ActivityStatus status)
    {
        // Arrange
        // The form was the trap: it rendered happily and only refused on submit, after the work was done.
        var harness = new Harness(status);

        // Act
        var result = await harness.Controller.Complete("activity-1", returnUrl: null);

        // Assert
        Assert.IsNotType<ViewResult>(result);
        Assert.IsNotType<NotFoundResult>(result);
        Harness.AssertRedirected(result);
    }

    [Fact]
    public async Task AFinishedActivity_IsNotDispositionedASecondTime()
    {
        // Arrange
        // The outcome on record belongs to whoever closed it first; a late wrap-up must not overwrite it.
        var harness = new Harness(ActivityStatus.Completed);

        // Act
        await harness.Controller.CompleteAsync("activity-1", returnUrl: null, [], []);

        // Assert
        harness.DispositionService.Verify(
            service => service.ApplyAsync(It.IsAny<ActivityDispositionRequest>()),
            Times.Never);
    }

    [Fact]
    public async Task AnActivityThatDoesNotExist_IsStillNotFound()
    {
        // Arrange
        // The 404 is right for this one, and the new handling must not swallow it.
        var harness = new Harness(status: null);

        // Act
        var get = await harness.Controller.Complete("missing", returnUrl: null);
        var post = await harness.Controller.CompleteAsync("missing", returnUrl: null, [], []);

        // Assert
        Assert.IsType<NotFoundResult>(get);
        Assert.IsType<NotFoundResult>(post);
    }

    [Fact]
    public async Task TheAgentIsSentBackWhereTheyCameFrom()
    {
        // Arrange
        // Agents reach this from the workspace, so dropping them on the activities list loses their place.
        var harness = new Harness(ActivityStatus.Completed);

        // Act
        var result = await harness.Controller.CompleteAsync("activity-1", "/Admin/contact-center/workspace", [], []);

        // Assert
        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/Admin/contact-center/workspace", redirect.Url);
    }

    private sealed class Harness
    {
        public Harness(ActivityStatus? status)
        {
            var activityManager = new Mock<IOmnichannelActivityManager>();
            activityManager
                .Setup(manager => manager.FindByIdAsync("activity-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(status is null ? null : new OmnichannelActivity { ItemId = "activity-1", Status = status.Value });

            // The permission-based extension routes through the requirements overload, so both have to answer.
            var authorization = new Mock<IAuthorizationService>();
            authorization
                .Setup(service => service.AuthorizeAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<string>()))
                .ReturnsAsync(AuthorizationResult.Success());
            authorization
                .Setup(service => service.AuthorizeAsync(
                    It.IsAny<System.Security.Claims.ClaimsPrincipal>(),
                    It.IsAny<object>(),
                    It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
                .ReturnsAsync(AuthorizationResult.Success());

            Notifier = new Mock<INotifier>();

            var htmlLocalizer = new Mock<IHtmlLocalizer<ActivitiesController>>();
            htmlLocalizer
                .Setup(x => x[It.IsAny<string>()])
                .Returns<string>(name => new LocalizedHtmlString(name, name));

            var stringLocalizer = new Mock<IStringLocalizer<ActivitiesController>>();
            stringLocalizer
                .Setup(x => x[It.IsAny<string>()])
                .Returns<string>(name => new LocalizedString(name, name));

            DispositionService = new Mock<IActivityDispositionService>();

            Controller = new ActivitiesController(
                new Mock<global::YesSql.ISession>().Object,
                new Mock<IUpdateModelAccessor>().Object,
                new Mock<IContentManager>().Object,
                new Mock<IDisplayManager<OmnichannelActivityContainer>>().Object,
                new Mock<IDisplayManager<OmnichannelActivity>>().Object,
                activityManager.Object,
                authorization.Object,
                new Mock<IContentDefinitionManager>().Object,
                new Mock<IContentItemDisplayManager>().Object,
                DispositionService.Object,
                new Mock<ISubjectFlowSettingsService>().Object,
                new Mock<IClock>().Object,
                new Mock<ILocalClock>().Object,
                Notifier.Object,
                new UserManager<IUser>(new Mock<IUserStore<IUser>>().Object, null, null, null, null, null, null, null, null),
                new Mock<IDisplayNameProvider>().Object,
                [],
                stringLocalizer.Object,
                htmlLocalizer.Object);

            var urlHelper = new Mock<IUrlHelper>();
            urlHelper.Setup(x => x.IsLocalUrl(It.IsAny<string>())).Returns<string>(url => url is not null && url.StartsWith('/'));

            Controller.Url = urlHelper.Object;
            Controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            };
        }

        public ActivitiesController Controller { get; }

        public Mock<IActivityDispositionService> DispositionService { get; }

        public Mock<INotifier> Notifier { get; private set; }

        /// <summary>
        /// Whether the agent was told anything, however the notifier overload was reached.
        /// </summary>
        public bool AgentWasTold => Notifier.Invocations.Count > 0;

        public static void AssertRedirected(IActionResult result)
        {
            if (result is RedirectResult or RedirectToActionResult)
            {
                return;
            }

            Assert.Fail($"Expected the agent to be sent somewhere, but got '{result.GetType().Name}'.");
        }
    }
}
