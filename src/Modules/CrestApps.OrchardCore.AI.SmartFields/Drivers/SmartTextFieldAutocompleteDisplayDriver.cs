using CrestApps.OrchardCore.AI.SmartFields.Settings;
using CrestApps.OrchardCore.AI.SmartFields.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentFields.Settings;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.SmartFields.Drivers;

public sealed class SmartTextFieldAutocompleteDisplayDriver : ContentFieldDisplayDriver<TextField>
{
    internal readonly IStringLocalizer S;

    public SmartTextFieldAutocompleteDisplayDriver(
        IStringLocalizer<SmartTextFieldAutocompleteDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(TextField field, BuildFieldEditorContext context)
    {
        if (!IsAutocompleteEditor(context.PartFieldDefinition))
        {
            return null;
        }

        return Initialize<EditSmartTextFieldViewModel>(GetEditorShapeType(context), model =>
        {
            model.Text = field.Text;

            var settings = context.PartFieldDefinition.GetSettings<SmartTextFieldAutocompleteSettings>();

            model.ProfileId = settings.ProfileId;
            model.Hint = settings.Hint;
            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(TextField field, UpdateFieldEditorContext context)
    {
        if (!IsAutocompleteEditor(context.PartFieldDefinition))
        {
            return null;
        }

        var model = new EditSmartTextFieldViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix, f => f.Text);

        var settings = context.PartFieldDefinition.GetSettings<TextFieldSettings>();

        field.Text = model.Text;

        if (settings.Required && string.IsNullOrEmpty(field.Text))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(field.Text), S["The {0} field is required.", context.PartFieldDefinition.DisplayName()]);
        }

        return Edit(field, context);
    }

    private static bool IsAutocompleteEditor(ContentPartFieldDefinition partFieldDefinition)
    {
        return partFieldDefinition.Editor() == SmartTextFieldAutocompleteSettingsDisplayDriver.EditorName;
    }
}
