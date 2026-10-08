using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides a fail-closed <see cref="ISecureCaptureTokenSink"/> that refuses every tokenization. It is registered
/// as the default outside development so a deployment that enables secure data capture without supplying a
/// PCI-DSS-compliant tokenization sink cannot silently accept sensitive customer data into a non-vault
/// implementation. A production deployment replaces it with a sink that forwards the raw value to a compliant
/// tokenization provider.
/// </summary>
public sealed class UnconfiguredSecureCaptureTokenSink : ISecureCaptureTokenSink
{
    /// <inheritdoc/>
    public Task<SecureCaptureTokenResult> TokenizeAsync(
        SecureCaptureField field,
        string rawValue,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(SecureCaptureTokenResult.Failure(
            "Secure data capture is not configured for this environment. A production deployment must register a PCI-DSS-compliant ISecureCaptureTokenSink before capturing sensitive data."));
    }
}
