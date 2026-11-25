using CrestApps.OrchardCore.Customers.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Customers.Drivers;

/// <summary>
/// Builds the editor of a customer's fields, grouped in cards by their placement.
/// </summary>
internal sealed class CustomerEditDisplayDriver : DisplayDriver<CustomerEditViewModel>
{
    internal readonly IStringLocalizer S;

    public CustomerEditDisplayDriver(IStringLocalizer<CustomerEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(CustomerEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<CustomerEditViewModel>("CustomerGeneral_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["General"], "10")),
            Initialize<CustomerEditViewModel>("CustomerBusiness_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Business"], "20")),
            Initialize<CustomerEditViewModel>("CustomerContact_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "3").Card(S["Contact"], "30")));
    }

    public override async Task<IDisplayResult> UpdateAsync(CustomerEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(
            model,
            Prefix,
            m => m.Kind,
            m => m.Status,
            m => m.FirstName,
            m => m.LastName,
            m => m.DisplayName,
            m => m.CompanyName,
            m => m.TaxId,
            m => m.IsTaxExempt,
            m => m.TaxExemptionReason,
            m => m.Email,
            m => m.Phone,
            m => m.AcceptsMarketing);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the page script selects.
    protected override void BuildPrefix(CustomerEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(CustomerEditViewModel source, CustomerEditViewModel model)
    {
        model.CustomerId = source.CustomerId;
        model.IsNew = source.IsNew;
        model.CanManage = source.CanManage;
        model.UserId = source.UserId;
        model.UserName = source.UserName;
        model.Kinds = source.Kinds;
        model.Statuses = source.Statuses;
        model.Kind = source.Kind;
        model.Status = source.Status;
        model.FirstName = source.FirstName;
        model.LastName = source.LastName;
        model.DisplayName = source.DisplayName;
        model.CompanyName = source.CompanyName;
        model.TaxId = source.TaxId;
        model.IsTaxExempt = source.IsTaxExempt;
        model.TaxExemptionReason = source.TaxExemptionReason;
        model.Email = source.Email;
        model.Phone = source.Phone;
        model.AcceptsMarketing = source.AcceptsMarketing;
    }
}
