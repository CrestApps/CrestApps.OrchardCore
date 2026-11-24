using CrestApps.Core.Models;
using CrestApps.OrchardCore.Subscriptions.AI;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Agent.Indexing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Subscriptions.AI;

/// <summary>
/// The subscription, site provisioning and installment plan tools need the permission of the page they mirror, call the
/// services those pages call, say when nothing changed, never refund, queue a site retry for the background task, and
/// charge an installment only with its exact amount, currency and an unused idempotency key.
/// </summary>
public sealed class SubscriptionAIToolTests
{
    private readonly Subscription _subscription = new()
    {
        ItemId = "sub-1",
        Title = "Pro plan",
        OwnerId = "owner-1",
        Status = SubscriptionStatus.Active,
        Currency = "USD",
        Amount = 20m,
        BillingDuration = 1,
        CurrentPeriodEndUtc = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc),
        NextBillingUtc = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc),
        ProviderKey = "stripe",
        ProviderCustomerId = "cus_SECRET",
        ProviderSubscriptionId = "sub_provider",
        CheckoutSessionId = "session-secret",
        GuestContactEmail = "guest@example.com",
    };

    private readonly TenantProvisioningJob _abandoned = new()
    {
        ItemId = "job-1",
        TenantName = "bluebird",
        Status = TenantProvisioningStatus.Abandoned,
        AttemptCount = 5,
        ProtectedAdminPassword = "PROTECTED-PASSWORD",
        AdminEmail = "admin@bluebird.example.com",
        LastError = "Database unreachable",
        CreatedUtc = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    private readonly InstallmentPlan _plan = new()
    {
        ItemId = "plan-1",
        Title = "Season fee",
        OwnerId = "customer-1",
        CustomerName = "Thompson",
        CustomerEmail = "thompson@example.com",
        Currency = "USD",
        TotalAmount = 300m,
        InstallmentCount = 3,
        Status = InstallmentPlanStatus.Active,
        PaymentMethod = new() { ProviderKey = "stripe", PaymentMethodReference = "pm_SECRET", Brand = "visa", Last4 = "4242" },
        Payments =
        [
            new() { Number = 1, Amount = 100m, Status = InstallmentPaymentStatus.Paid, DueUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc) },
            new() { Number = 2, Amount = 100m, Status = InstallmentPaymentStatus.Failed, DueUtc = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc) },
            new() { Number = 3, Amount = 100m, Status = InstallmentPaymentStatus.Scheduled, DueUtc = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc) },
        ],
    };

    private readonly Mock<ISubscriptionManager> _subscriptions = new();
    private readonly Mock<ISubscriptionLifecycleService> _lifecycle = new();
    private readonly Mock<ITenantProvisioningJobStore> _jobs = new();
    private readonly Mock<IInstallmentPlanStore> _plans = new();
    private readonly Mock<IInstallmentPlanService> _planService = new();
    private readonly MemoryDistributedCache _cache = new(Options.Create(new MemoryDistributedCacheOptions()));
    private SubscriptionQuery _subscriptionQuery;
    private CreateInstallmentPlanRequest _createRequest;

    public SubscriptionAIToolTests()
    {
        _subscriptions.Setup(manager => manager.FindByIdAsync("sub-1", It.IsAny<CancellationToken>())).ReturnsAsync(_subscription);
        _subscriptions.Setup(manager => manager.PageAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<SubscriptionQuery>(), It.IsAny<CancellationToken>()))
            .Callback((int _, int _, SubscriptionQuery query, CancellationToken _) => _subscriptionQuery = query)
            .ReturnsAsync(new PageResult<Subscription> { Count = 1, Entries = [_subscription] });
        _lifecycle.Setup(lifecycle => lifecycle.CancelAsync("sub-1", It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, bool atPeriodEnd, string _, string _, CancellationToken _) =>
            {
                _subscription.Status = SubscriptionStatus.Canceled;
                _subscription.CancelAtPeriodEnd = atPeriodEnd;

                return _subscription;
            });

        _jobs.Setup(store => store.FindByIdAsync("job-1", It.IsAny<CancellationToken>())).ReturnsAsync(_abandoned);
        _jobs.Setup(store => store.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([_abandoned]);

        _plans.Setup(store => store.FindByIdAsync("plan-1", It.IsAny<CancellationToken>())).ReturnsAsync(_plan);
        _planService.Setup(service => service.ChargeAsync("plan-1", 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => InstallmentPlanResult.Success(_plan));
        _planService.Setup(service => service.CreateAsync(It.IsAny<CreateInstallmentPlanRequest>(), It.IsAny<CancellationToken>()))
            .Callback((CreateInstallmentPlanRequest request, CancellationToken _) => _createRequest = request)
            .ReturnsAsync(() => new InstallmentPlanResult().Fail(nameof(CreateInstallmentPlanRequest.InstallmentCount), "The balance is too small to split into that many payments."));
    }

    public static TheoryData<string, string> Tools => new()
    {
        { ListSubscriptionsTool.TheName, SubscriptionPermissions.ManageSubscriptions.Name },
        { GetSubscriptionTool.TheName, SubscriptionPermissions.ManageSubscriptions.Name },
        { CancelSubscriptionTool.TheName, SubscriptionPermissions.ManageSubscriptions.Name },
        { SetSubscriptionPausedTool.TheName, SubscriptionPermissions.ManageSubscriptions.Name },
        { ListSiteProvisioningJobsTool.TheName, SubscriptionPermissions.ManageSubscriptions.Name },
        { ResolveSiteProvisioningJobTool.TheName, SubscriptionPermissions.ManageSubscriptions.Name },
        { ListInstallmentPlansTool.TheName, SubscriptionPermissions.ManageInstallmentPlans.Name },
        { GetInstallmentPlanTool.TheName, SubscriptionPermissions.ManageInstallmentPlans.Name },
        { CreateInstallmentPlanTool.TheName, SubscriptionPermissions.ManageInstallmentPlans.Name },
        { ChargeInstallmentPaymentTool.TheName, SubscriptionPermissions.ManageInstallmentPlans.Name },
        { CancelInstallmentPlanTool.TheName, SubscriptionPermissions.ManageInstallmentPlans.Name },
    };

    [Theory]
    [MemberData(nameof(Tools))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName, string permission)
    {
        // Act
        var result = await InvokeAsync(toolName, Build(signedIn: false), Arguments(toolName));

        // Assert
        Assert.NotNull(permission);
        Assert.Equal(SubscriptionsAIToolBase.NotAllowedMessage, result);
        Assert.Empty(_subscriptions.Invocations);
        Assert.Empty(_lifecycle.Invocations);
        Assert.Empty(_planService.Invocations);
    }

    [Theory]
    [MemberData(nameof(Tools))]
    public async Task Tool_RefusesACallerWithoutThePagesPermission(string toolName, string permission)
    {
        // Arrange
        var services = Build(authorize: (asked, _) => asked.Name != permission);

        // Act
        var result = await InvokeAsync(toolName, services, Arguments(toolName));

        // Assert
        Assert.Equal(SubscriptionsAIToolBase.NotAllowedMessage, result);
        Assert.Equal(permission, Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks).Permission);
        Assert.Empty(_subscriptions.Invocations);
        Assert.Empty(_jobs.Invocations);
        Assert.Empty(_plans.Invocations);
        Assert.Empty(_planService.Invocations);
    }

    [Fact]
    public async Task ListSubscriptions_PassesTheStatusAndSearch_AndReturnsNoProviderReferences()
    {
        // Act
        var result = await InvokeAsync(ListSubscriptionsTool.TheName, Build(), new() { ["status"] = "pastDue", ["search"] = "Pro" });
        var wrong = await InvokeAsync(ListSubscriptionsTool.TheName, Build(), new() { ["status"] = "late" });
        var json = AgentToolTestContext.ReadJson(result);

        // Assert
        Assert.Equal(SubscriptionStatus.PastDue, _subscriptionQuery.Status);
        Assert.Equal("Pro", _subscriptionQuery.Search);
        Assert.Equal("sub-1", Assert.Single(json.GetProperty("items").EnumerateArray()).GetProperty("id").GetString());
        Assert.DoesNotContain("cus_SECRET", result, StringComparison.Ordinal);
        Assert.DoesNotContain("session-secret", result, StringComparison.Ordinal);
        Assert.DoesNotContain("guest@example.com", result, StringComparison.Ordinal);
        Assert.StartsWith("Nothing was listed", wrong, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetSubscription_SaysWhenTheAgreementIsNotFound()
    {
        // Act
        var result = await InvokeAsync(GetSubscriptionTool.TheName, Build(), new() { ["subscriptionId"] = "missing" });

        // Assert
        Assert.Contains("no subscription agreement has the id 'missing'", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancelSubscription_CancelsAsTheCaller_AndNeverRefunds()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(CancelSubscriptionTool.TheName, Build(), new()
        {
            ["subscriptionId"] = "sub-1",
            ["atPeriodEnd"] = true,
            ["reason"] = "Asked by phone",
        }));

        // Assert
        _lifecycle.Verify(lifecycle => lifecycle.CancelAsync("sub-1", true, "Asked by phone", AgentToolTestContext.UserId, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("canceled", json.GetProperty("status").GetString());
        Assert.False(json.GetProperty("refunded").GetBoolean());
        Assert.Contains("stripe", json.GetProperty("provider").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancelSubscription_DoesNothing_WhenAlreadyCanceledTheSameWay()
    {
        // Arrange
        _subscription.Status = SubscriptionStatus.Canceled;
        _subscription.CancelAtPeriodEnd = false;

        // Act
        var result = await InvokeAsync(CancelSubscriptionTool.TheName, Build(), new() { ["subscriptionId"] = "sub-1", ["atPeriodEnd"] = false });

        // Assert
        Assert.Contains("already canceled", result, StringComparison.Ordinal);
        Assert.Empty(_lifecycle.Invocations);
    }

    [Fact]
    public async Task SetSubscriptionPaused_RefusesToResumeAnActiveAgreement()
    {
        // Act
        var result = await InvokeAsync(SetSubscriptionPausedTool.TheName, Build(), new() { ["subscriptionId"] = "sub-1", ["paused"] = false });

        // Assert
        Assert.StartsWith("Nothing was changed", result, StringComparison.Ordinal);
        Assert.Empty(_lifecycle.Invocations);
    }

    [Fact]
    public async Task ListSiteProvisioningJobs_NeverReturnsThePasswordOrTheAdministratorsEmail()
    {
        // Act
        var result = await InvokeAsync(ListSiteProvisioningJobsTool.TheName, Build(), new() { ["status"] = "abandoned" });
        var item = Assert.Single(AgentToolTestContext.ReadJson(result).GetProperty("items").EnumerateArray());

        // Assert
        Assert.Equal("bluebird", item.GetProperty("tenantName").GetString());
        Assert.True(item.GetProperty("canRetry").GetBoolean());
        Assert.DoesNotContain("PROTECTED-PASSWORD", result, StringComparison.Ordinal);
        Assert.DoesNotContain("admin@bluebird.example.com", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveSiteProvisioningJob_Retry_ResetsTheJobAndQueuesIt()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(ResolveSiteProvisioningJobTool.TheName, Build(), new() { ["jobId"] = "job-1", ["action"] = "retry" }));

        // Assert
        Assert.Equal("queued", json.GetProperty("status").GetString());
        Assert.Equal(TenantProvisioningStatus.Pending, _abandoned.Status);
        Assert.Equal(0, _abandoned.AttemptCount);
        Assert.Null(_abandoned.NextAttemptUtc);
        _jobs.Verify(store => store.UpdateAsync(_abandoned, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveSiteProvisioningJob_MarkCreated_NeedsTheSitesAddress()
    {
        // Act
        var missing = await InvokeAsync(ResolveSiteProvisioningJobTool.TheName, Build(), new() { ["jobId"] = "job-1", ["action"] = "markCreated" });
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(ResolveSiteProvisioningJobTool.TheName, Build(), new()
        {
            ["jobId"] = "job-1",
            ["action"] = "markCreated",
            ["siteUrl"] = "https://bluebird.example.com",
        }));

        // Assert
        Assert.Contains("give the siteUrl", missing, StringComparison.Ordinal);
        Assert.Equal("succeeded", json.GetProperty("status").GetString());
        Assert.Equal("https://bluebird.example.com", _abandoned.SiteUrl);
        Assert.Null(_abandoned.LastError);
    }

    [Fact]
    public async Task ResolveSiteProvisioningJob_RefusesARetryWithoutThePassword()
    {
        // Arrange
        _abandoned.ProtectedAdminPassword = null;

        // Act
        var result = await InvokeAsync(ResolveSiteProvisioningJobTool.TheName, Build(), new() { ["jobId"] = "job-1", ["action"] = "retry" });

        // Assert
        Assert.Contains("no longer holds the administrator password", result, StringComparison.Ordinal);
        _jobs.Verify(store => store.UpdateAsync(It.IsAny<TenantProvisioningJob>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetInstallmentPlan_DescribesTheCardWithoutItsReferences()
    {
        // Act
        var result = await InvokeAsync(GetInstallmentPlanTool.TheName, Build(), new() { ["planId"] = "plan-1" });
        var json = AgentToolTestContext.ReadJson(result);

        // Assert
        Assert.Equal(3, json.GetProperty("schedule").GetArrayLength());
        Assert.Equal("2026-11-01", json.GetProperty("nextPayment").GetProperty("due").GetString());
        Assert.Contains("4242", json.GetProperty("savedCard").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain("pm_SECRET", result, StringComparison.Ordinal);
        Assert.DoesNotContain("thompson@example.com", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateInstallmentPlan_PassesTheDateAsEntered_AndRelaysTheServicesErrors()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(CreateInstallmentPlanTool.TheName, Build(), Arguments(CreateInstallmentPlanTool.TheName)));

        // Assert
        Assert.Equal("customer-1", _createRequest.CustomerUserId);
        Assert.Null(_createRequest.NewCustomerEmail);
        Assert.Equal(new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc), _createRequest.FirstDueUtc);
        Assert.Equal(InstallmentFrequency.Monthly, _createRequest.Frequency);
        Assert.False(json.GetProperty("saved").GetBoolean());
        Assert.Equal("installmentCount", Assert.Single(json.GetProperty("errors").EnumerateArray()).GetProperty("field").GetString());
    }

    [Fact]
    public async Task ChargeInstallmentPayment_ChargesOnce_PerIdempotencyKey()
    {
        // Arrange
        var services = Build();

        // Act
        var first = AgentToolTestContext.ReadJson(await InvokeAsync(ChargeInstallmentPaymentTool.TheName, services, Arguments(ChargeInstallmentPaymentTool.TheName)));
        var again = await InvokeAsync(ChargeInstallmentPaymentTool.TheName, services, Arguments(ChargeInstallmentPaymentTool.TheName));

        // Assert
        Assert.Equal("pending", first.GetProperty("state").GetString());
        Assert.StartsWith("Nothing was charged again", again, StringComparison.Ordinal);
        _planService.Verify(service => service.ChargeAsync("plan-1", 2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(99, "USD", "Give exactly that amount")]
    [InlineData(100, "EUR", "is in USD")]
    public async Task ChargeInstallmentPayment_RefusesAnAmountOrCurrencyThatIsNotThePayments(int amount, string currency, string expected)
    {
        // Arrange
        var arguments = Arguments(ChargeInstallmentPaymentTool.TheName);
        arguments["amount"] = (decimal)amount;
        arguments["currency"] = currency;

        // Act
        var result = await InvokeAsync(ChargeInstallmentPaymentTool.TheName, Build(), arguments);

        // Assert
        Assert.StartsWith("Nothing was charged", result, StringComparison.Ordinal);
        Assert.Contains(expected, result, StringComparison.Ordinal);
        Assert.Empty(_planService.Invocations);
    }

    [Fact]
    public async Task ChargeInstallmentPayment_NeedsAnIdempotencyKey()
    {
        // Arrange
        var arguments = Arguments(ChargeInstallmentPaymentTool.TheName);
        arguments.Remove("idempotencyKey");

        // Act
        var result = await InvokeAsync(ChargeInstallmentPaymentTool.TheName, Build(), arguments);

        // Assert
        Assert.Contains("idempotencyKey", result, StringComparison.Ordinal);
        Assert.Empty(_planService.Invocations);
    }

    [Fact]
    public async Task CancelInstallmentPlan_RefusesAFinishedPlan()
    {
        // Arrange
        _plan.Status = InstallmentPlanStatus.Completed;

        // Act
        var result = await InvokeAsync(CancelInstallmentPlanTool.TheName, Build(), new() { ["planId"] = "plan-1" });

        // Assert
        Assert.Contains("already completed", result, StringComparison.Ordinal);
        Assert.Empty(_planService.Invocations);
    }

    private static Dictionary<string, object> Arguments(string toolName)
        => toolName switch
        {
            GetSubscriptionTool.TheName => new() { ["subscriptionId"] = "sub-1" },
            CancelSubscriptionTool.TheName => new() { ["subscriptionId"] = "sub-1", ["atPeriodEnd"] = true },
            SetSubscriptionPausedTool.TheName => new() { ["subscriptionId"] = "sub-1", ["paused"] = true },
            ResolveSiteProvisioningJobTool.TheName => new() { ["jobId"] = "job-1", ["action"] = "retry" },
            GetInstallmentPlanTool.TheName or CancelInstallmentPlanTool.TheName => new() { ["planId"] = "plan-1" },
            CreateInstallmentPlanTool.TheName => new()
            {
                ["title"] = "Season fee",
                ["customerUserId"] = "customer-1",
                ["currency"] = "USD",
                ["totalAmount"] = 1200m,
                ["downPaymentAmount"] = 200m,
                ["installmentCount"] = 5,
                ["frequency"] = "monthly",
                ["firstDueDate"] = "2026-12-01",
                ["collectionMethod"] = "autoCharge",
            },
            ChargeInstallmentPaymentTool.TheName => new()
            {
                ["planId"] = "plan-1",
                ["paymentNumber"] = 2,
                ["amount"] = 100m,
                ["currency"] = "USD",
                ["idempotencyKey"] = "charge-plan-1-2",
            },
            _ => [],
        };

    private ServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);

        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(new DateTime(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc));

        services.AddSingleton(_subscriptions.Object);
        services.AddSingleton(_lifecycle.Object);
        services.AddSingleton(_jobs.Object);
        services.AddSingleton(_plans.Object);
        services.AddSingleton(_planService.Object);
        services.AddSingleton(clock.Object);
        services.AddSingleton(Mock.Of<YesSqlSession>());
        services.AddSingleton<IDistributedCache>(_cache);
        services.AddSingleton<IDistributedLock>(new FakeDistributedLock());

        return services.BuildServiceProvider();
    }

    private static Task<string> InvokeAsync(string toolName, IServiceProvider services, Dictionary<string, object> arguments)
    {
        AIFunction tool = toolName switch
        {
            ListSubscriptionsTool.TheName => new ListSubscriptionsTool(),
            GetSubscriptionTool.TheName => new GetSubscriptionTool(),
            CancelSubscriptionTool.TheName => new CancelSubscriptionTool(),
            SetSubscriptionPausedTool.TheName => new SetSubscriptionPausedTool(),
            ListSiteProvisioningJobsTool.TheName => new ListSiteProvisioningJobsTool(),
            ResolveSiteProvisioningJobTool.TheName => new ResolveSiteProvisioningJobTool(),
            ListInstallmentPlansTool.TheName => new ListInstallmentPlansTool(),
            GetInstallmentPlanTool.TheName => new GetInstallmentPlanTool(),
            CreateInstallmentPlanTool.TheName => new CreateInstallmentPlanTool(),
            ChargeInstallmentPaymentTool.TheName => new ChargeInstallmentPaymentTool(),
            _ => new CancelInstallmentPlanTool(),
        };

        return AgentToolTestContext.InvokeAsync(tool, services, arguments);
    }
}
