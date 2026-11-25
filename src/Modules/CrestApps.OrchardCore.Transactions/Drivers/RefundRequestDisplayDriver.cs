using CrestApps.OrchardCore.Transactions.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Transactions.Drivers;

/// <summary>
/// Builds the editor of the page that refunds a payment: one shape per card.
/// </summary>
internal sealed class RefundRequestDisplayDriver : DisplayDriver<RefundRequestViewModel>
{
    internal readonly IStringLocalizer S;

    public RefundRequestDisplayDriver(IStringLocalizer<RefundRequestDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(RefundRequestViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<RefundRequestViewModel>("RefundRequestPayment_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Payment"], "10")),
            Initialize<RefundRequestViewModel>("RefundRequestRefund_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Refund"], "20")));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(RefundRequestViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Amount, m => m.Reason);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    /// <inheritdoc/>
    protected override void BuildPrefix(RefundRequestViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(RefundRequestViewModel source, RefundRequestViewModel target)
    {
        target.Attempt = source.Attempt;
        target.Amount = source.Amount;
        target.AlreadyRefunded = source.AlreadyRefunded;
        target.Reason = source.Reason;
    }
}
