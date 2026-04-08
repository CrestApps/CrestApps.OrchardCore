using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using CrestApps.Core;

namespace CrestApps.OrchardCore.AI.Drivers;

/// <summary>
/// Display driver for SystemPrompt-source templates.
/// Captures the system message stored in <see cref="SystemPromptTemplateMetadata"/>.
/// </summary>
internal sealed class SystemPromptTemplateDisplayDriver : DisplayDriver<AIProfileTemplate>
{
    public override IDisplayResult Edit(AIProfileTemplate template, BuildEditorContext context)
    {
        return Initialize<SystemPromptTemplateViewModel>("SystemPromptTemplate_Edit", model =>
        {
            var metadata = template.As<SystemPromptTemplateMetadata>();
            model.SystemMessage = metadata.SystemMessage;
        }).Location("Content:10")
        .RenderWhen(() => Task.FromResult(template.Source == AITemplateSources.SystemPrompt));
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfileTemplate template, UpdateEditorContext context)
    {
        if (template.Source != AITemplateSources.SystemPrompt)
        {
            return null;
        }

        var model = new SystemPromptTemplateViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var metadata = template.As<SystemPromptTemplateMetadata>();
        metadata.SystemMessage = model.SystemMessage;
        template.Put(metadata);

        return Edit(template, context);
    }
}
