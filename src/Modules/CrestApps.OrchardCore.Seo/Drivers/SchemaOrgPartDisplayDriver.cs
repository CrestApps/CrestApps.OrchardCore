using CrestApps.OrchardCore.Seo.Models;
using CrestApps.OrchardCore.Seo.SchemaOrg;
using CrestApps.OrchardCore.Seo.ViewModels;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Seo.Drivers;

/// <summary>
/// The Schema.org part on a content item: the values its content type leaves to editors, a switch to publish no
/// structured data about the item, and a preview of the JSON-LD its page publishes.
/// </summary>
internal sealed class SchemaOrgPartDisplayDriver : ContentPartDisplayDriver<SchemaOrgPart>
{
    private readonly IStructuredDataManager _structuredDataManager;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaOrgPartDisplayDriver"/> class.
    /// </summary>
    /// <param name="structuredDataManager">The structured data manager, which builds the preview.</param>
    /// <param name="logger">The logger.</param>
    public SchemaOrgPartDisplayDriver(
        IStructuredDataManager structuredDataManager,
        ILogger<SchemaOrgPartDisplayDriver> logger)
    {
        _structuredDataManager = structuredDataManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(SchemaOrgPart part, BuildPartEditorContext context)
    {
        return Initialize<SchemaOrgPartViewModel>(GetEditorShapeType(context), async model =>
        {
            var settings = context.TypePartDefinition.GetSettings<SchemaOrgPartSettings>();

            model.Disabled = part.Disabled;
            model.SchemaType = settings.SchemaType;
            model.Values = new Dictionary<string, string>(part.Values ?? [], StringComparer.Ordinal);
            model.Fields = BuildFields(settings);
            model.Preview = await BuildPreviewAsync(part);
        }).Location("Parts#Schema.org:20");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(SchemaOrgPart part, UpdatePartEditorContext context)
    {
        var model = new SchemaOrgPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var settings = context.TypePartDefinition.GetSettings<SchemaOrgPartSettings>();
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        // Only the properties the content type leaves to editors are kept, so a value cannot be set any other way.
        foreach (var field in BuildFields(settings))
        {
            if (model.Values is not null &&
                model.Values.TryGetValue(field.Name, out var value) &&
                !string.IsNullOrWhiteSpace(value))
            {
                values[field.Name] = value.Trim();
            }
        }

        part.Disabled = model.Disabled;
        part.Values = values;

        return Edit(part, context);
    }

    /// <summary>
    /// Lists the properties whose values editors enter, with the label, hint and input each needs.
    /// </summary>
    /// <param name="settings">The part's settings on the content type.</param>
    /// <returns>The fields.</returns>
    internal static List<SchemaOrgEditorField> BuildFields(SchemaOrgPartSettings settings)
    {
        var fields = new List<SchemaOrgEditorField>();

        foreach (var mapping in settings.Properties ?? [])
        {
            if (mapping.Source != SchemaOrgValueSource.Editor || !SchemaOrgValueWriter.IsValidPropertyName(mapping.Name))
            {
                continue;
            }

            var property = SchemaOrgVocabulary.GetProperty(settings.SchemaType, mapping.Name);

            fields.Add(new SchemaOrgEditorField
            {
                Name = mapping.Name,
                Label = Humanize(mapping.Name),
                Hint = property?.Description,
                Kind = property?.Kind ?? SchemaOrgValueKind.Text,
                Level = property?.Level ?? SchemaOrgPropertyLevel.Optional,
                Options = property?.Options ?? [],
            });
        }

        return fields;
    }

    /// <summary>
    /// Turns a property name into a label: <c>startDate</c> becomes "Start date" and <c>offers.price</c> becomes
    /// "Offers › Price".
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <returns>The label.</returns>
    internal static string Humanize(string name)
    {
        var segments = (name ?? string.Empty).Split('.', StringSplitOptions.RemoveEmptyEntries);

        return string.Join(" › ", segments.Select(segment =>
        {
            var builder = new System.Text.StringBuilder(segment.Length + 4);

            for (var i = 0; i < segment.Length; i++)
            {
                var character = segment[i];

                if (i > 0 && char.IsUpper(character) && !char.IsUpper(segment[i - 1]))
                {
                    builder.Append(' ');
                }

                builder.Append(builder.Length == 0 ? char.ToUpperInvariant(character) : char.ToLowerInvariant(character));
            }

            return builder.ToString();
        }));
    }

    private async Task<string> BuildPreviewAsync(SchemaOrgPart part)
    {
        if (part.ContentItem is null || string.IsNullOrEmpty(part.ContentItem.ContentItemId) || part.ContentItem.Id == 0)
        {
            return null;
        }

        try
        {
            var document = await _structuredDataManager.BuildPageDocumentAsync(part.ContentItem);

            return JsonLd.ToScriptJson(document, indented: true);
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            _logger.LogWarning(ex, "The structured data preview of the content item '{ContentItemId}' could not be built.", part.ContentItem.ContentItemId);

            return null;
        }
    }
}
