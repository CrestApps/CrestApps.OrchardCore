using CrestApps.OrchardCore.Checks.Core.Indexes;
using CrestApps.OrchardCore.Checks.Models;
using CrestApps.OrchardCore.SourceAccess;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Checks.Indexes;

internal sealed class CheckRecordIndexProvider : IndexProvider<CheckRecord>
{
    public CheckRecordIndexProvider()
    {
        CollectionName = ChecksConstants.CollectionName;
    }

    public override void Describe(DescribeContext<CheckRecord> context)
    {
        context
            .For<CheckRecordIndex>()
            .Map(check => new CheckRecordIndex
            {
                ItemId = check.ItemId,
                BankAccountId = check.BankAccountId,
                Number = check.Number,
                Status = check.Status.ToString(),
                PayeeName = check.PayeeName,
                Amount = check.Amount,
                Currency = check.Currency,
                CheckDate = check.CheckDate.ToDateTime(TimeOnly.MinValue),
                SourceKind = check.SourceKind,
                SourceId = check.SourceId,
                IssuedUtc = check.IssuedUtc,
                AccessKey = SourceAccessFilter.KeyOf(check.SourceKind, check.AccessScope),
                ModifiedUtc = check.ModifiedUtc ?? check.IssuedUtc,
            });
    }
}
