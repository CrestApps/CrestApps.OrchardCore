using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Transactions.Drivers;

/// <summary>
/// Builds the row of a payment term in the admin list, in both layouts, and its editor.
/// </summary>
public sealed class PaymentTermDisplayDriver : DisplayDriver<PaymentTerm>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentTermDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PaymentTermDisplayDriver(IStringLocalizer<PaymentTermDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(PaymentTerm term, BuildDisplayContext context)
    {
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = term.Name?.ToLowerInvariant() ?? string.Empty;
        }

        return CombineAsync(
            View("PaymentTerm_Fields_SummaryAdmin", term)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("PaymentTerm_Description_SummaryAdmin", term)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5")
                .RenderWhen(() => Task.FromResult(!string.IsNullOrWhiteSpace(term.Description))),
            View("PaymentTerm_Tags_SummaryAdmin", term)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Due:5"),
            View("PaymentTerm_Buttons_SummaryAdmin", term).Location("Actions:5")
        );
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(PaymentTerm term, BuildEditorContext context)
        => Initialize<PaymentTermViewModel>("PaymentTermFields_Edit", model =>
        {
            model.Name = term.Name;
            model.Description = term.Description;
            model.Kind = term.Kind;
            model.Days = term.Days;
            model.DayOfMonth = term.DayOfMonth;
            model.DiscountPercent = term.DiscountPercent;
            model.DiscountDays = term.DiscountDays;
            model.Enabled = term.Enabled;
            model.KindOptions =
            [
                new SelectListItem(S["Due on receipt"], nameof(PaymentTermKind.DueOnReceipt), term.Kind == PaymentTermKind.DueOnReceipt),
                new SelectListItem(S["A number of days after the issue date"], nameof(PaymentTermKind.NetDays), term.Kind == PaymentTermKind.NetDays),
                new SelectListItem(S["End of the month, plus a number of days"], nameof(PaymentTermKind.EndOfMonth), term.Kind == PaymentTermKind.EndOfMonth),
                new SelectListItem(S["A day of the next month"], nameof(PaymentTermKind.DayOfNextMonth), term.Kind == PaymentTermKind.DayOfNextMonth),
            ];
        }).Location("Content:1");

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(PaymentTerm term, UpdateEditorContext context)
    {
        var model = new PaymentTermViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        term.Name = model.Name?.Trim();
        term.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        term.Kind = model.Kind;
        term.Days = model.Kind is PaymentTermKind.NetDays or PaymentTermKind.EndOfMonth ? model.Days : 0;
        term.DayOfMonth = model.Kind == PaymentTermKind.DayOfNextMonth ? model.DayOfMonth : 0;
        term.DiscountPercent = model.DiscountPercent is > 0m ? model.DiscountPercent : null;
        term.DiscountDays = term.DiscountPercent is null ? null : model.DiscountDays;
        term.Enabled = model.Enabled;

        return Edit(term, context);
    }
}
