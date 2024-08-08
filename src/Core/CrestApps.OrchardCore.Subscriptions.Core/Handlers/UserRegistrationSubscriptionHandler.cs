using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

public sealed class UserRegistrationSubscriptionHandler : SubscriptionHandlerBase
{
    public const string StepKey = "UserRegistration";

    private readonly SubscriptionPaymentSession _subscriptionPaymentSession;

    internal readonly IStringLocalizer S;

    public UserRegistrationSubscriptionHandler(
        SubscriptionPaymentSession subscriptionPaymentSession,
        IStringLocalizer<PaymentSubscriptionHandler> stringLocalizer)
    {
        _subscriptionPaymentSession = subscriptionPaymentSession;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(SubscriptionFlowInitializingContext context)
    {
        context.Session.Steps.Add(new SubscriptionFlowStep()
        {
            Title = S["Create an account"],
            Description = S["Create an account to manage your subscription."],
            Key = StepKey,
            Order = 2,
        });

        return Task.CompletedTask;
    }
}
