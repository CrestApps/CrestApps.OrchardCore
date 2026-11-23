using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.MailingLists.Models;

namespace CrestApps.OrchardCore.MailingLists.Recipes;

/// <summary>
/// Creates or updates mailing lists, matched by <c>ItemId</c> (kept on a new list), through the list manager so the list rules run.
/// </summary>
internal sealed class MailingListsRecipeStep : CatalogRecipeStep<MailingList>
{
    /// <summary>
    /// The name of the step.
    /// </summary>
    public const string RecipeName = "MailingLists";

    /// <summary>
    /// The member of the step that holds the lists.
    /// </summary>
    public const string EntriesName = "MailingLists";

    public MailingListsRecipeStep(ICatalogManager<MailingList> manager)
        : base(RecipeName, EntriesName, manager)
    {
    }

    /// <summary>
    /// Creates a list that keeps the recipe's <c>ItemId</c>, so sign-up widgets, members and campaigns that name the list
    /// by id still find it on the site the recipe runs on.
    /// </summary>
    /// <param name="data">The list's entry in the step.</param>
    protected override async ValueTask<MailingList> NewEntryAsync(JsonObject data)
    {
        var list = await base.NewEntryAsync(data);

        if (data[nameof(CatalogItem.ItemId)] is JsonValue value && value.TryGetValue<string>(out var itemId) && !string.IsNullOrWhiteSpace(itemId))
        {
            list.ItemId = itemId.Trim();
        }

        return list;
    }
}
