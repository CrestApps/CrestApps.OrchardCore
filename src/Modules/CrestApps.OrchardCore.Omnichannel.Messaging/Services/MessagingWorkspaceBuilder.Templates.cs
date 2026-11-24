using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Services;

// The reply templates the composer offers: the workspace's own, filled in for the open conversation's contact.
public sealed partial class MessagingWorkspaceBuilder
{
    /// <summary>
    /// Returns the workspace's reply templates (those without a scope), with their placeholders filled in for the
    /// conversation's contact.
    /// </summary>
    private async Task<MessageTemplate[]> GetTemplatesAsync(string contactName, CancellationToken cancellationToken)
    {
        var models = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["Contact"] = new { DisplayName = contactName ?? string.Empty },
        };

        var templates = new List<MessageTemplate>();

        // Only the workspace's own templates are read, from the index; other scopes' never load here.
        foreach (var template in await _templateManager.ListByScopeAsync(scope: null, cancellationToken))
        {
            templates.Add(new MessageTemplate
            {
                ItemId = template.ItemId,
                Name = template.Name,
                Body = await _templateRenderer.RenderAsync(template, models, cancellationToken),
            });
        }

        return [.. templates];
    }
}
