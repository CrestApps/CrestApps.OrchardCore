using CrestApps.Core;
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
    private readonly DataConnectionEditor _editor;

    internal readonly IStringLocalizer S;

    public HttpConnectionDisplayDriver(
        IDataConnectionSecretManager secretManager,
        DataConnectionEditor editor,
        IStringLocalizer<HttpConnectionDisplayDriver> stringLocalizer)
    {
        _secretManager = secretManager;
        _editor = editor;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(DataConnection connection, BuildEditorContext context)
    {
        if (!Applies(connection))
        {
            return null;
        }

        // The connection's own settings are two cards: where it connects, and how it signs in. Both are copies of the
        // connection's values, so the fields keep posting under the same prefix.
        return Combine(
            Initialize<HttpConnectionViewModel>("HttpConnection_Edit", model => FillValues(model, connection, context))
                .Location(l => l.Zone("Content", "5").Card(S["Connection"], "20")),
            Initialize<HttpConnectionViewModel>("HttpConnectionAuthentication_Edit", model => FillValues(model, connection, context))
                .Location(l => l.Zone("Content", "6").Card(S["Authentication"], "30")));
    }

    private void FillValues(HttpConnectionViewModel model, DataConnection connection, BuildEditorContext context)
    {
            var settings = connection.GetOrCreate<HttpConnectionSettings>();
            var authentication = settings.Authentication ?? new HttpAuthenticationSettings();
            var isElasticsearch = DataConnectionEditor.IsElasticsearch(connection);

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
    }

    public override async Task<IDisplayResult> UpdateAsync(DataConnection connection, UpdateEditorContext context)
    {
        if (!Applies(connection))
        {
            return null;
        }

        var model = new HttpConnectionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        _editor.ApplyHttp(connection, model, (key, message) => context.Updater.ModelState.AddModelError(Prefix, key, message));

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
        => string.Equals(connection.Connector, HttpApiDataConnector.Name, StringComparison.OrdinalIgnoreCase) || DataConnectionEditor.IsElasticsearch(connection);

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
    private readonly DataConnectionEditor _editor;

    internal readonly IStringLocalizer S;

    public MediaFilesConnectionDisplayDriver(
        DataConnectionEditor editor,
        IStringLocalizer<MediaFilesConnectionDisplayDriver> stringLocalizer)
    {
        _editor = editor;
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
        }).Location(l => l.Zone("Content", "5").Card(S["Connection"], "20"));
    }

    public override async Task<IDisplayResult> UpdateAsync(DataConnection connection, UpdateEditorContext context)
    {
        if (!Applies(connection))
        {
            return null;
        }

        var model = new MediaFilesConnectionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        _editor.ApplyMediaFiles(connection, model, (key, message) => context.Updater.ModelState.AddModelError(Prefix, key, message));

        return Edit(connection, context);
    }

    private static bool Applies(DataConnection connection)
        => string.Equals(connection.Connector, MediaFilesDataConnector.Name, StringComparison.OrdinalIgnoreCase);
}
