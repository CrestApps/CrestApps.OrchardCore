using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Users.Audiences;

/// <summary>
/// Builds the editor of an audience in cards: its name and description, then who is in it and the rules.
/// </summary>
internal sealed class UserAudienceEditDisplayDriver : DisplayDriver<UserAudienceEditViewModel>
{
    internal readonly IStringLocalizer S;

    public UserAudienceEditDisplayDriver(IStringLocalizer<UserAudienceEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(UserAudienceEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<UserAudienceEditViewModel>("UserAudienceGeneral_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["General"], "10")),
            Initialize<UserAudienceEditViewModel>("UserAudienceRules_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Membership"], "20")));
    }

    public override async Task<IDisplayResult> UpdateAsync(UserAudienceEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.IsNew, m => m.Name, m => m.DisplayName, m => m.Description, m => m.Join, m => m.Rules);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the script of the page renumbers.
    protected override void BuildPrefix(UserAudienceEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(UserAudienceEditViewModel source, UserAudienceEditViewModel model)
    {
        model.IsNew = source.IsNew;
        model.Name = source.Name;
        model.DisplayName = source.DisplayName;
        model.Description = source.Description;
        model.Join = source.Join;
        model.Rules = source.Rules;
        model.Sources = source.Sources;
    }
}