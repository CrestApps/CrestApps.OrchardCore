using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

/// <summary>
/// Builds the editor of the lead conversion page: one shape per card, so the choices are grouped by placement. The
/// fields keep the names the page posts, so the controller and the scripts of the page keep working.
/// </summary>
internal sealed class ConvertLeadDisplayDriver : DisplayDriver<ConvertLeadViewModel>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConvertLeadDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ConvertLeadDisplayDriver(IStringLocalizer<ConvertLeadDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(ConvertLeadViewModel model, BuildEditorContext context)
    {
        var results = new List<IDisplayResult>
        {
            Initialize<ConvertLeadViewModel>("ConvertLeadContact_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Contact"], "10")),
        };

        if (model.HasAccountTypes)
        {
            results.Add(Initialize<ConvertLeadViewModel>("ConvertLeadAccount_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Account"], "20")));
        }

        if (model.OpportunityContentTypes.Count > 0)
        {
            results.Add(Initialize<ConvertLeadViewModel>("ConvertLeadOpportunity_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "3").Card(S["Opportunity"], "30")));
        }

        results.Add(Initialize<ConvertLeadViewModel>("ConvertLeadActivities_Edit", m => Populate(model, m))
            .Location(l => l.Zone("Content", "4").Card(S["Activities"], "40")));

        return Combine(results);
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ConvertLeadViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(
            model,
            Prefix,
            m => m.ExistingContactItemId,
            m => m.ContactContentType,
            m => m.AccountMode,
            m => m.AccountName,
            m => m.ExistingAccountItemId,
            m => m.CreateOpportunity,
            m => m.OpportunityContentType,
            m => m.OpportunityName,
            m => m.OpportunityAmount,
            m => m.OpportunityCloseDate,
            m => m.OpenActivities);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller reads and the scripts of the page select.
    /// <inheritdoc/>
    protected override void BuildPrefix(ConvertLeadViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(ConvertLeadViewModel source, ConvertLeadViewModel model)
    {
        model.ExistingContactItemId = source.ExistingContactItemId;
        model.ContactContentType = source.ContactContentType;
        model.AccountMode = source.AccountMode;
        model.AccountName = source.AccountName;
        model.ExistingAccountItemId = source.ExistingAccountItemId;
        model.CreateOpportunity = source.CreateOpportunity;
        model.OpportunityContentType = source.OpportunityContentType;
        model.OpportunityName = source.OpportunityName;
        model.OpportunityAmount = source.OpportunityAmount;
        model.OpportunityCloseDate = source.OpportunityCloseDate;
        model.OpenActivities = source.OpenActivities;
        model.Lead = source.Lead;
        model.Company = source.Company;
        model.MatchingContacts = source.MatchingContacts;
        model.MatchingAccounts = source.MatchingAccounts;
        model.ContactContentTypes = source.ContactContentTypes;
        model.OpportunityContentTypes = source.OpportunityContentTypes;
        model.HasAccountTypes = source.HasAccountTypes;
        model.OpenActivityCount = source.OpenActivityCount;
        model.FinishedActivityCount = source.FinishedActivityCount;
    }
}
