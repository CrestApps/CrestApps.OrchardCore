using System.Text.Json.Nodes;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Orders.Core.Services;

/// <summary>
/// Reads the categories a product is in: the taxonomy terms its taxonomy fields hold.
/// </summary>
public static class ProductCategories
{
    private const int MaxCategories = 100;

    /// <summary>
    /// Returns the term content item ids of every taxonomy field on the product, without duplicates.
    /// </summary>
    /// <param name="contentItem">The product.</param>
    public static string[] GetIds(ContentItem contentItem)
    {
        if (contentItem is null)
        {
            return [];
        }

        var ids = new List<string>();

        Collect(JObject.FromObject(contentItem), ids);

        return [.. ids.Distinct(StringComparer.Ordinal).Take(MaxCategories)];
    }

    private static void Collect(JsonNode node, List<string> ids)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                foreach (var (name, value) in jsonObject)
                {
                    if (name == "TermContentItemIds" && value is JsonArray terms)
                    {
                        ids.AddRange(terms.Select(term => term?.GetValueKind() == System.Text.Json.JsonValueKind.String ? term.GetValue<string>() : null).Where(id => !string.IsNullOrEmpty(id)));
                    }
                    else
                    {
                        Collect(value, ids);
                    }
                }

                break;

            case JsonArray jsonArray:
                foreach (var item in jsonArray)
                {
                    Collect(item, ids);
                }

                break;
        }
    }
}
