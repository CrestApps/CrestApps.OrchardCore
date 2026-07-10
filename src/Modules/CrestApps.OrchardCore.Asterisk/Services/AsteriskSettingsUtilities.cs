using System.Globalization;
using CrestApps.OrchardCore.Asterisk.Models;
using Microsoft.AspNetCore.WebUtilities;

namespace CrestApps.OrchardCore.Asterisk.Services;

internal static class AsteriskSettingsUtilities
{
    public static void ApplyDefaults(AsteriskConnectionSettings settings)
    {
        settings.ApplicationName = string.IsNullOrWhiteSpace(settings.ApplicationName)
            ? AsteriskConstants.DefaultApplicationName
            : settings.ApplicationName.Trim();

        settings.TimeoutSeconds = settings.TimeoutSeconds > 0
            ? settings.TimeoutSeconds
            : AsteriskConstants.DefaultTimeoutSeconds;

        settings.BaseUrl = NormalizeBaseUrl(settings.BaseUrl);
        settings.UserName = settings.UserName?.Trim();
        settings.EndpointTemplate = settings.EndpointTemplate?.Trim();
        settings.OutboundCallerId = settings.OutboundCallerId?.Trim();
        settings.VoicemailContext = settings.VoicemailContext?.Trim();
        settings.VoicemailExtensionTemplate = settings.VoicemailExtensionTemplate?.Trim();
        settings.VoicemailPriority = settings.VoicemailPriority > 0
            ? settings.VoicemailPriority
            : 1;
    }

    public static void ApplyDefaults(AsteriskResolvedSettings settings)
    {
        settings.ApplicationName = string.IsNullOrWhiteSpace(settings.ApplicationName)
            ? AsteriskConstants.DefaultApplicationName
            : settings.ApplicationName.Trim();

        settings.TimeoutSeconds = settings.TimeoutSeconds > 0
            ? settings.TimeoutSeconds
            : AsteriskConstants.DefaultTimeoutSeconds;

        settings.BaseUrl = NormalizeBaseUrl(settings.BaseUrl);
        settings.UserName = settings.UserName?.Trim();
        settings.Password = settings.Password?.Trim();
        settings.EndpointTemplate = settings.EndpointTemplate?.Trim();
        settings.OutboundCallerId = settings.OutboundCallerId?.Trim();
        settings.VoicemailContext = settings.VoicemailContext?.Trim();
        settings.VoicemailExtensionTemplate = settings.VoicemailExtensionTemplate?.Trim();
        settings.VoicemailPriority = settings.VoicemailPriority > 0
            ? settings.VoicemailPriority
            : 1;
    }

    public static bool HasRequiredConfiguration(AsteriskConnectionSettings settings, string password)
        => !string.IsNullOrWhiteSpace(settings.BaseUrl) &&
            !string.IsNullOrWhiteSpace(settings.UserName) &&
            !string.IsNullOrWhiteSpace(password) &&
            !string.IsNullOrWhiteSpace(settings.ApplicationName);

    public static bool HasRequiredConfiguration(AsteriskResolvedSettings settings)
        => settings is not null &&
            settings.IsEnabled &&
            !string.IsNullOrWhiteSpace(settings.BaseUrl) &&
            !string.IsNullOrWhiteSpace(settings.UserName) &&
            !string.IsNullOrWhiteSpace(settings.Password) &&
            !string.IsNullOrWhiteSpace(settings.ApplicationName);

    public static string ResolveEndpoint(string endpointTemplate, string destination)
    {
        if (string.IsNullOrWhiteSpace(destination))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(endpointTemplate))
        {
            return destination.Trim();
        }

        return endpointTemplate.Replace("{number}", destination.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsImmediateConnectionEndpoint(string endpoint)
        => !string.IsNullOrWhiteSpace(endpoint) &&
            endpoint.StartsWith("Local/", StringComparison.OrdinalIgnoreCase);

    public static bool TryGetImmediateConnectionRoute(string endpoint, out string extension, out string context)
    {
        extension = null;
        context = null;

        if (!IsImmediateConnectionEndpoint(endpoint))
        {
            return false;
        }

        var route = endpoint.Substring("Local/".Length);
        var atIndex = route.IndexOf('@');

        if (atIndex <= 0 || atIndex == route.Length - 1)
        {
            return false;
        }

        var resolvedExtension = route.Substring(0, atIndex).Trim();
        var resolvedContext = route.Substring(atIndex + 1).Trim();
        var separatorIndex = resolvedContext.IndexOfAny(['/', ';']);

        if (separatorIndex >= 0)
        {
            resolvedContext = resolvedContext.Substring(0, separatorIndex).Trim();
        }

        if (string.IsNullOrWhiteSpace(resolvedExtension) || string.IsNullOrWhiteSpace(resolvedContext))
        {
            return false;
        }

        extension = resolvedExtension;
        context = resolvedContext;

        return true;
    }

    public static string NormalizeBaseUrl(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }

        if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri))
        {
            return baseUrl.Trim();
        }

        var builder = new UriBuilder(uri);
        var path = string.IsNullOrWhiteSpace(builder.Path) || builder.Path == "/"
            ? "/ari/"
            : builder.Path;

        if (path[path.Length - 1] != '/')
        {
            path += "/";
        }

        builder.Path = path;

        return builder.Uri.ToString();
    }

    public static Uri CreateEventsUri(AsteriskResolvedSettings settings)
    {
        if (settings is null || string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            return null;
        }

        var baseUri = new Uri(NormalizeBaseUrl(settings.BaseUrl), UriKind.Absolute);
        var builder = new UriBuilder(baseUri)
        {
            Scheme = string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                ? "wss"
                : "ws",
            Path = $"{baseUri.AbsolutePath.TrimEnd('/')}/events",
        };

        builder.Query = QueryHelpers.AddQueryString(
            string.Empty,
            new Dictionary<string, string>
            {
                ["app"] = settings.ApplicationName,
                ["api_key"] = $"{settings.UserName}:{settings.Password}",
            }).TrimStart('?');

        return builder.Uri;
    }

    public static string ToInvariantString(int value)
        => value.ToString(CultureInfo.InvariantCulture);

    public static bool HasVoicemailConfiguration(AsteriskConnectionSettings settings)
        => settings is not null &&
            !string.IsNullOrWhiteSpace(settings.VoicemailContext) &&
            !string.IsNullOrWhiteSpace(settings.VoicemailExtensionTemplate) &&
            settings.VoicemailPriority > 0;

    public static bool HasVoicemailConfiguration(AsteriskResolvedSettings settings)
        => settings is not null &&
            !string.IsNullOrWhiteSpace(settings.VoicemailContext) &&
            !string.IsNullOrWhiteSpace(settings.VoicemailExtensionTemplate) &&
            settings.VoicemailPriority > 0;
}
