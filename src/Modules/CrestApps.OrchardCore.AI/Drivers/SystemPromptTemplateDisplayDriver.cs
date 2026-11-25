using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Drivers;

/// <summary>
/// Display driver for SystemPrompt-source templates.
/// Captures the system message stored in <see cref="SystemPromptTemplateMetadata"/>.
/// </summary>
internal sealed class SystemPromptTemplateDisplayDriver : DisplayDriver<AIProfileTemplate>
{
    internal readonly IStringLocalizer S;

    public SystemPromptTemplateDisplayDriver(IStringLocalizer<SystemPromptTemplateDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfileTemplate template, BuildEditorContext context)
    {
        return Initialize<SystemPromptTemplateViewModel>("SystemPromptTemplate_Edit", model =>
        {
            var metadata = template.GetOrCreate<SystemPromptTemplateMetadata>();
            model.SystemMessage = metadata.SystemMessage;
        }).Location(l => l.Zone("Content", "10").Card(S["System Prompt"], "20"))
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

        var metadata = template.GetOrCreate<SystemPromptTemplateMetadata>();
        metadata.SystemMessage = model.SystemMessage;
        template.Put(metadata);

        return Edit(template, context);
    }
}
