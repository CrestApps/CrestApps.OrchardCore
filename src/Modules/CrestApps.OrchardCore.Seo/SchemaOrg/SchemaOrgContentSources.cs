using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Seo.Models;
using CrestApps.OrchardCore.Seo.Providers;
using CrestApps.OrchardCore.Seo.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentLocalization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Models;
using OrchardCore.Media;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Seo.SchemaOrg;

/// <summary>
/// A value of a content item that a schema.org property can take: one of its fields, or a value every content item
/// has, such as its title or its author.
/// </summary>
/// <param name="Key">The key stored in the mapping, such as <c>Item:DisplayText</c> or <c>Field:BlogPost.Image</c>.</param>
/// <param name="Text">The name shown to editors.</param>
/// <param name="Group">The group the source is listed under.</param>
/// <param name="Kind">The kind of value the source gives, which the editor uses to suggest it.</param>
internal sealed record SchemaOrgContentSource(string Key, string Text, string Group, SchemaOrgValueKind Kind);

/// <summary>
/// Lists the values of a content type that schema.org properties can take, and reads them from a content item.
/// </summary>
internal sealed class SchemaOrgContentSources
{
    /// <summary>
    /// The longest summary taken from the body of a content item.
    /// </summary>
    internal const int SummaryLength = 300;

    internal const string ItemPrefix = "Item:";
    internal const string SitePrefix = "Site:";
    internal const string FieldPrefix = "Field:";

    private readonly IContentManager _contentManager;
    private readonly ISiteService _siteService;
    private readonly ContentItemUrlResolver _urlResolver;
    private readonly IServiceProvider _serviceProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaOrgContentSources"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="urlResolver">The resolver of content item URLs.</param>
    /// <param name="serviceProvider">The service provider, which gives the media store when Media is enabled.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SchemaOrgContentSources(
        IContentManager contentManager,
        ISiteService siteService,
        ContentItemUrlResolver urlResolver,
        IServiceProvider serviceProvider,
        IStringLocalizer<SchemaOrgContentSources> stringLocalizer)
    {
        _contentManager = contentManager;
        _siteService = siteService;
        _urlResolver = urlResolver;
        _serviceProvider = serviceProvider;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the values a content type's items can give: the values every content item has, then the fields of each
    /// of its parts.
    /// </summary>
    /// <param name="definition">The content type definition.</param>
    /// <returns>The sources.</returns>
    public IReadOnlyList<SchemaOrgContentSource> List(ContentTypeDefinition definition)
    {
        var item = S["Content item"].Value;
        var site = S["Site"].Value;

        var sources = new List<SchemaOrgContentSource>
        {
            new(ItemPrefix + "DisplayText", S["Title"], item, SchemaOrgValueKind.Text),
            new(ItemPrefix + "Summary", S["Summary (start of the body)"], item, SchemaOrgValueKind.LongText),
            new(ItemPrefix + "Body", S["Body text"], item, SchemaOrgValueKind.LongText),
            new(ItemPrefix + "Url", S["Page URL"], item, SchemaOrgValueKind.Url),
            new(ItemPrefix + "Author", S["Author"], item, SchemaOrgValueKind.Object),
            new(ItemPrefix + "PublishedUtc", S["Published date"], item, SchemaOrgValueKind.DateTime),
            new(ItemPrefix + "ModifiedUtc", S["Modified date"], item, SchemaOrgValueKind.DateTime),
            new(ItemPrefix + "CreatedUtc", S["Created date"], item, SchemaOrgValueKind.DateTime),
            new(ItemPrefix + "Language", S["Language"], item, SchemaOrgValueKind.Text),
            new(SitePrefix + "Publisher", S["The site's publisher"], site, SchemaOrgValueKind.Object),
            new(SitePrefix + "Name", S["The site's name"], site, SchemaOrgValueKind.Text),
        };

        if (definition is null)
        {
            return sources;
        }

        foreach (var part in definition.Parts)
        {
            var group = part.PartDefinition.Name == definition.Name
                ? S["Fields of {0}", definition.DisplayName].Value
                : S["Fields of {0}", part.DisplayName()].Value;

            foreach (var field in part.PartDefinition.Fields)
            {
                sources.Add(new SchemaOrgContentSource(
                    $"{FieldPrefix}{part.Name}.{field.Name}",
                    field.DisplayName(),
                    group,
                    GetFieldKind(field.FieldDefinition.Name)));
            }
        }

        return sources;
    }

    /// <summary>
    /// Reads a value of a content item.
    /// </summary>
    /// <param name="key">The key of the source, such as <c>Item:DisplayText</c>.</param>
    /// <param name="context">The structured data context, which holds the content item.</param>
    /// <param name="definition">The content item's type definition.</param>
    /// <returns>The value, or <see langword="null"/> when the content item has none.</returns>
    public async Task<JsonNode> ReadAsync(string key, StructuredDataContext context, ContentTypeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(context);

        var contentItem = context.ContentItem;

        if (string.IsNullOrWhiteSpace(key) || contentItem is null)
        {
            return null;
        }

        if (key.StartsWith(ItemPrefix, StringComparison.Ordinal))
        {
            return await ReadItemValueAsync(key.Substring(ItemPrefix.Length), context);
        }

        if (key.StartsWith(SitePrefix, StringComparison.Ordinal))
        {
            return await ReadSiteValueAsync(key.Substring(SitePrefix.Length), context);
        }

        if (key.StartsWith(FieldPrefix, StringComparison.Ordinal))
        {
            return await ReadFieldAsync(key.Substring(FieldPrefix.Length), context, definition);
        }

        return null;
    }

    /// <summary>
    /// Gets the kind of value a field type gives.
    /// </summary>
    /// <param name="fieldType">The field type, such as <c>TextField</c>.</param>
    /// <returns>The kind.</returns>
    internal static SchemaOrgValueKind GetFieldKind(string fieldType)
        => fieldType switch
        {
            "HtmlField" or "MarkdownField" => SchemaOrgValueKind.LongText,
            "NumericField" => SchemaOrgValueKind.Number,
            "BooleanField" => SchemaOrgValueKind.Boolean,
            "DateField" => SchemaOrgValueKind.Date,
            "DateTimeField" => SchemaOrgValueKind.DateTime,
            "LinkField" or "YoutubeField" => SchemaOrgValueKind.Url,
            "MediaField" => SchemaOrgValueKind.Image,
            "ContentPickerField" or "UserPickerField" => SchemaOrgValueKind.Object,
            "TaxonomyField" => SchemaOrgValueKind.TextList,
            _ => SchemaOrgValueKind.Text,
        };

    private async Task<JsonNode> ReadItemValueAsync(string name, StructuredDataContext context)
    {
        var contentItem = context.ContentItem;

        switch (name)
        {
            case "DisplayText":
                return Text(contentItem.DisplayText);
            case "Url":
                return Text(context.Url);
            case "Author":
                return Text(contentItem.Author);
            case "CreatedUtc":
                return Text(ContentItemStructuredDataProvider.FormatDate(contentItem.CreatedUtc));
            case "PublishedUtc":
                return Text(ContentItemStructuredDataProvider.FormatDate(contentItem.PublishedUtc));
            case "ModifiedUtc":
                return Text(ContentItemStructuredDataProvider.FormatDate(contentItem.ModifiedUtc));
            case "Language":
                var culture = await _contentManager.PopulateAspectAsync<CultureAspect>(contentItem);

                return culture.HasCulture ? Text(culture.Culture?.Name) : null;
            case "Summary":
            case "Body":
                var body = await _contentManager.PopulateAspectAsync<BodyAspect>(contentItem);

                if (body.Body is null)
                {
                    return null;
                }

                using (var writer = new StringWriter(CultureInfo.InvariantCulture))
                {
                    body.Body.WriteTo(writer, HtmlEncoder.Default);

                    var text = StructuredDataText.ToPlainText(writer.ToString());

                    return Text(name == "Summary" ? StructuredDataText.Truncate(text, SummaryLength) : text);
                }

            default:
                return null;
        }
    }

    private async Task<JsonNode> ReadSiteValueAsync(string name, StructuredDataContext context)
    {
        var site = await _siteService.GetSiteSettingsAsync();

        if (name == "Name")
        {
            return Text(site.SiteName);
        }

        if (name != "Publisher")
        {
            return null;
        }

        var settings = await _siteService.GetSettingsAsync<StructuredDataSettings>();

        if (string.IsNullOrEmpty(settings.PublisherType))
        {
            return null;
        }

        // The page carries the publisher's node, so it is referenced; an answer stands alone, so it gets a copy.
        if (context.Purpose == StructuredDataPurpose.Page)
        {
            return JsonLd.Reference(SiteStructuredDataProvider.GetPublisherId(context.BaseUrl));
        }

        var publisher = new JsonObject
        {
            [StructuredDataConstants.Keywords.Type] = settings.PublisherType,
        };

        JsonLd.SetIfNotEmpty(publisher, "name", string.IsNullOrWhiteSpace(settings.PublisherName) ? site.SiteName : settings.PublisherName);

        return publisher;
    }

    private async Task<JsonNode> ReadFieldAsync(string path, StructuredDataContext context, ContentTypeDefinition definition)
    {
        var separator = path.IndexOf('.');

        if (separator <= 0)
        {
            return null;
        }

        var partName = path.Substring(0, separator);
        var fieldName = path.Substring(separator + 1);

        var fieldDefinition = definition?.Parts
            .FirstOrDefault(part => part.Name == partName)?
            .PartDefinition.Fields
            .FirstOrDefault(field => field.Name == fieldName);

        if (fieldDefinition is null ||
            ((JsonObject)context.ContentItem.Content)[partName] is not JsonObject part ||
            part[fieldName] is not JsonObject field)
        {
            return null;
        }

        switch (fieldDefinition.FieldDefinition.Name)
        {
            case "TextField":
                return Text(field["Text"]);
            case "HtmlField":
                return Text(StructuredDataText.ToPlainText(field["Html"]?.GetValue<string>()));
            case "MarkdownField":
                return Text(field["Markdown"]);
            case "NumericField":
            case "BooleanField":
            case "DateField":
            case "DateTimeField":
            case "TimeField":
                return field["Value"]?.DeepClone();
            case "LinkField":
                return Text(context.ToAbsoluteUrl(field["Url"]?.GetValue<string>()));
            case "YoutubeField":
                return Text(field["RawAddress"] ?? field["EmbeddedAddress"]);
            case "MediaField":
                return ReadMedia(field, context);
            case "ContentPickerField":
                return await ReadContentItemsAsync(field, context);
            case "UserPickerField":
                return field["UserNames"] is JsonArray userNames && userNames.Count > 0 ? userNames.DeepClone() : null;
            case "TaxonomyField":
                return await ReadTermsAsync(field);
            default:
                return (field["Text"] ?? field["Value"])?.DeepClone();
        }
    }

    private JsonArray ReadMedia(JsonObject field, StructuredDataContext context)
    {
        if (field["Paths"] is not JsonArray paths || paths.Count == 0)
        {
            return null;
        }

        var mediaFileStore = _serviceProvider.GetService<IMediaFileStore>();
        var urls = new JsonArray();

        foreach (var path in paths)
        {
            var value = path?.GetValue<string>();

            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var url = mediaFileStore is null ? value : mediaFileStore.MapPathToPublicUrl(value);

            urls.Add(context.ToAbsoluteUrl(url));
        }

        return urls.Count > 0 ? urls : null;
    }

    private async Task<JsonArray> ReadContentItemsAsync(JsonObject field, StructuredDataContext context)
    {
        var ids = Strings(field["ContentItemIds"]);

        if (ids.Count == 0)
        {
            return null;
        }

        var items = new JsonArray();

        foreach (var contentItem in await _contentManager.GetAsync(ids, VersionOptions.Published))
        {
            var item = new JsonObject();

            JsonLd.SetIfNotEmpty(item, "name", contentItem.DisplayText);

            JsonLd.SetIfNotEmpty(item, "url", await _urlResolver.GetUrlAsync(contentItem, context));

            items.Add(item);
        }

        return items.Count > 0 ? items : null;
    }

    private async Task<JsonArray> ReadTermsAsync(JsonObject field)
    {
        if (field["TagNames"] is JsonArray tagNames && tagNames.Count > 0)
        {
            return (JsonArray)tagNames.DeepClone();
        }

        var termIds = Strings(field["TermContentItemIds"]);
        var taxonomyId = field["TaxonomyContentItemId"]?.GetValue<string>();

        if (termIds.Count == 0 || string.IsNullOrEmpty(taxonomyId))
        {
            return null;
        }

        var taxonomy = await _contentManager.GetAsync(taxonomyId, VersionOptions.Published);

        if (taxonomy is null)
        {
            return null;
        }

        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        FindTerms((JsonObject)taxonomy.Content, new HashSet<string>(termIds, StringComparer.Ordinal), names);

        var result = new JsonArray();

        foreach (var id in termIds)
        {
            if (names.TryGetValue(id, out var name))
            {
                result.Add(name);
            }
        }

        return result.Count > 0 ? result : null;
    }

    /// <summary>
    /// Finds the terms of a taxonomy, at any depth, and records their names.
    /// </summary>
    /// <param name="node">The JSON to search.</param>
    /// <param name="ids">The ids of the terms to find.</param>
    /// <param name="names">The names found, by term id.</param>
    internal static void FindTerms(JsonNode node, HashSet<string> ids, Dictionary<string, string> names)
    {
        switch (node)
        {
            case JsonObject obj:
                if (obj["ContentItemId"] is JsonValue id &&
                    id.TryGetValue<string>(out var termId) &&
                    ids.Contains(termId) &&
                    obj["DisplayText"] is JsonValue displayText &&
                    displayText.TryGetValue<string>(out var name))
                {
                    names.TryAdd(termId, name);
                }

                foreach (var (_, value) in obj)
                {
                    FindTerms(value, ids, names);
                }

                break;
            case JsonArray array:
                foreach (var value in array)
                {
                    FindTerms(value, ids, names);
                }

                break;
        }
    }

    private static List<string> Strings(JsonNode node)
    {
        var result = new List<string>();

        if (node is JsonArray array)
        {
            foreach (var value in array)
            {
                if (value is JsonValue text && text.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s))
                {
                    result.Add(s);
                }
            }
        }

        return result;
    }

    private static JsonValue Text(JsonNode node)
        => node is JsonValue value && value.TryGetValue<string>(out var text) ? Text(text) : null;

    private static JsonValue Text(string value)
        => string.IsNullOrWhiteSpace(value) ? null : JsonValue.Create(value.Trim());
}
