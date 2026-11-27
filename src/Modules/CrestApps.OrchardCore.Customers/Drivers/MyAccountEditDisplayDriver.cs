using CrestApps.OrchardCore.Customers.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Customers.Drivers;

/// <summary>
/// Builds the editor of a customer's own profile on the admin's My Account page, grouped in cards by their placement.
/// </summary>
internal sealed class MyAccountEditDisplayDriver : DisplayDriver<MyAccountViewModel>
{
    internal readonly IStringLocalizer S;

    public MyAccountEditDisplayDriver(IStringLocalizer<MyAccountEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(MyAccountViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<MyAccountViewModel>("MyAccountGeneral_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["General"], "10")),
            Initialize<MyAccountViewModel>("MyAccountBusiness_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Business"], "20")),
            Initialize<MyAccountViewModel>("MyAccountPreferences_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "3").Card(S["Preferences"], "30")));
    }

    public override async Task<IDisplayResult> UpdateAsync(MyAccountViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(
            model,
            Prefix,
            m => m.Kind,
            m => m.FirstName,
            m => m.LastName,
            m => m.DisplayName,
            m => m.Phone,
            m => m.CompanyName,
            m => m.TaxId,
            m => m.AcceptsMarketing);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the page validates and its script selects.
    protected override void BuildPrefix(MyAccountViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(MyAccountViewModel source, MyAccountViewModel model)
    {
        model.Email = source.Email;
        model.Kinds = source.Kinds;
        model.Addresses = source.Addresses;
        model.Kind = source.Kind;
        model.DisplayName = source.DisplayName;
        model.FirstName = source.FirstName;
        model.LastName = source.LastName;
        model.CompanyName = source.CompanyName;
        model.TaxId = source.TaxId;
        model.Phone = source.Phone;
        model.AcceptsMarketing = source.AcceptsMarketing;
    }
}
