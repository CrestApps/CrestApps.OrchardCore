using System.Security.Claims;
using CrestApps.OrchardCore.Telephony.Controllers;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.Telephony.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Shapes;

namespace CrestApps.OrchardCore.Tests.Modules.Telephony;

public sealed class SoftPhoneControllerTests
{
    [Fact]
    public async Task Index_WhenUnauthorized_ReturnsForbid_AndNeverBuildsThePhone()
    {
        // Arrange
        var presenter = new Mock<ISoftPhoneWidgetPresenter>();
        var displayManager = new Mock<IDisplayManager<SoftPhoneWidget>>();
        var controller = CreateController(presenter.Object, displayManager.Object, isAuthorized: false);

        // Act
        var result = await controller.Index("call-1");

        // Assert - the standalone page is gated on UseSoftPhone, and nothing is rendered when it is denied.
        Assert.IsType<ForbidResult>(result);
        presenter.Verify(p => p.CreateWidgetAsync(), Times.Never);
        displayManager.Verify(
            m => m.BuildDisplayAsync(It.IsAny<SoftPhoneWidget>(), It.IsAny<IUpdateModel>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Index_WhenAuthorized_RendersEmbedded_AndCarriesTheAnswerCallId()
    {
        // Arrange
        var widget = new SoftPhoneWidget
        {
            AccentColor = "#123456",
            Capabilities = TelephonyCapabilities.Dial,
            RecentCallsCount = 25,
        };
        var presenter = new Mock<ISoftPhoneWidgetPresenter>();
        presenter.Setup(p => p.CreateWidgetAsync()).ReturnsAsync(widget);
        var shape = new Shape();
        var displayManager = new Mock<IDisplayManager<SoftPhoneWidget>>();
        displayManager
            .Setup(m => m.BuildDisplayAsync(widget, It.IsAny<IUpdateModel>(), "Detail", It.IsAny<string>()))
            .ReturnsAsync(shape);
        var controller = CreateController(presenter.Object, displayManager.Object, isAuthorized: true);

        // Act
        var result = await controller.Index("call-42");

        // Assert - the page renders the reused widget shape as an embedded, full-window phone and hands the
        // answer call id to the client so it can auto-answer the matching offer on load.
        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<SoftPhoneStandaloneViewModel>(view.Model);
        Assert.Same(shape, model.Shape);
        Assert.Equal("call-42", model.AnswerCallId);
        Assert.True(model.Embedded);
        Assert.Equal(true, shape.Properties["Embedded"]);
        presenter.Verify(p => p.RegisterResources(widget), Times.Once);
    }

    private static SoftPhoneController CreateController(
        ISoftPhoneWidgetPresenter presenter,
        IDisplayManager<SoftPhoneWidget> displayManager,
        bool isAuthorized)
    {
        var updateModelAccessor = new Mock<IUpdateModelAccessor>();

        return new SoftPhoneController(
            new TestAuthorizationService(isAuthorized),
            presenter,
            displayManager,
            updateModelAccessor.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, "user-1"),
                    ], "Test")),
                },
            },
        };
    }

    private sealed class TestAuthorizationService : IAuthorizationService
    {
        private readonly bool _isAuthorized;

        public TestAuthorizationService(bool isAuthorized)
        {
            _isAuthorized = isAuthorized;
        }

        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object resource,
            IEnumerable<IAuthorizationRequirement> requirements)
        {
            return Task.FromResult(_isAuthorized
                ? AuthorizationResult.Success()
                : AuthorizationResult.Failed());
        }

        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object resource,
            string policyName)
        {
            return Task.FromResult(_isAuthorized
                ? AuthorizationResult.Success()
                : AuthorizationResult.Failed());
        }
    }
}
