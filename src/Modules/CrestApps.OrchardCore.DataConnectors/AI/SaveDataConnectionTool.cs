using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core;
using CrestApps.OrchardCore.DataConnectors.Connectors.Files;
using CrestApps.OrchardCore.DataConnectors.Http;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.DataConnectors.Services;
using CrestApps.OrchardCore.DataConnectors.ViewModels;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.DataConnectors.AI;

/// <summary>
/// Creates or updates a data connection with the connection editor's checks. It never takes a credential: the rest is
/// saved and the result links to the page where a person enters the API key, password, token or client secret. A
/// stored credential is never sent somewhere new: the server, token endpoint, issuer and authentication type of a
/// connection that stores one can't be changed here. SQL Server connections are never created and their settings never
/// changed, since they sign in with a password or with the identity of the site's host.
/// </summary>
public sealed class SaveDataConnectionTool : DataConnectorAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveDataConnection";

    private static readonly string[] _httpArguments =
    [
        "baseUrl",
        "timeoutSeconds",
        "authenticationType",
        "apiKeyName",
        "apiKeyLocation",
        "apiKeyPrefix",
        "userName",
        "tokenUrl",
        "authority",
        "clientId",
        "scope",
        "audience",
        "grantType",
        "clientAuthentication",
    ];

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The ID of the connection to update. Leave it out to create a connection."
        },
        "connector": {
          "type": "string",
          "enum": ["HttpApi", "Elasticsearch", "MediaFiles"],
          "description": "The connector of a new connection, from the connectors listDataConnections returns. It can't be changed later."
        },
        "displayText": {
          "type": "string",
          "description": "The connection's name. Required for a new connection."
        },
        "name": {
          "type": "string",
          "description": "The technical name: a letter, then letters, digits and underscores. Made from the name when left out."
        },
        "description": {
          "type": "string",
          "description": "What the connection reaches. An empty text clears it."
        },
        "baseUrl": {
          "type": "string",
          "description": "HTTP API and Elasticsearch: the absolute http:// or https:// address of the server, without credentials in it."
        },
        "timeoutSeconds": {
          "type": "integer",
          "description": "HTTP API and Elasticsearch: how long to wait for an answer, from 1 to 3600 seconds. Defaults to 100."
        },
        "authenticationType": {
          "type": "string",
          "enum": ["None", "ApiKey", "Basic", "Bearer", "OAuth2", "OpenIdConnect"],
          "description": "HTTP API and Elasticsearch: how the connection signs in. Elasticsearch takes None, ApiKey, Basic or Bearer. The secret itself is entered on the connection's page."
        },
        "apiKeyName": {
          "type": "string",
          "description": "API key sign-in: the name of the header or query parameter that carries the key."
        },
        "apiKeyLocation": {
          "type": "string",
          "enum": ["Header", "Query"],
          "description": "API key sign-in: where the key is sent."
        },
        "apiKeyPrefix": {
          "type": "string",
          "description": "API key sign-in: text put before the key, such as 'ApiKey '."
        },
        "userName": {
          "type": "string",
          "description": "Basic sign-in, or the OAuth password grant: the user name."
        },
        "tokenUrl": {
          "type": "string",
          "description": "OAuth 2.0: the absolute https:// address of the token endpoint."
        },
        "authority": {
          "type": "string",
          "description": "OpenID Connect: the absolute https:// address of the issuer."
        },
        "clientId": {
          "type": "string",
          "description": "OAuth 2.0 and OpenID Connect: the client identifier."
        },
        "scope": {
          "type": "string",
          "description": "OAuth 2.0 and OpenID Connect: the scopes asked for, separated by spaces."
        },
        "audience": {
          "type": "string",
          "description": "OAuth 2.0 and OpenID Connect: the audience asked for, when the server needs one."
        },
        "grantType": {
          "type": "string",
          "enum": ["ClientCredentials", "Password"],
          "description": "OAuth 2.0 and OpenID Connect: the grant used to get a token."
        },
        "clientAuthentication": {
          "type": "string",
          "enum": ["BasicHeader", "RequestBody"],
          "description": "OAuth 2.0 and OpenID Connect: how the client identifies itself to the token endpoint."
        },
        "folder": {
          "type": "string",
          "description": "Media files: the media library folder the data sets read from, or empty for the whole library."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates a connection for a connector (HTTP API, Elasticsearch or media files), or updates one by id: name, description and the connector's settings. A connection's connector cannot be changed. Credentials (API key, password, bearer token, client secret) and headers are never taken: the rest is saved and the result links to the page where a person enters them. SQL Server connections are created and configured on their page only.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission RequiredPermission => DataConnectorPermissions.ManageDataConnections;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var store = services.GetRequiredService<IDataConnectionStore>();
        var connectorManager = services.GetRequiredService<IDataConnectorManager>();
        var secretManager = services.GetRequiredService<IDataConnectionSecretManager>();
        var editor = services.GetRequiredService<DataConnectionEditor>();
        var clock = services.GetRequiredService<IClock>();

        var id = ReadString(arguments, "id");
        DataConnection connection;
        var isNew = id is null;

        if (isNew)
        {
            var connectorName = ReadString(arguments, "connector");
            var descriptor = connectorName is null ? null : connectorManager.GetDescriptor(connectorName);

            if (descriptor is null)
            {
                var enabled = string.Join(", ", connectorManager.GetConnectors().Select(connector => connector.Name));

                return $"Nothing was saved: give the connector of the new connection in connector. The connectors enabled on this site are: {(enabled.Length == 0 ? "none" : enabled)}.";
            }

            if (string.Equals(descriptor.Name, DataConnectorConstants.Connectors.SqlServer, StringComparison.OrdinalIgnoreCase))
            {
                return $"Refused: a SQL Server connection signs in with a password, which a tool never takes, or with the identity of the site's host, which a tool must not grant (review finding SEC-A8). A person creates it on its page: {GetAdminPath(services, "DataConnections", "Create", descriptor.Name)}";
            }

            if (!DataConnectorToolDescriptions.EditableConnectors.Contains(descriptor.Name, StringComparer.OrdinalIgnoreCase))
            {
                return $"Refused: the connector '{descriptor.Name}' is added by another module and its settings are only known to its page. A person creates the connection there: {GetAdminPath(services, "DataConnections", "Create", descriptor.Name)}";
            }

            connection = new DataConnection
            {
                ItemId = IdGenerator.GenerateId(),
                Connector = descriptor.Name,
                CreatedUtc = clock.UtcNow,
                Author = user.Identity?.Name,
            };
        }
        else
        {
            connection = await store.FindByIdAsync(id, cancellationToken);

            if (connection is null)
            {
                return $"Nothing was saved: no data connection has the ID '{id}'. Call listDataConnections to find it.";
            }

            var connector = ReadString(arguments, "connector");

            if (connector is not null && !string.Equals(connector, connection.Connector, StringComparison.OrdinalIgnoreCase))
            {
                return $"Nothing was saved: the connection '{connection.DisplayText}' is a {connection.Connector} connection, and a connection's connector can't be changed. Create a new connection instead.";
            }
        }

        var errors = new List<string>();

        void AddError(string field, string message)
            => errors.Add(message);

        await editor.ApplyNamesAsync(connection, new DataConnectionFieldsViewModel
        {
            DisplayText = ReadString(arguments, "displayText") ?? connection.DisplayText,
            Name = ReadString(arguments, "name") ?? connection.Name,
            Description = Has(arguments, "description") ? ReadString(arguments, "description") : connection.Description,
        }, AddError);

        var givesHttp = _httpArguments.Any(name => Has(arguments, name));
        var givesFolder = Has(arguments, "folder");

        if (DataConnectorToolDescriptions.IsHttp(connection))
        {
            if (givesFolder)
            {
                return "Nothing was saved: folder is a setting of media files connections only.";
            }

            var refusal = ApplyHttp(arguments, services, connection, secretManager, editor, isNew, AddError);

            if (refusal is not null)
            {
                return refusal;
            }
        }
        else if (DataConnectorToolDescriptions.IsMediaFiles(connection))
        {
            if (givesHttp)
            {
                return "Nothing was saved: a media files connection has no server or authentication; give only its folder.";
            }

            editor.ApplyMediaFiles(connection, new MediaFilesConnectionViewModel
            {
                Folder = givesFolder ? ReadString(arguments, "folder") : connection.GetOrCreate<MediaFilesConnectionSettings>().Folder,
            }, AddError);
        }
        else if (givesHttp || givesFolder)
        {
            var reason = DataConnectorToolDescriptions.IsSqlServer(connection)
                ? "A SQL Server connection's server, database and sign-in are changed on its page only, because it signs in with a password or with the identity of the site's host (review finding SEC-A8)."
                : $"The settings of a {connection.Connector} connection are only known to its page.";

            return $"Nothing was saved: {reason} Only the name and description can be changed here. Its page: {GetAdminPath(services, "DataConnections", "Edit", connection.ItemId)}";
        }

        if (errors.Count > 0)
        {
            return Json(new
            {
                saved = false,
                errors,
            });
        }

        if (isNew)
        {
            await store.CreateAsync(connection, cancellationToken);
        }
        else
        {
            connection.ModifiedUtc = clock.UtcNow;
            await store.UpdateAsync(connection, cancellationToken);
        }

        var missing = MissingCredentials(connection, secretManager);
        var editPath = GetAdminPath(services, "DataConnections", "Edit", connection.ItemId);

        return Json(new
        {
            saved = true,
            created = isNew,
            connection = DataConnectorToolDescriptions.DescribeConnection(
                connection,
                connectorManager.GetDescriptor(connection.Connector),
                secretManager,
                await ToLocalAsync(services, connection.CreatedUtc),
                await ToLocalAsync(services, connection.ModifiedUtc),
                editPath),
            missingCredentials = missing,
            message = missing.Length == 0
                ? "The connection was saved. Call testDataConnection to check that it reaches its system."
                : $"The connection was saved without its credentials ({string.Join(", ", missing)}), so it can't sign in yet. A person enters them on the connection's page: {editPath}",
        });
    }

    private static string ApplyHttp(
        AIFunctionArguments arguments,
        IServiceProvider services,
        DataConnection connection,
        IDataConnectionSecretManager secretManager,
        DataConnectionEditor editor,
        bool isNew,
        Action<string, string> addError)
    {
        var settings = connection.GetOrCreate<HttpConnectionSettings>();
        var current = settings.Authentication ?? new HttpAuthenticationSettings();
        var isElasticsearch = DataConnectionEditor.IsElasticsearch(connection);

        // As the editor fills a new connection: an Elasticsearch API key goes in the Authorization header.
        var model = new HttpConnectionViewModel
        {
            BaseUrl = settings.BaseUrl,
            TimeoutSeconds = settings.TimeoutSeconds,
            Headers = string.Join(Environment.NewLine, (settings.Headers ?? []).Select(header => $"{header.Name}: {header.Value}")),
            AuthenticationType = current.Type,
            ApiKeyName = isElasticsearch && isNew ? "Authorization" : current.ApiKeyName,
            ApiKeyLocation = current.ApiKeyLocation,
            ApiKeyPrefix = isElasticsearch && isNew ? "ApiKey " : current.ApiKeyPrefix,
            UserName = current.UserName,
            TokenUrl = current.TokenUrl,
            Authority = current.Authority,
            ClientId = current.ClientId,
            Scope = current.Scope,
            Audience = current.Audience,
            GrantType = current.GrantType,
            ClientAuthentication = current.ClientAuthentication,
        };

        if (!TryReadInt(arguments, "timeoutSeconds", out var timeout))
        {
            return "Nothing was saved: timeoutSeconds must be a whole number of seconds.";
        }

        if (!TryReadEnum<HttpAuthenticationType>(arguments, "authenticationType", out var authenticationType)
            || !TryReadEnum<HttpApiKeyLocation>(arguments, "apiKeyLocation", out var apiKeyLocation)
            || !TryReadEnum<OAuthGrantType>(arguments, "grantType", out var grantType)
            || !TryReadEnum<OAuthClientAuthentication>(arguments, "clientAuthentication", out var clientAuthentication))
        {
            return "Nothing was saved: authenticationType, apiKeyLocation, grantType and clientAuthentication take one of the values the tool's schema lists.";
        }

        model.BaseUrl = Has(arguments, "baseUrl") ? ReadString(arguments, "baseUrl") : model.BaseUrl;
        model.TimeoutSeconds = timeout ?? model.TimeoutSeconds;
        model.AuthenticationType = authenticationType ?? model.AuthenticationType;
        model.ApiKeyName = Has(arguments, "apiKeyName") ? ReadString(arguments, "apiKeyName") : model.ApiKeyName;
        model.ApiKeyLocation = apiKeyLocation ?? model.ApiKeyLocation;
        model.ApiKeyPrefix = Has(arguments, "apiKeyPrefix") ? ReadRaw(arguments, "apiKeyPrefix") : model.ApiKeyPrefix;
        model.UserName = Has(arguments, "userName") ? ReadString(arguments, "userName") : model.UserName;
        model.TokenUrl = Has(arguments, "tokenUrl") ? ReadString(arguments, "tokenUrl") : model.TokenUrl;
        model.Authority = Has(arguments, "authority") ? ReadString(arguments, "authority") : model.Authority;
        model.ClientId = Has(arguments, "clientId") ? ReadString(arguments, "clientId") : model.ClientId;
        model.Scope = Has(arguments, "scope") ? ReadString(arguments, "scope") : model.Scope;
        model.Audience = Has(arguments, "audience") ? ReadString(arguments, "audience") : model.Audience;
        model.GrantType = grantType ?? model.GrantType;
        model.ClientAuthentication = clientAuthentication ?? model.ClientAuthentication;

        if (!isNew)
        {
            var refusal = KeepCredentialsInPlace(services, connection, secretManager, settings, current, model);

            if (refusal is not null)
            {
                return refusal;
            }
        }

        editor.ApplyHttp(connection, model, addError);

        return null;
    }

    // A stored credential, or a header that may carry one, must never be sent to a new place: another server, another
    // token endpoint or issuer, or another way of signing in, such as Basic for a password kept for the OAuth password
    // grant. Turning authentication off sends nothing, so it is allowed.
    private static string KeepCredentialsInPlace(
        IServiceProvider services,
        DataConnection connection,
        IDataConnectionSecretManager secretManager,
        HttpConnectionSettings settings,
        HttpAuthenticationSettings current,
        HttpConnectionViewModel model)
    {
        var storedSecrets = DataConnectorToolDescriptions.HttpSecrets.Where(secret => secretManager.Has(connection, secret)).ToArray();
        var hasHeaders = settings.Headers?.Count > 0;
        var editPath = GetAdminPath(services, "DataConnections", "Edit", connection.ItemId);

        if ((storedSecrets.Length > 0 || hasHeaders) && !SameHost(settings.BaseUrl, model.BaseUrl))
        {
            return $"Refused: the connection '{connection.DisplayText}' stores {Describe(storedSecrets, hasHeaders)}, which would then be sent to another server. A person changes the server on the connection's page, where the credentials can be entered again: {editPath}";
        }

        if (storedSecrets.Length == 0)
        {
            return null;
        }

        if (model.AuthenticationType != current.Type && model.AuthenticationType != HttpAuthenticationType.None)
        {
            return $"Refused: the connection '{connection.DisplayText}' stores {Describe(storedSecrets, false)}, which another way of signing in could send somewhere else. A person changes the authentication type on the connection's page: {editPath}";
        }

        if (model.GrantType != current.GrantType && secretManager.Has(connection, HttpAuthenticationSecrets.Password))
        {
            return $"Refused: the connection '{connection.DisplayText}' stores a password, which another grant could send to the token endpoint. A person changes the grant on the connection's page: {editPath}";
        }

        if (!SameHost(current.TokenUrl, model.TokenUrl) || !SameHost(current.Authority, model.Authority))
        {
            return $"Refused: the connection '{connection.DisplayText}' stores {Describe(storedSecrets, false)}, which would then be sent to another token endpoint or issuer. A person changes it on the connection's page: {editPath}";
        }

        return null;
    }

    private static string[] MissingCredentials(DataConnection connection, IDataConnectionSecretManager secretManager)
    {
        if (!DataConnectorToolDescriptions.IsHttp(connection))
        {
            return [];
        }

        var authentication = connection.GetOrCreate<HttpConnectionSettings>().Authentication ?? new HttpAuthenticationSettings();
        var missing = new List<string>();

        switch (authentication.Type)
        {
            case HttpAuthenticationType.ApiKey when !secretManager.Has(connection, HttpAuthenticationSecrets.ApiKey):
                missing.Add("API key");
                break;

            case HttpAuthenticationType.Basic when !secretManager.Has(connection, HttpAuthenticationSecrets.Password):
                missing.Add("password");
                break;

            case HttpAuthenticationType.Bearer when !secretManager.Has(connection, HttpAuthenticationSecrets.BearerToken):
                missing.Add("bearer token");
                break;

            case HttpAuthenticationType.OAuth2 or HttpAuthenticationType.OpenIdConnect:
                if (!secretManager.Has(connection, HttpAuthenticationSecrets.ClientSecret))
                {
                    missing.Add("client secret");
                }

                if (authentication.GrantType == OAuthGrantType.Password && !secretManager.Has(connection, HttpAuthenticationSecrets.Password))
                {
                    missing.Add("password");
                }

                break;
        }

        return [.. missing];
    }

    private static bool SameHost(string before, string after)
    {
        var hadAddress = Uri.TryCreate(before?.Trim(), UriKind.Absolute, out var beforeUri);
        var hasAddress = Uri.TryCreate(after?.Trim(), UriKind.Absolute, out var afterUri);

        if (!hadAddress && !hasAddress)
        {
            return string.IsNullOrWhiteSpace(before) == string.IsNullOrWhiteSpace(after) || string.IsNullOrWhiteSpace(after);
        }

        if (!hadAddress || !hasAddress)
        {
            return false;
        }

        return string.Equals(beforeUri.Scheme, afterUri.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(beforeUri.IdnHost, afterUri.IdnHost, StringComparison.OrdinalIgnoreCase)
            && beforeUri.Port == afterUri.Port;
    }

    private static string Describe(string[] storedSecrets, bool hasHeaders)
    {
        var parts = storedSecrets.Select(secret => secret switch
        {
            HttpAuthenticationSecrets.ApiKey => "an API key",
            HttpAuthenticationSecrets.Password => "a password",
            HttpAuthenticationSecrets.BearerToken => "a bearer token",
            HttpAuthenticationSecrets.ClientSecret => "a client secret",
            _ => "a credential",
        }).ToList();

        if (hasHeaders)
        {
            parts.Add("headers");
        }

        return string.Join(" and ", parts);
    }

    // The prefix keeps its spaces, such as the one after "ApiKey".
    private static string ReadRaw(AIFunctionArguments arguments, string name)
    {
        if (!arguments.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        var text = value is JsonElement { ValueKind: JsonValueKind.String } element ? element.GetString() : value.ToString();

        return string.IsNullOrEmpty(text) ? null : text;
    }
}
