using System.Text.Json;
using CrestApps.AI;
using CrestApps.AI.Chat;
using CrestApps.AI.Chat.Hubs;
using CrestApps.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Data.Documents;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Hubs;

public class ChatInteractionHub : ChatInteractionHubBase
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IServiceProvider _serviceProvider;
    private readonly CitationReferenceCollector _citationCollector;
    private readonly IClock _clock;
    private readonly IDocumentStore _documentStore;

    protected readonly IStringLocalizer S;

    public ChatInteractionHub(
        IAuthorizationService authorizationService,
        ISourceCatalogManager<ChatInteraction> interactionManager,
        IChatInteractionPromptStore promptStore,
        IServiceProvider serviceProvider,
        IOrchestrationContextBuilder orchestrationContextBuilder,
        IOrchestratorResolver orchestratorResolver,
        IEnumerable<IChatInteractionSettingsHandler> settingsHandlers,
        CitationReferenceCollector citationCollector,
        IClock clock,
        ILogger<ChatInteractionHub> logger,
        IDocumentStore documentStore,
        IStringLocalizer<ChatInteractionHub> stringLocalizer)
        : base(interactionManager, promptStore, orchestrationContextBuilder, orchestratorResolver, settingsHandlers, logger)
    {
        _authorizationService = authorizationService;
        _serviceProvider = serviceProvider;
        _citationCollector = citationCollector;
        _clock = clock;
        _documentStore = documentStore;
        S = stringLocalizer;
    }

    protected override Task CommitChangesAsync()
        => _documentStore.CommitAsync();

    protected override DateTime GetUtcNow()
        => _clock.UtcNow;

    protected override async Task<bool> AuthorizeAsync(ChatInteraction interaction)
    {
        var httpContext = Context.GetHttpContext();

        if (!await _authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.EditChatInteractions, interaction))
        {
            await Clients.Caller.ReceiveError(S["You are not authorized to access chat interactions."].Value);
            return false;
        }

        return true;
    }

    protected override string GetFriendlyErrorMessage(Exception ex)
        => AIHubErrorMessageHelper.GetFriendlyErrorMessage(ex, S).Value;

    protected override string GetRequiredFieldMessage(string fieldName)
        => S["{0} is required.", fieldName].Value;

    protected override string GetInteractionNotFoundMessage()
        => S["Interaction not found."].Value;

    protected override void CollectPreemptiveReferences(
        OrchestrationContext context,
        Dictionary<string, AICompletionReference> references,
        HashSet<string> contentItemIds)
    {
        _citationCollector.CollectPreemptiveReferences(context, references, contentItemIds);
    }

    protected override void CollectToolReferences(
        Dictionary<string, AICompletionReference> references,
        HashSet<string> contentItemIds)
    {
        _citationCollector.CollectToolReferences(references, contentItemIds);
    }

    protected override Task OnAssistantPromptCreatedAsync(
        ChatInteractionPrompt prompt,
        HashSet<string> contentItemIds)
    {
        if (contentItemIds.Count > 0)
        {
            prompt.Put(new ChatInteractionPromptContentMetadata
            {
                ContentItemIds = contentItemIds.ToList(),
            });
        }

        return Task.CompletedTask;
    }

    protected override void ApplyCoreSettings(ChatInteraction interaction, JsonElement settings)
    {
        base.ApplyCoreSettings(interaction, settings);

        var dataSourceId = JsonHelper.GetString(settings, "dataSourceId");

        if (!string.IsNullOrWhiteSpace(dataSourceId))
        {
            var dataSourceStore = _serviceProvider.GetService<ICatalog<AIDataSource>>();
            if (dataSourceStore is not null)
            {
                var dataSource = dataSourceStore.FindByIdAsync(dataSourceId).AsTask().GetAwaiter().GetResult();

                if (dataSource is not null)
                {
                    interaction.Put(new DataSourceMetadata()
                    {
                        DataSourceId = dataSource.ItemId,
                    });

                    interaction.Put(new AIDataSourceRagMetadata()
                    {
                        Strictness = JsonHelper.GetInt(settings, "strictness"),
                        TopNDocuments = JsonHelper.GetInt(settings, "topNDocuments"),
                        IsInScope = JsonHelper.GetBool(settings, "isInScope") ?? true,
                        Filter = JsonHelper.GetString(settings, "filter"),
                    });
                }
            }
        }
        else
        {
            interaction.Put(new DataSourceMetadata());
            interaction.Put(new AIDataSourceRagMetadata());
        }
    }
}
