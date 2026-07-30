using Microsoft.Extensions.Compliance.Classification;

namespace CrestApps.OrchardCore.Diagnostics;

/// <summary>
/// Provides the well-known <see cref="DataClassification"/> values used with the
/// <c>Microsoft.Extensions.Compliance.Redaction</c> infrastructure so sensitive values, such as customer phone
/// numbers, email addresses, and SIP URIs, are routed through a registered redactor before they reach a log sink.
/// </summary>
public static class LogDataClassifications
{
    /// <summary>
    /// The taxonomy name that groups the CrestApps data classifications.
    /// </summary>
    public const string TaxonomyName = "CrestApps";

    /// <summary>
    /// Classifies a customer or endpoint address, such as an E.164 phone number, email address, or SIP URI, that
    /// must be redacted before it is written to a log.
    /// </summary>
    public static DataClassification Address { get; } = new(TaxonomyName, nameof(Address));

    /// <summary>
    /// The <see cref="DataClassificationSet"/> containing <see cref="Address"/> for resolving an address redactor.
    /// </summary>
    public static DataClassificationSet AddressSet { get; } = new(Address);
}
