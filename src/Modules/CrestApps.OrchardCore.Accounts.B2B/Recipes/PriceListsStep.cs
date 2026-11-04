using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.OrchardCore.Accounts.B2B.Models;
using CrestApps.OrchardCore.Accounts.B2B.Services;
using OrchardCore.Modules;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Accounts.B2B.Recipes;

/// <summary>
/// Creates or updates price lists from a recipe. A list is matched by its <c>ItemId</c>, then by its name, so running
/// the same recipe twice changes nothing.
/// </summary>
internal sealed class PriceListsStep : NamedRecipeStepHandler
{
    internal const string Name = "PriceLists";

    private readonly IPriceListStore _priceLists;
    private readonly IClock _clock;

    public PriceListsStep(IPriceListStore priceLists, IClock clock)
        : base(Name)
    {
        _priceLists = priceLists;
        _clock = clock;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var lists = context.Step["PriceLists"] as JsonArray ?? [];
        var existing = (await _priceLists.ListAllAsync()).ToList();

        foreach (var node in lists.OfType<JsonObject>())
        {
            var imported = node.Deserialize<PriceList>(JOptions.Default);

            if (string.IsNullOrWhiteSpace(imported?.Name))
            {
                context.Errors.Add("A price list in the PriceLists step has no Name.");

                continue;
            }

            var list = existing.FirstOrDefault(entry => !string.IsNullOrEmpty(imported.ItemId) && entry.ItemId == imported.ItemId)
                ?? existing.FirstOrDefault(entry => string.Equals(entry.Name, imported.Name.Trim(), StringComparison.OrdinalIgnoreCase));

            var isNew = list is null;

            list ??= new PriceList
            {
                ItemId = UniqueId.IsValid(imported.ItemId) ? imported.ItemId : UniqueId.GenerateId(),
                CreatedUtc = _clock.UtcNow,
            };

            list.Name = imported.Name.Trim();
            list.PercentOff = imported.PercentOff;
            list.Currency = imported.Currency;
            list.Entries = [.. (imported.Entries ?? []).Where(entry => !string.IsNullOrWhiteSpace(entry.Sku))];

            if (isNew)
            {
                await _priceLists.CreateAsync(list);
                existing.Add(list);
            }
            else
            {
                list.ModifiedUtc = _clock.UtcNow;
                await _priceLists.UpdateAsync(list);
            }
        }
    }
}
