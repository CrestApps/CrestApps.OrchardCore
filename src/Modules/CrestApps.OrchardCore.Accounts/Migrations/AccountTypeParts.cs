using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;

namespace CrestApps.OrchardCore.Accounts.Migrations;

internal static class AccountTypeParts
{
    /// <summary>
    /// Adds the part to the <c>Account</c> type the Accounts feature creates, when the type has it not yet. Another
    /// account type can be given the part by hand.
    /// </summary>
    public static async Task AttachAsync(IContentDefinitionManager contentDefinitionManager, string partName, string position)
    {
        if (await contentDefinitionManager.GetTypeDefinitionAsync(AccountsConstants.ContentTypes.Account) is not { } accountType ||
            accountType.Parts.Any(part => part.PartDefinition?.Name == partName))
        {
            return;
        }

        await contentDefinitionManager.AlterTypeDefinitionAsync(AccountsConstants.ContentTypes.Account, type => type
            .WithPart(partName, part => part
                .WithPosition(position)));
    }
}
