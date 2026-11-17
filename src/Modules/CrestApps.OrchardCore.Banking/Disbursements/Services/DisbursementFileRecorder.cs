using CrestApps.OrchardCore.Banking.Disbursements.AuditTrail;
using CrestApps.OrchardCore.Banking.Disbursements.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.AuditTrail.Services;
using OrchardCore.AuditTrail.Services.Models;

namespace CrestApps.OrchardCore.Banking.Disbursements.Services;

/// <summary>
/// Records each time a bank file (every payee's full account number) leaves the site: downloaded by a person or
/// delivered by a data pipeline. It writes the batch and who, never a number, to the log and, when Audit Trail is on,
/// to the Audit Trail.
/// </summary>
public sealed class DisbursementFileRecorder
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisbursementFileRecorder"/> class.
    /// </summary>
    /// <param name="serviceProvider">The services, for the Audit Trail when it is on.</param>
    /// <param name="logger">The logger.</param>
    public DisbursementFileRecorder(IServiceProvider serviceProvider, ILogger<DisbursementFileRecorder> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Records that a batch's bank file left the site.
    /// </summary>
    /// <param name="batch">The batch.</param>
    /// <param name="how">Downloaded, or delivered by a pipeline.</param>
    /// <param name="userId">The user's id, if any.</param>
    /// <param name="userName">The user, or the pipeline's name.</param>
    /// <returns>A task.</returns>
    public async Task RecordAsync(DisbursementBatch batch, string how, string userId, string userName)
    {
        ArgumentNullException.ThrowIfNull(batch);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("The bank file of disbursement batch '{BatchId}' was {How} by '{User}'.", batch.ItemId, how, userName);
        }

        if (_serviceProvider.GetService<IAuditTrailManager>() is { } auditTrail)
        {
            await auditTrail.RecordEventAsync(new AuditTrailContext<DisbursementFileAuditData>(
                DisbursementsAuditTrailEvents.FileSent,
                DisbursementsAuditTrailEvents.CategoryName,
                batch.ItemId,
                userId: userId,
                userName: userName,
                new DisbursementFileAuditData
                {
                    BatchId = batch.ItemId,
                    Provider = batch.Provider,
                    FileName = batch.FileName,
                    Count = batch.Count,
                    How = how,
                }));
        }
    }
}
