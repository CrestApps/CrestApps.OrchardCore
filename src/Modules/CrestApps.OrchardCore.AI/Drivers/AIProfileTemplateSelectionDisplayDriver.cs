using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Drivers;

internal sealed class AIProfileTemplateSelectionDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly IAIProfileTemplateManager _templateManager;

    public AIProfileTemplateSelectionDisplayDriver(IAIProfileTemplateManager templateManager)
    {
        _templateManager = templateManager;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        if (!context.IsNew)
        {
            return null;
        }

        return Initialize<AIProfileTemplateSelectionViewModel>("AIProfileTemplateSelection_Edit", async model =>
        {
            var templates = await _templateManager.GetAsync(AITemplateSources.Profile);

            var groups = new Dictionary<string, SelectListGroup>();

            model.Templates = templates
                .Where(t => t.IsListable)
                .OrderBy(t => t.Category)
                .ThenBy(t => t.DisplayText ?? t.Name)
                .Select(t =>
                {
                    var item = new SelectListItem(t.DisplayText ?? t.Name, t.ItemId);

                    if (!string.IsNullOrEmpty(t.Category))
                    {
                        if (!groups.TryGetValue(t.Category, out var group))
                        {
                            group = new SelectListGroup { Name = t.Category };
                            groups.Add(t.Category, group);
                        }

                        item.Group = group;
                    }

                    return item;
                })
                .ToList();
        }).Location("Content:0");
    }
}
