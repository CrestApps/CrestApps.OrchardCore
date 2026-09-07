using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Subscriptions;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Workflows.Events;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Environment.Shell;
using OrchardCore.Json;
using OrchardCore.Modules;
using OrchardCore.Abstractions.Setup;
using OrchardCore.Recipes.Models;
using OrchardCore.Setup.Services;
using OrchardCore.Workflows.Services;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

public class TenantOnboardingSubscriptionHandlerTests
{
    [Fact]
    public async Task CompletingAsync_WhenContentItemDoesNotRequireTenantOnboarding_DoesNothing()
    {
        var workflowManager = CreateWorkflowManager();
        var setupService = new Mock<ISetupService>();
        var handler = CreateHandler(setupService.Object, workflowManager.Object);
        var session = new SubscriptionSession
        {
            SessionId = "session-1",
        };

        var flow = new SubscriptionFlow(session, new ContentItem { ContentType = "Plan" });
        var context = new SubscriptionFlowCompletingContext(flow);

        var exception = await Record.ExceptionAsync(() => handler.CompletingAsync(context));

        Assert.Null(exception);

        setupService.Verify(service => service.GetSetupRecipesAsync(), Times.Never);
    }

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

    /// <summary>
    /// The administrator password is captured by the step editor already data-protected, so provisioning has to
    /// unprotect it. Passing the protected value straight through creates a tenant whose administrator can never
    /// sign in, which is invisible until someone tries. This asserts the raw password reaches the setup service.
    /// </summary>
    [Fact]
    public async Task CompletedAsync_UnprotectsTheAdminPasswordBeforeSetup()
    {
        SetupContext captured = null;

        var setupService = new Mock<ISetupService>();
        setupService.Setup(s => s.GetSetupRecipesAsync())
            .ReturnsAsync([new RecipeDescriptor { Name = "Blog" }]);
        setupService.Setup(s => s.SetupAsync(It.IsAny<SetupContext>()))
            .Callback<SetupContext>(context => captured = context)
            .ReturnsAsync(string.Empty);

        var sessionStore = new Mock<ISubscriptionSessionStore>();
        var handler = CreateHandler(setupService.Object, CreateWorkflowManager().Object, CreateShellSettingsManager(), sessionStore.Object);

        await handler.CompletedAsync(CreateCompletedContext("acme"));

        Assert.NotNull(captured);
        Assert.Equal(RawAdminPassword, captured.Properties[SetupConstants.AdminPassword]);
    }

    /// <summary>
    /// The protected password is only needed while the tenant is provisioned, so it must not be left on the
    /// durable session for the life of the subscription.
    /// </summary>
    [Fact]
    public async Task CompletedAsync_RemovesTheProtectedPasswordFromTheSession()
    {
        var setupService = new Mock<ISetupService>();
        setupService.Setup(s => s.GetSetupRecipesAsync())
            .ReturnsAsync([new RecipeDescriptor { Name = "Blog" }]);
        setupService.Setup(s => s.SetupAsync(It.IsAny<SetupContext>()))
            .ReturnsAsync(string.Empty);

        var context = CreateCompletedContext("acme");
        var storedSession = (SubscriptionSession)context.Flow.Session;

        var sessionStore = new Mock<ISubscriptionSessionStore>();
        sessionStore.Setup(s => s.GetAsync(storedSession.SessionId)).ReturnsAsync(storedSession);

        var handler = CreateHandler(setupService.Object, CreateWorkflowManager().Object, CreateShellSettingsManager(), sessionStore.Object);

        await handler.CompletedAsync(context);

        var savedStep = storedSession.SavedSteps[SubscriptionConstants.StepKey.TenantOnboarding].AsObject();

        Assert.False(savedStep.ContainsKey(nameof(TenantOnboardingStep.ProtectedAdminPassword)));
        sessionStore.Verify(s => s.SaveAsync(storedSession), Times.Once);
    }

    /// <summary>
    /// A password that cannot be unprotected (for example after the data-protection key ring was rotated) must
    /// abort provisioning and be reported, never fall through and create an unusable tenant.
    /// </summary>
    [Fact]
    public async Task CompletedAsync_WhenPasswordCannotBeUnprotected_ReportsFailureAndDoesNotSetUp()
    {
        var setupService = new Mock<ISetupService>();
        setupService.Setup(s => s.GetSetupRecipesAsync())
            .ReturnsAsync([new RecipeDescriptor { Name = "Blog" }]);

        var workflowManager = CreateWorkflowManager();
        var handler = CreateHandler(setupService.Object, workflowManager.Object, sessionStore: new Mock<ISubscriptionSessionStore>().Object);

        // Protected by a different key ring than the one the handler unprotects with.
        var context = CreateCompletedContext("acme", new EphemeralDataProtectionProvider());

        await handler.CompletedAsync(context);

        setupService.Verify(s => s.SetupAsync(It.IsAny<SetupContext>()), Times.Never);
        workflowManager.Verify(
            m => m.TriggerEventAsync(
                SubscribedTenantFailedSetupEvent.EventName,
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()),
            Times.Once);
    }

    private const string RawAdminPassword = "Password1!";

    // One provider instance is shared by the handler and by the context builder so a value protected for a test
    // can actually be unprotected by the handler, exactly as the same tenant's key ring would in production.
    private static readonly IDataProtectionProvider _dataProtectionProvider = new EphemeralDataProtectionProvider();

    private static TenantOnboardingSubscriptionHandler CreateHandler(
        ISetupService setupService,
        IWorkflowManager workflowManager,
        IShellSettingsManager shellSettingsManager = null,
        ISubscriptionSessionStore sessionStore = null)
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
            _dataProtectionProvider,
            sessionStore ?? new Mock<ISubscriptionSessionStore>().Object,
            NullLogger<TenantOnboardingSubscriptionHandler>.Instance,
            Options.Create(new DocumentJsonSerializerOptions()),
            new PassThroughStringLocalizer<PaymentSubscriptionHandler>());
    }

    private static SubscriptionFlowCompletedContext CreateCompletedContext(string tenantName, IDataProtectionProvider protectionProvider = null)
    {
        var options = new DocumentJsonSerializerOptions().SerializerOptions;

        // Protect the password exactly as the step editor does, so the test exercises the real round trip
        // instead of seeding a raw value the handler would never actually receive.
        var protector = (protectionProvider ?? _dataProtectionProvider)
            .CreateProtector(SubscriptionConstants.ProtectorPurposes.TenantOnboardingStep);

        var step = new TenantOnboardingStep
        {
            TenantName = tenantName,
            TenantTitle = "Acme",
            AdminUsername = "admin",
            AdminEmail = "admin@acme.test",
            ProtectedAdminPassword = protector.Protect(RawAdminPassword),
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
