using System.Globalization;
using CrestApps.OrchardCore.Asterisk.Models;
using Microsoft.AspNetCore.WebUtilities;

namespace CrestApps.OrchardCore.Asterisk.Services;

internal static class AsteriskSettingsUtilities
{
    public static void ApplyDefaults(AsteriskConnectionSettings settings)
    {
        settings.ApplicationName = settings.ApplicationName?.Trim();

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
        settings.WebSocketUrl = settings.WebSocketUrl?.Trim();
        settings.SipDomain = settings.SipDomain?.Trim();
        settings.TurnUrls = settings.TurnUrls?.Trim();
        settings.TurnSharedSecret = settings.TurnSharedSecret?.Trim();
        settings.IceTransportPolicy = string.IsNullOrWhiteSpace(settings.IceTransportPolicy)
            ? AsteriskConstants.DefaultIceTransportPolicy
            : settings.IceTransportPolicy.Trim();
        settings.WebRtcCodecs = string.IsNullOrWhiteSpace(settings.WebRtcCodecs)
            ? AsteriskConstants.DefaultWebRtcCodecs
            : settings.WebRtcCodecs.Trim();
        settings.PjsipCredentialLifetimeMinutes = settings.PjsipCredentialLifetimeMinutes > 0
            ? settings.PjsipCredentialLifetimeMinutes
            : AsteriskConstants.DefaultPjsipCredentialLifetimeMinutes;
        settings.PjsipContactExpirationSeconds = settings.PjsipContactExpirationSeconds > 0
            ? settings.PjsipContactExpirationSeconds
            : AsteriskConstants.DefaultPjsipContactExpirationSeconds;
        settings.PjsipRealtimeProviderInvariantName = settings.PjsipRealtimeProviderInvariantName?.Trim();
        settings.PjsipRealtimeConnectionString = settings.PjsipRealtimeConnectionString?.Trim();
        settings.PjsipRealtimeTablePrefix = settings.PjsipRealtimeTablePrefix?.Trim();
    }

    public static void ApplyDefaults(AsteriskResolvedSettings settings)
    {
        settings.ApplicationName = settings.ApplicationName?.Trim();

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
        settings.WebSocketUrl = settings.WebSocketUrl?.Trim();
        settings.SipDomain = settings.SipDomain?.Trim();
        settings.TurnUrls = settings.TurnUrls?.Trim();
        settings.TurnSharedSecret = settings.TurnSharedSecret?.Trim();
        settings.IceTransportPolicy = string.IsNullOrWhiteSpace(settings.IceTransportPolicy)
            ? AsteriskConstants.DefaultIceTransportPolicy
            : settings.IceTransportPolicy.Trim();
        settings.WebRtcCodecs = string.IsNullOrWhiteSpace(settings.WebRtcCodecs)
            ? AsteriskConstants.DefaultWebRtcCodecs
            : settings.WebRtcCodecs.Trim();
        settings.PjsipCredentialLifetimeMinutes = settings.PjsipCredentialLifetimeMinutes > 0
            ? settings.PjsipCredentialLifetimeMinutes
            : AsteriskConstants.DefaultPjsipCredentialLifetimeMinutes;
        settings.PjsipContactExpirationSeconds = settings.PjsipContactExpirationSeconds > 0
            ? settings.PjsipContactExpirationSeconds
            : AsteriskConstants.DefaultPjsipContactExpirationSeconds;
        settings.PjsipRealtimeProviderInvariantName = settings.PjsipRealtimeProviderInvariantName?.Trim();
        settings.PjsipRealtimeConnectionString = settings.PjsipRealtimeConnectionString?.Trim();
        settings.PjsipRealtimeTablePrefix = settings.PjsipRealtimeTablePrefix?.Trim();
    }

    public static bool HasRequiredConfiguration(AsteriskConnectionSettings settings, string password)
        => !string.IsNullOrWhiteSpace(settings.BaseUrl) &&
            !string.IsNullOrWhiteSpace(settings.UserName) &&
            !string.IsNullOrWhiteSpace(password) &&
            !string.IsNullOrWhiteSpace(settings.ApplicationName);

    /// <summary>
    /// Determines whether <paramref name="resolved"/> collides with the host default ARI application.
    /// </summary>
    /// <param name="resolved">The tenant's resolved Asterisk settings to check.</param>
    /// <param name="defaultOptions">The host default Asterisk options to compare against.</param>
    public static bool CollidesWithHostDefaultApplication(
        AsteriskResolvedSettings resolved,
        DefaultAsteriskOptions defaultOptions)
    {
        if (resolved is null)
        {
            return false;
        }

        return CollidesWithHostDefaultApplication(resolved.BaseUrl, resolved.ApplicationName, defaultOptions);
    }

    /// <summary>
    /// Determines whether <paramref name="baseUrl"/> and <paramref name="applicationName"/> collide with
    /// the host default ARI application, normalizing <paramref name="baseUrl"/> internally before the
    /// comparison. A non-default tenant that resolves to the same ARI application on the same server as
    /// the host default connection would cross-deliver Stasis events with the default shell's listener;
    /// the tenant must configure a unique application name instead. A blank application starts no listener
    /// and the host default must be enabled for a collision to exist, so neither is treated as a collision.
    /// </summary>
    /// <param name="baseUrl">The ARI base URL to check; normalized internally before comparison.</param>
    /// <param name="applicationName">The Stasis application name to check.</param>
    /// <param name="defaultOptions">The host default Asterisk options to compare against.</param>
    public static bool CollidesWithHostDefaultApplication(
        string baseUrl,
        string applicationName,
        DefaultAsteriskOptions defaultOptions)
    {
        if (defaultOptions is null ||
            !defaultOptions.IsEnabled ||
            string.IsNullOrWhiteSpace(applicationName))
        {
            return false;
        }

        return string.Equals(
                NormalizeBaseUrl(baseUrl),
                NormalizeBaseUrl(defaultOptions.BaseUrl),
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                applicationName.Trim(),
                defaultOptions.ApplicationName?.Trim(),
                StringComparison.OrdinalIgnoreCase);
    }

    public static bool HasRequiredConfiguration(AsteriskResolvedSettings settings)
        => settings is not null &&
            settings.IsEnabled &&
            !string.IsNullOrWhiteSpace(settings.BaseUrl) &&
            !string.IsNullOrWhiteSpace(settings.UserName) &&
            !string.IsNullOrWhiteSpace(settings.Password) &&
            !string.IsNullOrWhiteSpace(settings.ApplicationName);

    public static bool HasRequiredWebRtcConfiguration(AsteriskConnectionSettings settings)
        => settings is not null &&
            (settings is not AsteriskSettings tenantSettings || tenantSettings.IsEnabled) &&
            (settings is not DefaultAsteriskOptions defaultOptions || defaultOptions.IsEnabled) &&
            !string.IsNullOrWhiteSpace(settings.WebSocketUrl) &&
            Uri.TryCreate(settings.WebSocketUrl, UriKind.Absolute, out var webSocketUri) &&
            string.Equals(webSocketUri.Scheme, "wss", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(settings.SipDomain) &&
            ParseDelimitedValues(settings.WebRtcCodecs).Count > 0 &&
            settings.PjsipCredentialLifetimeMinutes > 0 &&
            settings.PjsipContactExpirationSeconds > 0 &&
            !string.IsNullOrWhiteSpace(settings.PjsipRealtimeProviderInvariantName) &&
            !string.IsNullOrWhiteSpace(settings.PjsipRealtimeConnectionString);

    public static bool HasRequiredWebRtcConfiguration(AsteriskResolvedSettings settings)
        => settings is not null &&
            settings.IsEnabled &&
            !string.IsNullOrWhiteSpace(settings.WebSocketUrl) &&
            Uri.TryCreate(settings.WebSocketUrl, UriKind.Absolute, out var webSocketUri) &&
            string.Equals(webSocketUri.Scheme, "wss", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(settings.SipDomain) &&
            ParseDelimitedValues(settings.WebRtcCodecs).Count > 0 &&
            settings.PjsipCredentialLifetimeMinutes > 0 &&
            settings.PjsipContactExpirationSeconds > 0 &&
            !string.IsNullOrWhiteSpace(settings.PjsipRealtimeProviderInvariantName) &&
            !string.IsNullOrWhiteSpace(settings.PjsipRealtimeConnectionString);

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
                // Keep each tenant listener scoped to its configured ARI app instead of the global PBX event stream.
                ["subscribeAll"] = bool.FalseString.ToLowerInvariant(),
            }).TrimStart('?');

        return builder.Uri;
    }

    public static Uri CreateEventsUriForLogging(AsteriskResolvedSettings settings)
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
                // Keep logging output aligned with the tenant-scoped ARI app subscription used at runtime.
                ["subscribeAll"] = bool.FalseString.ToLowerInvariant(),
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

    public static IReadOnlyList<string> ParseDelimitedValues(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value.Split([',', '\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
