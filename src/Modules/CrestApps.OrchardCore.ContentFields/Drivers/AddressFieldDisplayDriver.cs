using CrestApps.OrchardCore.Addresses.Core.Editing;
using CrestApps.OrchardCore.ContentFields.Fields;
using CrestApps.OrchardCore.ContentFields.Settings;
using CrestApps.OrchardCore.ContentFields.ViewModels;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.ContentFields.Drivers;

/// <summary>
/// Shows an <see cref="AddressField"/> the way its country writes addresses, and edits it with the shared address
/// editor, checked against the country's rules.
/// </summary>
public sealed class AddressFieldDisplayDriver : ContentFieldDisplayDriver<AddressField>
{
    private readonly IAddressEditorService _addressEditor;
    private readonly IShapeFactory _shapeFactory;

    public AddressFieldDisplayDriver(IAddressEditorService addressEditor, IShapeFactory shapeFactory)
    {
        _addressEditor = addressEditor;
        _shapeFactory = shapeFactory;
    }

    public override IDisplayResult Display(AddressField field, BuildFieldDisplayContext context)
        => Initialize<DisplayAddressFieldViewModel>(GetDisplayShapeType(context), model =>
        {
            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;
        }).Location("Detail", "Content")
        .Location("Summary", "Content");

    public override IDisplayResult Edit(AddressField field, BuildFieldEditorContext context)
        => BuildEditor(field, context, posted: null);

    public override async Task<IDisplayResult> UpdateAsync(AddressField field, UpdateFieldEditorContext context)
    {
        var settings = context.PartFieldDefinition.GetSettings<AddressFieldSettings>();
        var posted = new AddressEditorViewModel();

        await context.Updater.TryUpdateModelAsync(posted, Prefix);

        // An optional address left blank is no address.
        if (!settings.Required && !posted.HasAnyValue())
        {
            field.Address = null;

            return Edit(field, context);
        }

        var result = await _addressEditor.ValidateAsync(posted, context.Updater.ModelState, Prefix, OptionsFor(settings));

        if (!result.IsValid)
        {
            return BuildEditor(field, context, posted);
        }

        field.Address = result.Address;

        return Edit(field, context);
    }

    private ShapeResult BuildEditor(AddressField field, BuildFieldEditorContext context, AddressEditorViewModel posted)
        => Initialize<EditAddressFieldViewModel>(GetEditorShapeType(context), async model =>
        {
            var options = OptionsFor(context.PartFieldDefinition.GetSettings<AddressFieldSettings>());
            AddressEditorViewModel editor;

            if (posted is null)
            {
                editor = await _addressEditor.BuildAsync(field.Address, options);
            }
            else
            {
                editor = posted;

                await _addressEditor.PopulateAsync(editor, options);
            }

            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;
            model.Editor = await _shapeFactory.CreateAddressEditorAsync(editor, Prefix);
        });

    private static AddressEditorOptions OptionsFor(AddressFieldSettings settings)
        => new()
        {
            ShowName = settings.ShowName,
            ShowCompany = settings.ShowCompany,
            ShowPhone = settings.ShowPhone,
            Countries = settings.Countries is { Length: > 0 } ? settings.Countries : null,
            DefaultCountry = settings.DefaultCountry,
        };
}
