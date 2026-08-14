using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.AI.Tooling.Instances;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Tools.Drivers;

/// <summary>
/// Display driver that captures the settings specific to the built-in HTTP API request tool instance
/// source, such as the base URL, HTTP method, static headers, timeout, and the authentication strategy
/// with its dependent credential fields.
/// </summary>
internal sealed class HttpApiRequestToolInstanceDisplayDriver : DisplayDriver<AIToolInstance>
{
    private static readonly string[] _httpMethods = ["GET", "POST", "PUT", "PATCH", "DELETE"];

    private readonly IDataProtectionProvider _dataProtectionProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpApiRequestToolInstanceDisplayDriver"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The data protection provider used to protect stored credentials.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public HttpApiRequestToolInstanceDisplayDriver(
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<HttpApiRequestToolInstanceDisplayDriver> stringLocalizer)
    {
        _dataProtectionProvider = dataProtectionProvider;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIToolInstance instance, BuildEditorContext context)
    {
        if (!IsHttpApiRequestSource(instance))
        {
            return null;
        }

        return Initialize<HttpApiRequestToolInstanceViewModel>("HttpApiRequestToolInstance_Edit", model =>
        {
            var settings = instance.GetOrCreate<HttpApiRequestToolSettings>();

            model.BaseUrl = settings.BaseUrl;
            model.HttpMethod = string.IsNullOrEmpty(settings.HttpMethod) ? "GET" : settings.HttpMethod;
            model.TimeoutSeconds = settings.TimeoutSeconds;
            model.DefaultHeaders = SerializeHeaders(settings.DefaultHeaders);
            model.AllowModelProvidedPath = settings.AllowModelProvidedPath;
            model.AllowModelProvidedQuery = settings.AllowModelProvidedQuery;
            model.AllowModelProvidedBody = settings.AllowModelProvidedBody;
            model.AuthenticationType = settings.AuthenticationType;
            model.ApiKeyHeaderName = string.IsNullOrEmpty(settings.ApiKeyHeaderName) ? "X-Api-Key" : settings.ApiKeyHeaderName;
            model.Username = settings.Username;
            model.TokenEndpoint = settings.TokenEndpoint;
            model.ClientId = settings.ClientId;
            model.Scope = settings.Scope;
            model.HasApiKey = !string.IsNullOrEmpty(settings.ApiKey);
            model.HasBearerToken = !string.IsNullOrEmpty(settings.BearerToken);
            model.HasPassword = !string.IsNullOrEmpty(settings.Password);
            model.HasClientSecret = !string.IsNullOrEmpty(settings.ClientSecret);
            model.HttpMethods = GetHttpMethods();
            model.AuthenticationTypes = GetAuthenticationTypes();
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIToolInstance instance, UpdateEditorContext context)
    {
        if (!IsHttpApiRequestSource(instance))
        {
            return null;
        }

        var model = new HttpApiRequestToolInstanceViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.BaseUrl))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.BaseUrl), S["The base URL is required."]);
        }
        else if (!Uri.TryCreate(model.BaseUrl.Trim(), UriKind.Absolute, out var baseUrl) ||
            (baseUrl.Scheme != Uri.UriSchemeHttp && baseUrl.Scheme != Uri.UriSchemeHttps))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.BaseUrl), S["The base URL must be an absolute HTTP or HTTPS URL."]);
        }

        if (model.TimeoutSeconds.HasValue && model.TimeoutSeconds.Value <= 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.TimeoutSeconds), S["The timeout must be greater than zero."]);
        }

        if (!TryParseHeaders(model.DefaultHeaders, out var headers))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DefaultHeaders), S["The default headers must be a valid JSON object of header names and values."]);
        }

        var existing = instance.GetOrCreate<HttpApiRequestToolSettings>();
        var protector = _dataProtectionProvider.CreateProtector(HttpApiRequestToolConstants.DataProtectionPurpose);

        var settings = new HttpApiRequestToolSettings
        {
            BaseUrl = model.BaseUrl?.Trim(),
            HttpMethod = string.IsNullOrWhiteSpace(model.HttpMethod) ? "GET" : model.HttpMethod.Trim().ToUpperInvariant(),
            TimeoutSeconds = model.TimeoutSeconds,
            DefaultHeaders = headers,
            AllowModelProvidedPath = model.AllowModelProvidedPath,
            AllowModelProvidedQuery = model.AllowModelProvidedQuery,
            AllowModelProvidedBody = model.AllowModelProvidedBody,
            AuthenticationType = model.AuthenticationType,
        };

        switch (model.AuthenticationType)
        {
            case HttpApiRequestAuthenticationType.ApiKey:
                settings.ApiKeyHeaderName = string.IsNullOrWhiteSpace(model.ApiKeyHeaderName)
                    ? "X-Api-Key"
                    : model.ApiKeyHeaderName.Trim();
                settings.ApiKey = ProtectOrReuse(model.ApiKey, existing.ApiKey, protector);

                if (string.IsNullOrEmpty(settings.ApiKey))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.ApiKey), S["The API key is required."]);
                }

                break;
            case HttpApiRequestAuthenticationType.Bearer:
                settings.BearerToken = ProtectOrReuse(model.BearerToken, existing.BearerToken, protector);

                if (string.IsNullOrEmpty(settings.BearerToken))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.BearerToken), S["The bearer token is required."]);
                }

                break;
            case HttpApiRequestAuthenticationType.Basic:
                settings.Username = model.Username?.Trim();
                settings.Password = ProtectOrReuse(model.Password, existing.Password, protector);

                if (string.IsNullOrEmpty(settings.Username))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.Username), S["The username is required."]);
                }

                if (string.IsNullOrEmpty(settings.Password))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.Password), S["The password is required."]);
                }

                break;
            case HttpApiRequestAuthenticationType.OAuth2:
                settings.TokenEndpoint = model.TokenEndpoint?.Trim();
                settings.ClientId = model.ClientId?.Trim();
                settings.ClientSecret = ProtectOrReuse(model.ClientSecret, existing.ClientSecret, protector);
                settings.Username = model.Username?.Trim();
                settings.Password = ProtectOrReuse(model.Password, existing.Password, protector);
                settings.Scope = model.Scope?.Trim();

                if (string.IsNullOrEmpty(settings.TokenEndpoint))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.TokenEndpoint), S["The token endpoint is required."]);
                }

                if (string.IsNullOrEmpty(settings.ClientId))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.ClientId), S["The client id is required."]);
                }

                break;
        }

        instance.Put(settings);

        return Edit(instance, context);
    }

    private static bool IsHttpApiRequestSource(AIToolInstance instance)
        => string.Equals(instance.Source, HttpApiRequestToolConstants.SourceName, StringComparison.OrdinalIgnoreCase);

    private static string ProtectOrReuse(string newValue, string existingValue, IDataProtector protector)
        => string.IsNullOrWhiteSpace(newValue) ? existingValue : protector.Protect(newValue.Trim());

    private static string SerializeHeaders(Dictionary<string, string> headers)
    {
        if (headers is null || headers.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(headers, JsonSerializerOptions.Web);
    }

    private static bool TryParseHeaders(string value, out Dictionary<string, string> headers)
    {
        headers = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(value);

            if (parsed is null || parsed.Count == 0)
            {
                return true;
            }

            headers = parsed;

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static IEnumerable<SelectListItem> GetHttpMethods()
    {
        foreach (var method in _httpMethods)
        {
            yield return new SelectListItem(method, method);
        }
    }

    private IEnumerable<SelectListItem> GetAuthenticationTypes() =>
    [
        new SelectListItem(S["None"], nameof(HttpApiRequestAuthenticationType.None)),
        new SelectListItem(S["API Key"], nameof(HttpApiRequestAuthenticationType.ApiKey)),
        new SelectListItem(S["Bearer Token"], nameof(HttpApiRequestAuthenticationType.Bearer)),
        new SelectListItem(S["Basic"], nameof(HttpApiRequestAuthenticationType.Basic)),
        new SelectListItem(S["OAuth 2.0"], nameof(HttpApiRequestAuthenticationType.OAuth2)),
    ];
}
