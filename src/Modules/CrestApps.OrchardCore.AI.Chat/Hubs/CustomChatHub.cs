using System.Security.Claims;
using System.Text;
using System.Threading.Channels;
using CrestApps.OrchardCore.AI.Chat.Indexes;
using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using YesSql;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.AI.Chat.Hubs;

public class CustomChatHub : Hub<IAIChatHubClient>
{
    private readonly IAICustomChatSessionManager _sessionManager;
    private readonly YesSqlSession _session;
    // this change will break the singleRHub YesSqlSession
    private readonly IAICompletionService _completionService;
    private readonly IAICompletionContextBuilder _aICompletionContextBuilder;
    private readonly IContentManager _contentManager;
    private readonly CustomChatDocumentStore _docStore;
    private readonly SessionDocumentRetriever _documentRetriever;

    protected readonly IStringLocalizer S;

    public CustomChatHub(
        SessionDocumentRetriever documentRetriever,
        CustomChatDocumentStore docStore,
        IContentManager contentManager,
        IAICustomChatSessionManager sessionManager,
        YesSqlSession session,
        IAICompletionService completionService,
        IAICompletionContextBuilder aICompletionContextBuilder,
        IStringLocalizer<CustomChatHub> stringLocalizer)
    {
        _documentRetriever = documentRetriever;
        _docStore = docStore;
        _contentManager = contentManager;
        _sessionManager = sessionManager;
        _session = session;
        _completionService = completionService;
        _aICompletionContextBuilder = aICompletionContextBuilder;
        S = stringLocalizer;
    }

    public ChannelReader<CompletionPartialMessage> SendCustomChatMessage(string customChatInstanceId, string prompt, string sessionId, CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<CompletionPartialMessage>();

        _ = HandleCustomChatPromptAsync(channel.Writer, customChatInstanceId, prompt, sessionId, cancellationToken);

        return channel.Reader;
    }

    public async Task LoadCustomChatSession(string customChatInstanceId)
    {
        if (string.IsNullOrWhiteSpace(customChatInstanceId))
        {
            await Clients.Caller.ReceiveError("customChatInstanceId is required.");

            return;
        }

        if (Context.User?.Identity?.IsAuthenticated != true)
        {
            await Clients.Caller.ReceiveError("User not authenticated.");

            return;
        }

        var customChatSession = await _sessionManager.FindByCustomChatInstanceIdAsync(customChatInstanceId);

        if (customChatSession == null)
        {
            customChatSession = new CustomChatSession
            {
                SessionId = IdGenerator.GenerateId(),
                CustomChatInstanceId = customChatInstanceId,
                UserId = Context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                CreatedUtc = DateTime.UtcNow
            };

            await _sessionManager.SaveCustomChatAsync(customChatSession, CancellationToken.None);
        }

        await Clients.Caller.LoadSession(new
        {
            customChatSession.SessionId,

            customChatSession.CustomChatInstanceId,

            Messages = customChatSession.Prompts.Select(x => new
            {
                x.Id,
                Role = x.Role.Value,
                x.Content,
                x.Title,
                x.IsGeneratedPrompt,
                x.References
            })
        });
    }

    private async Task HandleCustomChatPromptAsync(ChannelWriter<CompletionPartialMessage> writer, string customChatInstanceId, string prompt, string sessionId, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(customChatInstanceId))
            {
                await Clients.Caller.ReceiveError("customChatInstanceId is required.");

                return;
            }

            if (string.IsNullOrWhiteSpace(prompt))
            {
                await Clients.Caller.ReceiveError("prompt is required.");

                return;
            }

            CustomChatSession customChatSession;

            if (!string.IsNullOrWhiteSpace(sessionId))
            {
                customChatSession = await _sessionManager.FindCustomChatSessionAsync(sessionId);
            }
            else
            {
                customChatSession = await _sessionManager.FindByCustomChatInstanceIdAsync(customChatInstanceId);
            }

            customChatSession ??= new CustomChatSession
            {
                SessionId = IdGenerator.GenerateId(),
                CustomChatInstanceId = customChatInstanceId,
                UserId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                CreatedUtc = DateTime.UtcNow
            };

            customChatSession.Prompts.Add(new AIChatSessionPrompt
            {
                Id = IdGenerator.GenerateId(),
                Role = ChatRole.User,
                Content = prompt
            });

            var documentContext = _documentRetriever.Retrieve(customChatSession.Documents, prompt);

            var messages = new List<ChatMessage>();

            if (documentContext.Count > 0)
            {
                messages.Add(new ChatMessage(ChatRole.System,
                    "You are answering questions using the following private document context.\n\n"
                    + string.Join("\n\n---\n\n", documentContext)
                ));
            }

            messages.AddRange(customChatSession.Prompts.Select(x => new ChatMessage(x.Role, x.Content)));

            var transcript = messages.ToArray();

            var index = await _session.QueryIndex<CustomChatPartIndex>(x =>
                 x.CustomChatInstanceId == customChatInstanceId).FirstOrDefaultAsync(cancellationToken);

            var customChatItem = index != null ? await _contentManager.GetAsync(index.ContentItemId) : null;

            var part = customChatItem?.As<CustomChatPart>();

            if (index == null || customChatItem == null || string.IsNullOrWhiteSpace(part.ConnectionName) || string.IsNullOrWhiteSpace(part.DeploymentId))
            {
                await Clients.Caller.ReceiveError("Chat is not configured.");

                return;
            }

            var metadata = new AIChatInstanceMetadata
            {
                ProviderName = part.ProviderName,
                Source = part.ProviderName,
                ConnectionName = part.ConnectionName,
                DeploymentId = part.DeploymentId,
                SystemMessage = part.SystemMessage,
                MaxTokens = part.MaxTokens,
                Temperature = part.Temperature,
                TopP = part.TopP,
                FrequencyPenalty = part.FrequencyPenalty,
                PresencePenalty = part.PresencePenalty,
                PastMessagesCount = part.PastMessagesCount,
                ToolNames = part.ToolNames,
                UseCaching = part.UseCaching,
                IsCustomInstance = true
            };

            var completionContext = await _aICompletionContextBuilder.BuildCustomAsync(new CustomChatCompletionContext
            {
                CustomChatInstanceId = customChatSession.CustomChatInstanceId,
                Session = customChatSession,
                DocumentContext = documentContext
            });

            var assistantPrompt = new AIChatSessionPrompt
            {
                Id = IdGenerator.GenerateId(),
                Role = ChatRole.Assistant
            };

            var contentBuilder = new StringBuilder();

            var references = new Dictionary<string, AICompletionReference>();

            await foreach (var chunk in _completionService.CompleteStreamingAsync(metadata.Source, transcript, completionContext, cancellationToken))
            {
                if (string.IsNullOrEmpty(chunk.Text))
                {
                    continue;
                }

                contentBuilder.Append(chunk.Text);

                await writer.WriteAsync(new CompletionPartialMessage
                {
                    SessionId = customChatSession.SessionId,
                    MessageId = assistantPrompt.Id,
                    Content = chunk.Text,
                    References = references
                }, cancellationToken);
            }

            assistantPrompt.Content = contentBuilder.ToString();

            assistantPrompt.References = references;

            customChatSession.Prompts.Add(assistantPrompt);

            await _sessionManager.SaveCustomChatAsync(customChatSession, CancellationToken.None);
        }
        catch (Exception)
        {
            await writer.WriteAsync(new CompletionPartialMessage
            {
                Content = "The service is currently unavailable."
            }, cancellationToken);
        }
        finally
        {
            writer.Complete();
        }
    }
}
