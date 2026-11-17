using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.AI.Tooling.Instances;
using CrestApps.Core.AI.Tooling.Instances.DataSources;
using CrestApps.Core.AI.Tooling.Instances.Documentation;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Core.Http;

namespace CrestApps.OrchardCore.AI.Agent.ToolInstances;

/// <summary>
/// The settings of each tool instance source the AI tools may read and write, by explicit lists of fields. A credential
/// (API key, bearer token, password, client secret, OAuth token) and the default headers, which can carry one, are never
/// returned and never accepted: a person enters them on the tool instance's admin page.
/// </summary>
internal static class ToolInstanceSettings
{
    private static readonly Dictionary<string, SourceSettings> _sources = new(StringComparer.OrdinalIgnoreCase)
    {
        [HttpApiRequestToolConstants.SourceName] = new(
            nameof(HttpApiRequestToolSettings),
            [
                nameof(HttpApiRequestToolSettings.BaseUrl),
                nameof(HttpApiRequestToolSettings.PathTemplate),
                nameof(HttpApiRequestToolSettings.HttpMethod),
                nameof(HttpApiRequestToolSettings.AuthenticationType),
                nameof(HttpApiRequestToolSettings.ApiKeyHeaderName),
                nameof(HttpApiRequestToolSettings.Username),
                nameof(HttpApiRequestToolSettings.TokenEndpoint),
                nameof(HttpApiRequestToolSettings.ClientId),
                nameof(HttpApiRequestToolSettings.Scope),
                nameof(HttpApiRequestToolSettings.AllowModelProvidedPath),
                nameof(HttpApiRequestToolSettings.AllowModelProvidedQuery),
                nameof(HttpApiRequestToolSettings.AllowModelProvidedBody),
                nameof(HttpApiRequestToolSettings.TimeoutSeconds),
            ],
            [nameof(HttpApiRequestToolSettings.BaseUrl), nameof(HttpApiRequestToolSettings.TokenEndpoint)]),
        [DocumentationToolConstants.SitemapSourceName] = new(
            nameof(SitemapDocumentationToolSettings),
            [
                nameof(SitemapDocumentationToolSettings.BaseUrl),
                nameof(SitemapDocumentationToolSettings.SitemapUrl),
                nameof(SitemapDocumentationToolSettings.MaxResults),
                nameof(SitemapDocumentationToolSettings.MaxPages),
            ],
            [nameof(SitemapDocumentationToolSettings.BaseUrl), nameof(SitemapDocumentationToolSettings.SitemapUrl)]),
        [DocumentationToolConstants.SearchIndexSourceName] = new(
            nameof(SearchIndexDocumentationToolSettings),
            [
                nameof(SearchIndexDocumentationToolSettings.BaseUrl),
                nameof(SearchIndexDocumentationToolSettings.IndexUrl),
                nameof(SearchIndexDocumentationToolSettings.MaxResults),
            ],
            [nameof(SearchIndexDocumentationToolSettings.BaseUrl), nameof(SearchIndexDocumentationToolSettings.IndexUrl)]),
        [DocumentationToolConstants.AlgoliaSourceName] = new(
            nameof(AlgoliaDocumentationToolSettings),
            [
                nameof(AlgoliaDocumentationToolSettings.ApplicationId),
                nameof(AlgoliaDocumentationToolSettings.IndexName),
                nameof(AlgoliaDocumentationToolSettings.MaxResults),
            ],
            []),
        [DocumentationToolConstants.WebsiteSearchSourceName] = new(
            nameof(WebsiteSearchToolSettings),
            [
                nameof(WebsiteSearchToolSettings.BaseUrl),
                nameof(WebsiteSearchToolSettings.SearchPath),
                nameof(WebsiteSearchToolSettings.QueryParameter),
                nameof(WebsiteSearchToolSettings.ExtraQuery),
                nameof(WebsiteSearchToolSettings.ResultsPath),
                nameof(WebsiteSearchToolSettings.TitlePath),
                nameof(WebsiteSearchToolSettings.UrlPath),
                nameof(WebsiteSearchToolSettings.SnippetPath),
                nameof(WebsiteSearchToolSettings.MaxResults),
            ],
            [nameof(WebsiteSearchToolSettings.BaseUrl)]),
        [DataSourceSearchToolConstants.SourceName] = new(
            nameof(DataSourceSearchToolSettings),
            [
                nameof(DataSourceSearchToolSettings.DataSourceId),
                nameof(DataSourceSearchToolSettings.RetrievalMode),
                nameof(DataSourceSearchToolSettings.TopNDocuments),
                nameof(DataSourceSearchToolSettings.Strictness),
                nameof(DataSourceSearchToolSettings.Filter),
                nameof(DataSourceSearchToolSettings.ContentTypes),
            ],
            []),
    };

    /// <summary>
    /// Gets the names of the settings a source takes through the AI tools, or an empty list for a source whose settings
    /// are only set on its admin page.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The setting names, camel-cased as the tools use them.</returns>
    public static IReadOnlyList<string> GetSettingNames(string source)
        => source is not null && _sources.TryGetValue(source, out var settings)
            ? settings.Fields.Select(ToCamelCase).ToList()
            : [];

    /// <summary>
    /// Reads the non-secret settings of a tool instance, and whether each credential is stored.
    /// </summary>
    /// <param name="instance">The tool instance.</param>
    /// <returns>The settings and the stored-credential flags.</returns>
    public static (Dictionary<string, object> Settings, Dictionary<string, bool> Credentials) Read(AIToolInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var settings = new Dictionary<string, object>(StringComparer.Ordinal);
        var credentials = new Dictionary<string, bool>(StringComparer.Ordinal);

        if (instance.Source is null || !_sources.TryGetValue(instance.Source, out var source))
        {
            return (settings, credentials);
        }

        var node = GetSettingsNode(instance, source.Key);

        if (node is not null)
        {
            foreach (var field in source.Fields)
            {
                if (TryGetValue(node, field, out var value) && value is not null)
                {
                    settings[ToCamelCase(field)] = value.DeepClone();
                }
            }
        }

        if (string.Equals(instance.Source, HttpApiRequestToolConstants.SourceName, StringComparison.OrdinalIgnoreCase))
        {
            var http = instance.TryGet<HttpApiRequestToolSettings>(out var httpSettings) ? httpSettings : null;
            var tokens = instance.TryGet<HttpApiRequestTokenState>(out var tokenState) ? tokenState : null;

            credentials["hasApiKey"] = !string.IsNullOrEmpty(http?.ApiKey);
            credentials["hasBearerToken"] = !string.IsNullOrEmpty(http?.BearerToken);
            credentials["hasPassword"] = !string.IsNullOrEmpty(http?.Password);
            credentials["hasClientSecret"] = !string.IsNullOrEmpty(http?.ClientSecret);
            credentials["hasAccessToken"] = !string.IsNullOrEmpty(tokens?.AccessToken) || !string.IsNullOrEmpty(tokens?.RefreshToken);

            // Header values can carry a token, so only their names are shown.
            if (http?.DefaultHeaders is { Count: > 0 })
            {
                settings["defaultHeaderNames"] = http.DefaultHeaders.Keys.Order(StringComparer.OrdinalIgnoreCase).ToArray();
            }
        }
        else if (string.Equals(instance.Source, DocumentationToolConstants.AlgoliaSourceName, StringComparison.OrdinalIgnoreCase))
        {
            credentials["hasApiKey"] = instance.TryGet<AlgoliaDocumentationToolSettings>(out var algolia) && !string.IsNullOrEmpty(algolia.ApiKey);
        }

        return (settings, credentials);
    }

    /// <summary>
    /// Whether a stored credential would be sent to an address the instance's settings name.
    /// </summary>
    /// <param name="instance">The tool instance as stored.</param>
    /// <returns><see langword="true"/> when a credential is stored.</returns>
    public static bool HasStoredCredential(AIToolInstance instance)
        => Read(instance).Credentials.Values.Any(stored => stored);

    /// <summary>
    /// Turns the settings a tool was given into the instance's properties, refusing any setting the source does not
    /// take through the AI tools, credentials and headers included, and any internal or non-web address.
    /// </summary>
    /// <param name="source">The source of the instance.</param>
    /// <param name="given">The settings the tool was given, or <see langword="null"/>.</param>
    /// <param name="properties">The properties to merge into the instance.</param>
    /// <param name="addressFields">The names of the address settings that were given.</param>
    /// <returns>The refusal, or <see langword="null"/> when the settings may be saved.</returns>
    public static string TryBuildProperties(string source, JsonObject given, out JsonObject properties, out List<string> addressFields)
    {
        properties = null;
        addressFields = [];

        if (given is null || given.Count == 0)
        {
            return null;
        }

        if (source is null || !_sources.TryGetValue(source, out var settings))
        {
            return $"the settings of a '{source}' tool instance are set on its admin page only.";
        }

        var values = new JsonObject();

        foreach (var (key, value) in given)
        {
            var field = settings.Fields.FirstOrDefault(name => string.Equals(name, key, StringComparison.OrdinalIgnoreCase));

            if (field is null)
            {
                return IsCredentialName(key)
                    ? $"'{key}' is a credential or a header, and credentials are never taken from a chat. Save the rest, then a person enters it on the tool instance's admin page."
                    : $"'{key}' is not a setting of a '{source}' tool instance. Its settings are {string.Join(", ", settings.Fields.Select(ToCamelCase))}.";
            }

            if (settings.AddressFields.Contains(field, StringComparer.Ordinal) && value is not null)
            {
                var address = value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>()?.Trim() : null;

                if (!string.IsNullOrEmpty(address))
                {
                    var addressError = CheckAddress(address);

                    if (addressError is not null)
                    {
                        return $"{ToCamelCase(field)}: {addressError}";
                    }
                }

                addressFields.Add(field);
            }

            values[field] = value?.DeepClone();
        }

        properties = new JsonObject
        {
            [settings.Key] = values,
        };

        return null;
    }

    /// <summary>
    /// Whether the caller may change or delete a tool instance: the permission, and the permission over other people's
    /// instances when someone else owns it, as the admin page checks.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="instance">The tool instance.</param>
    /// <returns><see langword="true"/> when the caller may manage it.</returns>
    public static async Task<bool> CanManageAsync(IServiceProvider services, AIToolInstance instance)
    {
        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.ManageAIToolInstances))
        {
            return false;
        }

        var callerId = AgentToolAuthorization.GetCaller(services)?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!string.IsNullOrEmpty(instance.OwnerId) && !string.Equals(instance.OwnerId, callerId, StringComparison.Ordinal))
        {
            return await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.ManageAIToolInstancesCreatedByOthers);
        }

        return true;
    }

    private static string CheckAddress(string address)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return "only an absolute http or https address is accepted.";
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return "an address that carries a user name or a password is not accepted.";
        }

        return EgressNetwork.IsInternalHost(uri)
            ? "an address on the server itself or on an internal network is not accepted."
            : null;
    }

    private static bool IsCredentialName(string key)
        => key.Contains("key", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("token", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("password", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("header", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("credential", StringComparison.OrdinalIgnoreCase);

    private static JsonObject GetSettingsNode(AIToolInstance instance, string key)
    {
        if (instance.Properties is null || !instance.Properties.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            JsonObject jsonObject => jsonObject,
            JsonElement element when element.ValueKind == JsonValueKind.Object => JsonObject.Create(element),
            _ => JsonSerializer.SerializeToNode(value) as JsonObject,
        };
    }

    private static bool TryGetValue(JsonObject node, string field, out JsonNode value)
    {
        foreach (var (key, item) in node)
        {
            if (string.Equals(key, field, StringComparison.OrdinalIgnoreCase))
            {
                value = item;

                return true;
            }
        }

        value = null;

        return false;
    }

    private static string ToCamelCase(string name)
        => string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];

    private sealed record SourceSettings(string Key, string[] Fields, string[] AddressFields);
}
