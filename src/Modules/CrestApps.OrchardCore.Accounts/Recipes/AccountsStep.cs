using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Accounts.Models;
using OrchardCore.ContentManagement;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Accounts.Recipes;

/// <summary>
/// Imports accounts, with every part on them (members, locations, business buying...), and the records they hold, such
/// as contacts and opportunities. The items are imported as Orchard Core's <c>Content</c> step imports them: an item
/// with an existing content item id is updated, so running the same recipe twice changes nothing. Accounts are imported
/// before their records, so a record lands in its account.
/// </summary>
internal sealed class AccountsStep : NamedRecipeStepHandler
{
    internal const string Name = "Accounts";

    private readonly IContentManager _contentManager;

    public AccountsStep(IContentManager contentManager)
        : base(Name)
    {
        _contentManager = contentManager;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var accounts = Read(context, "Accounts");

        foreach (var account in accounts.Where(account => !account.Has<AccountPart>()))
        {
            context.Errors.Add($"The item '{account.ContentItemId}' in the Accounts step is not an account: it has no AccountPart.");
        }

        accounts = [.. accounts.Where(account => account.Has<AccountPart>())];

        if (accounts.Count > 0)
        {
            await _contentManager.ImportAsync(accounts);
        }

        var records = Read(context, "Records");

        if (records.Count > 0)
        {
            await _contentManager.ImportAsync(records);
        }
    }

    private static List<ContentItem> Read(RecipeExecutionContext context, string property)
    {
        var items = new List<ContentItem>();

        foreach (var node in (context.Step[property] as JsonArray ?? []).OfType<JsonObject>())
        {
            var item = node.ToObject<ContentItem>();

            if (string.IsNullOrEmpty(item?.ContentItemId) || string.IsNullOrEmpty(item.ContentType))
            {
                context.Errors.Add($"An item in the {property} of the Accounts step has no ContentItemId or ContentType.");

                continue;
            }

            items.Add(item);
        }

        return items;
    }
}
