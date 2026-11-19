using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.PayLater.Services;
using CrestApps.OrchardCore.PayLater.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.PayLater.Drivers;

/// <summary>
/// Renders the Pay Later panel on the checkout payment step: when the payment will be due, under the buyer's payment
/// term, and that renewals are billed the same way.
/// </summary>
/// <remarks>
/// Pay Later collects nothing, so the panel is purely an explanation of what the customer is agreeing to. It
/// still exists as a driver rather than being special-cased in the checkout page, because that is what keeps
/// the payment step provider-agnostic: adding a gateway later means adding a driver, not editing the page.
/// </remarks>
public sealed class PayLaterCheckoutPaymentMethodDisplayDriver : DisplayDriver<CheckoutFlowPaymentMethod>
{
    private readonly PayLaterDueDateResolver _dueDates;
    private readonly IClock _clock;
    private readonly ILocalClock _localClock;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayLaterCheckoutPaymentMethodDisplayDriver"/> class.
    /// </summary>
    /// <param name="dueDates">Says when a Pay Later balance is due.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="localClock">The clock that shows the due date in the site's time zone.</param>
    public PayLaterCheckoutPaymentMethodDisplayDriver(
        PayLaterDueDateResolver dueDates,
        IClock clock,
        ILocalClock localClock)
    {
        _dueDates = dueDates;
        _clock = clock;
        _localClock = localClock;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(CheckoutFlowPaymentMethod method, BuildEditorContext context)
        => Initialize<PayLaterCheckoutPaymentMethodViewModel>("PayLaterCheckoutPaymentMethod", async model =>
        {
            var session = method?.Flow?.Session as CheckoutSession;
            CheckoutInvoice invoice = null;
            session?.TryGet(out invoice);

            var lines = invoice?.LineItems ?? [];

            model.Recurring = lines.Any(line => line.Plan is not null);

            // A purchase that is all subscription owes nothing until its trial or delayed start ends, so the first
            // payment counts from then, as the balance does when it is recorded.
            var deferralDays = lines.Length > 0 && lines.All(line => line.Plan is not null) ? CheckoutObligations.GetDeferralDays(lines) : 0;
            var payer = string.IsNullOrEmpty(session?.OwnerId) ? default : new PartyRef(PartyKinds.User, session.OwnerId);

            model.StartsLater = deferralDays > 0;

            if (await _dueDates.GetDueUtcAsync(_clock.UtcNow.AddDays(deferralDays), payer) is { } dueUtc)
            {
                model.DueDate = (await _localClock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(dueUtc, DateTimeKind.Utc)))).DateTime;
            }
        })
        .Location("Content")
        .OnGroup(PayLaterCheckoutPaymentProvider.ProcessorKey);
}
