using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Reports.Contents.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Queries;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.ContentManagement.Records;
using YesSql;

namespace CrestApps.OrchardCore.Reports.Contents.Services;

/// <summary>
/// Finds every other property the parts of a content type store, so anything on a content type can be a report
/// column, including the properties of parts written in code that no <see cref="IContentReportPartProvider"/>
/// describes. The properties come from the <see cref="IContentReportPropertySource"/> services, typed, and from the
/// newest published content items of the type, typed by their values. Parts a content item stores without the type
/// listing them are included too. The content fields of a part, the fields that are already described, nested lists
/// of objects, and properties whose path names a secret are left out.
/// </summary>
public sealed class ContentReportPropertyDiscovery
{
    /// <summary>
    /// The number of content items read to find the properties of a type.
    /// </summary>
    public const int SampleSize = 50;

    /// <summary>
    /// The largest number of properties discovered for one content type.
    /// </summary>
    public const int MaxProperties = 300;

    private readonly ISession _session;
    private readonly IEnumerable<IContentReportPropertySource> _sources;
    private readonly IStringLocalizer S;
    private readonly Dictionary<string, IReadOnlyList<ContentReportField>> _cache = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentReportPropertyDiscovery"/> class.
    /// </summary>
    /// <param name="session">The session content items are read from.</param>
    /// <param name="sources">The services that describe the properties of parts.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContentReportPropertyDiscovery(
        ISession session,
        IEnumerable<IContentReportPropertySource> sources,
        IStringLocalizer<ContentReportPropertyDiscovery> stringLocalizer)
    {
        _session = session;
        _sources = sources;
        S = stringLocalizer;
    }

    /// <summary>
    /// Discovers the properties of a content type that the given fields do not describe yet.
    /// </summary>
    /// <param name="definition">The content type.</param>
    /// <param name="known">The fields already described, from the type definition and the providers.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The other properties, as report fields grouped under their part.</returns>
    public async Task<IReadOnlyList<ContentReportField>> DiscoverAsync(
        ContentTypeDefinition definition,
        IReadOnlyCollection<ContentReportField> known,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        // A request builds the fields of a type more than once (its schema, then its rows): read the items once.
        if (_cache.TryGetValue(definition.Name, out var cached))
        {
            return cached;
        }

        var knownNames = new HashSet<string>((known ?? []).Select(field => field.Descriptor.Name), StringComparer.Ordinal);
        var items = await SampleAsync(definition.Name, cancellationToken);
        var fields = new List<ContentReportField>();
        var parts = new List<(string Name, string DisplayName, ContentTypePartDefinition TypePart, HashSet<string> FieldNames)>();

        foreach (var typePart in definition.Parts ?? [])
        {
            if (typePart?.PartDefinition is null || string.IsNullOrEmpty(typePart.Name))
            {
                continue;
            }

            var fieldNames = new HashSet<string>(
                (typePart.PartDefinition.Fields ?? []).Where(field => field is not null).Select(field => field.Name),
                StringComparer.Ordinal);

            parts.Add((typePart.Name, typePart.DisplayName(), typePart, fieldNames));
        }

        // Parts stored by code without the type listing them, such as a part welded to the items of the type.
        foreach (var name in items
            .SelectMany(item => Root(item)?.Where(pair => pair.Value is JsonObject).Select(pair => pair.Key) ?? [])
            .Distinct(StringComparer.Ordinal)
            .Where(name => !parts.Any(part => string.Equals(part.Name, name, StringComparison.Ordinal))))
        {
            parts.Add((name, Humanize(name), null, []));
        }

        foreach (var (name, displayName, typePart, fieldNames) in parts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var properties = new Dictionary<string, ReportDataType>(StringComparer.Ordinal);
            var order = new List<string>();

            void Add(string path, ReportDataType dataType)
            {
                if (string.IsNullOrEmpty(path) ||
                    properties.ContainsKey(path) ||
                    fieldNames.Contains(path.Split('.')[0]) ||
                    ReportSecretNames.IsSecret(path) ||
                    knownNames.Contains(name + "." + path))
                {
                    return;
                }

                properties[path] = dataType;
                order.Add(path);
            }

            if (typePart is not null)
            {
                foreach (var source in _sources)
                {
                    foreach (var property in await source.GetPropertiesAsync(typePart, cancellationToken) ?? [])
                    {
                        Add(property?.Path, property?.DataType ?? ReportDataType.Text);
                    }
                }
            }

            var rows = items
                .Select(item => ContentReportJson.GetElement(item, name))
                .Where(part => part is not null)
                .Select(part => QueryResultSchema.Flatten(part))
                .ToList();
            var valued = new HashSet<string>(
                rows.SelectMany(row => row).Where(pair => pair.Value is not null).Select(pair => pair.Key),
                StringComparer.Ordinal);

            foreach (var inferred in QueryResultSchema.InferFields(rows))
            {
                // No stored value tells the type of an empty property: one named like a UTC date is taken as a date.
                var dataType = !valued.Contains(inferred.Name) && inferred.Name.EndsWith("Utc", StringComparison.Ordinal)
                    ? ReportDataType.DateTime
                    : inferred.DataType;

                Add(inferred.Name, dataType);
            }

            var group = S["{0} (more)", displayName].Value;

            foreach (var path in order)
            {
                if (fields.Count >= MaxProperties)
                {
                    break;
                }

                var descriptor = new ReportFieldDescriptor(name + "." + path, Humanize(path), properties[path], group)
                {
                    IsIdentifier = path.EndsWith("Id", StringComparison.Ordinal) || path.EndsWith("ID", StringComparison.Ordinal),
                };

                fields.Add(new ContentPathReportField(descriptor, name, path));
            }
        }

        _cache[definition.Name] = fields;

        return fields;
    }

    private async Task<IReadOnlyList<ContentItem>> SampleAsync(string contentType, CancellationToken cancellationToken)
    {
        var items = await _session
            .Query<ContentItem, ContentItemIndex>(index => index.ContentType == contentType && index.Published)
            .OrderByDescending(index => index.DocumentId)
            .Take(SampleSize)
            .ListAsync(cancellationToken);

        return items?.Where(item => item is not null).ToArray() ?? [];
    }

    private static JsonObject Root(ContentItem item)
    {
        return (JsonObject)item.Content;
    }

    // "RouteContainedItems" becomes "Route contained items", and "Settings.Mode" becomes "Settings › Mode".
    private static string Humanize(string path)
    {
        return string.Join(" › ", path.Split('.').Select(segment =>
        {
            var words = new StringBuilder();

            for (var index = 0; index < segment.Length; index++)
            {
                var character = segment[index];

                if (index > 0 && char.IsUpper(character) && !char.IsUpper(segment[index - 1]))
                {
                    words.Append(' ');
                    words.Append(char.ToLowerInvariant(character));
                }
                else
                {
                    words.Append(character);
                }
            }

            return words.ToString();
        }));
    }
}
