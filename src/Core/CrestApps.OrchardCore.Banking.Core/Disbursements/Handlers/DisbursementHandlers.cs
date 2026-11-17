using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Banking.Disbursements.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Banking.Disbursements.Core.Handlers;

/// <summary>
/// Stamps when disbursements are recorded and change.
/// </summary>
public sealed class DisbursementHandler : CatalogEntryHandlerBase<Disbursement>
{
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisbursementHandler"/> class.
    /// </summary>
    /// <param name="clock">The clock.</param>
    public DisbursementHandler(IClock clock)
    {
        _clock = clock;
    }

    /// <inheritdoc />
    public override Task CreatingAsync(CreatingContext<Disbursement> context, CancellationToken cancellationToken = default)
    {
        if (context.Model.CreatedUtc == default)
        {
            context.Model.CreatedUtc = _clock.UtcNow;
        }

        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task UpdatingAsync(UpdatingContext<Disbursement> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }
}

/// <summary>
/// Stamps when disbursement batches are made and change.
/// </summary>
public sealed class DisbursementBatchHandler : CatalogEntryHandlerBase<DisbursementBatch>
{
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisbursementBatchHandler"/> class.
    /// </summary>
    /// <param name="clock">The clock.</param>
    public DisbursementBatchHandler(IClock clock)
    {
        _clock = clock;
    }

    /// <inheritdoc />
    public override Task CreatingAsync(CreatingContext<DisbursementBatch> context, CancellationToken cancellationToken = default)
    {
        if (context.Model.CreatedUtc == default)
        {
            context.Model.CreatedUtc = _clock.UtcNow;
        }

        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task UpdatingAsync(UpdatingContext<DisbursementBatch> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }
}
