using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement.Handlers;

using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Drivers;

internal sealed class AIProfileTemplateSelectionDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly IAIProfileTemplateManager _templateManager;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileTemplateSelectionDisplayDriver"/> class.
    /// </summary>
    /// <param name="templateManager">The template manager.</param>
    /// <param name="httpContextAccessor">The http context accessor.</param>
    public AIProfileTemplateSelectionDisplayDriver(
        IAIProfileTemplateManager templateManager,
        IHttpContextAccessor httpContextAccessor)
    {
        _templateManager = templateManager;
        _httpContextAccessor = httpContextAccessor;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        if (!context.IsNew)
        {
            return null;
        }

        return Initialize<AIProfileTemplateSelectionViewModel>("AIProfileTemplateSelection_Edit", async model =>
        {
            // Applying a template reloads the page with ?templateId=, and the reloaded select has to come
            // back with that template chosen. Without it the select renders on its empty option, the POST
            // carries no template, and the document-clone path that reads this value off the form never
            // runs -- so a profile built from a template arrives with none of the template's files.
            model.TemplateId = _httpContextAccessor.HttpContext?.Request?.Query["templateId"].ToString();

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
        }).Location("Content:0%Apply Templates;1");
    }
}
