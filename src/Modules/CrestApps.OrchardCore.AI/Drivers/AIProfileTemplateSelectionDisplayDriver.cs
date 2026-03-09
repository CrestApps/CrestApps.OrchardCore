using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Drivers;

internal sealed class AIProfileTemplateSelectionDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly IAIProfileTemplateService _templateService;

    public AIProfileTemplateSelectionDisplayDriver(IAIProfileTemplateService templateService)
    {
        _templateService = templateService;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        if (!context.IsNew)
        {
            return null;
        }

        return Initialize<AIProfileTemplateSelectionViewModel>("AIProfileTemplateSelection_Edit", async model =>
        {
            model.Source = profile.Source;
            var templates = await _templateService.GetListableAsync();

            model.Templates = templates
                .Select(t => new AIProfileTemplateOption
                {
                    Id = t.ItemId,
                    DisplayText = t.DisplayText ?? t.Name,
                    Category = t.Category,
                })
                .OrderBy(t => t.Category)
                .ThenBy(t => t.DisplayText)
                .ToList();
        }).Location("Content:0");
    }
}
