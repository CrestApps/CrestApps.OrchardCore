namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Identifies the kind of sensitive value a secure capture collects. The field kind drives the customer-facing
/// input affordance, the validation applied before tokenization, and the masking rule, so the raw value never
/// needs to reach the agent, the supervisor, or the recording.
/// </summary>
public enum SecureCaptureField
{
    /// <summary>
    /// A payment card primary account number. Validated with the Luhn checksum and masked to the last four digits.
    /// </summary>
    CreditCardNumber,

    /// <summary>
    /// A payment card expiry date. Masked in full.
    /// </summary>
    CardExpiry,

    /// <summary>
    /// A payment card security code (CVV/CVC). Never stored in any form; masked in full.
    /// </summary>
    CardSecurityCode,

    /// <summary>
    /// A bank account number. Masked to the last four digits.
    /// </summary>
    BankAccountNumber,

    /// <summary>
    /// A national identifier, such as a social security or social insurance number. Masked to the last four digits.
    /// </summary>
    NationalId,

    /// <summary>
    /// A tenant-defined sensitive value that does not match a well-known kind. Masked in full.
    /// </summary>
    Custom,
}
