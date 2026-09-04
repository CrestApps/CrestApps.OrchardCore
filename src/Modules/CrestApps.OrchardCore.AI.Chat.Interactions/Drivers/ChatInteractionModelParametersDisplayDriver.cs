using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Core.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Drivers;

/// <summary>
/// Renders the metadata-driven model parameter editor on the chat interaction editor and persists the
/// selected values onto <see cref="AIDeploymentParametersMetadata"/>.
/// </summary>
public sealed class ChatInteractionModelParametersDisplayDriver : DisplayDriver<ChatInteraction>
{
    private readonly AIModelParameterViewService _viewService;

    public ChatInteractionModelParametersDisplayDriver(AIModelParameterViewService viewService)
    {
        _viewService = viewService;
    }

    public override IDisplayResult Edit(ChatInteraction interaction, BuildEditorContext context)
    {
        return Initialize<ModelParameterEditorViewModel>("AIModelParameters_Edit", async model =>
        {
            interaction.TryGet<AIDeploymentParametersMetadata>(out var metadata);

            var built = await _viewService.BuildAsync(metadata?.Values, "ChatDeploymentName", "interactionModelParameters");

            model.DeploymentFieldName = built.DeploymentFieldName;
            model.ElementPrefix = built.ElementPrefix;
            model.Parameters = built.Parameters;
            model.CapabilitiesJson = built.CapabilitiesJson;
            model.FeaturesJson = built.FeaturesJson;
        }).Location("Parameters:3#Settings;1");
    }

    public override async Task<IDisplayResult> UpdateAsync(ChatInteraction interaction, UpdateEditorContext context)
    {
        var model = new ModelParameterEditorViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var metadata = interaction.GetOrCreate<AIDeploymentParametersMetadata>();

        metadata.Values = (model.Values ?? [])
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Value))
            .ToDictionary(entry => entry.Key, entry => entry.Value.Trim(), StringComparer.OrdinalIgnoreCase);

        interaction.Put(metadata);

        return Edit(interaction, context);
    }
}
