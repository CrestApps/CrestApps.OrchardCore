namespace CrestApps.OrchardCore.AgenticCommerce.Core.Services;

/// <summary>
/// The product and variant an agent's item id names.
/// </summary>
/// <param name="ItemId">The item id.</param>
/// <param name="ContentItemId">The product's content item id.</param>
/// <param name="VariantId">The variant's id, or <see langword="null"/>.</param>
public sealed record AgentItem(string ItemId, string ContentItemId, string VariantId);
