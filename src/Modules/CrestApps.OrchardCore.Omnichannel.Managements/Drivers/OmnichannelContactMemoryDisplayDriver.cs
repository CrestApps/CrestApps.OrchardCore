using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

/// <summary>
/// Shows, edits and clears what the AI remembers about a contact, on the contact's editor.
/// </summary>
internal sealed class OmnichannelContactMemoryDisplayDriver : ContentPartDisplayDriver<OmnichannelContactPart>
{
    private readonly AIContactMemoryService _memoryService;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelContactMemoryDisplayDriver"/> class.
    /// </summary>
    /// <param name="memoryService">The contact memory service.</param>
    public OmnichannelContactMemoryDisplayDriver(AIContactMemoryService memoryService)
    {
        _memoryService = memoryService;
    }

    public override IDisplayResult Edit(OmnichannelContactPart part, BuildPartEditorContext context)
    {
        return Initialize<OmnichannelContactMemoryViewModel>("OmnichannelContactMemory_Edit", async model =>
        {
            model.AIMemory = context.IsNew ? null : await _memoryService.GetAsync(part.ContentItem.ContentItemId);
        }).Location("Parts:3");
    }

    public override async Task<IDisplayResult> UpdateAsync(OmnichannelContactPart part, UpdatePartEditorContext context)
    {
        var model = new OmnichannelContactMemoryViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var contactId = part.ContentItem.ContentItemId;
        var current = context.IsNew ? null : await _memoryService.GetAsync(contactId);

        // Only a change is written, so saving a contact does not stamp the note as edited by a person.
        if (!string.Equals(current?.Trim() ?? string.Empty, model.AIMemory?.Trim() ?? string.Empty, StringComparison.Ordinal))
        {
            await _memoryService.SetAsync(contactId, model.AIMemory);
        }

        // A rebuild reads every past conversation with a model call each, so it runs after the request.
        if (model.Rebuild && !context.IsNew)
        {
            ShellScope.AddDeferredTask(scope => OmnichannelContactMemoryRebuild.RunAsync(scope.ServiceProvider, contactId));
        }

        return Edit(part, context);
    }
}
