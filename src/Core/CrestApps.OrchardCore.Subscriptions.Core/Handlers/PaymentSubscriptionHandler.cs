using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

public sealed class PaymentSubscriptionHandler : SubscriptionHandlerBase
{
    public const string StepKey = "Payment";

    internal readonly IStringLocalizer S;

    public PaymentSubscriptionHandler(IStringLocalizer<PaymentSubscriptionHandler> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task InitializingAsync(SubscriptionFlowInitializingContext context)
    {
        context.Session.Steps.Add(new SubscriptionFlowStep()
        {
            Title = S["Payment"],
            Key = StepKey,
            Order = int.MaxValue,
        });

        return Task.CompletedTask;
    }
}
