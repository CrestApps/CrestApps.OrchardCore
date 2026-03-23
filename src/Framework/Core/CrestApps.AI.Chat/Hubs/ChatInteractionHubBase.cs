using System.Text.Json;
using System.Threading.Channels;
using CrestApps.AI.Chat.Models;
using CrestApps.AI.Models;
using CrestApps.Services;
using Cysharp.Text;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CrestApps.AI.Chat.Hubs;

/// <summary>
/// Base SignalR hub for AI chat interactions. Provides streaming message delivery,
/// interaction loading, settings persistence, and history clearing. Subclass to add
/// authorization, ORM-specific commit logic, or other app-specific behavior.
/// </summary>
public abstract class ChatInteractionHubBase : Hub<IChatInteractionHubClient>
{
    protected ChatInteractionHubBase(
        ICatalogManager<ChatInteraction> interactionManager,
        IChatInteractionPromptStore promptStore,
        IOrchestrationContextBuilder orchestrationContextBuilder,
        IOrchestratorResolver orchestratorResolver,
        IEnumerable<IChatInteractionSettingsHandler> settingsHandlers,
        ILogger logger)
    {
        InteractionManager = interactionManager;
        PromptStore = promptStore;
        OrchestrationContextBuilder = orchestrationContextBuilder;
        OrchestratorResolver = orchestratorResolver;
        SettingsHandlers = settingsHandlers;
        Logger = logger;
    }

    protected ICatalogManager<ChatInteraction> InteractionManager { get; }

    protected IChatInteractionPromptStore PromptStore { get; }

    protected IOrchestrationContextBuilder OrchestrationContextBuilder { get; }

    protected IOrchestratorResolver OrchestratorResolver { get; }

    protected IEnumerable<IChatInteractionSettingsHandler> SettingsHandlers { get; }

    protected ILogger Logger { get; }

    /// <summary>
    /// Override to commit changes to the underlying data store after mutations.
    /// </summary>
    protected virtual Task CommitChangesAsync()
        => Task.CompletedTask;

    /// <summary>
    /// Override to get the current UTC time. Default uses <see cref="DateTime.UtcNow"/>.
    /// </summary>
    protected virtual DateTime GetUtcNow()
        => DateTime.UtcNow;

    /// <summary>
    /// Override to perform authorization checks before processing a request.
    /// Return <c>false</c> to deny the request (the override should also send an error to the caller).
    /// </summary>
    protected virtual Task<bool> AuthorizeAsync(ChatInteraction interaction)
        => Task.FromResult(true);

    /// <summary>
    /// Override to generate a localized error message for exceptions.
    /// </summary>
    protected virtual string GetFriendlyErrorMessage(Exception ex)
        => "An error occurred while processing your message.";

    /// <summary>
    /// Override to format a localized "required" error message.
    /// </summary>
    protected virtual string GetRequiredFieldMessage(string fieldName)
        => $"{fieldName} is required.";

    /// <summary>
    /// Override to format a "not found" error message.
    /// </summary>
    protected virtual string GetInteractionNotFoundMessage()
        => "Interaction not found.";

    public virtual ChannelReader<CompletionPartialMessage> SendMessage(string itemId, string prompt, CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<CompletionPartialMessage>();

        _ = HandlePromptAsync(channel.Writer, itemId, prompt, cancellationToken);

        return channel.Reader;
    }

    public virtual async Task LoadInteraction(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            await Clients.Caller.ReceiveError(GetRequiredFieldMessage(nameof(itemId)));
            return;
        }

        var interaction = await InteractionManager.FindByIdAsync(itemId);

        if (interaction is null)
        {
            await Clients.Caller.ReceiveError(GetInteractionNotFoundMessage());
            return;
        }

        if (!await AuthorizeAsync(interaction))
        {
            return;
        }

        var prompts = await PromptStore.GetPromptsAsync(itemId);

        await Clients.Caller.LoadInteraction(new
        {
            interaction.ItemId,
            interaction.Title,
            interaction.ConnectionName,
            interaction.ChatDeploymentId,
            Messages = prompts.Select(message => new AIChatResponseMessageDetailed
            {
                Id = message.ItemId,
                Role = message.Role.Value,
                IsGeneratedPrompt = message.IsGeneratedPrompt,
                Title = message.Title,
                Content = message.Text,
                References = message.References,
            })
        });
    }

    public virtual async Task SaveSettings(string itemId, JsonElement settings)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            await Clients.Caller.ReceiveError(GetRequiredFieldMessage(nameof(itemId)));
            return;
        }

        var interaction = await InteractionManager.FindByIdAsync(itemId);

        if (interaction == null)
        {
            await Clients.Caller.ReceiveError(GetInteractionNotFoundMessage());
            return;
        }

        if (!await AuthorizeAsync(interaction))
        {
            return;
        }

        foreach (var handler in SettingsHandlers)
        {
            await handler.UpdatingAsync(interaction, settings);
        }

        ApplyCoreSettings(interaction, settings);

        await InteractionManager.UpdateAsync(interaction);
        await CommitChangesAsync();

        foreach (var handler in SettingsHandlers)
        {
            await handler.UpdatedAsync(interaction, settings);
        }

        await Clients.Caller.SettingsSaved(interaction.ItemId, interaction.Title);
    }

    public virtual async Task ClearHistory(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            await Clients.Caller.ReceiveError(GetRequiredFieldMessage(nameof(itemId)));
            return;
        }

        var interaction = await InteractionManager.FindByIdAsync(itemId);

        if (interaction == null)
        {
            await Clients.Caller.ReceiveError(GetInteractionNotFoundMessage());
            return;
        }

        if (!await AuthorizeAsync(interaction))
        {
            return;
        }

        await PromptStore.DeleteAllPromptsAsync(itemId);
        await CommitChangesAsync();

        await Clients.Caller.HistoryCleared(interaction.ItemId);
    }

    /// <summary>
    /// Override to collect citation references from preemptive RAG or tool calls.
    /// </summary>
    protected virtual void CollectPreemptiveReferences(
        OrchestrationContext context,
        Dictionary<string, AICompletionReference> references,
        HashSet<string> contentItemIds)
    {
    }

    /// <summary>
    /// Override to collect tool-invoked citation references during streaming.
    /// </summary>
    protected virtual void CollectToolReferences(
        Dictionary<string, AICompletionReference> references,
        HashSet<string> contentItemIds)
    {
    }

    /// <summary>
    /// Called after an assistant prompt is created. Override to add content metadata.
    /// </summary>
    protected virtual Task OnAssistantPromptCreatedAsync(
        ChatInteractionPrompt prompt,
        HashSet<string> contentItemIds)
        => Task.CompletedTask;

    protected virtual async Task HandlePromptAsync(
        ChannelWriter<CompletionPartialMessage> writer,
        string itemId,
        string prompt,
        CancellationToken cancellationToken)
    {
        using var invocationScope = AIInvocationScope.Begin();

        try
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                await Clients.Caller.ReceiveError(GetRequiredFieldMessage("Interaction ID"));
                return;
            }

            var interaction = await InteractionManager.FindByIdAsync(itemId);

            if (interaction == null)
            {
                await Clients.Caller.ReceiveError(GetInteractionNotFoundMessage());
                return;
            }

            if (!await AuthorizeAsync(interaction))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(prompt))
            {
                await Clients.Caller.ReceiveError(GetRequiredFieldMessage(nameof(prompt)));
                return;
            }

            prompt = prompt.Trim();

            var utcNow = GetUtcNow();

            var userPrompt = new ChatInteractionPrompt
            {
                ItemId = UniqueId.GenerateId(),
                ChatInteractionId = itemId,
                Role = ChatRole.User,
                Text = prompt,
                CreatedUtc = utcNow,
            };

            await PromptStore.CreateAsync(userPrompt);

            var needsTitleUpdate = string.IsNullOrEmpty(interaction.Title);
            if (needsTitleUpdate)
            {
                interaction.Title = prompt.Length > 255 ? prompt[..255] : prompt;
            }

            var existingPrompts = await PromptStore.GetPromptsAsync(itemId);

            var assistantPrompt = new ChatInteractionPrompt
            {
                ItemId = UniqueId.GenerateId(),
                ChatInteractionId = itemId,
                Role = ChatRole.Assistant,
                CreatedUtc = utcNow,
            };

            var builder = ZString.CreateStringBuilder();

            var orchestratorContext = await OrchestrationContextBuilder.BuildAsync(interaction, ctx =>
            {
                ctx.UserMessage = prompt;
                ctx.ConversationHistory = existingPrompts
                    .Where(x => !x.IsGeneratedPrompt)
                    .Select(p => new ChatMessage(p.Role, p.Text))
                    .ToList();
            });

            AIInvocationScope.Current.DataSourceId = orchestratorContext.CompletionContext.DataSourceId;

            var orchestrator = OrchestratorResolver.Resolve(interaction.OrchestratorName);

            var contentItemIds = new HashSet<string>();
            var references = new Dictionary<string, AICompletionReference>();

            CollectPreemptiveReferences(orchestratorContext, references, contentItemIds);

            await foreach (var chunk in orchestrator.ExecuteStreamingAsync(orchestratorContext, cancellationToken))
            {
                if (string.IsNullOrEmpty(chunk.Text))
                {
                    continue;
                }

                builder.Append(chunk.Text);

                CollectToolReferences(references, contentItemIds);

                var partialMessage = new CompletionPartialMessage
                {
                    SessionId = interaction.ItemId,
                    MessageId = assistantPrompt.ItemId,
                    Content = chunk.Text,
                    References = references,
                };

                await writer.WriteAsync(partialMessage, cancellationToken);
            }

            CollectToolReferences(references, contentItemIds);

            if (builder.Length > 0)
            {
                assistantPrompt.Text = builder.ToString();
                assistantPrompt.References = references;

                await OnAssistantPromptCreatedAsync(assistantPrompt, contentItemIds);

                await PromptStore.CreateAsync(assistantPrompt);
            }

            if (needsTitleUpdate)
            {
                await InteractionManager.UpdateAsync(interaction);
            }

            await CommitChangesAsync();
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException || (ex is TaskCanceledException && cancellationToken.IsCancellationRequested))
            {
                Logger.LogDebug("Chat interaction processing was cancelled.");
                return;
            }

            Logger.LogError(ex, "An error occurred while processing the chat interaction.");

            try
            {
                var errorMessage = new CompletionPartialMessage
                {
                    SessionId = itemId,
                    MessageId = UniqueId.GenerateId(),
                    Content = GetFriendlyErrorMessage(ex),
                };

                await writer.WriteAsync(errorMessage, CancellationToken.None);
            }
            catch (Exception writeEx)
            {
                Logger.LogWarning(writeEx, "Failed to write error message to the channel.");
            }
        }
        finally
        {
            writer.Complete();
        }
    }

    /// <summary>
    /// Applies core settings from a JSON payload to a <see cref="ChatInteraction"/>.
    /// Override to apply additional module-specific settings.
    /// </summary>
    protected virtual void ApplyCoreSettings(ChatInteraction interaction, JsonElement settings)
    {
        interaction.Title = JsonHelper.GetString(settings, "title") ?? "Untitled";
        interaction.OrchestratorName = JsonHelper.GetString(settings, "orchestratorName");
        interaction.ConnectionName = JsonHelper.GetString(settings, "connectionName");
        interaction.ChatDeploymentId = JsonHelper.GetString(settings, "deploymentId");
        interaction.SystemMessage = JsonHelper.GetString(settings, "systemMessage");
        interaction.Temperature = JsonHelper.GetFloat(settings, "temperature");
        interaction.TopP = JsonHelper.GetFloat(settings, "topP");
        interaction.FrequencyPenalty = JsonHelper.GetFloat(settings, "frequencyPenalty");
        interaction.PresencePenalty = JsonHelper.GetFloat(settings, "presencePenalty");
        interaction.MaxTokens = JsonHelper.GetInt(settings, "maxTokens");
        interaction.PastMessagesCount = JsonHelper.GetInt(settings, "pastMessagesCount");
        interaction.ToolNames = JsonHelper.GetStringArray(settings, "toolNames");
        interaction.McpConnectionIds = JsonHelper.GetStringArray(settings, "mcpConnectionIds");
    }

    protected static class JsonHelper
    {
        public static string GetString(JsonElement el, string name)
        {
            if (el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString();
            }

            return null;
        }

        public static float? GetFloat(JsonElement el, string name)
        {
            if (el.TryGetProperty(name, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number)
                {
                    return prop.GetSingle();
                }

                if (prop.ValueKind == JsonValueKind.String && float.TryParse(prop.GetString(), out var f))
                {
                    return f;
                }
            }

            return null;
        }

        public static int? GetInt(JsonElement el, string name)
        {
            if (el.TryGetProperty(name, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number)
                {
                    return prop.GetInt32();
                }

                if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var i))
                {
                    return i;
                }
            }

            return null;
        }

        public static bool? GetBool(JsonElement el, string name)
        {
            if (el.TryGetProperty(name, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.True)
                {
                    return true;
                }

                if (prop.ValueKind == JsonValueKind.False)
                {
                    return false;
                }

                if (prop.ValueKind == JsonValueKind.String && bool.TryParse(prop.GetString(), out var b))
                {
                    return b;
                }
            }

            return null;
        }

        public static List<string> GetStringArray(JsonElement el, string name)
        {
            if (el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Array)
            {
                var list = new List<string>();

                foreach (var item in prop.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var value = item.GetString();

                        if (!string.IsNullOrEmpty(value))
                        {
                            list.Add(value);
                        }
                    }
                }

                return list;
            }

            return [];
        }
    }
}
