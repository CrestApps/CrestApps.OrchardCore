using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CrestApps.OrchardCore.Diagnostics;

/// <summary>
/// Provides centralized classification and redaction for values written to operational logs. Customer addresses,
/// secrets, and free-form text are never emitted, while stable identifiers are pseudonymized with a process-local
/// keyed hash so operators can correlate related log lines within one process lifetime without exposing the raw
/// value or a digest that can be brute-forced offline. Every method also strips control characters from emitted
/// content so a redacted or pseudonymized result can never be used to split or forge a log line.
/// </summary>
public static class OperationalLogRedactor
{
    private const string NoneToken = "(none)";
    private const string RedactedAddressToken = "[address-redacted]";
    private const string RedactedSecretToken = "[secret-redacted]";
    private const string RedactedTextToken = "[text-redacted]";
    private const string RedactedMetadataToken = "[metadata-redacted]";
    private const string IdentifierPrefix = "id_";
    private const int PseudonymHexLength = 12;
    private const int MinimumSecretShapeLength = 24;
    private const int MaxStackTraceLength = 4096;
    private static readonly byte[] _pseudonymizationKey = RandomNumberGenerator.GetBytes(32);

    /// <summary>
    /// Redacts a value according to its <see cref="OperationalLogFieldKind"/> classification.
    /// </summary>
    /// <param name="value">The raw value to classify and redact.</param>
    /// <param name="kind">The operational-logging sensitivity of the value.</param>
    /// <param name="identifierCategory">
    /// The correlation category to use when <paramref name="kind"/> is <see cref="OperationalLogFieldKind.Identifier"/>.
    /// Ignored for every other kind. See <see cref="Pseudonymize(string, string)"/>.
    /// </param>
    /// <returns>A log-safe representation of the value that never contains the raw input.</returns>
    public static string Redact(
        string value,
        OperationalLogFieldKind kind,
        string identifierCategory = OperationalLogIdentifierCategory.Metadata)
    {
        if (string.IsNullOrEmpty(value))
        {
            return NoneToken;
        }

        return kind switch
        {
            OperationalLogFieldKind.Identifier => Pseudonymize(value, identifierCategory),
            OperationalLogFieldKind.Address => RedactedAddressToken,
            OperationalLogFieldKind.Secret => RedactedSecretToken,
            _ => RedactedTextToken,
        };
    }

    /// <summary>
    /// Pseudonymizes a stable identifier into a short, process-local token. The same raw identifier and
    /// <paramref name="category"/> produce the same token for the lifetime of the current process, while the random
    /// in-memory HMAC key prevents an operator with log access from brute-forcing low-entropy identifiers offline.
    /// </summary>
    /// <param name="identifier">The raw identifier value, such as a user, agent, call, or queue id.</param>
    /// <param name="category">
    /// A correlation category, such as one of the constants on <see cref="OperationalLogIdentifierCategory"/>, that
    /// keeps identically valued identifiers from different domains from producing the same pseudonym. Every log
    /// call site for the same kind of identifier must pass the same category so its pseudonyms remain correlatable
    /// within the process lifetime.
    /// </param>
    /// <returns>A deterministic pseudonym for the identifier, or <c>"(none)"</c> when the identifier is empty.</returns>
    public static string Pseudonymize(string identifier, string category = OperationalLogIdentifierCategory.Metadata)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            return NoneToken;
        }

        var sanitized = RemoveControlCharacters(identifier);
        var domain = string.IsNullOrEmpty(category) ? OperationalLogIdentifierCategory.Metadata : category;
        var bytes = Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"{domain}:{sanitized}"));
        var hash = HMACSHA256.HashData(_pseudonymizationKey, bytes);
        var hex = Convert.ToHexStringLower(hash).Substring(0, PseudonymHexLength);

        return $"{IdentifierPrefix}{hex}";
    }

    /// <summary>
    /// Determines whether a value has the shape of a secret or credential.
    /// </summary>
    /// <param name="value">The raw value to inspect.</param>
    /// <returns><see langword="true"/> when the value looks like a secret or token; otherwise, <see langword="false"/>.</returns>
    public static bool LooksLikeSecret(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.Contains("api_key", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("apikey", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("password", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("bearer ", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("authorization", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return value.Length >= MinimumSecretShapeLength &&
            value.All(static c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or '+' or '/' or '=');
    }

    /// <summary>
    /// Creates an exception representation that retains the original exception type and a bounded, sanitized
    /// stack-frame summary for diagnostics while removing the message, inner exceptions, data, and overridable
    /// exception text that may contain PII, credentials, provider responses, or other attacker-controlled content.
    /// </summary>
    /// <param name="exception">The exception to make safe for operational logging.</param>
    /// <returns>An exception that is safe to pass to <c>ILogger</c>.</returns>
    public static Exception RedactException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var stackTrace = RemoveControlCharacters(new StackTrace(exception, false).ToString());

        if (stackTrace.Length > MaxStackTraceLength)
        {
            stackTrace = stackTrace.Substring(0, MaxStackTraceLength);
        }

        return new LogSafeException(exception.GetType().FullName ?? exception.GetType().Name, stackTrace);
    }

    /// <summary>
    /// Redacts a provider or request metadata dictionary for operational logging. Both keys and values are
    /// provider- or client-controlled and may contain PII, secrets, or log-forging delimiters, so non-empty metadata
    /// is represented by a fixed token rather than a structured dump.
    /// </summary>
    /// <param name="metadata">The metadata dictionary to redact, or <see langword="null"/>.</param>
    /// <returns>A fixed redaction token, or <c>"(none)"</c> when the metadata is empty.</returns>
    public static string RedactMetadata(IEnumerable<KeyValuePair<string, object>> metadata)
    {
        return metadata is null
            ? NoneToken
            : RedactMetadataCore(metadata);
    }

    /// <summary>
    /// Redacts a provider or request metadata dictionary for operational logging. Both keys and values are
    /// provider- or client-controlled and may contain PII, secrets, or log-forging delimiters, so non-empty metadata
    /// is represented by a fixed token rather than a structured dump.
    /// </summary>
    /// <param name="metadata">The metadata dictionary to redact, or <see langword="null"/>.</param>
    /// <returns>A fixed redaction token, or <c>"(none)"</c> when the metadata is empty.</returns>
    public static string RedactMetadata(IEnumerable<KeyValuePair<string, string>> metadata)
    {
        return metadata is null
            ? NoneToken
            : RedactMetadataCore(metadata);
    }

    private static string RedactMetadataCore<TValue>(IEnumerable<KeyValuePair<string, TValue>> metadata)
    {
        return metadata.Any()
            ? RedactedMetadataToken
            : NoneToken;
    }

    private static string RemoveControlCharacters(string value)
    {
        return string.Create(value.Length, value, static (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                span[i] = char.IsControl(source[i]) || source[i] is '\u2028' or '\u2029'
                    ? ' '
                    : source[i];
            }
        }).Trim();
    }

    private sealed class LogSafeException : Exception
    {
        private readonly string _exceptionType;
        private readonly string _stackTrace;

        public LogSafeException(string exceptionType, string stackTrace)
            : base("[exception-message-redacted]")
        {
            _exceptionType = exceptionType;
            _stackTrace = stackTrace;
        }

        public override string StackTrace => _stackTrace;

        public override string ToString()
        {
            return string.IsNullOrEmpty(_stackTrace)
                ? $"{_exceptionType}: {Message}"
                : $"{_exceptionType}: {Message}{Environment.NewLine}{_stackTrace}";
        }
    }
}
