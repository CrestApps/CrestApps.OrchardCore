using CrestApps.Core;
using CrestApps.OrchardCore.DataConnectors.Connectors.Elasticsearch;
using CrestApps.OrchardCore.DataConnectors.Connectors.Files;
using CrestApps.OrchardCore.DataConnectors.Drivers;
using CrestApps.OrchardCore.DataConnectors.Http;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.DataConnectors.ViewModels;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.DataConnectors.Services;

/// <summary>
/// The checks and the mapping of the connection editor, shared by its display drivers and the AI tools: a connection's
/// names, the server, authentication and headers of an HTTP-based connection, and the folder of a media files
/// connection. Each problem is reported with the name of the editor field it belongs to. Secrets are not handled here:
/// the editor stores them after these checks.
/// </summary>
internal sealed class DataConnectionEditor
{
    private readonly IDataConnectionStore _connectionStore;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataConnectionEditor"/> class.
    /// </summary>
    /// <param name="connectionStore">The connections.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DataConnectionEditor(
        IDataConnectionStore connectionStore,
        IStringLocalizer<DataConnectionEditor> stringLocalizer)
    {
        _connectionStore = connectionStore;
        S = stringLocalizer;
    }

    /// <summary>
    /// Checks and applies a connection's display name, technical name and description.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="model">The entered names.</param>
    /// <param name="addError">Receives each problem with the name of its field.</param>
    /// <returns>A task.</returns>
    public async Task ApplyNamesAsync(DataConnection connection, DataConnectionFieldsViewModel model, Action<string, string> addError)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(addError);

        if (string.IsNullOrWhiteSpace(model.DisplayText))
        {
            addError(nameof(model.DisplayText), S["The name is required."]);
        }

        var name = string.IsNullOrWhiteSpace(model.Name)
            ? (string.IsNullOrWhiteSpace(model.DisplayText) ? null : ConnectedDataSetFields.ToFieldName(model.DisplayText))
            : model.Name.Trim();

        if (string.IsNullOrEmpty(name) || !DataConnectionDisplayDriver.TechnicalName().IsMatch(name))
        {
            addError(nameof(model.Name), S["The technical name must start with a letter and hold only letters, digits and underscores."]);
        }
        else if (await _connectionStore.FindByNameAsync(name) is { } existing && existing.ItemId != connection.ItemId)
        {
            addError(nameof(model.Name), S["Another connection already has the technical name '{0}'.", name]);
        }

        connection.DisplayText = model.DisplayText?.Trim();
        connection.Name = name;
        connection.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
    }

    /// <summary>
    /// Checks and applies the server, authentication and headers of an HTTP API or Elasticsearch connection. The secrets
    /// of the model are not read.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="model">The entered settings.</param>
    /// <param name="addError">Receives each problem with the name of its field.</param>
    public void ApplyHttp(DataConnection connection, HttpConnectionViewModel model, Action<string, string> addError)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(addError);

        if (!Uri.TryCreate(model.BaseUrl?.Trim(), UriKind.Absolute, out var baseUri) || baseUri.Scheme is not ("http" or "https"))
        {
            addError(nameof(model.BaseUrl), S["The URL must be an absolute http:// or https:// address."]);
        }
        else if (!string.IsNullOrEmpty(baseUri.UserInfo))
        {
            addError(nameof(model.BaseUrl), S["Don't put credentials in the URL; use the authentication settings."]);
        }

        if (model.TimeoutSeconds is < 1 or > 3_600)
        {
            addError(nameof(model.TimeoutSeconds), S["The timeout must be between 1 and 3600 seconds."]);
        }

        if (IsElasticsearch(connection) && model.AuthenticationType is HttpAuthenticationType.OAuth2 or HttpAuthenticationType.OpenIdConnect)
        {
            addError(nameof(model.AuthenticationType), S["Elasticsearch connections use no authentication, Basic, an API key or a bearer token."]);
        }

        switch (model.AuthenticationType)
        {
            case HttpAuthenticationType.ApiKey when string.IsNullOrWhiteSpace(model.ApiKeyName):
                addError(nameof(model.ApiKeyName), S["The name of the API key's header or parameter is required."]);
                break;

            case HttpAuthenticationType.Basic when string.IsNullOrWhiteSpace(model.UserName):
                addError(nameof(model.UserName), S["The user name is required."]);
                break;

            case HttpAuthenticationType.OAuth2 when !IsHttps(model.TokenUrl):
                addError(nameof(model.TokenUrl), S["The token endpoint must be an absolute https:// URL."]);
                break;

            case HttpAuthenticationType.OpenIdConnect when !IsHttps(model.Authority):
                addError(nameof(model.Authority), S["The issuer must be an absolute https:// URL."]);
                break;
        }

        if (model.AuthenticationType is HttpAuthenticationType.OAuth2 or HttpAuthenticationType.OpenIdConnect)
        {
            if (string.IsNullOrWhiteSpace(model.ClientId))
            {
                addError(nameof(model.ClientId), S["The client identifier is required."]);
            }

            if (model.GrantType == OAuthGrantType.Password && string.IsNullOrWhiteSpace(model.UserName))
            {
                addError(nameof(model.UserName), S["The password grant needs a user name."]);
            }
        }

        var headers = new List<HttpHeaderSetting>();

        foreach (var line in (model.Headers ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = line.IndexOf(':');

            if (separator <= 0)
            {
                addError(nameof(model.Headers), S["Write each header as Name: value, one per line."]);

                break;
            }

            var name = line[..separator].Trim();

            if (string.Equals(name, "Host", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                addError(nameof(model.Headers), S["The {0} header can't be set.", name]);

                break;
            }

            headers.Add(new HttpHeaderSetting { Name = name, Value = line[(separator + 1)..].Trim() });
        }

        connection.Put(new HttpConnectionSettings
        {
            BaseUrl = model.BaseUrl?.Trim(),
            TimeoutSeconds = model.TimeoutSeconds,
            Headers = headers,
            Authentication = new HttpAuthenticationSettings
            {
                Type = model.AuthenticationType,
                ApiKeyName = model.ApiKeyName?.Trim(),
                ApiKeyLocation = model.ApiKeyLocation,
                ApiKeyPrefix = model.ApiKeyPrefix,
                UserName = model.UserName?.Trim(),
                TokenUrl = model.TokenUrl?.Trim(),
                Authority = model.Authority?.Trim(),
                ClientId = model.ClientId?.Trim(),
                Scope = model.Scope?.Trim(),
                Audience = model.Audience?.Trim(),
                GrantType = model.GrantType,
                ClientAuthentication = model.ClientAuthentication,
            },
        });
    }

    /// <summary>
    /// Checks and applies the folder of a media files connection.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="model">The entered folder.</param>
    /// <param name="addError">Receives each problem with the name of its field.</param>
    public void ApplyMediaFiles(DataConnection connection, MediaFilesConnectionViewModel model, Action<string, string> addError)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(addError);

        var folder = model.Folder?.Trim().Replace('\\', '/').Trim('/');

        if (!string.IsNullOrEmpty(folder) && folder.Split('/').Any(segment => segment is ".." or "."))
        {
            addError(nameof(model.Folder), S["The folder can't contain '.' or '..' segments."]);
        }

        connection.Put(new MediaFilesConnectionSettings { Folder = string.IsNullOrEmpty(folder) ? null : folder });
    }

    /// <summary>
    /// Whether a connection reads Elasticsearch.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <returns><see langword="true"/> for an Elasticsearch connection.</returns>
    public static bool IsElasticsearch(DataConnection connection)
        => string.Equals(connection?.Connector, ElasticsearchDataConnector.Name, StringComparison.OrdinalIgnoreCase);

    private static bool IsHttps(string value)
        => Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
}
