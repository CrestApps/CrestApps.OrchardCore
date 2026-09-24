using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Records the holds an agent places from the soft phone against the Contact Center call they belong to, so a call's
/// hold time and the audit log's holds and resumes include the holds a provider performs in the agent's own media and
/// never reports.
/// </summary>
/// <remarks>
/// A call the Contact Center does not track -- an extension call to a colleague, or one the browser placed itself --
/// is left alone. A write that lost a race with the provider's own events for the call is retried once in a fresh
/// scope; a failure after that is logged rather than thrown, because the hold itself is already in effect.
/// </remarks>
public sealed class ContactCenterTelephonyCallHoldObserver : ITelephonyCallHoldObserver
{
    private readonly IAgentCallHoldRecorder _recorder;
    private readonly IContactCenterScopeExecutor _scopeExecutor;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterTelephonyCallHoldObserver"/> class.
    /// </summary>
    /// <param name="recorder">The recorder that writes the hold to the call and the audit log.</param>
    /// <param name="scopeExecutor">The executor used to retry a write that lost a concurrency race.</param>
    /// <param name="logger">The logger.</param>
    public ContactCenterTelephonyCallHoldObserver(
        IAgentCallHoldRecorder recorder,
        IContactCenterScopeExecutor scopeExecutor,
        ILogger<ContactCenterTelephonyCallHoldObserver> logger)
    {
        _recorder = recorder;
        _scopeExecutor = scopeExecutor;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task CallHoldChangedAsync(TelephonyCallHoldChange change, CancellationToken cancellationToken = default)
    {
        if (change is null || string.IsNullOrEmpty(change.CallId))
        {
            return;
        }

        try
        {
            try
            {
                await _recorder.RecordAsync(change, cancellationToken);
            }
            catch (ConcurrencyException)
            {
                // The retry reads the call afresh, so a change the other writer already made (such as the call
                // ending) is seen and respected rather than overwritten.
                await _scopeExecutor.ExecuteAsync<IAgentCallHoldRecorder>(recorder => recorder.RecordAsync(change, cancellationToken));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "The agent's hold change on call '{CallId}' could not be recorded against the Contact Center call.",
                change.CallId.SanitizeLogValue());
        }
    }
}
