using CrestApps.OrchardCore.Reports.Contents.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Reports.Contents.Providers;

/// <summary>
/// The display text of the content items a content picker field points at. The display texts of every picked item
/// of a query are loaded in a few batched index queries, only when a report uses the field. Only published items of a
/// content type the user may view are shown.
/// </summary>
public sealed class ContentPickerDisplayTextReportField : ContentReportField
{
    /// <summary>
    /// The JSON property of a content picker field that holds the picked content item IDs.
    /// </summary>
    public const string ContentItemIdsProperty = "ContentItemIds";

    /// <summary>
    /// The most content item IDs one lookup query passes to the database.
    /// </summary>
    public const int BatchSize = 500;

    private const string DisplayTextsKey = "CrestApps.Reports.Contents.PickedDisplayTexts";

    private readonly string _partName;
    private readonly string _fieldName;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentPickerDisplayTextReportField"/> class.
    /// </summary>
    /// <param name="descriptor">The descriptor of the report field.</param>
    /// <param name="partName">The name of the part in the content type.</param>
    /// <param name="fieldName">The name of the content picker field in the part.</param>
    public ContentPickerDisplayTextReportField(
        ReportFieldDescriptor descriptor,
        string partName,
        string fieldName)
        : base(descriptor)
    {
        _partName = partName;
        _fieldName = fieldName;
    }

    /// <inheritdoc/>
    public override async Task PrepareAsync(ContentReportQueryContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var displayTexts = GetDisplayTexts(context);

        if (context.Session is null)
        {
            return;
        }

        var missing = context.ContentItems
            .SelectMany(GetPickedIds)
            .Where(id => !displayTexts.ContainsKey(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var id in missing)
        {
            displayTexts[id] = null;
        }

        foreach (var batch in missing.Chunk(BatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rows = await context.Session
                .QueryIndex<ContentItemIndex>(index => index.ContentItemId.IsIn(batch) && index.Published)
                .ListAsync(cancellationToken);

            foreach (var row in rows)
            {
                if (await context.CanViewContentTypeAsync(row.ContentType))
                {
                    displayTexts[row.ContentItemId] = row.DisplayText;
                }
            }
        }
    }

    /// <inheritdoc/>
    public override object GetValue(ContentItem contentItem, ContentReportQueryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var displayTexts = GetDisplayTexts(context);

        return ContentReportJson.Join(GetPickedIds(contentItem)
            .Select(id => displayTexts.GetValueOrDefault(id)));
    }

    private IEnumerable<string> GetPickedIds(ContentItem contentItem)
    {
        var field = ContentReportJson.GetElement(contentItem, _partName, _fieldName);

        if (field is null || !field.TryGetPropertyValue(ContentItemIdsProperty, out var ids))
        {
            return [];
        }

        return ContentReportJson.ReadStrings(ids);
    }

    private static Dictionary<string, string> GetDisplayTexts(ContentReportQueryContext context)
    {
        if (context.Properties.TryGetValue(DisplayTextsKey, out var value) && value is Dictionary<string, string> displayTexts)
        {
            return displayTexts;
        }

        displayTexts = new Dictionary<string, string>(StringComparer.Ordinal);
        context.Properties[DisplayTextsKey] = displayTexts;

        return displayTexts;
    }
}
