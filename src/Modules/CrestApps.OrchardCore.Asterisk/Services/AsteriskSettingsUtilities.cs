using System.Globalization;
using CrestApps.OrchardCore.Asterisk.Models;

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
    }

    public static bool HasRequiredConfiguration(AsteriskConnectionSettings settings, string password)
        => !string.IsNullOrWhiteSpace(settings.BaseUrl) &&
            !string.IsNullOrWhiteSpace(settings.UserName) &&
            !string.IsNullOrWhiteSpace(password) &&
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

        if (path[^1] != '/')
        {
            path += "/";
        }

        builder.Path = path;

        return builder.Uri.ToString();
    }

    public static string ToInvariantString(int value)
        => value.ToString(CultureInfo.InvariantCulture);
}
