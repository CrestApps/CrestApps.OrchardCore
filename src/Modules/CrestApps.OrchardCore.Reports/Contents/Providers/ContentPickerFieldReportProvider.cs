using CrestApps.OrchardCore.Reports.Contents.Models;
using CrestApps.OrchardCore.Reports.Contents.Services;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Reports.Contents.Providers;

/// <summary>
/// Describes a <c>ContentPickerField</c>: <c>{PartName}.{FieldName}</c> is the first picked content item ID (an
/// identifier, so an order can be joined to its customer on it), <c>{PartName}.{FieldName}.ContentItemIds</c> is every
/// picked ID joined with commas, and <c>{PartName}.{FieldName}.DisplayText</c> is the display text of every picked
/// item the user may view, joined with commas.
/// </summary>
public sealed class ContentPickerFieldReportProvider : IContentReportFieldProvider
{
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentPickerFieldReportProvider"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContentPickerFieldReportProvider(IStringLocalizer<ContentPickerFieldReportProvider> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string FieldType => "ContentPickerField";

    /// <inheritdoc/>
    public IEnumerable<ContentReportField> GetFields(ContentReportFieldContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var first = context.CreateElementField(null, context.DisplayName, ReportDataType.Text, ContentReportValueMode.First, ContentPickerDisplayTextReportField.ContentItemIdsProperty);

        first.Descriptor.IsIdentifier = true;

        foreach (var contentType in ContentReportJson.ReadSetting(context.PartFieldDefinition?.Settings, "ContentPickerFieldSettings", "DisplayedContentTypes"))
        {
            first.Descriptor.References.Add(new ReportFieldReference(ReportsConstants.ContentsDataSource, contentType, ContentReportFieldNames.ContentItemId));
        }

        return
        [
            first,
            context.CreateElementField(
                ContentPickerDisplayTextReportField.ContentItemIdsProperty,
                S["{0} (all IDs)", context.DisplayName],
                ReportDataType.Text,
                ContentReportValueMode.Join,
                ContentPickerDisplayTextReportField.ContentItemIdsProperty),
            new ContentPickerDisplayTextReportField(
                context.CreateDescriptor("DisplayText", S["{0} (display text)", context.DisplayName], ReportDataType.Text),
                context.PartName,
                context.FieldName),
        ];
    }
}
