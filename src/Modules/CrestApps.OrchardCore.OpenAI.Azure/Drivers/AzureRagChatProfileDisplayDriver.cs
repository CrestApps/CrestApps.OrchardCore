using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using CrestApps.OrchardCore.OpenAI.Azure.ViewModels;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.OpenAI.Azure.Drivers;

/// <summary>
/// Display driver for Azure RAG query parameters on AIProfile.
/// Allows users to customize query-time parameters like Filter, Strictness, and TopNDocuments
/// per AI profile instead of per data source.
/// </summary>
public sealed class AzureRagChatProfileDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly IODataFilterValidator _filterValidator;

    internal readonly IStringLocalizer S;

    public AzureRagChatProfileDisplayDriver(
        IODataFilterValidator filterValidator,
        IStringLocalizer<AzureRagChatProfileDisplayDriver> stringLocalizer)
    {
        _filterValidator = filterValidator;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        // Only show for Azure OpenAI profiles that have a data source configured
        if (profile.Source != AzureOpenAIConstants.ProviderName)
        {
            return null;
        }

        return Initialize<AzureRagChatViewModel>("AzureRagChat_Edit", model =>
        {
            var ragMetadata = profile.As<AzureRagChatMetadata>();

            model.Strictness = ragMetadata?.Strictness;
            model.TopNDocuments = ragMetadata?.TopNDocuments;
            model.Filter = ragMetadata?.Filter;
        }).Location("Content:3");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        // Only update for Azure OpenAI profiles that have a data source configured
        if (profile.Source != AzureOpenAIConstants.ProviderName)
        {
            return null;
        }

        var dataSourceMetadata = profile.As<AIProfileDataSourceMetadata>();
        if (string.IsNullOrEmpty(dataSourceMetadata?.DataSourceId))
        {
            return null;
        }

        var model = new AzureRagChatViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // Validate OData filter expression
        if (!string.IsNullOrWhiteSpace(model.Filter) && !_filterValidator.IsValid(model.Filter))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Filter), S["The Filter must be a valid OData filter expression."]);
        }

        profile.Put(new AzureRagChatMetadata
        {
            Strictness = model.Strictness,
            TopNDocuments = model.TopNDocuments,
            Filter = model.Filter,
        });

        return Edit(profile, context);
    }
}
