using CrestApps.OrchardCore.PhoneNumbers;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;

internal static class PhoneNumberVerificationProviderLogMessages
{
    private static readonly Action<ILogger, string, string, string, bool, Exception> _starting =
        LoggerMessage.Define<string, string, string, bool>(
            LogLevel.Information,
            new EventId(1000, nameof(Starting)),
            "Starting {ProviderName} phone number verification request. Endpoint: {Endpoint}. Authentication: {Authentication}. CredentialsConfigured: {CredentialsConfigured}.");

    private static readonly Action<ILogger, string, int, int, Exception> _responseReceived =
        LoggerMessage.Define<string, int, int>(
            LogLevel.Debug,
            new EventId(1001, nameof(ResponseReceived)),
            "{ProviderName} phone number verification response received. StatusCode: {StatusCode}. ResponseLength: {ResponseLength}.");

    private static readonly Action<ILogger, string, int, string, Exception> _nonSuccessStatusCode =
        LoggerMessage.Define<string, int, string>(
            LogLevel.Warning,
            new EventId(1002, nameof(NonSuccessStatusCode)),
            "{ProviderName} returned a non-success status code while verifying a phone number. StatusCode: {StatusCode}. ReasonPhrase: {ReasonPhrase}.");

    private static readonly Action<ILogger, string, PhoneNumberVerificationStatus, PhoneNumberLineType, string, Exception> _completed =
        LoggerMessage.Define<string, PhoneNumberVerificationStatus, PhoneNumberLineType, string>(
            LogLevel.Information,
            new EventId(1003, nameof(Completed)),
            "{ProviderName} phone number verification completed. Status: {VerificationStatus}. LineType: {LineType}. CountryCode: {CountryCode}.");

    public static void Starting(
        ILogger logger,
        string providerName,
        Uri requestUri,
        string authentication,
        bool credentialsConfigured)
    {
        if (!logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        _starting(logger, providerName, requestUri.GetLeftPart(UriPartial.Path), authentication, credentialsConfigured, null);
    }

    public static void ResponseReceived(ILogger logger, string providerName, int statusCode, int responseLength)
    {
        _responseReceived(logger, providerName, statusCode, responseLength, null);
    }

    public static void NonSuccessStatusCode(ILogger logger, string providerName, int statusCode, string reasonPhrase)
    {
        _nonSuccessStatusCode(logger, providerName, statusCode, reasonPhrase, null);
    }

    public static void Completed(ILogger logger, string providerName, PhoneNumberVerificationResult result)
    {
        if (!logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        _completed(logger, providerName, result.Status, result.LineType, result.CountryCode, null);
    }
}
