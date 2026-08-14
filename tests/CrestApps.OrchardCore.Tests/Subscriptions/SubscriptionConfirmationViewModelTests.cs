using System.Text.Json;
using CrestApps.OrchardCore.Subscriptions;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using OrchardCore.Entities;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

public class SubscriptionConfirmationViewModelTests
{
    private static readonly JsonSerializerOptions _options = new();

    [Fact]
    public void Create_GathersInvoiceAndSubscriptions()
    {
        var session = new SubscriptionSession();
        session.Put(new Invoice
        {
            Currency = "usd",
            DueNow = 29.99,
            GrandTotal = 59.98,
        });
        session.Put(new SubscriptionsMetadata
        {
            Subscriptions =
            [
                new SubscriptionInfo
                {
                    SubscriptionId = "sub_1",
                    StartedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMonths(1),
                },
            ],
        });

        var model = SubscriptionConfirmationViewModel.Create(session, _options);

        Assert.NotNull(model.Invoice);
        Assert.Equal(29.99, model.Invoice.DueNow, 2);
        Assert.Single(model.Subscriptions);
        Assert.Equal("sub_1", model.Subscriptions[0].SubscriptionId);
    }

    [Fact]
    public void Create_WithoutTenantOnboarding_LeavesTenantInfoNull()
    {
        var session = new SubscriptionSession();

        var model = SubscriptionConfirmationViewModel.Create(session, _options);

        Assert.Null(model.TenantOnboarding);
        Assert.Empty(model.Subscriptions);
    }

    [Fact]
    public void Create_WithTenantOnboarding_ExposesAdminInfoWithoutPassword()
    {
        var session = new SubscriptionSession();

        var step = new TenantOnboardingStep
        {
            TenantTitle = "Contoso",
            AdminUsername = "admin",
            AdminEmail = "admin@contoso.test",
            AdminPassword = "super-secret-should-not-leak",
            Domains = ["contoso.test"],
        };

        session.SavedSteps[SubscriptionConstants.StepKey.TenantOnboarding] = JsonSerializer.SerializeToNode(step, _options);

        var model = SubscriptionConfirmationViewModel.Create(session, _options);

        Assert.NotNull(model.TenantOnboarding);
        Assert.Equal("Contoso", model.TenantOnboarding.SiteTitle);
        Assert.Equal("admin", model.TenantOnboarding.AdminUsername);
        Assert.Equal("admin@contoso.test", model.TenantOnboarding.AdminEmail);
        Assert.Contains("contoso.test", model.TenantOnboarding.Domains);
    }

    [Fact]
    public void Create_NullSession_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => SubscriptionConfirmationViewModel.Create(null, _options));
    }

    [Fact]
    public void Create_TenantOnboardingViewModel_NeverContainsPassword()
    {
        var session = new SubscriptionSession();

        var step = new TenantOnboardingStep
        {
            AdminUsername = "admin",
            AdminPassword = "leak-me",
            Domains = ["a.test"],
        };

        session.SavedSteps[SubscriptionConstants.StepKey.TenantOnboarding] = JsonSerializer.SerializeToNode(step, _options);

        var model = SubscriptionConfirmationViewModel.Create(session, _options);

        var serialized = JsonSerializer.Serialize(model.TenantOnboarding);

        Assert.DoesNotContain("leak-me", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", serialized, StringComparison.OrdinalIgnoreCase);
    }
}
