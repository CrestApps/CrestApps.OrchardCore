using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Resolves the stages an opportunity type moves through.
/// </summary>
internal static class OpportunityStages
{
    /// <summary>
    /// Returns the stages that apply to the opportunity type, in pipeline order. A type that chooses no stages uses
    /// every stage in the catalog.
    /// </summary>
    /// <param name="stages">Every stage in the catalog.</param>
    /// <param name="typeDefinition">The opportunity content type definition.</param>
    public static IReadOnlyList<OpportunityStage> ForType(IEnumerable<OpportunityStage> stages, ContentTypeDefinition typeDefinition)
    {
        var ordered = stages
            .OrderBy(stage => stage.Order)
            .ThenBy(stage => stage.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var typePart = typeDefinition?.Parts
            .FirstOrDefault(part => part.PartDefinition?.Name == OmnichannelConstants.ContentParts.Opportunity);

        var selected = typePart?.GetSettings<OpportunityPartSettings>()?.StageIds;

        if (selected is null || selected.Length == 0)
        {
            return ordered;
        }

        var selectedIds = selected.ToHashSet(StringComparer.Ordinal);

        return ordered
            .Where(stage => selectedIds.Contains(stage.ItemId))
            .ToArray();
    }
}
