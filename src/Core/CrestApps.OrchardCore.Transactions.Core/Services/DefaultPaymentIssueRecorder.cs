using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Transactions.Core.Services;

/// <summary>
/// Saves payment issues through <see cref="IPaymentIssueStore"/>, and logs instead of throwing when it cannot.
/// </summary>
internal sealed class DefaultPaymentIssueRecorder : IPaymentIssueRecorder
{
    private readonly IPaymentIssueStore _store;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public DefaultPaymentIssueRecorder(
        IPaymentIssueStore store,
        IClock clock,
        ILogger<DefaultPaymentIssueRecorder> logger)
    {
        _store = store;
        _clock = clock;
        _logger = logger;
    }

    public async Task<bool> RecordAsync(PaymentIssue issue, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(issue);

        issue.ItemId ??= global::OrchardCore.IdGenerator.GenerateId();
        issue.Status = PaymentIssueStatus.Open;

        if (issue.CreatedUtc == default)
        {
            issue.CreatedUtc = _clock.UtcNow;
        }

        try
        {
            await _store.CreateAsync(issue, cancellationToken);

            return true;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "The payment issue '{Kind}' for '{ReferenceType}' '{ReferenceId}' could not be recorded: {Message}", issue.Kind, issue.ReferenceType, issue.ReferenceId, issue.Message);

            return false;
        }
    }
}
