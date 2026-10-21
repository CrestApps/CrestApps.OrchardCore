using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// The commerce screens show stored values by name. A value added to an enum without a name would fall back to its
/// identifier (<c>PastDue</c>, <c>PendingManualReview</c>), which is what these screens used to show everywhere.
/// </summary>
public sealed class CommerceDisplayTextTests
{
    [Fact]
    public void EveryStoredValue_HasAName()
    {
        var transactions = CreateTransactionText();
        var subscriptions = CreateSubscriptionText();

        // Single words are allowed to read the same as their identifier; anything else must have been named.
        Assert.All(Enum.GetValues<PaymentAttemptState>(), value => AssertNamed(value.ToString(), transactions.PaymentStateName(value)));
        Assert.All(Enum.GetValues<RefundStatus>(), value => AssertNamed(value.ToString(), transactions.RefundStatusName(value)));
        Assert.All(Enum.GetValues<TransactionEventType>(), value => AssertNamed(value.ToString(), transactions.EventTypeName(value)));
        Assert.All(Enum.GetValues<SubscriptionStatus>(), value => AssertNamed(value.ToString(), subscriptions.StatusName(value)));
        Assert.All(Enum.GetValues<SubscriptionEventType>(), value => AssertNamed(value.ToString(), subscriptions.EventTypeName(value)));
        Assert.All(Enum.GetValues<TenantProvisioningStatus>(), value => AssertNamed(value.ToString(), subscriptions.ProvisioningStatusName(value)));
    }

    [Theory]
    [InlineData(1, DurationType.Month, "every month")]
    [InlineData(3, DurationType.Month, "every 3 months")]
    [InlineData(1, DurationType.Year, "every year")]
    [InlineData(2, DurationType.Week, "every 2 weeks")]
    public void BillingInterval_ReadsAsASentence(int count, DurationType type, string expected)
        => Assert.Equal(expected, CreateSubscriptionText().BillingInterval(count, type));

    [Fact]
    public void PaymentMethodName_UsesTheProvidersName_AndFallsBackToTheKey()
    {
        var provider = new Mock<ICheckoutPaymentProvider>();
        provider.SetupGet(p => p.Key).Returns("pay-later");
        provider.SetupGet(p => p.DisplayName).Returns("Pay Later");

        var text = CreateTransactionText(provider.Object);

        Assert.Equal("Pay Later", text.PaymentMethodName("pay-later"));
        Assert.Equal("retired-gateway", text.PaymentMethodName("retired-gateway"));
    }

    [Fact]
    public void SourceName_UsesTheRegisteredName()
    {
        var options = new TransactionSourceOptions();
        options.AddSource(new TransactionSource("installment-plan") { DisplayName = LocalizationSource.Create<CommerceDisplayTextTests>("Installment plan") });

        var text = new TransactionDisplayText([], Options.Create(options), new PassThroughStringLocalizer<TransactionDisplayText>(), new SourceTextLocalizerFactory());

        Assert.Equal("Installment plan", text.SourceName("installment-plan"));
        Assert.Equal("unknown", text.SourceName("unknown"));
    }

    [Theory]
    [InlineData(TenantProvisioningStatus.Pending, true, true)]
    [InlineData(TenantProvisioningStatus.Failed, true, true)]
    [InlineData(TenantProvisioningStatus.Running, true, false)]
    [InlineData(TenantProvisioningStatus.Succeeded, true, false)]
    [InlineData(TenantProvisioningStatus.Abandoned, false, false)]
    [InlineData(TenantProvisioningStatus.Failed, false, false)]
    public void CanRetry_OnlyWhenTheJobCanFinish(TenantProvisioningStatus status, bool holdsPassword, bool expected)
    {
        var job = new TenantProvisioningJob
        {
            Status = status,
            ProtectedAdminPassword = holdsPassword ? "protected" : null,
        };

        Assert.Equal(expected, TenantProvisioningJobActions.CanRetry(job));
    }

    [Theory]
    [InlineData(TenantProvisioningStatus.Abandoned, true)]
    [InlineData(TenantProvisioningStatus.Failed, false)]
    [InlineData(TenantProvisioningStatus.Succeeded, false)]
    public void CanMarkCreated_OnlyAnAbandonedJob(TenantProvisioningStatus status, bool expected)
        => Assert.Equal(expected, TenantProvisioningJobActions.CanMarkCreated(new TenantProvisioningJob { Status = status }));

    private static void AssertNamed(string identifier, string name)
    {
        Assert.False(string.IsNullOrWhiteSpace(name));

        if (identifier.Skip(1).Any(char.IsUpper))
        {
            Assert.NotEqual(identifier, name);
        }
    }

    private static TransactionDisplayText CreateTransactionText(params ICheckoutPaymentProvider[] providers)
        => new(providers, Options.Create(new TransactionSourceOptions()), new PassThroughStringLocalizer<TransactionDisplayText>(), new SourceTextLocalizerFactory());

    private static SubscriptionDisplayText CreateSubscriptionText()
        => new([], new PassThroughStringLocalizer<SubscriptionDisplayText>());
}
