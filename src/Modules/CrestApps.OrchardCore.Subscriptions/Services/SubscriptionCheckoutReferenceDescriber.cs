using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Subscriptions.Core;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Subscriptions.Services;

/// <summary>
/// Names what a subscription checkout bought, the plan, wherever a checkout's reference is shown (a transaction's
/// reference, a balance's title), instead of its raw content item id.
/// </summary>
public sealed class SubscriptionCheckoutReferenceDescriber : ICheckoutReferenceDescriber
{
    private readonly IContentManager _contentManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionCheckoutReferenceDescriber"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager the plans are read from.</param>
    public SubscriptionCheckoutReferenceDescriber(IContentManager contentManager)
    {
        _contentManager = contentManager;
    }

    /// <inheritdoc/>
    public async Task<CheckoutReferenceDescription> DescribeAsync(string referenceType, string referenceId, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(referenceType, SubscriptionCheckout.ReferenceType, StringComparison.Ordinal) || string.IsNullOrEmpty(referenceId))
        {
            return null;
        }

        var plan = await _contentManager.GetAsync(referenceId, VersionOptions.Latest);

        return string.IsNullOrWhiteSpace(plan?.DisplayText)
            ? null
            : new CheckoutReferenceDescription { Text = plan.DisplayText };
    }
}
