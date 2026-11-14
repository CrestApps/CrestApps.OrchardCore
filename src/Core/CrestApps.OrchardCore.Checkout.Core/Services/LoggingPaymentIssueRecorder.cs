using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// The payment issue recorder used when no store for payment issues is registered. It logs each issue at error level,
/// so code that reports a money operation that must be finished by hand never has to check for a missing recorder,
/// and an issue is never dropped without a trace.
/// </summary>
internal sealed class LoggingPaymentIssueRecorder : IPaymentIssueRecorder
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingPaymentIssueRecorder"/> class.
    /// </summary>
    /// <param name="logger">The logger the issues are written to.</param>
    public LoggingPaymentIssueRecorder(ILogger logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<bool> RecordAsync(PaymentIssue issue, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(issue);

        _logger.LogError(
            "Payment issue '{Kind}' for {ReferenceType} '{ReferenceId}' (provider '{ProviderKey}', reference '{ProviderReference}'): {Message}",
            issue.Kind,
            issue.ReferenceType,
            issue.ReferenceId,
            issue.ProviderKey,
            issue.ProviderReference,
            issue.Message);

        return Task.FromResult(false);
    }
}
