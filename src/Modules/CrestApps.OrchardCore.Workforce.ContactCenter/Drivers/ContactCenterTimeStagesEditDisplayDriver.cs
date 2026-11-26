using CrestApps.OrchardCore.Workforce.ContactCenter.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Drivers;

/// <summary>
/// Builds the editor of the stages of agents' states and reasons, whose rows are grouped in cards by their placement.
/// </summary>
internal sealed class ContactCenterTimeStagesEditDisplayDriver : DisplayDriver<ContactCenterTimeStagesViewModel>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterTimeStagesEditDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterTimeStagesEditDisplayDriver(IStringLocalizer<ContactCenterTimeStagesEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(ContactCenterTimeStagesViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<ContactCenterTimeStagesViewModel>("ContactCenterTimeStagesStates_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Agent States"], "10")),
            Initialize<ContactCenterTimeStagesViewModel>("ContactCenterTimeStagesReasons_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Reasons"], "20")));
    }

    /// <inheritdoc />
    public override Task<IDisplayResult> UpdateAsync(ContactCenterTimeStagesViewModel model, UpdateEditorContext context)
    {
        // The rows are posted as States[i].Key, States[i].StageId...: the page action has bound them already, and
        // binding them again would drop the labels and hints, which are not posted.
        return Task.FromResult(Edit(model, context));
    }

    // The fields keep the names the page posts.
    protected override void BuildPrefix(ContactCenterTimeStagesViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(ContactCenterTimeStagesViewModel source, ContactCenterTimeStagesViewModel model)
    {
        model.States = source.States;
        model.Reasons = source.Reasons;
        model.StageOptions = source.StageOptions;
    }
}
