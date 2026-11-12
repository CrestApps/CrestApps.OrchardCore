using System.Security.Claims;
using CrestApps.OrchardCore.Wizard;
using CrestApps.OrchardCore.Wizard.Controllers;
using CrestApps.OrchardCore.Wizard.Core.Services;
using CrestApps.OrchardCore.Wizard.Handlers;
using CrestApps.OrchardCore.Wizard.Services;
using CrestApps.OrchardCore.Wizard.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Modules;
using OrchardCore.RateLimits;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Wizard;

/// <summary>
/// SEC-A18: an anonymous visitor's cookieless GET of a public wizard stored a session every time, an unknown definition
/// id was accepted, and nothing purged abandoned sessions.
/// </summary>
public sealed class WizardControllerSessionTests
{
    private const string WizardType = "Onboarding";

    [Fact]
    public async Task Start_AGetAlone_StoresNothing()
    {
        var (controller, store) = CreateController();

        var result = await controller.Start(WizardType, definitionId: null);

        Assert.IsType<ViewResult>(result);
        store.Verify(s => s.SaveAsync(It.IsAny<WizardSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Start_WithAnUnknownDefinition_Answers404()
    {
        var (controller, store) = CreateController(new UnknownDefinitionPolicy());

        var result = await controller.Start(WizardType, "missing");

        Assert.IsType<NotFoundResult>(result);
        store.Verify(s => s.NewAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Start_WithADefinitionNoPolicyKnows_Answers404()
    {
        var (controller, _) = CreateController();

        var result = await controller.Start(WizardType, "anything");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task StartPOST_FirstValidPost_StoresTheSession()
    {
        var (controller, store) = CreateController();

        var result = await controller.StartPOST(new WizardViewModel { WizardType = WizardType });

        Assert.IsType<RedirectToActionResult>(result);
        store.Verify(s => s.SaveAsync(It.IsAny<WizardSession>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task StartPOST_AnInvalidFirstPost_StoresNothing()
    {
        var (controller, store) = CreateController(invalidModel: true);

        var result = await controller.StartPOST(new WizardViewModel { WizardType = WizardType });

        Assert.IsType<ViewResult>(result);
        store.Verify(s => s.SaveAsync(It.IsAny<WizardSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(nameof(WizardController.Start))]
    [InlineData(nameof(WizardController.StartPOST))]
    [InlineData(nameof(WizardController.Display))]
    [InlineData(nameof(WizardController.Confirmation))]
    public void WizardRoutes_CarryTheWizardRateLimitGroup(string action)
    {
        var method = typeof(WizardController).GetMethod(action);
        var attribute = method.GetCustomAttributes(typeof(RateLimitGroupAttribute), inherit: true).Cast<RateLimitGroupAttribute>().SingleOrDefault()
            ?? typeof(WizardController).GetCustomAttributes(typeof(RateLimitGroupAttribute), inherit: true).Cast<RateLimitGroupAttribute>().SingleOrDefault();

        Assert.NotNull(attribute);
        Assert.Contains(WizardConstants.RateLimitGroups.Wizard, attribute.GroupNames);
    }

    private static (WizardController Controller, Mock<IWizardSessionStore> Store) CreateController(IWizardAccessPolicy policy = null, bool invalidModel = false)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity()),
        };

        var store = new Mock<IWizardSessionStore>();
        store
            .Setup(s => s.NewAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string type, string definitionId, string _, CancellationToken _) => new WizardSession
            {
                SessionId = "session-1",
                WizardType = type,
                DefinitionId = definitionId,
                Status = WizardSessionStatus.Pending,
            });

        var modelState = new ModelStateDictionary();

        if (invalidModel)
        {
            modelState.AddModelError("field", "error");
        }

        var updater = new Mock<IUpdateModel>();
        updater.SetupGet(u => u.ModelState).Returns(modelState);
        var updateModelAccessor = Mock.Of<IUpdateModelAccessor>(accessor => accessor.ModelUpdater == updater.Object);

        var displayManager = new Mock<IDisplayManager<WizardFlow>>();
        displayManager.Setup(m => m.BuildEditorAsync(It.IsAny<WizardFlow>(), It.IsAny<IUpdateModel>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(Mock.Of<IShape>());
        displayManager.Setup(m => m.UpdateEditorAsync(It.IsAny<WizardFlow>(), It.IsAny<IUpdateModel>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(Mock.Of<IShape>());

        var definition = Mock.Of<IWizardDefinition>(d => d.WizardType == WizardType);
        var definitionProvider = Mock.Of<IWizardDefinitionProvider>(p => p.GetDefinitions() == new[] { definition });

        var engine = new Mock<IWizardEngine>();
        engine
            .Setup(e => e.CompleteAsync(It.IsAny<WizardFlow>(), It.IsAny<Func<WizardFlow, Task<bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WizardCompletionResult { Status = WizardCompletionStatus.Completed });

        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());

        var controller = new WizardController(
            updateModelAccessor,
            displayManager.Object,
            Array.Empty<IWizardHandler>(),
            store.Object,
            engine.Object,
            definitionProvider,
            policy is null ? [] : [policy],
            new WizardResumeCookieManager(new FixedHttpContextAccessor { HttpContext = httpContext }, new EphemeralDataProtectionProvider()),
            authorizationService.Object,
            Mock.Of<IClock>(c => c.UtcNow == new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc)),
            NullLogger<WizardController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
        };

        return (controller, store);
    }

    private sealed class UnknownDefinitionPolicy : IWizardAccessPolicy
    {
        public Task<bool> RequiresAuthenticatedUserAsync(string wizardType, string definitionId)
            => Task.FromResult(false);

        public Task<bool?> IsKnownDefinitionAsync(string wizardType, string definitionId)
            => Task.FromResult<bool?>(wizardType == WizardType ? false : null);
    }

    private sealed class FixedHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext HttpContext { get; set; }
    }
}
