using System.Text.Json.Nodes;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.ModelBinding;

namespace CrestApps.OrchardCore.ContentFields.ConditionalFields;

/// <summary>
/// After every field editor has read the post (this handler is registered after Orchard Core's display coordinator),
/// works out which fields are hidden and, for each, gives back what the item held before the post and removes its
/// validation errors. So a value posted for a hidden field is never kept, and a required field that is hidden doesn't
/// block the form.
/// </summary>
internal sealed class ConditionalFieldsDisplayHandler : IContentDisplayHandler
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ConditionalFieldsService _service;
    private readonly ConditionalFieldsSnapshots _snapshots;

    public ConditionalFieldsDisplayHandler(IContentDefinitionManager contentDefinitionManager, ConditionalFieldsService service, ConditionalFieldsSnapshots snapshots)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _service = service;
        _snapshots = snapshots;
    }

    public Task BuildDisplayAsync(ContentItem contentItem, BuildDisplayContext context)
        => Task.CompletedTask;

    public Task BuildEditorAsync(ContentItem contentItem, BuildEditorContext context)
        => Task.CompletedTask;

    public async Task UpdateEditorAsync(ContentItem contentItem, UpdateEditorContext context)
    {
        var snapshot = _snapshots.Get(contentItem);

        if (snapshot is null)
        {
            // Nothing could be hidden on this item: no rules and no visibility provider.
            return;
        }

        var type = await _contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);
        var hidden = await _service.GetHiddenAsync(contentItem, type);

        if (hidden.Count == 0)
        {
            return;
        }

        var content = (JsonObject)contentItem.Content;

        foreach (var key in hidden)
        {
            var dot = key.IndexOf('.');

            if (dot <= 0)
            {
                continue;
            }

            var partName = key[..dot];
            var fieldName = key[(dot + 1)..];

            Restore(content, snapshot, partName, fieldName);
            RemoveErrors(context.Updater, string.IsNullOrEmpty(context.HtmlFieldPrefix) ? key : $"{context.HtmlFieldPrefix}.{key}");
        }
    }

    private static void Restore(JsonObject content, JsonObject snapshot, string partName, string fieldName)
    {
        var before = snapshot[partName]?[fieldName];

        if (content[partName] is not JsonObject part)
        {
            if (before is null)
            {
                return;
            }

            part = [];
            content[partName] = part;
        }

        if (before is null)
        {
            part.Remove(fieldName);
        }
        else
        {
            part[fieldName] = before.DeepClone();
        }
    }

    private static void RemoveErrors(IUpdateModel updater, string prefix)
    {
        var modelState = updater?.ModelState;

        if (modelState is null)
        {
            return;
        }

        foreach (var key in modelState.Keys.ToList())
        {
            if (key == prefix
                || key.StartsWith(prefix + ".", StringComparison.Ordinal)
                || key.StartsWith(prefix + "[", StringComparison.Ordinal))
            {
                modelState.Remove(key);
            }
        }
    }
}
