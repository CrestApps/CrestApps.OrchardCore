using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Accounts.Models;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Accounts.Core.Indexes;

/// <summary>
/// Indexes account invitations.
/// </summary>
public sealed class AccountInvitationIndex : CatalogItemIndex
{
    public string AccountId { get; set; }

    public string NormalizedEmail { get; set; }

    public string TokenHash { get; set; }

    public AccountInvitationState State { get; set; }

    public string AcceptedUserId { get; set; }

    public string Source { get; set; }

    public string SourceId { get; set; }
}

/// <summary>
/// Maps account invitations.
/// </summary>
public sealed class AccountInvitationIndexProvider : IndexProvider<AccountInvitation>
{
    public AccountInvitationIndexProvider()
    {
        CollectionName = AccountsConstants.InvitationsCollectionName;
    }

    public override void Describe(DescribeContext<AccountInvitation> context)
        => context.For<AccountInvitationIndex>()
            .Map(invitation => new AccountInvitationIndex
            {
                ItemId = invitation.ItemId,
                AccountId = invitation.AccountId,
                NormalizedEmail = invitation.Email?.Trim().ToUpperInvariant(),
                TokenHash = invitation.TokenHash,
                State = invitation.State,
                AcceptedUserId = invitation.AcceptedUserId,
                Source = invitation.Source,
                SourceId = invitation.SourceId,
            });
}

/// <summary>
/// Creates the index table of account invitations.
/// </summary>
public static class AccountInvitationIndexSchema
{
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = AccountsConstants.InvitationsCollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<AccountInvitationIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("AccountId", column => column.WithLength(26))
            .Column<string>("NormalizedEmail", column => column.WithLength(255))
            .Column<string>("TokenHash", column => column.WithLength(64))
            .Column<int>("State")
            .Column<string>("AcceptedUserId", column => column.Nullable().WithLength(26))
            .Column<string>("Source", column => column.Nullable().WithLength(100))
            .Column<string>("SourceId", column => column.Nullable().WithLength(100)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<AccountInvitationIndex>(table =>
        {
            table.CreateIndex("IDX_AccountInvitation_ItemId", "ItemId");
            table.CreateIndex("IDX_AccountInvitation_Token", "TokenHash");
            table.CreateIndex("IDX_AccountInvitation_AccountState", "AccountId", "State");
            table.CreateIndex("IDX_AccountInvitation_Email", "NormalizedEmail");
            table.CreateIndex("IDX_AccountInvitation_User", "AcceptedUserId");
        }, collection: collection);
    }
}
