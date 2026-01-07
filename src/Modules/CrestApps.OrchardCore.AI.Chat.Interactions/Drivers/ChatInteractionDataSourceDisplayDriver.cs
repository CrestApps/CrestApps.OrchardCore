using CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Drivers;

/// <summary>
/// Display driver that shows data source selector when the chat interaction uses AzureOpenAIOwnData provider.
/// </summary>
public sealed class ChatInteractionDataSourceDisplayDriver : DisplayDriver<ChatInteraction>
{
    private readonly IAIDataSourceStore _dataSourceStore;
    private readonly AIOptions _aiOptions;

    private readonly IStringLocalizer S;

    public ChatInteractionDataSourceDisplayDriver(
        IAIDataSourceStore dataSourceStore,
        IOptions<AIOptions> aiOptions,
        IStringLocalizer<ChatInteractionDataSourceDisplayDriver> stringLocalizer)
    {
        _dataSourceStore = dataSourceStore;
        _aiOptions = aiOptions.Value;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ChatInteraction interaction, BuildEditorContext context)
    {
        // Only show data source selector for AzureOpenAIOwnData provider
        if (!string.Equals(interaction.Source, AzureOpenAIConstants.AzureOpenAIOwnData, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Check if data sources are configured for this provider
        var entries = _aiOptions.DataSources.Values
            .Where(x => string.Equals(x.ProfileSource, interaction.Source, StringComparison.OrdinalIgnoreCase));

        if (!entries.Any())
        {
            return null;
        }

        return Initialize<EditChatInteractionDataSourceViewModel>("ChatInteractionDataSource_Edit", async model =>
        {
            var metadata = interaction.As<ChatInteractionDataSourceMetadata>();
            model.DataSourceId = metadata?.DataSourceId;
            model.DataSources = (await _dataSourceStore.GetAsync(interaction.Source))
                .Select(x => new SelectListItem(x.DisplayText, x.ItemId));
        }).Location("Parameters:3#Settings:3");
    }

    public override async Task<IDisplayResult> UpdateAsync(ChatInteraction interaction, UpdateEditorContext context)
    {
        // Only update for AzureOpenAIOwnData provider
        if (!string.Equals(interaction.Source, AzureOpenAIConstants.AzureOpenAIOwnData, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var entries = _aiOptions.DataSources.Values
            .Where(x => string.Equals(x.ProfileSource, interaction.Source, StringComparison.OrdinalIgnoreCase));

        if (!entries.Any())
        {
            return null;
        }

        var model = new EditChatInteractionDataSourceViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (!string.IsNullOrEmpty(model.DataSourceId))
        {
            var dataSource = await _dataSourceStore.FindByIdAsync(model.DataSourceId);

            interaction.Put(new ChatInteractionDataSourceMetadata
            {
                DataSourceType = dataSource?.Type,
                DataSourceId = model.DataSourceId,
            });
        }
        else
        {
            // Clear the metadata if no data source is selected
            interaction.Put(new ChatInteractionDataSourceMetadata());
        }

        return Edit(interaction, context);
    }
}
