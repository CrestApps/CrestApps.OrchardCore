using CrestApps.Core;
using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Drivers;

/// <summary>
/// Display driver that shows data source selector for chat interactions.
/// </summary>
public sealed class ChatInteractionDataSourceDisplayDriver : DisplayDriver<ChatInteraction>
{
    private readonly ISiteService _siteService;
    private readonly IAIDataSourceStore _dataSourceStore;
    private readonly IODataValidator _oDataValidator;

    internal readonly IStringLocalizer<ChatInteractionDataSourceDisplayDriver> S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatInteractionDataSourceDisplayDriver"/> class.
    /// </summary>
    /// <param name="siteService">The site service.</param>
    /// <param name="dataSourceStore">The data source store.</param>
    /// <param name="oDataValidator">The o data validator.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ChatInteractionDataSourceDisplayDriver(
        ISiteService siteService,
        IAIDataSourceStore dataSourceStore,
        IODataValidator oDataValidator,
        IStringLocalizer<ChatInteractionDataSourceDisplayDriver> stringLocalizer)
    {
        _siteService = siteService;
        _dataSourceStore = dataSourceStore;
        _oDataValidator = oDataValidator;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ChatInteraction interaction, BuildEditorContext context)
    {
        var selectorAndBehaviorResult = Initialize<EditChatInteractionDataSourceViewModel>("ChatInteractionDataSource_Edit", async model =>
        {
            var dataSourceSettings = await _siteService.GetSettingsAsync<AIDataSourceSettings>();

            var metadata = interaction.GetOrCreate<DataSourceMetadata>();
            model.DataSourceId = metadata?.DataSourceId;

            var ragMetadata = interaction.GetOrCreate<AIDataSourceRagMetadata>();

            model.Strictness = dataSourceSettings.GetStrictness(ragMetadata.Strictness);
            model.TopNDocuments = dataSourceSettings.GetTopNDocuments(ragMetadata.TopNDocuments);
            model.IsInScope = ragMetadata.IsInScope;
            model.Filter = ragMetadata.Filter;

            model.DataSources = await _dataSourceStore.GetAllAsync();
        }).Location("Parameters:4#Knowledge;2");

        var retrievalResult = Initialize<EditChatInteractionDataSourceViewModel>("ChatInteractionDataSourceRetrieval_Edit", async model =>
        {
            var dataSourceSettings = await _siteService.GetSettingsAsync<AIDataSourceSettings>();

            var metadata = interaction.GetOrCreate<DataSourceMetadata>();
            model.DataSourceId = metadata?.DataSourceId;

            var ragMetadata = interaction.GetOrCreate<AIDataSourceRagMetadata>();

            model.Strictness = dataSourceSettings.GetStrictness(ragMetadata.Strictness);
            model.TopNDocuments = dataSourceSettings.GetTopNDocuments(ragMetadata.TopNDocuments);
            model.IsInScope = ragMetadata.IsInScope;
            model.Filter = ragMetadata.Filter;

            model.DataSources = await _dataSourceStore.GetAllAsync();
        }).Location("Parameters:5#Knowledge;2");

        return Combine(selectorAndBehaviorResult, retrievalResult);
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
                interaction.Alter<DataSourceMetadata>(metadata =>
                {
                    metadata.DataSourceId = dataSource.ItemId;
                });
            }
        }
        else
        {
            interaction.Alter<DataSourceMetadata>(metadata => metadata.DataSourceId = null);
        }

        var dataSourceSettings = await _siteService.GetSettingsAsync<AIDataSourceSettings>();

        var strictness = dataSourceSettings.GetStrictness(model.Strictness);
        var topN = dataSourceSettings.GetTopNDocuments(model.TopNDocuments);

        if (strictness != model.Strictness)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Strictness),
            S["Invalid strictness value. A valid value must be between {0} and {1}.", AIDataSourceSettings.MinStrictness, AIDataSourceSettings.MaxStrictness]);
        }

        if (topN != model.TopNDocuments)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.TopNDocuments),
            S["Invalid total retrieved documents value. A valid value must be between {0} and {1}.", AIDataSourceSettings.MinTopNDocuments, AIDataSourceSettings.MaxTopNDocuments]);
        }

        if (!string.IsNullOrWhiteSpace(model.Filter) && !_oDataValidator.IsValidFilter(model.Filter))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Filter), S["Invalid filter value. It must be a valid OData filter."]);
        }

        interaction.Alter<AIDataSourceRagMetadata>(metadata =>
        {
            metadata.Strictness = strictness;
            metadata.TopNDocuments = topN;
            metadata.IsInScope = model.IsInScope;
            metadata.Filter = model.Filter;
        });

        return Edit(interaction, context);
    }
}
