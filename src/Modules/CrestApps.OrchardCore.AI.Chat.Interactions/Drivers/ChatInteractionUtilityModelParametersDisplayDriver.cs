using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Core.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Drivers;

/// <summary>
/// Renders the metadata-driven model parameter editor for the <em>utility</em> deployment on the chat
/// interaction editor and persists the selected values onto <see cref="UtilityDeploymentParametersMetadata"/>.
/// </summary>
public sealed class ChatInteractionUtilityModelParametersDisplayDriver : DisplayDriver<ChatInteraction>
{
    private const string BindingPrefix = "UtilityModelParameters";

    private readonly AIModelParameterViewService _viewService;

    public ChatInteractionUtilityModelParametersDisplayDriver(AIModelParameterViewService viewService)
    {
        _viewService = viewService;
    }

    public override IDisplayResult Edit(ChatInteraction interaction, BuildEditorContext context)
    {
        return Initialize<ModelParameterEditorViewModel>("AIModelParameters_Edit", async model =>
        {
            interaction.TryGet<UtilityDeploymentParametersMetadata>(out var metadata);

            var built = await _viewService.BuildAsync(metadata?.Values, "UtilityDeploymentName", "interactionUtilityModelParameters", BindingPrefix);

            model.DeploymentFieldName = built.DeploymentFieldName;
            model.ElementPrefix = built.ElementPrefix;
            model.BindingPrefix = built.BindingPrefix;
            model.Parameters = built.Parameters;
            model.CapabilitiesJson = built.CapabilitiesJson;
            model.FeaturesJson = built.FeaturesJson;
            // The interaction persists via the SignalR settings hub, so expose the inputs as namespaced
            // setting-inputs the hub collects and ApplyCoreSettingsAsync stores on UtilityDeploymentParametersMetadata.
            model.SettingKeyPrefix = ChatInteractionModelParameterSettingKeys.UtilityDeployment;
        }).Location("Parameters:3.8#Settings;1");
    }

    public override async Task<IDisplayResult> UpdateAsync(ChatInteraction interaction, UpdateEditorContext context)
    {
        var model = new ModelParameterEditorViewModel();

        await context.Updater.TryUpdateModelAsync(model, $"{Prefix}.{BindingPrefix}");

        var metadata = interaction.GetOrCreate<UtilityDeploymentParametersMetadata>();

        metadata.Values = (model.Values ?? [])
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Value))
            .ToDictionary(entry => entry.Key, entry => entry.Value.Trim(), StringComparer.OrdinalIgnoreCase);

        interaction.Put(metadata);

        return Edit(interaction, context);
    }
}
