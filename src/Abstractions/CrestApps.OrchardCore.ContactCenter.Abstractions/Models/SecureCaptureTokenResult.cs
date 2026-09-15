namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Represents the outcome of tokenizing a single raw sensitive value. The result deliberately never carries the
/// raw value back: only a durable, non-reversible token reference and a masked representation safe to show an
/// agent or persist for the audit trail.
/// </summary>
public sealed class SecureCaptureTokenResult
{
    /// <summary>
    /// Gets or sets a value indicating whether tokenization succeeded.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the durable, non-reversible reference the tokenization vault returns for the raw value. It is
    /// safe to persist and lets an authorized downstream system act on the value without the platform ever
    /// storing it.
    /// </summary>
    public string Token { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the token reference and masked value may be retained by the
    /// platform. It is <see langword="false"/> for values that must never be stored in any form after they are
    /// used, such as a card security code, which is sensitive authentication data that PCI-DSS forbids retaining
    /// once the authorization it supports has completed.
    /// </summary>
    public bool IsRetainable { get; set; } = true;

    /// <summary>
    /// Gets or sets the masked representation safe to show the agent and persist for the audit trail, such as the
    /// last four digits of a card number.
    /// </summary>
    public string MaskedValue { get; set; }

    /// <summary>
    /// Gets or sets the reason tokenization failed, when <see cref="Succeeded"/> is <see langword="false"/>.
    /// </summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// Creates a successful tokenization result whose token reference and masked value may be retained.
    /// </summary>
    /// <param name="token">The durable token reference for the raw value.</param>
    /// <param name="maskedValue">The masked representation safe to retain.</param>
    /// <returns>A successful <see cref="SecureCaptureTokenResult"/>.</returns>
    public static SecureCaptureTokenResult Success(string token, string maskedValue)
        => new() { Succeeded = true, Token = token, MaskedValue = maskedValue, IsRetainable = true };

    /// <summary>
    /// Creates a successful tokenization result for a value that was validated and used but must never be
    /// retained in any form, such as a card security code. Neither a token reference nor a masked value is
    /// carried, so nothing about the value can be persisted.
    /// </summary>
    /// <returns>A successful, non-retainable <see cref="SecureCaptureTokenResult"/>.</returns>
    public static SecureCaptureTokenResult SuccessNonRetainable()
        => new() { Succeeded = true, IsRetainable = false };

    /// <summary>
    /// Creates a failed tokenization result.
    /// </summary>
    /// <param name="errorMessage">The reason tokenization failed.</param>
    /// <returns>A failed <see cref="SecureCaptureTokenResult"/>.</returns>
    public static SecureCaptureTokenResult Failure(string errorMessage)
        => new() { Succeeded = false, ErrorMessage = errorMessage };
}
