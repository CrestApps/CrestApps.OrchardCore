using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Core;

/// <summary>
/// Provides shared error message formatting for AI chat hubs.
/// </summary>
internal static class AIHubErrorMessageHelper
{
    private const string ClientResultExceptionName = "ClientResultException";
    private static readonly string[] RateLimitIndicators = ["ratelimitreached", "rate limit", "too many requests"];

    /// <summary>
    /// Maps provider exceptions to localized, user-friendly error messages.
    /// </summary>
    /// <param name="ex">The exception thrown during provider communication.</param>
    /// <param name="localizer">The localizer used to format messages.</param>
    /// <returns>A localized message suitable for end users.</returns>
    public static LocalizedString GetFriendlyErrorMessage(Exception ex, IStringLocalizer localizer)
    {
        var message = ex?.Message ?? string.Empty;
        var clientStatusCode = TryGetClientResultStatusCode(ex);

        if (clientStatusCode == (int)System.Net.HttpStatusCode.TooManyRequests ||
            ContainsRateLimitIndicator(message))
        {
            var retryAfterMessage = ExtractRetryAfterMessage(message);

            return string.IsNullOrWhiteSpace(retryAfterMessage)
                ? localizer["Rate limit reached. Please wait and try again later."]
                : localizer["Rate limit reached. {0}", retryAfterMessage];
        }

        if (ex is HttpRequestException httpEx)
        {
            if (httpEx.StatusCode is { } code)
            {
                return code switch
                {
                    System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden
                      => localizer["Authentication failed. Please check your API credentials."],

                    System.Net.HttpStatusCode.BadRequest
                      => localizer["Invalid request. Please verify your connection settings."],

                    System.Net.HttpStatusCode.NotFound
                      => localizer["The provider endpoint could not be found. Please verify the API URL."],

                    System.Net.HttpStatusCode.TooManyRequests
                      => localizer["Rate limit reached. Please wait and try again later."],

                    >= System.Net.HttpStatusCode.InternalServerError
                      => localizer["The provider service is currently unavailable. Please try again later."],

                    _ => localizer["An error occurred while communicating with the provider."]
                };
            }

            return localizer["Unable to reach the provider. Please check your connection or endpoint URL."];
        }

        return localizer["Our service is currently unavailable. Please try again later."];
    }

    private static int? TryGetClientResultStatusCode(Exception ex)
    {
        if (ex is null)
        {
            return null;
        }

        var type = ex.GetType();
        if (!string.Equals(type.Name, ClientResultExceptionName, StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            var statusProperty = type.GetProperty("Status") ?? type.GetProperty("StatusCode");
            if (statusProperty?.GetValue(ex) is int status)
            {
                return status;
            }
        }
        catch (Exception)
        {
            return null;
        }

        return null;
    }

    private static bool ContainsRateLimitIndicator(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        foreach (var indicator in RateLimitIndicators)
        {
            if (message.Contains(indicator, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string ExtractRetryAfterMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        var retryIndex = message.IndexOf("retry after", StringComparison.OrdinalIgnoreCase);
        if (retryIndex < 0)
        {
            return string.Empty;
        }

        var sentence = message.Substring(retryIndex);
        var endIndex = sentence.IndexOf('.', StringComparison.Ordinal);
        if (endIndex >= 0)
        {
            sentence = sentence.Substring(0, endIndex + 1);
        }

        return sentence.Trim();
    }
}
