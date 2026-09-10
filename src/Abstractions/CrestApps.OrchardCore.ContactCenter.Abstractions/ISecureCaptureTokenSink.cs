using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Tokenizes a raw sensitive value captured from a customer during an agent-assisted secure capture. Within the
/// capture service this is the boundary the raw value is exchanged at: an implementation must exchange it for a
/// non-reversible token and a masked representation and must never return, log, or persist the raw value. The
/// default in-tree implementation masks and issues a surrogate token without an external vault; a production
/// deployment replaces it with an implementation that forwards the value to a PCI-DSS-compliant tokenization
/// provider.
/// </summary>
/// <remarks>
/// For a retainable field (such as a card number), a production implementation must treat <c>idempotencyKey</c>
/// as a mandatory idempotency contract: a repeated call with the same key and the same value must return the
/// original result without creating a second vault token, and a call with the same key but a different value must
/// fail rather than silently tokenize the new value. This makes a retried or replayed submission exactly-once at
/// the sink even though the capture service runs inside an ambient unit of work whose commit is evaluated after
/// the call returns.
/// A non-retainable field - sensitive authentication data such as a card security code, for which
/// <see cref="SecureCaptureTokenResult.SuccessNonRetainable"/> is returned - is exempt from value-comparing
/// idempotency: the sink must not store the value or any verifier of it, so it cannot and must not compare a
/// later value against an earlier one. Each such call is validated independently as a one-shot operation and
/// nothing about the value is retained between calls.
/// </remarks>
public interface ISecureCaptureTokenSink
{
    /// <summary>
    /// Exchanges a raw sensitive value for a non-reversible token and a masked representation.
    /// </summary>
    /// <param name="field">The kind of sensitive value being tokenized, which drives validation and masking.</param>
    /// <param name="rawValue">The raw sensitive value submitted by the customer. It must not be persisted or logged.</param>
    /// <param name="idempotencyKey">A stable per-capture, per-field key. For a retainable field a production sink must honor it as an idempotency contract: the same key with the same value returns the original result and never mints a second vault token, and the same key with a different value must fail safely instead of tokenizing the new value. A non-retainable field (sensitive authentication data such as a card security code) is exempt, because comparing values would require retaining the value or a verifier of it; such calls are validated independently and nothing is retained between them.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The tokenization result carrying only the token reference and the masked value.</returns>
    Task<SecureCaptureTokenResult> TokenizeAsync(
        SecureCaptureField field,
        string rawValue,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}
