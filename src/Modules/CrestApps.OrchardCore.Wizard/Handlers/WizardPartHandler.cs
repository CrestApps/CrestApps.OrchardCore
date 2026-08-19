using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Wizard.Core.Models;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentManagement.Routing;

namespace CrestApps.OrchardCore.Wizard.Handlers;

/// <summary>
/// Exposes the step-definition content items of a <see cref="WizardPart"/> to the contained-content-items
/// aspect so nested items are versioned, indexed, and cloned correctly by the content pipeline.
/// </summary>
public sealed class WizardPartHandler : ContentPartHandler<WizardPart>
{
    /// <inheritdoc/>
    public override Task GetContentItemAspectAsync(ContentItemAspectContext context, WizardPart part)
    {
        return context.ForAsync<ContainedContentItemsAspect>(aspect =>
        {
            aspect.Accessors.Add((jsonObject) =>
            {
                var jContent = (JsonObject)part.Content;

                return jsonObject[jContent.GetNormalizedPath()]["Steps"] as JsonArray;
            });

            return Task.CompletedTask;
        });
    }
}
