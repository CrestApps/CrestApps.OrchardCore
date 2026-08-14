using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Subscriptions;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Workflows.Events;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Environment.Shell;
using OrchardCore.Json;
using OrchardCore.Modules;
using OrchardCore.Recipes.Models;
using OrchardCore.Setup.Services;
using OrchardCore.Workflows.Services;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

public class TenantOnboardingSubscriptionHandlerTests
{
    [Fact]
    public async Task CompletedAsync_WhenProvisioningThrows_TriggersFailedSetupEvent_AndDoesNotPropagate()
    {
        // Payment already succeeded before CompletedAsync runs, so a provisioning failure must be
        // surfaced through the durable failure workflow event rather than thrown to the caller.
        var workflowManager = new Mock<IWorkflowManager>();
        workflowManager
            .Setup(m => m.TriggerEventAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync([]);

        var setupService = new Mock<ISetupService>();
        setupService
            .Setup(s => s.GetSetupRecipesAsync())
            .ThrowsAsync(new InvalidOperationException("boom"));

        var handler = CreateHandler(setupService.Object, workflowManager.Object);
        var context = CreateCompletedContext("acme");

        var exception = await Record.ExceptionAsync(() => handler.CompletedAsync(context));

        Assert.Null(exception);

        workflowManager.Verify(m => m.TriggerEventAsync(
            SubscribedTenantFailedSetupEvent.EventName,
            It.Is<IDictionary<string, object>>(d => (string)d["TenantName"] == "acme"),
            "TenantAutoSetup_acme",
            It.IsAny<bool>(),
            It.IsAny<bool>()), Times.Once);

        workflowManager.Verify(m => m.TriggerEventAsync(
            SubscribedTenantSetupSucceededEvent.EventName,
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task CompletedAsync_WhenNoTenantOnboardingStep_DoesNothing()
    {
        var workflowManager = new Mock<IWorkflowManager>();
        var setupService = new Mock<ISetupService>();

        var handler = CreateHandler(setupService.Object, workflowManager.Object);

        var session = new SubscriptionSession
        {
            SessionId = "session-1",
        };

        var flow = new SubscriptionFlow(session, new ContentItem());
        var context = new SubscriptionFlowCompletedContext(flow);

        await handler.CompletedAsync(context);

        setupService.Verify(s => s.GetSetupRecipesAsync(), Times.Never);
        workflowManager.Verify(m => m.TriggerEventAsync(
            It.IsAny<string>(),
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task CompletedAsync_WhenSetupReportsSoftErrors_TriggersFailedSetupEvent()
    {
        var workflowManager = CreateWorkflowManager();

        var setupService = new Mock<ISetupService>();
        setupService
            .Setup(s => s.GetSetupRecipesAsync())
            .ReturnsAsync([new RecipeDescriptor { Name = "Blog" }]);
        setupService
            .Setup(s => s.SetupAsync(It.IsAny<SetupContext>()))
            .Returns((SetupContext ctx) =>
            {
                ctx.Errors["Feature"] = "failed to enable";
                return Task.FromResult("failed");
            });

        var handler = CreateHandler(setupService.Object, workflowManager.Object, CreateShellSettingsManager());
        var context = CreateCompletedContext("acme");

        await handler.CompletedAsync(context);

        workflowManager.Verify(m => m.TriggerEventAsync(
            SubscribedTenantFailedSetupEvent.EventName,
            It.Is<IDictionary<string, object>>(d => (string)d["TenantName"] == "acme" && d.ContainsKey("Errors")),
            "TenantAutoSetup_acme",
            It.IsAny<bool>(),
            It.IsAny<bool>()), Times.Once);
    }

    [Fact]
    public async Task CompletedAsync_WhenSetupSucceeds_TriggersSucceededEvent()
    {
        var workflowManager = CreateWorkflowManager();

        var setupService = new Mock<ISetupService>();
        setupService
            .Setup(s => s.GetSetupRecipesAsync())
            .ReturnsAsync([new RecipeDescriptor { Name = "Blog" }]);
        setupService
            .Setup(s => s.SetupAsync(It.IsAny<SetupContext>()))
            .ReturnsAsync("ok");

        var handler = CreateHandler(setupService.Object, workflowManager.Object, CreateShellSettingsManager());
        var context = CreateCompletedContext("acme");

        await handler.CompletedAsync(context);

        workflowManager.Verify(m => m.TriggerEventAsync(
            SubscribedTenantSetupSucceededEvent.EventName,
            It.Is<IDictionary<string, object>>(d => (string)d["TenantName"] == "acme"),
            "TenantAutoSetup_acme",
            It.IsAny<bool>(),
            It.IsAny<bool>()), Times.Once);

        workflowManager.Verify(m => m.TriggerEventAsync(
            SubscribedTenantFailedSetupEvent.EventName,
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task CompletedAsync_WhenCancelled_Propagates()
    {
        var workflowManager = CreateWorkflowManager();

        var setupService = new Mock<ISetupService>();
        setupService
            .Setup(s => s.GetSetupRecipesAsync())
            .ThrowsAsync(new OperationCanceledException());

        var handler = CreateHandler(setupService.Object, workflowManager.Object);
        var context = CreateCompletedContext("acme");

        await Assert.ThrowsAsync<OperationCanceledException>(() => handler.CompletedAsync(context));

        workflowManager.Verify(m => m.TriggerEventAsync(
            It.IsAny<string>(),
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<bool>()), Times.Never);
    }

    private static Mock<IWorkflowManager> CreateWorkflowManager()
    {
        var workflowManager = new Mock<IWorkflowManager>();
        workflowManager
            .Setup(m => m.TriggerEventAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync([]);

        return workflowManager;
    }

    private static IShellSettingsManager CreateShellSettingsManager()
    {
        var manager = new Mock<IShellSettingsManager>();
        manager
            .Setup(m => m.CreateDefaultSettings())
            .Returns(() => new ShellSettings());

        return manager.Object;
    }

    private static TenantOnboardingSubscriptionHandler CreateHandler(ISetupService setupService, IWorkflowManager workflowManager, IShellSettingsManager shellSettingsManager = null)
    {
        var shellHost = new Mock<IShellHost>();
        shellHost
            .Setup(h => h.UpdateShellSettingsAsync(It.IsAny<ShellSettings>()))
            .Returns(Task.CompletedTask);

        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(sp => sp.GetService(typeof(IWorkflowManager)))
            .Returns(workflowManager);

        var clock = new Mock<IClock>();

        return new TenantOnboardingSubscriptionHandler(
            shellHost.Object,
            shellSettingsManager ?? new Mock<IShellSettingsManager>().Object,
            new ShellSettings { Name = "Default" },
            clock.Object,
            setupService,
            serviceProvider.Object,
            NullLogger<TenantOnboardingSubscriptionHandler>.Instance,
            Options.Create(new DocumentJsonSerializerOptions()),
            new PassThroughStringLocalizer<PaymentSubscriptionHandler>());
    }

    private static SubscriptionFlowCompletedContext CreateCompletedContext(string tenantName)
    {
        var options = new DocumentJsonSerializerOptions().SerializerOptions;

        var step = new TenantOnboardingStep
        {
            TenantName = tenantName,
            TenantTitle = "Acme",
            AdminUsername = "admin",
            AdminEmail = "admin@acme.test",
            AdminPassword = "Password1!",
            RecipeName = "Blog",
        };

        var session = new SubscriptionSession
        {
            SessionId = "session-1",
        };

        session.SavedSteps[SubscriptionConstants.StepKey.TenantOnboarding] = JsonSerializer.SerializeToNode(step, options);

        var flow = new SubscriptionFlow(session, new ContentItem());

        return new SubscriptionFlowCompletedContext(flow);
    }
}
