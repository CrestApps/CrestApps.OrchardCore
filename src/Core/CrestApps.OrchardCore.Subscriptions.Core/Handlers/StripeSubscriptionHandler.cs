using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Http;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

/// <summary>
/// Synchronizes Stripe customer information after a subscription flow creates a new user account.
/// </summary>
public sealed class StripeSubscriptionHandler : SubscriptionHandlerBase
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IStripeCustomerService _stripeCustomerService;
    private readonly IDisplayNameProvider _displayNameProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="StripeSubscriptionHandler"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The accessor used to read request features for the active subscription flow.</param>
    /// <param name="stripeCustomerService">The Stripe customer service used to update the created customer.</param>
    /// <param name="displayNameProvider">The provider used to resolve the display name for the created user.</param>
    public StripeSubscriptionHandler(
        IHttpContextAccessor httpContextAccessor,
        IStripeCustomerService stripeCustomerService,
        IDisplayNameProvider displayNameProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _stripeCustomerService = stripeCustomerService;
        _displayNameProvider = displayNameProvider;
    }

    /// <summary>
    /// Updates the Stripe customer with the profile information from the user created during the flow.
    /// </summary>
    /// <param name="context">The context for the subscription flow that completed.</param>
    public override async Task CompletedAsync(SubscriptionFlowCompletedContext context)
    {
        var subscriber = _httpContextAccessor.HttpContext.Features.Get<CustomerCreatedDuringSubscriptionFlow>();

        if (subscriber?.User == null || !context.Flow.Session.TryGet<StripeMetadata>(out var metadata))
        {
            return;
        }

        // When a subscriber is new, Stripe customer is created without all the user info.
        // After the process is completed, we'll update the customer info to sync the
        // from the new user.
        await _stripeCustomerService.UpdateAsync(metadata.CustomerId, new UpdateCustomerRequest
        {
            Name = await _displayNameProvider.GetAsync(subscriber.User),
            Phone = subscriber.User.PhoneNumber,
            Email = subscriber.User.Email,
            Metadata = new Dictionary<string, string>()
            {
                { "userName", subscriber.User.UserName },
                { "userId", subscriber.User.UserId },
            },
        });
    }
}
