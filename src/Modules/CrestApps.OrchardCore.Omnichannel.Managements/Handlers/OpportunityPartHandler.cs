using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

/// <summary>
/// Gives a new opportunity the first stage of its type and copies the stage's closed, won and probability values
/// onto the opportunity whenever it is saved, so the pipeline can be read from the opportunity index alone.
/// </summary>
internal sealed class OpportunityPartHandler : ContentPartHandler<OpportunityPart>
{
    private readonly INamedCatalog<OpportunityStage> _stages;
    private readonly IContentDefinitionManager _contentDefinitionManager;

    private IReadOnlyList<OpportunityStage> _cachedStages;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpportunityPartHandler"/> class.
    /// </summary>
    /// <param name="stages">The opportunity stage catalog.</param>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    public OpportunityPartHandler(
        INamedCatalog<OpportunityStage> stages,
        IContentDefinitionManager contentDefinitionManager)
    {
        _stages = stages;
        _contentDefinitionManager = contentDefinitionManager;
    }

    public override Task CreatingAsync(CreateContentContext context, OpportunityPart part)
        => ApplyStageAsync(context.ContentItem.ContentType, part);

    public override Task UpdatingAsync(UpdateContentContext context, OpportunityPart part)
        => ApplyStageAsync(context.ContentItem.ContentType, part);

    public override Task ImportingAsync(ImportContentContext context, OpportunityPart part)
        => ApplyStageAsync(context.ContentItem.ContentType, part);

    private async Task ApplyStageAsync(string contentType, OpportunityPart part)
    {
        _cachedStages ??= (await _stages.GetAllAsync()).ToArray();

        var typeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentType);
        var stages = OpportunityStages.ForType(_cachedStages, typeDefinition);

        var stage = string.IsNullOrEmpty(part.StageId)
            ? null
            : _cachedStages.FirstOrDefault(entry => entry.ItemId == part.StageId);

        if (stage is null)
        {
            stage = stages.FirstOrDefault(entry => !entry.IsClosed) ?? (stages.Count > 0 ? stages[0] : null);
            part.StageId = stage?.ItemId;
            part.Probability = null;
        }

        part.IsClosed = stage?.IsClosed == true;
        part.IsWon = part.IsClosed && stage?.IsWon == true;

        // A closed stage settles the chance of winning; an open one keeps what the editor chose, starting from the
        // stage's default.
        if (stage is not null && (part.Probability is null || part.IsClosed))
        {
            part.Probability = stage.Probability;
        }

        // A part a handler changes is a copy until it is applied back to the content item.
        part.Apply();
    }
}
