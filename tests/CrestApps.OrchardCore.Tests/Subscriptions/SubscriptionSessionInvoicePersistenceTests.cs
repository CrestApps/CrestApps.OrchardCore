using System.Text.Json;
using CrestApps.OrchardCore.Payments.Core.Models;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Subscriptions;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Json;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// Guards the checkout invariant that a pending subscription session persists its computed
/// <see cref="Invoice"/>. The Stripe and Pay Later payment endpoints reload the session from the store
/// and read the invoice with <c>TryGet&lt;Invoice&gt;</c>; if the invoice does not survive persistence
/// those endpoints return a 404 and the customer cannot pay. These tests reproduce the exact store
/// round-trip so the regression can never return silently.
/// </summary>
public class SubscriptionSessionInvoicePersistenceTests
{
    private const string Currency = "USD";

    [Fact]
    public async Task PendingSession_AfterActivation_CarriesInvoice()
    {
        // Arrange
        var handler = CreateHandler();

        // Act
        var session = await CreateActivatedSessionAsync(handler, price: 20.00);

        // Assert
        Assert.True(session.TryGet<Invoice>(out var invoice));
        Assert.Equal(20.00, invoice.GrandTotal, 2);
    }

    [Fact]
    public async Task PersistedSession_AfterStoreRoundTrip_StillCarriesInvoice()
    {
        // Arrange
        var handler = CreateHandler();
        var session = await CreateActivatedSessionAsync(handler, price: 20.00);

        Assert.True(session.TryGet<Invoice>(out var invoice));

        // Act: round-trip the session through the exact document serializer YesSql uses to persist it,
        // which is what the payment endpoints observe when they reload the session.
        var options = new DocumentJsonSerializerOptions().SerializerOptions;
        var json = JsonSerializer.Serialize(session, options);
        var reloaded = JsonSerializer.Deserialize<SubscriptionSession>(json, options);

        // Assert
        Assert.NotNull(reloaded);
        Assert.True(reloaded.TryGet<Invoice>(out var reloadedInvoice));
        Assert.Equal(invoice.GrandTotal, reloadedInvoice.GrandTotal, 2);
        Assert.Equal(invoice.Currency, reloadedInvoice.Currency);
    }

    private static async Task<SubscriptionSession> CreateActivatedSessionAsync(PaymentSubscriptionHandler handler, double price)
    {
        var contentItem = new ContentItem { ContentType = "Plan" };
        contentItem.Weld(new ProductPart { Price = price });
        contentItem.Weld(new SubscriptionPart
        {
            BillingDuration = 1,
            DurationType = DurationType.Month,
        });

        var session = new SubscriptionSession
        {
            SessionId = Guid.NewGuid().ToString(),
            Status = SubscriptionSessionStatus.Pending,
            ContentItemVersionId = "plan-version-1",
        };

        await handler.ActivatingAsync(new SubscriptionFlowActivatingContext(session, contentItem));

        var flow = new SubscriptionFlow(session, contentItem);

        await handler.ActivatedAsync(new SubscriptionFlowActivatedContext(flow));

        return session;
    }

    private static PaymentSubscriptionHandler CreateHandler()
    {
        var siteService = new Mock<ISiteService>();
        var site = new Mock<ISite>();
        site.Setup(s => s.GetOrCreate<SubscriptionSettings>())
            .Returns(new SubscriptionSettings { Currency = Currency });
        siteService.Setup(s => s.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return new PaymentSubscriptionHandler(
            PaymentTestHelpers.CreatePaymentSession(),
            siteService.Object,
            new NullSubscriptionTaxService(),
            NullLogger<PaymentSubscriptionHandler>.Instance,
            Mock.Of<IStringLocalizer<PaymentSubscriptionHandler>>());
    }
}
