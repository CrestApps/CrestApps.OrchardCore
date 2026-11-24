using CrestApps.OrchardCore.Learning.AI.Models;
using CrestApps.OrchardCore.Learning.AI.ViewModels;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.AI.Drivers;

/// <summary>
/// The learning assistant switch on the course editor. It is off until an author turns it on.
/// </summary>
internal sealed class LearningAssistantPartDisplayDriver : ContentPartDisplayDriver<LearningAssistantPart>
{
    /// <inheritdoc />
    public override IDisplayResult Edit(LearningAssistantPart part, BuildPartEditorContext context)
    {
        return Initialize<LearningAssistantPartViewModel>(GetEditorShapeType(context), model => model.Enabled = part.Enabled);
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(LearningAssistantPart part, UpdatePartEditorContext context)
    {
        var model = new LearningAssistantPartViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        part.Enabled = model.Enabled;

        return Edit(part, context);
    }
}
