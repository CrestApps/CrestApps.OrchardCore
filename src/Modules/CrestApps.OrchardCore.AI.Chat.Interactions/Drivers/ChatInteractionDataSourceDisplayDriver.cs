using CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Drivers;

/// <summary>
/// Display driver that shows data source selector for chat interactions.
/// </summary>
public sealed class ChatInteractionDataSourceDisplayDriver : DisplayDriver<ChatInteraction>
{
    private readonly ICatalog<AIDataSource> _dataSourceStore;

    public ChatInteractionDataSourceDisplayDriver(ICatalog<AIDataSource> dataSourceStore)
    {
        _dataSourceStore = dataSourceStore;
    }

    public override IDisplayResult Edit(ChatInteraction interaction, BuildEditorContext context)
    {
        return Initialize<EditChatInteractionDataSourceViewModel>("ChatInteractionDataSource_Edit", async model =>
        {
            var metadata = interaction.As<ChatInteractionDataSourceMetadata>();
            model.DataSourceId = metadata?.DataSourceId;

            var ragMetadata = interaction.As<AIDataSourceRagMetadata>();
            model.Strictness = ragMetadata?.Strictness;
            model.TopNDocuments = ragMetadata?.TopNDocuments;
            model.IsInScope = ragMetadata?.IsInScope ?? true;
            model.Filter = ragMetadata?.Filter;

            model.DataSources = await _dataSourceStore.GetAllAsync();
        }).Location("Parameters:3#Settings:3");
    }

    public override async Task<IDisplayResult> UpdateAsync(ChatInteraction interaction, UpdateEditorContext context)
    {
        var model = new EditChatInteractionDataSourceViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (!string.IsNullOrEmpty(model.DataSourceId))
        {
            var dataSource = await _dataSourceStore.FindByIdAsync(model.DataSourceId);

            if (dataSource != null)
            {
                interaction.Put(new ChatInteractionDataSourceMetadata
                {
                    DataSourceId = dataSource.ItemId,
                });
            }
        }
        else
        {
            // Clear the metadata if no data source is selected
            interaction.Put(new ChatInteractionDataSourceMetadata());
        }

        interaction.Put(new AIDataSourceRagMetadata
        {
            Strictness = model.Strictness,
            TopNDocuments = model.TopNDocuments,
            IsInScope = model.IsInScope,
            Filter = model.Filter,
        });

        return Edit(interaction, context);
    }
}
