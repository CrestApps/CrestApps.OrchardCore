using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.SmartFields.Settings;
using CrestApps.OrchardCore.AI.SmartFields.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.SmartFields.Drivers;

public sealed class SmartTextFieldAutocompleteSettingsDisplayDriver : ContentPartFieldDefinitionDisplayDriver<TextField>
{
    public const string EditorName = "AIAutocomplete";

    private readonly IAIProfileManager _profileManager;

    internal readonly IStringLocalizer S;

    public SmartTextFieldAutocompleteSettingsDisplayDriver(
        IAIProfileManager profileManager,
        IStringLocalizer<SmartTextFieldAutocompleteSettingsDisplayDriver> stringLocalizer)
    {
        _profileManager = profileManager;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ContentPartFieldDefinition partFieldDefinition, BuildEditorContext context)
    {
        if (!IsAutocompleteEditor(partFieldDefinition))
        {
            return null;
        }

        return Initialize<SmartTextFieldAutocompleteSettingsViewModel>("SmartTextFieldAutocompleteSettings_Edit", async model =>
        {
            var settings = partFieldDefinition.GetSettings<SmartTextFieldAutocompleteSettings>();

            model.ProfileId = settings.ProfileId;
            model.Hint = settings.Hint;

            var profiles = await _profileManager.GetAsync(AIProfileType.Utility);

            model.Profiles = profiles
                .Select(p => new SelectListItem(p.DisplayText, p.ItemId))
                .ToArray();
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentPartFieldDefinition partFieldDefinition, UpdatePartFieldEditorContext context)
    {
        if (!IsAutocompleteEditor(partFieldDefinition))
        {
            return null;
        }

        var model = new SmartTextFieldAutocompleteSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrEmpty(model.ProfileId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ProfileId), S["AI Profile is required."]);
        }

        context.Builder.WithSettings(new SmartTextFieldAutocompleteSettings
        {
            ProfileId = model.ProfileId,
            Hint = model.Hint,
        });

        return Edit(partFieldDefinition, context);
    }

    private static bool IsAutocompleteEditor(ContentPartFieldDefinition partFieldDefinition)
    {
        return partFieldDefinition.Editor() == EditorName;
    }
}
