using CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Drivers;

/// <summary>
/// Display driver that shows data source selector for chat interactions.
/// </summary>
public sealed class ChatInteractionDataSourceDisplayDriver : DisplayDriver<ChatInteraction>
{
    private readonly ISiteService _siteService;
    private readonly ICatalog<AIDataSource> _dataSourceStore;

    internal readonly IStringLocalizer<ChatInteractionDataSourceDisplayDriver> S;

    public ChatInteractionDataSourceDisplayDriver(
        ISiteService siteService,
        ICatalog<AIDataSource> dataSourceStore,
        IStringLocalizer<ChatInteractionDataSourceDisplayDriver> stringLocalizer)
    {
        _siteService = siteService;
        _dataSourceStore = dataSourceStore;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ChatInteraction interaction, BuildEditorContext context)
    {
        return Initialize<EditChatInteractionDataSourceViewModel>("ChatInteractionDataSource_Edit", async model =>
        {
            var dataSourceSettings = await _siteService.GetSettingsAsync<AIDataSourceSettings>();

            var metadata = interaction.As<ChatInteractionDataSourceMetadata>();
            model.DataSourceId = metadata?.DataSourceId;

            var ragMetadata = interaction.As<AIDataSourceRagMetadata>();

            model.Strictness = dataSourceSettings.GetStrictness(ragMetadata.Strictness);
            model.TopNDocuments = dataSourceSettings.GetTopNDocuments(ragMetadata.TopNDocuments);
            model.IsInScope = ragMetadata.IsInScope;
            model.EnableEarlyRag = context.IsNew ? dataSourceSettings.EnableEarlyRag : ragMetadata.EnableEarlyRag;
            model.Filter = ragMetadata.Filter;

            model.DataSources = await _dataSourceStore.GetAllAsync();
        }).Location("Parameters:1#Settings;3");
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

        var dataSourceSettings = await _siteService.GetSettingsAsync<AIDataSourceSettings>();

        var strictness = dataSourceSettings.GetStrictness(model.Strictness);
        var topN = dataSourceSettings.GetTopNDocuments(model.TopNDocuments);

        if (strictness != model.Strictness)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.Strictness),
                S["Invalid strictness value. A valid value must be between {0} and {1}.", AIDataSourceSettings.MinStrictness, AIDataSourceSettings.MaxStrictness]);
        }

        if (topN != model.TopNDocuments)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.TopNDocuments),
                S["Invalid total retrieved documents value. A valid value must be between {0} and {1}.", AIDataSourceSettings.MinTopNDocuments, AIDataSourceSettings.MaxTopNDocuments]);
        }

        interaction.Put(new AIDataSourceRagMetadata
        {
            Strictness = strictness,
            TopNDocuments = topN,
            IsInScope = model.IsInScope,
            EnableEarlyRag = model.EnableEarlyRag,
            Filter = model.Filter,
        });

        return Edit(interaction, context);
    }
}
