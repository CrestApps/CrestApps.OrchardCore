using CrestApps.OrchardCore.Learning.Programs.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Programs.Drivers;

/// <summary>
/// The editor of the form that exempts a person from required learning: the reason, and the evidence file that is
/// posted next to the editor under the name <c>evidence</c>.
/// </summary>
internal sealed class RequiredLearningExemptDisplayDriver : DisplayDriver<RequiredLearningExemptViewModel>
{
    internal readonly IStringLocalizer S;

    public RequiredLearningExemptDisplayDriver(IStringLocalizer<RequiredLearningExemptDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(RequiredLearningExemptViewModel model, BuildEditorContext context)
    {
        return Initialize<RequiredLearningExemptViewModel>("RequiredLearningExempt_Edit", m => m.Reason = model.Reason)
            .Location(l => l.Zone("Content", "1").Card(S["Exemption"], "10"));
    }

    public override async Task<IDisplayResult> UpdateAsync(RequiredLearningExemptViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Reason);

        return Edit(model, context);
    }

    // The field keeps the name the page posts, which the controller validates.
    protected override void BuildPrefix(RequiredLearningExemptViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }
}
