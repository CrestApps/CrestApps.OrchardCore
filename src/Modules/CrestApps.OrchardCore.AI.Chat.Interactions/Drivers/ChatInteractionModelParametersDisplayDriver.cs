using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Core.ViewModels;
using Microsoft.Extensions.Localization;
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

    internal readonly IStringLocalizer S;

    public ChatInteractionModelParametersDisplayDriver(
        AIModelParameterViewService viewService,
        IStringLocalizer<ChatInteractionModelParametersDisplayDriver> stringLocalizer)
    {
        _viewService = viewService;
        S = stringLocalizer;
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
            // The interaction persists via the SignalR settings hub, so expose the inputs as namespaced
            // setting-inputs the hub collects and ApplyCoreSettingsAsync stores on AIDeploymentParametersMetadata.
            model.SettingKeyPrefix = ChatInteractionModelParameterSettingKeys.ChatDeployment;
            // The settings panel stacks each label above its input.
            model.UseStackedLayout = true;
        }).Location(l => l.Zone("Parameters", "3.5").Tab(S["Settings"], "1").Card(S["Deployments & Interactions"], "2"));
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
