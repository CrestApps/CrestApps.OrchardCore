using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Users.Cohorts;

/// <summary>
/// Builds the editor of a cohort in cards.
/// </summary>
internal sealed class CohortEditDisplayDriver : DisplayDriver<CohortEditViewModel>
{
    internal readonly IStringLocalizer S;

    public CohortEditDisplayDriver(IStringLocalizer<CohortEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(CohortEditViewModel model, BuildEditorContext context)
    {
        return Initialize<CohortEditViewModel>("CohortFields_Edit", m =>
        {
            m.CohortId = model.CohortId;
            m.Name = model.Name;
            m.Description = model.Description;
        }).Location(l => l.Zone("Content", "1").Card(S["General"], "10"));
    }

    public override async Task<IDisplayResult> UpdateAsync(CohortEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Name, m => m.Description);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates.
    protected override void BuildPrefix(CohortEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }
}