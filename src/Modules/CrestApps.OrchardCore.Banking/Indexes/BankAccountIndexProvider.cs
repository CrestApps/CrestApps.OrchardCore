using CrestApps.OrchardCore.Banking.Core.Indexes;
using CrestApps.OrchardCore.Banking.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Banking.Indexes;

internal sealed class BankAccountIndexProvider : IndexProvider<BankAccount>
{
    public BankAccountIndexProvider()
    {
        CollectionName = BankingConstants.CollectionName;
    }

    public override void Describe(DescribeContext<BankAccount> context)
    {
        context
            .For<BankAccountIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(account => new BankAccountIndex
            {
                ItemId = account.ItemId,
                Name = account.Name,
                OwnerKind = account.Owner.IsEmpty ? null : account.Owner.Kind,
                OwnerId = account.Owner.IsEmpty ? null : account.Owner.Id,
                Status = account.Status.ToString(),
                Country = account.Country,
                Currency = account.Currency,
                CreatedUtc = account.CreatedUtc,
            });
    }
}
