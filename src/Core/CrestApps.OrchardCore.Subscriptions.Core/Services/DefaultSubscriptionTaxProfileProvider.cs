using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Taxation.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// The default <see cref="ISubscriptionTaxProfileProvider"/>. It reads the tax classification from the
/// subscribed content item's <c>TaxationPart</c> and derives the customer destination from the payment
/// method captured during the flow (the card's issuing country). Register a custom implementation to
/// source a full billing/shipping address from your own checkout data.
/// </summary>
public sealed class DefaultSubscriptionTaxProfileProvider : ISubscriptionTaxProfileProvider
{
    public Task<SubscriptionTaxProfile> GetProfileAsync(SubscriptionFlow flow, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(flow);

        var profile = new SubscriptionTaxProfile();

        ReadClassification(flow, profile);
        ReadDestination(flow.Session, profile);

        return Task.FromResult(profile);
    }

    public Task<SubscriptionTaxProfile> GetProfileAsync(ISubscriptionFlowSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var profile = new SubscriptionTaxProfile();

        // The subscribed content item (and thus its TaxationPart) is not available at recurring billing
        // time, so the classification resolved at checkout is reused from the persisted invoice. The
        // destination is re-resolved from the session so that a customer address change takes effect on
        // future cycles while historical snapshots remain untouched.
        if (session.TryGet<Invoice>(out var invoice))
        {
            profile.DefaultTaxCategoryCode = invoice.TaxCategoryCode;
            profile.DefaultTaxClassificationCode = invoice.TaxClassificationCode;
        }

        ReadDestination(session, profile);

        return Task.FromResult(profile);
    }

    private static void ReadClassification(SubscriptionFlow flow, SubscriptionTaxProfile profile)
    {
        JsonNode content = flow.ContentItem?.Content;
        var taxationPart = content?["TaxationPart"];

        if (taxationPart is null)
        {
            return;
        }

        profile.DefaultTaxCategoryCode = taxationPart["TaxCategoryCode"]?.GetValue<string>();
        profile.DefaultTaxClassificationCode = taxationPart["TaxClassificationCode"]?.GetValue<string>();
    }

    private static void ReadDestination(ISubscriptionFlowSession session, SubscriptionTaxProfile profile)
    {
        if (!session.TryGet<SubscriptionInfo>(out var subscriptionInfo))
        {
            return;
        }

        var country = subscriptionInfo?.PaymentMethod?.Card?.Country;

        if (!string.IsNullOrEmpty(country))
        {
            profile.Destination = new TaxAddress
            {
                Country = country,
            };
        }
    }
}
