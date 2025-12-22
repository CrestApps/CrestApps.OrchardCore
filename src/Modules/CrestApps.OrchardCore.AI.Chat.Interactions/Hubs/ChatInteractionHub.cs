using System.Text;
using System.Threading.Channels;
using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using YesSql;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Hubs;

public class ChatInteractionHub : Hub<IChatInteractionHubClient>
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IChatInteractionManager _interactionManager;
    private readonly ISession _session;
    private readonly IAICompletionService _completionService;
    private readonly ILogger<ChatInteractionHub> _logger;

    protected readonly IStringLocalizer S;

    public ChatInteractionHub(
        IAuthorizationService authorizationService,
        IChatInteractionManager interactionManager,
        ISession session,
        IAICompletionService completionService,
        ILogger<ChatInteractionHub> logger,
        IStringLocalizer<ChatInteractionHub> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _interactionManager = interactionManager;
        _session = session;
        _completionService = completionService;
        _logger = logger;
        S = stringLocalizer;
    }

    public ChannelReader<CompletionPartialMessage> SendMessage(string interactionId, string prompt, CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<CompletionPartialMessage>();

        _ = HandlePromptAsync(channel.Writer, interactionId, prompt, cancellationToken);

        return channel.Reader;
    }

    public async Task LoadInteraction(string interactionId)
    {
        if (string.IsNullOrWhiteSpace(interactionId))
        {
            await Clients.Caller.ReceiveError(S["{0} is required.", nameof(interactionId)].Value);
            return;
        }

        var httpContext = Context.GetHttpContext();

        if (!await _authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.ManageChatInteractions))
        {
            await Clients.Caller.ReceiveError(S["You are not authorized to access chat interactions."].Value);
            return;
        }

        var interaction = await _interactionManager.FindAsync(interactionId);

        if (interaction == null)
        {
            await Clients.Caller.ReceiveError(S["Interaction not found."].Value);
            return;
        }

        await Clients.Caller.LoadInteraction(new
        {
            interaction.InteractionId,
            Messages = interaction.Prompts.Select(message => new AIChatResponseMessageDetailed
            {
                Id = message.Id,
                Role = message.Role.Value,
                IsGeneratedPrompt = message.IsGeneratedPrompt,
                Title = message.Title,
                Content = message.Content,
                References = message.References,
            })
        });
    }

    private async Task HandlePromptAsync(ChannelWriter<CompletionPartialMessage> writer, string interactionId, string prompt, CancellationToken cancellationToken)
    {
        try
        {
            var httpContext = Context.GetHttpContext();

            if (!await _authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.ManageChatInteractions))
            {
                await Clients.Caller.ReceiveError(S["You are not authorized to access chat interactions."].Value);
                return;
            }

            ChatInteraction interaction;
            bool isNew = false;

            if (!string.IsNullOrWhiteSpace(interactionId))
            {
                interaction = await _interactionManager.FindAsync(interactionId);

                if (interaction == null)
                {
                    await Clients.Caller.ReceiveError(S["Interaction not found."].Value);
                    return;
                }
            }
            else
            {
                interaction = await _interactionManager.NewAsync();
                isNew = true;
            }

            if (string.IsNullOrWhiteSpace(prompt))
            {
                await Clients.Caller.ReceiveError(S["{0} is required.", nameof(prompt)].Value);
                return;
            }

            prompt = prompt.Trim();

            interaction.Prompts.Add(new AIChatSessionPrompt
            {
                Id = IdGenerator.GenerateId(),
                Role = ChatRole.User,
                Content = prompt,
            });

            if (isNew || string.IsNullOrEmpty(interaction.Title))
            {
                interaction.Title = Str.Truncate(prompt, 255);
            }

            var transcript = interaction.Prompts
                .Where(x => !x.IsGeneratedPrompt)
                .Select(p => new ChatMessage(p.Role, p.Content));

            var assistantMessage = new AIChatSessionPrompt
            {
                Id = IdGenerator.GenerateId(),
                Role = ChatRole.Assistant,
            };

            var builder = new StringBuilder();

            var completionContext = new AICompletionContext
            {
                ConnectionName = interaction.ConnectionName,
                DeploymentId = interaction.DeploymentId,
                SystemMessage = interaction.SystemMessage,
                Temperature = interaction.Temperature,
                TopP = interaction.TopP,
                FrequencyPenalty = interaction.FrequencyPenalty,
                PresencePenalty = interaction.PresencePenalty,
                MaxTokens = interaction.MaxTokens,
                PastMessagesCount = interaction.PastMessagesCount,
                ToolNames = interaction.ToolNames?.ToArray(),
                InstanceIds = interaction.ToolInstanceIds?.ToArray(),
                McpConnectionIds = interaction.McpConnectionIds?.ToArray(),
                UserMarkdownInResponse = true,
            };

            var contentItemIds = new HashSet<string>();
            var references = new Dictionary<string, AICompletionReference>();

            await foreach (var chunk in _completionService.CompleteStreamingAsync(interaction.ConnectionName, transcript, completionContext, cancellationToken))
            {
                if (chunk.AdditionalProperties is not null)
                {
                    if (chunk.AdditionalProperties.TryGetValue<IList<string>>("ContentItemIds", out var ids))
                    {
                        foreach (var id in ids)
                        {
                            contentItemIds.Add(id);
                        }
                    }

                    if (chunk.AdditionalProperties.TryGetValue<Dictionary<string, AICompletionReference>>("References", out var referenceItems))
                    {
                        foreach (var (key, value) in referenceItems)
                        {
                            references[key] = value;
                        }
                    }
                }

                if (string.IsNullOrEmpty(chunk.Text))
                {
                    continue;
                }

                builder.Append(chunk.Text);

                var partialMessage = new CompletionPartialMessage
                {
                    SessionId = interaction.InteractionId,
                    MessageId = assistantMessage.Id,
                    Content = chunk.Text,
                    References = references,
                };

                await writer.WriteAsync(partialMessage, cancellationToken);
            }

            if (builder.Length > 0)
            {
                assistantMessage.Content = builder.ToString();
                assistantMessage.ContentItemIds = contentItemIds.ToList();
                assistantMessage.References = references;

                interaction.Prompts.Add(assistantMessage);
            }

            await _interactionManager.SaveAsync(interaction);
            await _session.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException || (ex is TaskCanceledException && cancellationToken.IsCancellationRequested))
            {
                _logger.LogDebug("Chat interaction processing was cancelled.");
                return;
            }

            _logger.LogError(ex, "An error occurred while processing the chat interaction.");

            var errorMessage = new CompletionPartialMessage
            {
                SessionId = interactionId,
                MessageId = IdGenerator.GenerateId(),
                Content = GetFriendlyErrorMessage(ex).Value,
            };

            await writer.WriteAsync(errorMessage, cancellationToken);
        }
        finally
        {
            writer.Complete();
        }
    }

    private LocalizedString GetFriendlyErrorMessage(Exception ex)
    {
        if (ex is HttpRequestException httpEx)
        {
            if (httpEx.StatusCode is { } code)
            {
                return code switch
                {
                    System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden
                      => S["Authentication failed. Please check your API credentials."],

                    System.Net.HttpStatusCode.BadRequest
                      => S["Invalid request. Please verify your connection settings."],

                    System.Net.HttpStatusCode.NotFound
                      => S["The provider endpoint could not be found. Please verify the API URL."],

                    System.Net.HttpStatusCode.TooManyRequests
                      => S["Rate limit reached. Please wait and try again later."],

                    >= System.Net.HttpStatusCode.InternalServerError
                      => S["The provider service is currently unavailable. Please try again later."],

                    _ => S["An error occurred while communicating with the provider."]
                };
            }

            return S["Unable to reach the provider. Please check your connection or endpoint URL."];
        }

        return S["Our service is currently unavailable. Please try again later."];
    }
}
