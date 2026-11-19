using CrestApps.OrchardCore.Accounts;
using CrestApps.OrchardCore.Tickets.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Tickets.Migrations;

/// <summary>
/// Adds the Ticket Account part (the account's ticket service level) and gives it to the Account type.
/// </summary>
internal sealed class TicketAccountMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public TicketAccountMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(TicketAccountPart), part => part
            .Attachable()
            .WithDisplayName("Ticket Service Level")
            .WithDescription("The service-level policy an account's tickets run on."));

        if (await _contentDefinitionManager.GetTypeDefinitionAsync(AccountsConstants.ContentTypes.Account) is { } account
            && !account.Parts.Any(part => part.PartDefinition?.Name == nameof(TicketAccountPart)))
        {
            await _contentDefinitionManager.AlterTypeDefinitionAsync(AccountsConstants.ContentTypes.Account, type => type
                .WithPart(nameof(TicketAccountPart), part => part.WithPosition("20")));
        }

        return 1;
    }
}
