using CrestApps.OrchardCore.Agreements.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Agreements.Core.Indexes;

/// <summary>
/// Maps agreements to their index.
/// </summary>
public sealed class AgreementIndexProvider : IndexProvider<Agreement>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgreementIndexProvider"/> class.
    /// </summary>
    public AgreementIndexProvider()
    {
        CollectionName = AgreementsConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<Agreement> context)
        => context.For<AgreementIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(item => new AgreementIndex
            {
                ItemId = item.ItemId,
                Number = item.Number,
                Name = item.Name?.Length > 255 ? item.Name[..255] : item.Name,
                Status = item.Status.ToString(),
                AccountId = item.AccountId,
            });
}
