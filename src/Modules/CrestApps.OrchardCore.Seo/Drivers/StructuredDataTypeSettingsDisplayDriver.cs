using CrestApps.OrchardCore.Seo.Models;
using CrestApps.OrchardCore.Seo.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Seo.Drivers;

/// <summary>
/// The structured data settings of a content type: its schema.org type and its Liquid property map.
/// </summary>
internal sealed class StructuredDataTypeSettingsDisplayDriver : ContentTypeDefinitionDisplayDriver
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="StructuredDataTypeSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public StructuredDataTypeSettingsDisplayDriver(IStringLocalizer<StructuredDataTypeSettingsDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <summary>
    /// Gives the fields their own prefix: every content type settings driver gets <c>ContentTypeDefinition</c> by
    /// default, so generic names such as <c>Disabled</c> would bind another driver's fields too.
    /// </summary>
    /// <param name="model">The content type definition.</param>
    /// <param name="htmlFieldPrefix">The prefix of the surrounding editor.</param>
    protected override void BuildPrefix(ContentTypeDefinition model, string htmlFieldPrefix)
    {
        Prefix = string.IsNullOrEmpty(htmlFieldPrefix)
            ? nameof(StructuredDataTypeSettings)
            : $"{htmlFieldPrefix}.{nameof(StructuredDataTypeSettings)}";
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(ContentTypeDefinition contentTypeDefinition, BuildEditorContext context)
    {
        return Initialize<StructuredDataTypeSettingsViewModel>("StructuredDataTypeSettings_Edit", model =>
        {
            var settings = contentTypeDefinition.GetSettings<StructuredDataTypeSettings>();

            model.Disabled = settings.Disabled;
            model.SchemaType = settings.SchemaType;
            model.Properties = settings.Properties;
        }).Location("Content:6");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ContentTypeDefinition contentTypeDefinition, UpdateTypeEditorContext context)
    {
        var model = new StructuredDataTypeSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var schemaType = model.SchemaType?.Trim();

        if (!string.IsNullOrEmpty(schemaType) && !IsValidTypeName(schemaType))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SchemaType), S["The schema.org type must be a single type name, such as Article or Event."]);
        }

        context.Builder.WithSettings(new StructuredDataTypeSettings
        {
            Disabled = model.Disabled,
            SchemaType = schemaType,
            Properties = model.Properties?.Trim(),
        });

        return Edit(contentTypeDefinition, context);
    }

    /// <summary>
    /// Checks that a schema.org type name is one word of letters and digits that starts with a letter.
    /// </summary>
    /// <param name="name">The type name.</param>
    /// <returns><see langword="true"/> when the name is valid.</returns>
    internal static bool IsValidTypeName(string name)
    {
        if (string.IsNullOrEmpty(name) || !char.IsLetter(name[0]))
        {
            return false;
        }

        foreach (var character in name)
        {
            if (!char.IsLetterOrDigit(character))
            {
                return false;
            }
        }

        return true;
    }
}
