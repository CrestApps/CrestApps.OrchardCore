using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat.Hubs;
using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.ResponseHandling;
using CrestApps.OrchardCore.AI.Chat.Interactions.Settings;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Hubs;

/// <summary>
/// OrchardCore-specific chat interaction hub. Inherits all behavior from
/// <see cref="ChatInteractionHubBase"/> and overrides hooks to integrate with
/// OrchardCore's scoping, authorization, localization, analytics, and citation systems.
/// </summary>
public class ChatInteractionHub : ChatInteractionHubBase
{
    private readonly IStringLocalizer S;

    public ChatInteractionHub(
        IServiceProvider services,
        TimeProvider timeProvider,
        ILogger<ChatInteractionHub> logger,
        IStringLocalizer<ChatInteractionHub> stringLocalizer)
    : base(services, timeProvider, logger)
    {
        S = stringLocalizer;
    }

    // ───────────────────── Scope override ─────────────────────

    protected override Task ExecuteInScopeAsync(Func<IServiceProvider, Task> action)
        => ShellScope.UsingChildScopeAsync(scope => action(scope.ServiceProvider));

    // ──────────────────── Authorization ────────────────────

    protected override async Task<bool> AuthorizeAsync(IServiceProvider services, ChatInteraction interaction)
    {
        var authorizationService = services.GetRequiredService<IAuthorizationService>();
        var httpContext = Context.GetHttpContext();

        return await authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.EditChatInteractions, interaction);
    }

    // ──────────────── Time / ID generation ────────────────

    protected override DateTime GetUtcNow()
    {
        var clock = Context.GetHttpContext()?.RequestServices?.GetService<IClock>();
        return clock?.UtcNow ?? base.GetUtcNow();
    }

    protected override string GenerateId()
        => IdGenerator.GenerateId();

    // ─────────────── Deployment / chat mode settings ───────────────

    protected override async Task<DefaultAIDeploymentSettings> GetDeploymentSettingsAsync(IServiceProvider services)
    {
        var siteService = services.GetRequiredService<ISiteService>();
        var site = await siteService.GetSiteSettingsAsync();

        return site.As<DefaultAIDeploymentSettings>();
    }

    protected override async Task<ChatMode> GetChatModeAsync(IServiceProvider services)
    {
        var siteService = services.GetRequiredService<ISiteService>();
        var site = await siteService.GetSiteSettingsAsync();

        return site.As<ChatInteractionChatModeSettings>().ChatMode;
    }

    protected override async Task<bool> IsTextToSpeechPlaybackEnabledAsync(IServiceProvider services)
    {
        var siteService = services.GetRequiredService<ISiteService>();
        var site = await siteService.GetSiteSettingsAsync();

        return site.As<ChatInteractionChatModeSettings>().EnableTextToSpeechPlayback;
    }

    // ──────────────────── Error messages ────────────────────

    protected override string GetRequiredFieldMessage(string fieldName)
        => S["{0} is required.", fieldName].Value;

    protected override string GetInteractionNotFoundMessage()
        => S["Interaction not found."].Value;

    protected override string GetNotAuthorizedMessage()
        => S["You are not authorized to access chat interactions."].Value;

    protected override string GetFriendlyErrorMessage(Exception ex)
        => AIHubErrorMessageHelper.GetFriendlyErrorMessage(ex, S).Value;

    protected override string GetConversationNotEnabledMessage()
        => S["Conversation mode is not enabled for chat interactions."].Value;

    protected override string GetNoSttDeploymentMessage()
        => S["No speech-to-text deployment is configured or available."].Value;

    protected override string GetNoTtsDeploymentMessage()
        => S["No text-to-speech deployment is configured or available."].Value;

    protected override string GetTtsNotEnabledMessage()
        => S["Text-to-speech is not enabled for chat interactions."].Value;

    protected override string GetConversationErrorMessage()
        => S["An error occurred during the conversation. Please try again."].Value;

    protected override string GetNotificationActionErrorMessage()
        => S["An error occurred while processing your action. Please try again."].Value;

    protected override string GetTranscriptionErrorMessage(Exception ex = null)
        => S["An error occurred while transcribing the audio. Please try again."].Value;

    protected override string GetSpeechSynthesisErrorMessage(Exception ex = null)
        => S["An error occurred while synthesizing speech. Please try again."].Value;

    protected override string GetSettingsValidationMessage(string propertyName) => propertyName switch
    {
        "strictness" => S["Strictness must be between 1 and 5."].Value,
        "topNDocuments" => S["Retrieved documents must be between 3 and 20."].Value,
        "temperature" => S["Temperature must be between 0 and 2."].Value,
        "topP" => S["Top P must be between 0 and 1."].Value,
        "frequencyPenalty" => S["Frequency penalty must be between 0 and 2."].Value,
        "presencePenalty" => S["Presence penalty must be between 0 and 2."].Value,
        "pastMessagesCount" => S["Past messages must be between 2 and 50."].Value,
        "maxTokens" => S["Max response tokens must be 4 or greater."].Value,
        _ => S["One or more settings are invalid."].Value,
    };

    // ────────────── Settings validation ──────────────

    protected override string ValidateSettings(JsonElement settings)
        => ChatInteractionSettingsValidator.Validate(settings);

    // ────────────── Citation collection ──────────────

    protected override void CollectStreamingReferences(
        IServiceProvider services,
        ChatResponseHandlerContext handlerContext,
        Dictionary<string, AICompletionReference> references,
        HashSet<string> contentItemIds)
    {
        var citationCollector = services.GetRequiredService<CitationReferenceCollector>();

        if (handlerContext.Properties.TryGetValue("OrchestrationContext", out var ctxObj) && ctxObj is OrchestrationContext orchestratorContext)
        {
            citationCollector.CollectPreemptiveReferences(orchestratorContext, references, contentItemIds);
            handlerContext.Properties.Remove("OrchestrationContext");
        }

        citationCollector.CollectToolReferences(references, contentItemIds);
    }

    // ────────────── Post-completion hooks ──────────────

    protected override Task OnAssistantPromptCreatedAsync(
        IServiceProvider services,
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

    // ────────────── Payload customization ──────────────

    protected override object CreateInteractionPayload(
        ChatInteraction interaction,
        IReadOnlyCollection<ChatInteractionPrompt> prompts)
    {
        return new
        {
            interaction.ItemId,
            interaction.Title,
            interaction.ConnectionName,
            DeploymentId = interaction.ChatDeploymentName,
            Messages = prompts.Select(message => new AIChatResponseMessageDetailed
            {
                Id = message.ItemId,
                Role = message.Role.Value,
                IsGeneratedPrompt = message.IsGeneratedPrompt,
                Title = message.Title,
                Content = message.Text,
                References = message.References,
                Appearance = message.GetOrCreate<AssistantMessageAppearance>(),
            }),
        };
    }

    // ────────────── Data source settings ──────────────

    protected override async Task ApplyCoreSettingsAsync(
        IServiceProvider services,
        ChatInteraction interaction,
        JsonElement settings)
    {
        await base.ApplyCoreSettingsAsync(services, interaction, settings);

        var dataSourceId = JsonHelper.GetString(settings, "dataSourceId");
        var topNDocuments = JsonHelper.GetInt(settings, "topNDocuments");
        var isInScope = JsonHelper.GetBool(settings, "isInScope") ?? false;

        if (!string.IsNullOrWhiteSpace(dataSourceId))
        {
            var dataSourceStore = services.GetService<IAIDataSourceStore>();
            if (dataSourceStore is not null)
            {
                var dataSource = await dataSourceStore.FindByIdAsync(dataSourceId);
                if (dataSource is not null)
                {
                    interaction.Put(new DataSourceMetadata
                    {
                        DataSourceId = dataSource.ItemId,
                    });

                    interaction.Put(new AIDataSourceRagMetadata
                    {
                        Strictness = JsonHelper.GetInt(settings, "strictness"),
                        TopNDocuments = topNDocuments,
                        IsInScope = isInScope,
                        Filter = JsonHelper.GetString(settings, "filter"),
                    });
                }
            }
        }
        else
        {
            interaction.Put(new DataSourceMetadata());
            interaction.Alter<AIDataSourceRagMetadata>(metadata =>
            {
                metadata.Strictness = null;
                metadata.TopNDocuments = topNDocuments;
                metadata.IsInScope = isInScope;
                metadata.Filter = null;
            });
        }

        if (Logger.IsEnabled(LogLevel.Debug))
        {
            Logger.LogDebug(
                "Saving chat interaction settings for '{InteractionId}' with data source '{DataSourceId}', strictness '{Strictness}', top documents '{TopNDocuments}', and in-scope '{IsInScope}'.",
                interaction.ItemId,
                dataSourceId,
                JsonHelper.GetInt(settings, "strictness"),
                topNDocuments,
                isInScope);
        }
    }
}
