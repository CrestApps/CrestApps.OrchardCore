using CrestApps.OrchardCore.Subscriptions.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

/// <summary>
/// Builds the editor of the page that creates an installment plan: one shape per card.
/// </summary>
internal sealed class CreateInstallmentPlanDisplayDriver : DisplayDriver<CreateInstallmentPlanViewModel>
{
    internal readonly IStringLocalizer S;

    public CreateInstallmentPlanDisplayDriver(IStringLocalizer<CreateInstallmentPlanDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(CreateInstallmentPlanViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<CreateInstallmentPlanViewModel>("CreateInstallmentPlanCustomer_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Customer"], "10")),
            Initialize<CreateInstallmentPlanViewModel>("CreateInstallmentPlanDetails_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Plan"], "20")),
            Initialize<CreateInstallmentPlanViewModel>("CreateInstallmentPlanCollection_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "3").Card(S["Collecting the Payments"], "30")),
            Initialize<CreateInstallmentPlanViewModel>("CreateInstallmentPlanSchedule_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "4").Card(S["Schedule"], "40")));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(CreateInstallmentPlanViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.NewCustomer, m => m.CustomerUserId, m => m.NewCustomerName, m => m.NewCustomerEmail, m => m.Title, m => m.Currency, m => m.TotalAmount, m => m.DownPaymentAmount, m => m.InstallmentCount, m => m.Frequency, m => m.FirstDueDate, m => m.CollectionMethod, m => m.Notes);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    /// <inheritdoc/>
    protected override void BuildPrefix(CreateInstallmentPlanViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(CreateInstallmentPlanViewModel source, CreateInstallmentPlanViewModel target)
    {
        target.Title = source.Title;
        target.NewCustomer = source.NewCustomer;
        target.CustomerUserId = source.CustomerUserId;
        target.NewCustomerName = source.NewCustomerName;
        target.NewCustomerEmail = source.NewCustomerEmail;
        target.Currency = source.Currency;
        target.TotalAmount = source.TotalAmount;
        target.DownPaymentAmount = source.DownPaymentAmount;
        target.InstallmentCount = source.InstallmentCount;
        target.Frequency = source.Frequency;
        target.FirstDueDate = source.FirstDueDate;
        target.CollectionMethod = source.CollectionMethod;
        target.Notes = source.Notes;
        target.Currencies = source.Currencies;
        target.CurrencyDecimals = source.CurrencyDecimals;
    }
}
