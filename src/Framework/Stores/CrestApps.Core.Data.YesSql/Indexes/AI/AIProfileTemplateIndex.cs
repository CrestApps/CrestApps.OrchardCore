using CrestApps.Core.AI.Models;
using YesSql.Indexes;

namespace CrestApps.Core.Data.YesSql.Indexes.AI;

/// <summary>
/// YesSql index for AI profile templates stored in the database.
/// </summary>
public sealed class AIProfileTemplateIndex : CatalogItemIndex, INameAwareIndex, ISourceAwareIndex
{
    /// <summary>
    /// Gets or sets the source that produced this template.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the template.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the category for grouping templates.
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// Gets or sets the profile type this template creates.
    /// Stored as the string representation of <see cref="Models.AIProfileType"/>.
    /// </summary>
    public string ProfileType { get; set; }

    /// <summary>
    /// Gets or sets whether the template is listable in the admin UI.
    /// </summary>
    public bool IsListable { get; set; }
}

public sealed class AIProfileTemplateIndexProvider : IndexProvider<AIProfileTemplate>
{
    public AIProfileTemplateIndexProvider()
    {
        CollectionName = OrchardCoreAICollectionNames.AI;
    }

    public override void Describe(DescribeContext<AIProfileTemplate> context)
    {
        context
            .For<AIProfileTemplateIndex>()
            .Map(template =>
            {
                var profileMetadata = template.As<ProfileTemplateMetadata>();

                return new AIProfileTemplateIndex
                {
                    ItemId = template.ItemId,
                    Source = template.Source,
                    Name = template.Name,
                    Category = template.Category,
                    ProfileType = profileMetadata.ProfileType?.ToString(),
                    IsListable = template.IsListable,
                };
            });
    }
}
