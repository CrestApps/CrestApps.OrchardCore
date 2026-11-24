using CrestApps.OrchardCore.Learning.Credentials.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Learning.Credentials.Core.Indexes;

/// <summary>
/// Maps credential awards to <see cref="CredentialAwardIndex"/>.
/// </summary>
public sealed class CredentialAwardIndexProvider : IndexProvider<CredentialAward>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialAwardIndexProvider"/> class.
    /// </summary>
    public CredentialAwardIndexProvider()
    {
        CollectionName = CredentialsConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<CredentialAward> context)
        => context.For<CredentialAwardIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(award => new CredentialAwardIndex
            {
                ItemId = award.ItemId,
                PublicId = award.PublicId,
                DefinitionId = award.DefinitionId,
                RecipientUserId = award.RecipientUserId,
                RuleKind = award.RuleKind,
                SourceId = award.SourceId,
                IssuedUtc = award.IssuedUtc,
                ExpiresUtc = award.ExpiresUtc,
                Revoked = award.IsRevoked,
                StatusListNumber = award.StatusListNumber,
                StatusListIndex = award.StatusListIndex,
            });
}
