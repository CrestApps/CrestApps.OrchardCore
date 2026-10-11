using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Reports.DataSources;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Reports.Contents.Models;

/// <summary>
/// A report field read from a path within a part of a content item, such as <c>AutoroutePart.RouteContainedItems</c>.
/// A list of plain values is read as the values joined with commas.
/// </summary>
public sealed class ContentPathReportField : ContentReportField
{
    private readonly string _partName;
    private readonly string[] _path;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentPathReportField"/> class.
    /// </summary>
    /// <param name="descriptor">The field as reports see it.</param>
    /// <param name="partName">The name of the part in the content item.</param>
    /// <param name="path">The path of the value within the part, with dots between nested names.</param>
    public ContentPathReportField(ReportFieldDescriptor descriptor, string partName, string path)
        : base(descriptor)
    {
        ArgumentException.ThrowIfNullOrEmpty(partName);
        ArgumentException.ThrowIfNullOrEmpty(path);

        _partName = partName;
        _path = path.Split('.');
    }

    /// <inheritdoc/>
    public override object GetValue(ContentItem contentItem, ContentReportQueryContext context)
    {
        JsonNode node = ContentReportJson.GetElement(contentItem, _partName);

        foreach (var name in _path)
        {
            if (node is not JsonObject element || !element.TryGetPropertyValue(name, out node) || node is null)
            {
                return null;
            }
        }

        return node switch
        {
            JsonArray array => ContentReportJson.Join(ContentReportJson.ReadStrings(array)),
            JsonValue => ContentReportJson.ReadValue(node),
            _ => null,
        };
    }
}
