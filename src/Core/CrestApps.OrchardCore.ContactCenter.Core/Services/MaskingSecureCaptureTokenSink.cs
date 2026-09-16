using CrestApps.OrchardCore.ContactCenter.Models;
using OrchardCore;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default, self-contained implementation of <see cref="ISecureCaptureTokenSink"/>. It validates
/// and masks the raw value and returns an opaque surrogate token, without ever persisting or logging the raw
/// value and without contacting an external service.
/// </summary>
/// <remarks>
/// This default is intended for development, evaluation, and non-cardholder use. It does NOT descope a deployment
/// from PCI-DSS: the surrogate token it returns is not backed by a compliant vault and cannot be used to charge a
/// card. A production deployment that captures cardholder data MUST replace this implementation with one that
/// forwards the raw value to a PCI-DSS-compliant tokenization provider and returns that provider's token.
/// </remarks>
public sealed class MaskingSecureCaptureTokenSink : ISecureCaptureTokenSink
{
    private const string MaskCharacter = "•";

    /// <inheritdoc/>
    public Task<SecureCaptureTokenResult> TokenizeAsync(
        SecureCaptureField field,
        string rawValue,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return Task.FromResult(SecureCaptureTokenResult.Failure("A value is required."));
        }

        var normalized = NormalizeDigits(rawValue);

        switch (field)
        {
            case SecureCaptureField.CreditCardNumber:
                if (normalized.Length < 12 || normalized.Length > 19 || !IsValidLuhn(normalized))
                {
                    return Task.FromResult(SecureCaptureTokenResult.Failure("The card number is not valid."));
                }

                return Task.FromResult(SecureCaptureTokenResult.Success(NewToken(), MaskToLastFour(normalized)));

            case SecureCaptureField.BankAccountNumber:
                if (normalized.Length < 4)
                {
                    return Task.FromResult(SecureCaptureTokenResult.Failure("The account number is not valid."));
                }

                return Task.FromResult(SecureCaptureTokenResult.Success(NewToken(), MaskToLastFour(normalized)));

            case SecureCaptureField.NationalId:
                if (normalized.Length < 4)
                {
                    return Task.FromResult(SecureCaptureTokenResult.Failure("The identifier is not valid."));
                }

                return Task.FromResult(SecureCaptureTokenResult.Success(NewToken(), MaskToLastFour(normalized)));

            case SecureCaptureField.CardSecurityCode:
                // A card security code is sensitive authentication data that must never be retained in any form,
                // not even masked to a length or referenced by a durable token. Validate it and return a
                // non-retainable success so the value is used for its one-shot purpose and nothing about it is
                // ever persisted.
                if (normalized.Length is < 3 or > 4)
                {
                    return Task.FromResult(SecureCaptureTokenResult.Failure("The security code is not valid."));
                }

                return Task.FromResult(SecureCaptureTokenResult.SuccessNonRetainable());

            case SecureCaptureField.CardExpiry:
            case SecureCaptureField.Custom:
            default:
                var trimmed = rawValue.Trim();

                return Task.FromResult(SecureCaptureTokenResult.Success(NewToken(), MaskFully(trimmed.Length)));
        }
    }

    private static string NewToken()
        => $"tok_{IdGenerator.GenerateId()}";

    private static string NormalizeDigits(string value)
    {
        var buffer = new char[value.Length];
        var length = 0;

        foreach (var character in value)
        {
            if (char.IsDigit(character))
            {
                buffer[length++] = character;
            }
        }

        return new string(buffer, 0, length);
    }

    private static string MaskToLastFour(string digits)
    {
        var lastFour = digits.Length <= 4
            ? digits
            : digits.Substring(digits.Length - 4);

        return $"{MaskCharacter}{MaskCharacter}{MaskCharacter}{MaskCharacter} {lastFour}";
    }

    private static string MaskFully(int length)
    {
        var visibleLength = Math.Clamp(length, 1, 8);

        return string.Concat(Enumerable.Repeat(MaskCharacter, visibleLength));
    }

    private static bool IsValidLuhn(string digits)
    {
        var sum = 0;
        var alternate = false;

        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var current = digits[i] - '0';

            if (alternate)
            {
                current *= 2;

                if (current > 9)
                {
                    current -= 9;
                }
            }

            sum += current;
            alternate = !alternate;
        }

        return sum % 10 == 0;
    }
}
