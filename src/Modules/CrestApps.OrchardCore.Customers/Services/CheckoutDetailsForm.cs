using System.Text.Json.Nodes;
using System.Text.Json.Settings;
using CrestApps.OrchardCore.Customers.Core;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.Customers.Services;

/// <summary>
/// The store's checkout fields: whether there are any, and the Checkout details item that edits or shows what a buyer
/// entered.
/// </summary>
internal sealed class CheckoutDetailsForm
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IContentManager _contentManager;

    public CheckoutDetailsForm(
        IContentDefinitionManager contentDefinitionManager,
        IContentManager contentManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _contentManager = contentManager;
    }

    /// <summary>
    /// Gets whether the store has added anything to ask for: a field, or a part of its own.
    /// </summary>
    public async Task<bool> HasFieldsAsync()
    {
        var type = await _contentDefinitionManager.GetTypeDefinitionAsync(CustomersConstants.CheckoutDetailsContentType);

        return type is not null && type.Parts.Any(part =>
            part.PartDefinition.Fields.Any() ||
            !string.Equals(part.PartDefinition.Name, CustomersConstants.CheckoutDetailsContentType, StringComparison.Ordinal));
    }

    /// <summary>
    /// Makes a Checkout details item holding what was entered before, or a new one.
    /// </summary>
    public async Task<ContentItem> NewAsync(JsonObject content)
    {
        var item = await _contentManager.NewAsync(CustomersConstants.CheckoutDetailsContentType);

        if (content is not null)
        {
            item.Merge(content.DeepClone(), new JsonMergeSettings
            {
                MergeArrayHandling = MergeArrayHandling.Replace,
            });
        }

        return item;
    }

    /// <summary>
    /// Gets what was entered, to keep on the order: the item's parts and fields, without its metadata.
    /// </summary>
    public static JsonObject GetContent(ContentItem item)
    {
        var content = new JsonObject();

        foreach (var (name, value) in (JsonObject)item.Content)
        {
            if (value is JsonObject)
            {
                content[name] = value.DeepClone();
            }
        }

        return content;
    }
}
