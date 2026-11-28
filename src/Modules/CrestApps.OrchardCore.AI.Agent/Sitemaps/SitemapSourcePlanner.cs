using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Sitemaps.Models;
using OrchardCore.Sitemaps.Services;

namespace CrestApps.OrchardCore.AI.Agent.Sitemaps;

/// <summary>
/// Reads the sources the save tool is given for a sitemap, checks them the way the source editors of the Sitemaps
/// admin do, and plans the change. The plan runs only when every source is valid, so a refused call changes nothing.
/// </summary>
internal static class SitemapSourcePlanner
{
    private const int MinPriority = 0;

    private const int MaxPriority = 10;

    private const int DefaultPriority = 5;

    private const int DefaultTake = 50_000;

    private static readonly string[] _contentTypesSourceNames =
    [
        "sourceId",
        "type",
        "indexAll",
        "limitItems",
        "changeFrequency",
        "priority",
        "contentTypes",
        "limitedContentType",
    ];

    private static readonly string[] _customPathSourceNames =
    [
        "sourceId",
        "type",
        "path",
        "changeFrequency",
        "priority",
    ];

    private static readonly string[] _contentTypeEntryNames =
    [
        "contentType",
        "changeFrequency",
        "priority",
    ];

    private static readonly string[] _limitedContentTypeNames =
    [
        "contentType",
        "changeFrequency",
        "priority",
        "skip",
        "take",
    ];

    /// <summary>
    /// Checks every source of the list and returns, for each, the step that creates or updates it. Nothing is changed
    /// until the steps run.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="sitemap">The sitemap that gets the sources.</param>
    /// <param name="sources">The sources the tool was given.</param>
    /// <returns>The steps, in the order of the list, or the sentence that says what is wrong.</returns>
    public static async Task<(List<Func<SitemapSource>> Plans, string Error)> PlanAsync(IServiceProvider services, SitemapType sitemap, JsonArray sources)
    {
        var plans = new List<Func<SitemapSource>>();
        var usedSourceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var factories = services.GetServices<ISitemapSourceFactory>();
        HashSet<string> routableContentTypes = null;

        for (var index = 0; index < sources.Count; index++)
        {
            var where = $"source {index + 1}";

            if (sources[index] is not JsonObject entry)
            {
                return (null, $"{where} must be a JSON object.");
            }

            var type = GetText(entry, "type");
            var isContentTypes = string.Equals(type, SitemapContent.ContentTypesSource, StringComparison.OrdinalIgnoreCase);

            if (!isContentTypes && !string.Equals(type, SitemapContent.CustomPathSource, StringComparison.OrdinalIgnoreCase))
            {
                return (null, $"{where} needs a type: {SitemapContent.ContentTypesSource} or {SitemapContent.CustomPathSource}.");
            }

            var allowedNames = isContentTypes
                ? _contentTypesSourceNames
                : _customPathSourceNames;
            var error = GetUnknownNamesError(entry, allowedNames, where);

            if (error is not null)
            {
                return (null, error);
            }

            SitemapSource existing = null;
            var sourceId = GetText(entry, "sourceId");

            if (!string.IsNullOrEmpty(sourceId))
            {
                existing = sitemap.SitemapSources.FirstOrDefault(source => string.Equals(source.Id, sourceId, StringComparison.OrdinalIgnoreCase));

                if (existing is null)
                {
                    return (null, $"{where}: the sitemap has no source with the ID '{sourceId}'. Leave sourceId out to add a new source.");
                }

                if (!usedSourceIds.Add(existing.Id))
                {
                    return (null, $"{where}: the source '{sourceId}' is listed twice.");
                }

                if (isContentTypes ? existing is not ContentTypesSitemapSource : existing is not CustomPathSitemapSource)
                {
                    return (null, $"{where}: the source '{sourceId}' is not a {type} source, and the type of a source cannot be changed. Leave sourceId out to add a new source.");
                }
            }

            var factoryName = isContentTypes
                ? nameof(ContentTypesSitemapSource)
                : nameof(CustomPathSitemapSource);
            var factory = factories.FirstOrDefault(candidate => string.Equals(candidate.Name, factoryName, StringComparison.Ordinal));

            if (existing is null && factory is null)
            {
                return (null, $"{where}: this site cannot create {type} sources. A content types source needs the Contents feature.");
            }

            Func<SitemapSource> plan;

            if (isContentTypes)
            {
                routableContentTypes ??= (await services.GetRequiredService<IRouteableContentTypeCoordinator>().ListRoutableTypeDefinitionsAsync())
                    .Select(definition => definition.Name)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                (plan, error) = PlanContentTypesSource(entry, (ContentTypesSitemapSource)existing, factory, routableContentTypes, where);
            }
            else
            {
                (plan, error) = PlanCustomPathSource(services, entry, (CustomPathSitemapSource)existing, factory, where);
            }

            if (error is not null)
            {
                return (null, error);
            }

            plans.Add(plan);
        }

        return (plans, null);
    }

    private static (Func<SitemapSource> Plan, string Error) PlanContentTypesSource(
        JsonObject entry,
        ContentTypesSitemapSource existing,
        ISitemapSourceFactory factory,
        HashSet<string> routableContentTypes,
        string where)
    {
        bool? indexAll = null;
        bool? limitItems = null;
        ChangeFrequency? changeFrequency = null;
        int? priority = null;

        var error =
            ReadBool(entry, "indexAll", where, out indexAll) ??
            ReadBool(entry, "limitItems", where, out limitItems) ??
            ReadChangeFrequency(entry, where, out changeFrequency) ??
            ReadInt(entry, "priority", where, MinPriority, MaxPriority, out priority);

        if (error is not null)
        {
            return (null, error);
        }

        ContentTypeSitemapEntry[] contentTypes = null;

        if (entry["contentTypes"] is JsonArray contentTypeEntries)
        {
            var entries = new List<ContentTypeSitemapEntry>();

            for (var index = 0; index < contentTypeEntries.Count; index++)
            {
                var contentType = new ContentTypeSitemapEntry();

                error = ReadContentTypeEntry(contentTypeEntries[index], contentType, _contentTypeEntryNames, routableContentTypes, $"{where}, content type {index + 1}");

                if (error is not null)
                {
                    return (null, error);
                }

                entries.Add(contentType);
            }

            contentTypes = entries.ToArray();
        }
        else if (entry["contentTypes"] is not null)
        {
            return (null, $"{where}: contentTypes must be an array.");
        }

        LimitedContentTypeSitemapEntry limitedContentType = null;

        if (entry["limitedContentType"] is not null)
        {
            var limitedWhere = $"{where}, limitedContentType";

            limitedContentType = new LimitedContentTypeSitemapEntry();
            error = ReadContentTypeEntry(entry["limitedContentType"], limitedContentType, _limitedContentTypeNames, routableContentTypes, limitedWhere);

            if (error is not null)
            {
                return (null, error);
            }

            var limitedEntry = (JsonObject)entry["limitedContentType"];
            int? skip = null;
            int? take = null;

            error =
                ReadInt(limitedEntry, "skip", limitedWhere, 0, int.MaxValue, out skip) ??
                ReadInt(limitedEntry, "take", limitedWhere, 1, int.MaxValue, out take);

            if (error is not null)
            {
                return (null, error);
            }

            limitedContentType.Skip = skip ?? 0;
            limitedContentType.Take = take ?? DefaultTake;
        }

        return (() =>
        {
            var source = existing ?? (ContentTypesSitemapSource)factory.Create();

            // The site reads the flags in this order: all content types, then the limited one, then the list. A list or
            // a limited content type that is given is meant to be used, so the flag above it is turned off unless given.
            if (contentTypes is not null)
            {
                source.ContentTypes = contentTypes;
                source.IndexAll = false;
                source.LimitItems = false;
            }

            if (limitedContentType is not null)
            {
                source.LimitedContentType = limitedContentType;
                source.IndexAll = false;
                source.LimitItems = true;
            }

            source.IndexAll = indexAll ?? source.IndexAll;
            source.LimitItems = limitItems ?? source.LimitItems;
            source.ChangeFrequency = changeFrequency ?? source.ChangeFrequency;
            source.Priority = priority ?? source.Priority;

            return source;
        }, null);
    }

    private static (Func<SitemapSource> Plan, string Error) PlanCustomPathSource(
        IServiceProvider services,
        JsonObject entry,
        CustomPathSitemapSource existing,
        ISitemapSourceFactory factory,
        string where)
    {
        ChangeFrequency? changeFrequency = null;
        int? priority = null;

        var error =
            ReadChangeFrequency(entry, where, out changeFrequency) ??
            ReadInt(entry, "priority", where, MinPriority, MaxPriority, out priority);

        if (error is not null)
        {
            return (null, error);
        }

        // The site puts the path after its own address, so it is stored without a leading slash.
        var path = GetText(entry, "path")?.Trim().TrimStart('/');

        if (string.IsNullOrEmpty(path))
        {
            if (existing is null)
            {
                return (null, $"{where}: a new custom path source needs a path.");
            }
        }
        else if (!IsValidCustomPath(path))
        {
            return (null, $"{where}: the path '{path}' cannot be used. Do not use spaces, consecutive slashes or any of these characters: {string.Join(' ', CustomPathSitemapSource.InvalidCharactersForPath)}. It can be up to {CustomPathSitemapSource.MaxPathLength} characters.");
        }

        return (() =>
        {
            var source = existing ?? (CustomPathSitemapSource)factory.Create();
            var newPath = string.IsNullOrEmpty(path)
                ? source.Path
                : path;
            var newChangeFrequency = changeFrequency ?? source.ChangeFrequency;
            var newPriority = priority ?? source.Priority;

            // The last update is what search engines read as the date of the page, so it moves only with a change.
            if (existing is null ||
                !string.Equals(newPath, source.Path, StringComparison.Ordinal) ||
                newChangeFrequency != source.ChangeFrequency ||
                newPriority != source.Priority)
            {
                source.LastUpdate = services.GetRequiredService<IClock>().UtcNow;
            }

            source.Path = newPath;
            source.ChangeFrequency = newChangeFrequency;
            source.Priority = newPriority;

            return source;
        }, null);
    }

    /// <summary>
    /// The checks of the custom path editor.
    /// </summary>
    private static bool IsValidCustomPath(string path)
    {
        return path.IndexOfAny(CustomPathSitemapSource.InvalidCharactersForPath) < 0 &&
            !path.Contains(' ') &&
            !path.Contains("//", StringComparison.Ordinal) &&
            path.Length <= CustomPathSitemapSource.MaxPathLength;
    }

    private static string ReadContentTypeEntry(
        JsonNode node,
        ContentTypeSitemapEntry contentType,
        string[] allowedNames,
        HashSet<string> routableContentTypes,
        string where)
    {
        if (node is not JsonObject entry)
        {
            return $"{where} must be a JSON object.";
        }

        ChangeFrequency? changeFrequency = null;
        int? priority = null;

        var error =
            GetUnknownNamesError(entry, allowedNames, where) ??
            ReadChangeFrequency(entry, where, out changeFrequency) ??
            ReadInt(entry, "priority", where, MinPriority, MaxPriority, out priority);

        if (error is not null)
        {
            return error;
        }

        var name = GetText(entry, "contentType")?.Trim();

        if (string.IsNullOrEmpty(name) || !routableContentTypes.TryGetValue(name, out var contentTypeName))
        {
            return routableContentTypes.Count == 0
                ? $"{where}: '{name}' cannot be listed. No content type of the site has an address."
                : $"{where}: '{name}' is not a content type that has an address. The content types that can be listed are: {string.Join(", ", routableContentTypes.Order(StringComparer.OrdinalIgnoreCase))}.";
        }

        contentType.ContentTypeName = contentTypeName;
        contentType.ChangeFrequency = changeFrequency ?? ChangeFrequency.Daily;
        contentType.Priority = priority ?? DefaultPriority;

        return null;
    }

    private static string GetUnknownNamesError(JsonObject entry, string[] allowedNames, string where)
    {
        var unknown = entry
            .Select(property => property.Key)
            .Where(name => !allowedNames.Contains(name, StringComparer.Ordinal))
            .ToList();

        return unknown.Count == 0
            ? null
            : $"{where} has properties it cannot take: {string.Join(", ", unknown)}. It takes: {string.Join(", ", allowedNames)}.";
    }

    private static string GetText(JsonObject entry, string name)
    {
        return entry[name] is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : null;
    }

    private static string ReadBool(JsonObject entry, string name, string where, out bool? value)
    {
        value = null;

        if (entry[name] is null)
        {
            return null;
        }

        if (entry[name] is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out var flag))
        {
            value = flag;

            return null;
        }

        return $"{where}: {name} must be true or false.";
    }

    private static string ReadInt(JsonObject entry, string name, string where, int min, int max, out int? value)
    {
        value = null;

        if (entry[name] is null)
        {
            return null;
        }

        if (entry[name] is JsonValue jsonValue && jsonValue.TryGetValue<int>(out var number) && number >= min && number <= max)
        {
            value = number;

            return null;
        }

        return max == int.MaxValue
            ? $"{where}: {name} must be a whole number of {min} or more."
            : $"{where}: {name} must be a whole number from {min} to {max}.";
    }

    private static string ReadChangeFrequency(JsonObject entry, string where, out ChangeFrequency? value)
    {
        value = null;

        if (entry["changeFrequency"] is null)
        {
            return null;
        }

        if (SitemapContent.TryParseChangeFrequency(GetText(entry, "changeFrequency"), out var changeFrequency))
        {
            value = changeFrequency;

            return null;
        }

        return $"{where}: changeFrequency must be always, hourly, daily, weekly, monthly, yearly or never.";
    }
}
