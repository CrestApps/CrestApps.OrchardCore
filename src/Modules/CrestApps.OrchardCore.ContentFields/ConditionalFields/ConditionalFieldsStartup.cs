using CrestApps.OrchardCore.ContentFields.Conditions;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.DisplayManagement.Descriptors;
using OrchardCore.Modules;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.ContentFields.ConditionalFields;

/// <summary>
/// Rules on content fields: a field shows only when other answers say so.
/// </summary>
[Feature(ContentFieldsConstants.Features.ConditionalFields)]
public sealed class ConditionalFieldsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ConditionalFieldsService>();
        services.AddScoped<ConditionalFieldsSnapshots>();

        // The driver runs before the field editors read the post; the handler, registered after Orchard Core's display
        // coordinator (this feature depends on Contents), runs after them.
        services.AddScoped<IContentDisplayDriver, ConditionalFieldsDisplayDriver>();
        services.AddScoped<IContentDisplayHandler, ConditionalFieldsDisplayHandler>();
        services.AddScoped<IShapePlacementProvider, ConditionalFieldsPlacementProvider>();
        services.AddScoped<IContentPartFieldDefinitionDisplayDriver, ConditionalFieldSettingsDisplayDriver>();
        services.AddTransient<IConfigureOptions<ResourceManagementOptions>, ConditionalFieldsResources>();

        foreach (var reader in BuiltInReaders())
        {
            services.AddSingleton<IContentFieldValueReader>(reader);
        }

        services.AddScoped<IContentFieldValueReader, TaxonomyFieldValueReader>();
    }

    // Readers for the fields whose value is one property of their stored JSON.
    private static IEnumerable<IContentFieldValueReader> BuiltInReaders()
    {
        yield return new JsonFieldValueReader("TextField", "Text", definition =>
            definition.Editor() == "PredefinedList" ? JsonFieldValueReader.SettingsOptions(definition, "TextFieldPredefinedListEditorSettings") : []);
        yield return new JsonFieldValueReader("BooleanField", "Value", _ => [new("true", "Yes"), new("false", "No")]);
        yield return new JsonFieldValueReader("NumericField", "Value");
        yield return new JsonFieldValueReader("DateField", "Value");
        yield return new JsonFieldValueReader("DateTimeField", "Value");
        yield return new JsonFieldValueReader("TimeField", "Value");
        yield return new JsonFieldValueReader("MultiTextField", "Values", definition => JsonFieldValueReader.SettingsOptions(definition, "MultiTextFieldSettings"));
        yield return new JsonFieldValueReader("ContentPickerField", "ContentItemIds");
        yield return new JsonFieldValueReader("UserPickerField", "UserIds");
        yield return new JsonFieldValueReader("LocalizationSetContentPickerField", "LocalizationSets");
        yield return new JsonFieldValueReader("LinkField", "Url");
        yield return new JsonFieldValueReader("HtmlField", "Html");
        yield return new JsonFieldValueReader("MarkdownField", "Markdown");
        yield return new JsonFieldValueReader("PhoneField", "PhoneNumber");
        yield return new JsonFieldValueReader("AddressField", "Address.CountryCode");
    }
}

/// <summary>
/// Describes the conditional field settings in the content definition recipe schemas, for each field type that can be
/// read or have a rule.
/// </summary>
[Feature(ContentFieldsConstants.Features.ConditionalFields)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class ConditionalFieldsRecipesSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        foreach (var fieldType in ConditionalFieldSettingsSchema.FieldTypes)
        {
            services.AddScoped<IContentSchemaDefinition>(_ => new ConditionalFieldSettingsSchema(fieldType));
        }
    }
}

internal sealed class ConditionalFieldsResources : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest = Build();

    public void Configure(ResourceManagementOptions options)
        => options.ResourceManifests.Add(_manifest);

    private static ResourceManifest Build()
    {
        var manifest = new ResourceManifest();

        manifest
            .DefineScript(ContentFieldsConstants.ConditionalFieldsScript)
            .SetUrl(
                "~/CrestApps.OrchardCore.ContentFields/scripts/conditional-fields.min.js",
                "~/CrestApps.OrchardCore.ContentFields/scripts/conditional-fields.js")
            .SetVersion("1.0.0");

        return manifest;
    }
}
