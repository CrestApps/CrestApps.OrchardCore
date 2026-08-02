using CrestApps.OrchardCore.AI.Workflows.Models;
using CrestApps.OrchardCore.AI.Workflows.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Workflows.Activities;

namespace CrestApps.OrchardCore.AI.Workflows.Drivers;

/// <summary>
/// Display driver that contributes the system instructions and model tuning parameters to the
/// <see cref="AICompletionWithConfigTask"/> workflow activity Settings tab.
/// </summary>
public sealed class AICompletionWithConfigParametersDisplayDriver : DisplayDriver<IActivity, AICompletionWithConfigTask>
{
    public override IDisplayResult Edit(AICompletionWithConfigTask activity, BuildEditorContext context)
    {
        return Initialize<AICompletionWithConfigParametersViewModel>("AICompletionWithConfigParameters_Edit", model =>
        {
            var interaction = activity.Interaction;

            model.SystemMessage = interaction.SystemMessage;
            model.MaxTokens = interaction.MaxTokens;
            model.Temperature = interaction.Temperature;
            model.TopP = interaction.TopP;
            model.FrequencyPenalty = interaction.FrequencyPenalty;
            model.PresencePenalty = interaction.PresencePenalty;
        }).Location("Content:4#Content;1");
    }

    public override async Task<IDisplayResult> UpdateAsync(AICompletionWithConfigTask activity, UpdateEditorContext context)
    {
        var model = new AICompletionWithConfigParametersViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var interaction = activity.Interaction;

        interaction.SystemMessage = model.SystemMessage;
        interaction.MaxTokens = model.MaxTokens;
        interaction.Temperature = model.Temperature;
        interaction.TopP = model.TopP;
        interaction.FrequencyPenalty = model.FrequencyPenalty;
        interaction.PresencePenalty = model.PresencePenalty;

        activity.Interaction = interaction;

        return Edit(activity, context);
    }
}
