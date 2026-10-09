using CrestApps.OrchardCore.Reports.Contents.Models;
using CrestApps.OrchardCore.Reports.Contents.Providers;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Metadata.Settings;

namespace CrestApps.OrchardCore.Reports.Contents.Services;

/// <summary>
/// Builds the report fields of a content type: the content item metadata first, then, part by part, the fields of
/// every <see cref="IContentReportPartProvider"/> and of the <see cref="IContentReportFieldProvider"/> of each content
/// field. When two fields get the same name, the first one wins.
/// </summary>
public sealed class ContentReportSchemaBuilder
{
    /// <summary>
    /// The field type name of the provider used when no provider handles a content field type.
    /// </summary>
    public const string FallbackFieldType = "*";

    private static readonly PropertyContentReportFieldProvider _fallbackProvider = new(
        FallbackFieldType,
        ReportDataType.Text,
        ContentReportValueMode.Single,
        "Text",
        "Value");

    private readonly Dictionary<string, IContentReportFieldProvider> _fieldProviders = new(StringComparer.Ordinal);
    private readonly IEnumerable<IContentReportPartProvider> _partProviders;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentReportSchemaBuilder"/> class.
    /// </summary>
    /// <param name="fieldProviders">The content field providers. The last one registered for a field type wins.</param>
    /// <param name="partProviders">The content part providers.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContentReportSchemaBuilder(
        IEnumerable<IContentReportFieldProvider> fieldProviders,
        IEnumerable<IContentReportPartProvider> partProviders,
        IStringLocalizer<ContentReportSchemaBuilder> stringLocalizer)
    {
        foreach (var provider in fieldProviders)
        {
            if (!string.IsNullOrEmpty(provider.FieldType))
            {
                _fieldProviders[provider.FieldType] = provider;
            }
        }

        _partProviders = partProviders;
        S = stringLocalizer;
    }

    /// <summary>
    /// Builds the report fields of a content type.
    /// </summary>
    /// <param name="definition">The content type definition.</param>
    /// <returns>The report fields, in the order the designer lists them.</returns>
    public IReadOnlyList<ContentReportField> Build(ContentTypeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var fields = new List<ContentReportField>();
        var names = new HashSet<string>(StringComparer.Ordinal);

        void Add(IEnumerable<ContentReportField> candidates)
        {
            foreach (var field in candidates ?? [])
            {
                if (field?.Descriptor is not null &&
                    !string.IsNullOrEmpty(field.Descriptor.Name) &&
                    names.Add(field.Descriptor.Name))
                {
                    fields.Add(field);
                }
            }
        }

        Add(GetMetadataFields());

        foreach (var typePart in definition.Parts ?? [])
        {
            if (typePart?.PartDefinition is null || string.IsNullOrEmpty(typePart.Name))
            {
                continue;
            }

            var partDisplayName = typePart.DisplayName();
            var partContext = new ContentReportFieldContext
            {
                ContentTypeDefinition = definition,
                TypePartDefinition = typePart,
                Name = typePart.Name,
                DisplayName = partDisplayName,
                Group = partDisplayName,
            };

            foreach (var partProvider in _partProviders)
            {
                Add(partProvider.GetFields(partContext));
            }

            foreach (var partField in typePart.PartDefinition.Fields ?? [])
            {
                if (partField is null || string.IsNullOrEmpty(partField.Name))
                {
                    continue;
                }

                var fieldContext = new ContentReportFieldContext
                {
                    ContentTypeDefinition = definition,
                    TypePartDefinition = typePart,
                    PartFieldDefinition = partField,
                    Name = $"{typePart.Name}.{partField.Name}",
                    DisplayName = partField.DisplayName(),
                    Group = partDisplayName,
                };

                Add(GetFieldProvider(partField.FieldDefinition?.Name).GetFields(fieldContext));
            }
        }

        return fields;
    }

    /// <summary>
    /// Gets the provider of a content field type.
    /// </summary>
    /// <param name="fieldType">The technical name of the content field type.</param>
    /// <returns>The provider registered last for the type, or the generic text provider.</returns>
    public IContentReportFieldProvider GetFieldProvider(string fieldType)
    {
        if (!string.IsNullOrEmpty(fieldType) && _fieldProviders.TryGetValue(fieldType, out var provider))
        {
            return provider;
        }

        return _fallbackProvider;
    }

    private static ReportFieldDescriptor Owner(ReportFieldDescriptor descriptor)
    {
        descriptor.References.Add(new ReportFieldReference(ReportsConstants.UsersDataSource, ReportsConstants.UsersDataSet, ReportsConstants.UserIdField));

        return descriptor;
    }

    private IEnumerable<ContentReportField> GetMetadataFields()
    {
        var group = S["Content item"].Value;

        ReportFieldDescriptor Descriptor(string name, string displayName, ReportDataType dataType, bool isIdentifier = false, string description = null)
        {
            return new ReportFieldDescriptor(name, displayName, dataType, group)
            {
                IsIdentifier = isIdentifier,
                Description = description,
            };
        }

        return
        [
            new ContentItemReportField(Descriptor(ContentReportFieldNames.ContentItemId, S["Content item ID"], ReportDataType.Text, isIdentifier: true), item => item.ContentItemId),
            new ContentItemReportField(Descriptor(ContentReportFieldNames.ContentItemVersionId, S["Content item version ID"], ReportDataType.Text), item => item.ContentItemVersionId),
            new ContentItemReportField(Descriptor(ContentReportFieldNames.DisplayText, S["Display text"], ReportDataType.Text), item => item.DisplayText),
            new ContentItemReportField(Descriptor(ContentReportFieldNames.ContentType, S["Content type"], ReportDataType.Text), item => item.ContentType),
            new ContentItemReportField(Owner(Descriptor(ContentReportFieldNames.Owner, S["Owner"], ReportDataType.Text, isIdentifier: true, S["The ID of the user who owns the content item."])), item => item.Owner),
            new ContentItemReportField(Descriptor(ContentReportFieldNames.Author, S["Author"], ReportDataType.Text, description: S["The user name of the last person who edited the content item."]), item => item.Author),
            new ContentItemReportField(Descriptor(ContentReportFieldNames.CreatedUtc, S["Created"], ReportDataType.DateTime), item => item.CreatedUtc),
            new ContentItemReportField(Descriptor(ContentReportFieldNames.ModifiedUtc, S["Modified"], ReportDataType.DateTime), item => item.ModifiedUtc),
            new ContentItemReportField(Descriptor(ContentReportFieldNames.PublishedUtc, S["Published on"], ReportDataType.DateTime), item => item.PublishedUtc),
            new ContentItemReportField(Descriptor(ContentReportFieldNames.Published, S["Published"], ReportDataType.Boolean), item => item.Published),
        ];
    }
}
