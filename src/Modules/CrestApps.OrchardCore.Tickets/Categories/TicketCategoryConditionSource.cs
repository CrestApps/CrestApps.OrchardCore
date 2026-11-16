using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.ContentFields.Conditions;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Tickets.Categories;

/// <summary>
/// Lets the field rules of a ticket type read the ticket's category (<see cref="TicketsConstants.CategoryConditionSource"/>),
/// with the categories above it, so "Laptop model" can show for Hardware > Laptop and a rule on Hardware covers its
/// sub-categories.
/// </summary>
internal sealed class TicketCategoryConditionSource : IContentConditionSource
{
    /// <summary>
    /// The name the ticket forms post the category under, which the browser reads.
    /// </summary>
    public const string InputName = "CategoryId";

    private readonly ITicketCategoryService _categories;

    internal readonly IStringLocalizer S;

    public TicketCategoryConditionSource(ITicketCategoryService categories, IStringLocalizer<TicketCategoryConditionSource> stringLocalizer)
    {
        _categories = categories;
        S = stringLocalizer;
    }

    public async Task<IEnumerable<ContentConditionSourceDescriptor>> DescribeAsync(ContentTypeDefinition type)
    {
        if (type is null || !type.Parts.Any(part => part.PartDefinition.Name == TicketsConstants.PartName))
        {
            return [];
        }

        var categories = await _categories.ListAsync(type.Name);

        if (categories.Count == 0)
        {
            return [];
        }

        var descriptor = new ContentConditionSourceDescriptor
        {
            Name = TicketsConstants.CategoryConditionSource,
            DisplayName = S["Category"],
            Kind = TicketsConstants.CategoryConditionSource,
            InputName = InputName,
            IsHierarchical = true,
        };

        foreach (var category in categories)
        {
            descriptor.Options.Add(new ContentConditionOption(category.Id, category.Name, category.Level));

            var ancestors = Ancestors(category);

            if (ancestors.Length > 0)
            {
                descriptor.Ancestors[category.Id] = ancestors;
            }
        }

        return [descriptor];
    }

    public Task<ConditionValue> ReadAsync(string name, ContentItem contentItem)
    {
        if (name != TicketsConstants.CategoryConditionSource)
        {
            return Task.FromResult<ConditionValue>(null);
        }

        if (contentItem is null || !contentItem.TryGet<TicketPart>(out var part) || string.IsNullOrEmpty(part.CategoryId))
        {
            return Task.FromResult(ConditionValue.Empty);
        }

        // The path the ticket keeps holds the categories above it, top first.
        var ancestors = (part.CategoryPath ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries).Where(id => id != part.CategoryId);

        return Task.FromResult(new ConditionValue([part.CategoryId], ancestors));
    }

    // The ids above a category, parent first, from its path (top first).
    private static string[] Ancestors(TicketCategory category)
        => [.. (category.Path ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries).Where(id => id != category.Id).Reverse()];
}
