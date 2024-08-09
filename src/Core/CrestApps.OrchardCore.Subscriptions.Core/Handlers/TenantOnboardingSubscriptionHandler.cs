using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

public sealed class TenantOnboardingSubscriptionHandler : SubscriptionHandlerBase
{
    public const string StepKey = "SiteInfo";

    private readonly SubscriptionPaymentSession _subscriptionPaymentSession;

    internal readonly IStringLocalizer S;

    public TenantOnboardingSubscriptionHandler(SubscriptionPaymentSession subscriptionPaymentSession,
        IStringLocalizer<PaymentSubscriptionHandler> stringLocalizer)
    {
        _subscriptionPaymentSession = subscriptionPaymentSession;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(SubscriptionFlowInitializingContext context)
    {
        context.Session.Steps.Add(new SubscriptionFlowStep()
        {
            Title = S["New Site Info"],
            Description = S["Information to be used for setting up your new site."],
            Key = StepKey,
            CollectData = true,
            Order = 5,
        });

        return Task.CompletedTask;
    }
}
