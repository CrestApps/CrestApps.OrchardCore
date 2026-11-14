using CrestApps.OrchardCore.Disbursements.Core.Indexes;
using CrestApps.OrchardCore.Disbursements.Models;
using CrestApps.OrchardCore.SourceAccess;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Disbursements.Indexes;

internal sealed class DisbursementIndexProvider : IndexProvider<Disbursement>
{
    public DisbursementIndexProvider()
    {
        CollectionName = DisbursementsConstants.CollectionName;
    }

    public override void Describe(DescribeContext<Disbursement> context)
    {
        context
            .For<DisbursementIndex>()
            .Map(disbursement => new DisbursementIndex
            {
                ItemId = disbursement.ItemId,
                Status = disbursement.Status.ToString(),
                Method = disbursement.Method,
                Provider = disbursement.Provider,
                PayeeName = disbursement.PayeeName,
                Currency = disbursement.Currency,
                SourceBankAccountId = disbursement.SourceBankAccountId,
                DestinationBankAccountId = disbursement.DestinationBankAccountId,
                SourceKind = disbursement.SourceKind,
                SourceId = disbursement.SourceId,
                AccessKey = SourceAccessFilter.KeyOf(disbursement.SourceKind, disbursement.AccessScope),
                IdempotencyKey = disbursement.IdempotencyKey,
                TraceNumber = disbursement.TraceNumber,
                CheckId = disbursement.CheckId,
                BatchId = disbursement.BatchId,
                CreatedUtc = disbursement.CreatedUtc,
                ModifiedUtc = disbursement.ModifiedUtc ?? disbursement.CreatedUtc,
            });
    }
}

internal sealed class DisbursementBatchIndexProvider : IndexProvider<DisbursementBatch>
{
    public DisbursementBatchIndexProvider()
    {
        CollectionName = DisbursementsConstants.CollectionName;
    }

    public override void Describe(DescribeContext<DisbursementBatch> context)
    {
        context
            .For<DisbursementBatchIndex>()
            .Map(batch => new DisbursementBatchIndex
            {
                ItemId = batch.ItemId,
                Provider = batch.Provider,
                Status = batch.Status.ToString(),
                SourceKind = batch.SourceKind,
                AccessKey = string.IsNullOrEmpty(batch.SourceKind) ? null : CrestApps.OrchardCore.SourceAccess.SourceAccessFilter.KeyOf(batch.SourceKind, batch.AccessScope),
                SourceBankAccountId = batch.SourceBankAccountId,
                EffectiveDate = batch.EffectiveDate.ToDateTime(TimeOnly.MinValue),
                CreatedUtc = batch.CreatedUtc,
                ReleasedUtc = batch.ReleasedUtc,
            });
    }
}
