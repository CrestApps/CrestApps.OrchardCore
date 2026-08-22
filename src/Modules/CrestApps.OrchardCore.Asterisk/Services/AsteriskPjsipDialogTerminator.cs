using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Asterisk.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Asterisk.Services;

internal sealed class AsteriskPjsipDialogTerminator : IAsteriskPjsipDialogTerminator
{
    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DefaultAsteriskOptions _defaultOptions;
    private readonly ShellSettings _shellSettings;
    private readonly ILogger<AsteriskPjsipDialogTerminator> _logger;

    public AsteriskPjsipDialogTerminator(
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        IHttpClientFactory httpClientFactory,
        IOptions<DefaultAsteriskOptions> defaultOptions,
        ShellSettings shellSettings,
        ILogger<AsteriskPjsipDialogTerminator> logger)
    {
        _siteService = siteService;
        _dataProtectionProvider = dataProtectionProvider;
        _httpClientFactory = httpClientFactory;
        _defaultOptions = defaultOptions.Value;
        _shellSettings = shellSettings;
        _logger = logger;
    }

    public async Task TerminateAsync(
        string authorizationUser,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(authorizationUser))
        {
            return;
        }

        var settings = ResolveSettings();

        if (!AsteriskSettingsUtilities.HasRequiredConfiguration(settings))
        {
            return;
        }

        try
        {
            var client = CreateClient(settings);
            using var response = await client.GetAsync("channels", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            var channels = await response.Content.ReadFromJsonAsync<JsonElement[]>(cancellationToken);

            foreach (var channel in channels ?? [])
            {
                var id = ReadString(channel, "id");

                if (string.IsNullOrWhiteSpace(id) || !BelongsToAuthorizationUser(channel, authorizationUser))
                {
                    continue;
                }

                var url = QueryHelpers.AddQueryString("channels/" + Uri.EscapeDataString(id), "reason", reason ?? "revoked");
                using var deleteResponse = await client.DeleteAsync(url, cancellationToken);

                if (!deleteResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Asterisk did not terminate browser SIP channel {ChannelId} for a revoked PJSIP credential. Status code: {StatusCode}.",
                        id.SanitizeLogValue(),
                        deleteResponse.StatusCode);
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "Asterisk browser SIP dialog teardown did not complete for a revoked credential.");
        }
    }

    private AsteriskResolvedSettings ResolveSettings()
    {
        var tenantSettings = _siteService.GetSettings<AsteriskSettings>();

        if (tenantSettings.IsEnabled)
        {
            var resolved = new AsteriskResolvedSettings
            {
                IsEnabled = tenantSettings.IsEnabled,
                BaseUrl = tenantSettings.BaseUrl,
                UserName = tenantSettings.UserName,
                Password = Unprotect(tenantSettings.Password),
                ApplicationName = tenantSettings.ApplicationName,
            };

            AsteriskSettingsUtilities.ApplyDefaults(resolved);

            return resolved;
        }

        return new AsteriskResolvedSettings
        {
            IsEnabled = _defaultOptions.IsEnabled,
            BaseUrl = _defaultOptions.BaseUrl,
            UserName = _defaultOptions.UserName,
            Password = _defaultOptions.Password,
            ApplicationName = AsteriskSettingsUtilities.BuildHostDefaultApplicationName(_defaultOptions.ApplicationName, _shellSettings.Name),
        };
    }

    private HttpClient CreateClient(AsteriskResolvedSettings settings)
    {
        var client = _httpClientFactory.CreateClient(AsteriskConstants.HttpClientName);
        client.BaseAddress = new Uri(AsteriskSettingsUtilities.NormalizeBaseUrl(settings.BaseUrl), UriKind.Absolute);
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.UserName}:{settings.Password}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);

        return client;
    }

    private static bool BelongsToAuthorizationUser(
        JsonElement channel,
        string authorizationUser)
    {
        var name = ReadString(channel, "name");
        var callerNumber = ReadNestedString(channel, "caller", "number");
        var connectedNumber = ReadNestedString(channel, "connected", "number");

        return ContainsAuthorizationUser(name, authorizationUser) ||
            ContainsAuthorizationUser(callerNumber, authorizationUser) ||
            ContainsAuthorizationUser(connectedNumber, authorizationUser);
    }

    private static bool ContainsAuthorizationUser(
        string value,
        string authorizationUser)
        => !string.IsNullOrWhiteSpace(value) && value.Contains(authorizationUser, StringComparison.OrdinalIgnoreCase);

    private static string ReadNestedString(
        JsonElement element,
        string propertyName,
        string nestedPropertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var nested) ||
            nested.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return ReadString(nested, nestedPropertyName);
    }

    private static string ReadString(
        JsonElement element,
        string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private string Unprotect(string protectedValue)
    {
        if (string.IsNullOrWhiteSpace(protectedValue))
        {
            return null;
        }

        try
        {
            return _dataProtectionProvider.CreateProtector(AsteriskConstants.ProtectorName).Unprotect(protectedValue);
        }
        catch
        {
            return null;
        }
    }
}
