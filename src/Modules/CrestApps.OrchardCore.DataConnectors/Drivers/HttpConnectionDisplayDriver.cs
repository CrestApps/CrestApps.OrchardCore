using CrestApps.Core;
using CrestApps.OrchardCore.DataConnectors.Connectors.Elasticsearch;
using CrestApps.OrchardCore.DataConnectors.Connectors.Files;
using CrestApps.OrchardCore.DataConnectors.Connectors.Http;
using CrestApps.OrchardCore.DataConnectors.Http;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.DataConnectors.Services;
using CrestApps.OrchardCore.DataConnectors.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.DataConnectors.Drivers;

/// <summary>
/// Edits the server, authentication and headers of the HTTP-based connections: HTTP APIs and Elasticsearch. Secrets
/// are write-only: an empty box keeps the stored secret.
/// </summary>
internal sealed class HttpConnectionDisplayDriver : DisplayDriver<DataConnection>
{
    private readonly IDataConnectionSecretManager _secretManager;

    internal readonly IStringLocalizer S;

    public HttpConnectionDisplayDriver(
        IDataConnectionSecretManager secretManager,
        IStringLocalizer<HttpConnectionDisplayDriver> stringLocalizer)
    {
        _secretManager = secretManager;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(DataConnection connection, BuildEditorContext context)
    {
        if (!Applies(connection))
        {
            return null;
        }

        return Initialize<HttpConnectionViewModel>("HttpConnection_Edit", model =>
        {
            var settings = connection.GetOrCreate<HttpConnectionSettings>();
            var authentication = settings.Authentication ?? new HttpAuthenticationSettings();
            var isElasticsearch = IsElasticsearch(connection);

            model.BaseUrl = settings.BaseUrl;
            model.TimeoutSeconds = settings.TimeoutSeconds;
            model.Headers = string.Join(Environment.NewLine, (settings.Headers ?? []).Select(header => $"{header.Name}: {header.Value}"));
            model.AuthenticationType = authentication.Type;
            model.ApiKeyName = isElasticsearch && context.IsNew ? "Authorization" : authentication.ApiKeyName;
            model.ApiKeyLocation = authentication.ApiKeyLocation;
            model.ApiKeyPrefix = isElasticsearch && context.IsNew ? "ApiKey " : authentication.ApiKeyPrefix;
            model.UserName = authentication.UserName;
            model.TokenUrl = authentication.TokenUrl;
            model.Authority = authentication.Authority;
            model.ClientId = authentication.ClientId;
            model.Scope = authentication.Scope;
            model.Audience = authentication.Audience;
            model.GrantType = authentication.GrantType;
            model.ClientAuthentication = authentication.ClientAuthentication;
            model.HasApiKey = _secretManager.Has(connection, HttpAuthenticationSecrets.ApiKey);
            model.HasPassword = _secretManager.Has(connection, HttpAuthenticationSecrets.Password);
            model.HasBearerToken = _secretManager.Has(connection, HttpAuthenticationSecrets.BearerToken);
            model.HasClientSecret = _secretManager.Has(connection, HttpAuthenticationSecrets.ClientSecret);
            model.IsElasticsearch = isElasticsearch;
            model.AuthenticationTypes = GetAuthenticationTypes(isElasticsearch);
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(DataConnection connection, UpdateEditorContext context)
    {
        if (!Applies(connection))
        {
            return null;
        }

        var model = new HttpConnectionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (!Uri.TryCreate(model.BaseUrl?.Trim(), UriKind.Absolute, out var baseUri) || baseUri.Scheme is not ("http" or "https"))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.BaseUrl), S["The URL must be an absolute http:// or https:// address."]);
        }
        else if (!string.IsNullOrEmpty(baseUri.UserInfo))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.BaseUrl), S["Don't put credentials in the URL; use the authentication settings."]);
        }

        if (model.TimeoutSeconds is < 1 or > 3_600)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.TimeoutSeconds), S["The timeout must be between 1 and 3600 seconds."]);
        }

        var isElasticsearch = IsElasticsearch(connection);

        if (isElasticsearch && model.AuthenticationType is HttpAuthenticationType.OAuth2 or HttpAuthenticationType.OpenIdConnect)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.AuthenticationType), S["Elasticsearch connections use no authentication, Basic, an API key or a bearer token."]);
        }

        switch (model.AuthenticationType)
        {
            case HttpAuthenticationType.ApiKey when string.IsNullOrWhiteSpace(model.ApiKeyName):
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.ApiKeyName), S["The name of the API key's header or parameter is required."]);
                break;

            case HttpAuthenticationType.Basic when string.IsNullOrWhiteSpace(model.UserName):
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.UserName), S["The user name is required."]);
                break;

            case HttpAuthenticationType.OAuth2 when !IsHttps(model.TokenUrl):
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.TokenUrl), S["The token endpoint must be an absolute https:// URL."]);
                break;

            case HttpAuthenticationType.OpenIdConnect when !IsHttps(model.Authority):
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Authority), S["The issuer must be an absolute https:// URL."]);
                break;
        }

        if (model.AuthenticationType is HttpAuthenticationType.OAuth2 or HttpAuthenticationType.OpenIdConnect)
        {
            if (string.IsNullOrWhiteSpace(model.ClientId))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.ClientId), S["The client identifier is required."]);
            }

            if (model.GrantType == OAuthGrantType.Password && string.IsNullOrWhiteSpace(model.UserName))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.UserName), S["The password grant needs a user name."]);
            }
        }

        var headers = new List<HttpHeaderSetting>();

        foreach (var line in (model.Headers ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = line.IndexOf(':');

            if (separator <= 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Headers), S["Write each header as Name: value, one per line."]);

                break;
            }

            var name = line[..separator].Trim();

            if (string.Equals(name, "Host", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Headers), S["The {0} header can't be set.", name]);

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

        _secretManager.Set(connection, HttpAuthenticationSecrets.ApiKey, model.ApiKey?.Trim());
        _secretManager.Set(connection, HttpAuthenticationSecrets.Password, model.Password);
        _secretManager.Set(connection, HttpAuthenticationSecrets.BearerToken, model.BearerToken?.Trim());
        _secretManager.Set(connection, HttpAuthenticationSecrets.ClientSecret, model.ClientSecret);

        if (model.AuthenticationType == HttpAuthenticationType.ApiKey && !_secretManager.Has(connection, HttpAuthenticationSecrets.ApiKey))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ApiKey), S["The API key is required."]);
        }

        if (model.AuthenticationType == HttpAuthenticationType.Bearer && !_secretManager.Has(connection, HttpAuthenticationSecrets.BearerToken))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.BearerToken), S["The bearer token is required."]);
        }

        return Edit(connection, context);
    }

    private static bool Applies(DataConnection connection)
        => string.Equals(connection.Connector, HttpApiDataConnector.Name, StringComparison.OrdinalIgnoreCase) || IsElasticsearch(connection);

    private static bool IsElasticsearch(DataConnection connection)
        => string.Equals(connection.Connector, ElasticsearchDataConnector.Name, StringComparison.OrdinalIgnoreCase);

    private static bool IsHttps(string value)
        => Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    private SelectListItem[] GetAuthenticationTypes(bool isElasticsearch)
    {
        var types = new List<SelectListItem>
        {
            new(S["None"], nameof(HttpAuthenticationType.None)),
            new(S["API key"], nameof(HttpAuthenticationType.ApiKey)),
            new(S["Basic (user name and password)"], nameof(HttpAuthenticationType.Basic)),
            new(S["Bearer token"], nameof(HttpAuthenticationType.Bearer)),
        };

        if (!isElasticsearch)
        {
            types.Add(new(S["OAuth 2.0 (token endpoint)"], nameof(HttpAuthenticationType.OAuth2)));
            types.Add(new(S["OpenID Connect (discovery)"], nameof(HttpAuthenticationType.OpenIdConnect)));
        }

        return [.. types];
    }
}

/// <summary>
/// Edits the folder of a media files connection.
/// </summary>
internal sealed class MediaFilesConnectionDisplayDriver : DisplayDriver<DataConnection>
{
    internal readonly IStringLocalizer S;

    public MediaFilesConnectionDisplayDriver(IStringLocalizer<MediaFilesConnectionDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(DataConnection connection, BuildEditorContext context)
    {
        if (!Applies(connection))
        {
            return null;
        }

        return Initialize<MediaFilesConnectionViewModel>("MediaFilesConnection_Edit", model =>
        {
            model.Folder = connection.GetOrCreate<MediaFilesConnectionSettings>().Folder;
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(DataConnection connection, UpdateEditorContext context)
    {
        if (!Applies(connection))
        {
            return null;
        }

        var model = new MediaFilesConnectionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var folder = model.Folder?.Trim().Replace('\\', '/').Trim('/');

        if (!string.IsNullOrEmpty(folder) && folder.Split('/').Any(segment => segment is ".." or "."))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Folder), S["The folder can't contain '.' or '..' segments."]);
        }

        connection.Put(new MediaFilesConnectionSettings { Folder = string.IsNullOrEmpty(folder) ? null : folder });

        return Edit(connection, context);
    }

    private static bool Applies(DataConnection connection)
        => string.Equals(connection.Connector, MediaFilesDataConnector.Name, StringComparison.OrdinalIgnoreCase);
}
