using System.Text;
using System.Threading.Channels;
using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.Support;
using Fluid;
using Fluid.Values;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Liquid;
using YesSql;

namespace CrestApps.OrchardCore.AI.Chat.Hubs;

public class AIChatHub : Hub<IAIChatHubClient>
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IAIProfileManager _profileManager;
    private readonly IAIChatSessionManager _sessionManager;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly ISession _session;
    private readonly IAICompletionService _completionService;
    private readonly IAICompletionContextBuilder _aICompletionContextBuilder;
    private readonly IPromptRouter _promptRouter;
    private readonly ILogger<AIChatHub> _logger;

    protected readonly IStringLocalizer S;

    public AIChatHub(
        IAuthorizationService authorizationService,
        IAIProfileManager profileManager,
        IAIChatSessionManager sessionManager,
        ILiquidTemplateManager liquidTemplateManager,
        ISession session,
        IAICompletionService completionService,
        IAICompletionContextBuilder aICompletionContextBuilder,
        IPromptRouter promptRouter,
        ILogger<AIChatHub> logger,
        IStringLocalizer<AIChatHub> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _profileManager = profileManager;
        _sessionManager = sessionManager;
        _liquidTemplateManager = liquidTemplateManager;
        _session = session;
        _completionService = completionService;
        _aICompletionContextBuilder = aICompletionContextBuilder;
        _promptRouter = promptRouter;
        _logger = logger;
        S = stringLocalizer;
    }

    public ChannelReader<CompletionPartialMessage> SendMessage(string profileId, string prompt, string sessionId, string sessionProfileId, CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<CompletionPartialMessage>();

        // Avoid awaiting HandlePromptAsync to prevent blocking until all items are written,  
        // ensuring the channel is returned to the client immediately.
        _ = HandlePromptAsync(channel.Writer, profileId, prompt, sessionId, sessionProfileId, cancellationToken);

        return channel.Reader;
    }

    public async Task LoadSession(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            await Clients.Caller.ReceiveError(S["{0} is required.", nameof(sessionId)].Value);

            return;
        }

        var chatSession = await _sessionManager.FindAsync(sessionId);

        if (chatSession == null)
        {
            await Clients.Caller.ReceiveError(S["Session not found."].Value);

            return;
        }

        var profile = await _profileManager.FindByIdAsync(chatSession.ProfileId);

        if (profile is null)
        {
            await Clients.Caller.ReceiveError(S["Profile not found."].Value);

            return;
        }

        var httpContext = Context.GetHttpContext();

        if (!await _authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.QueryAnyAIProfile, profile))
        {
            await Clients.Caller.ReceiveError(S["You are not authorized to interact with the given profile."].Value);

            return;
        }

        await Clients.Caller.LoadSession(new
        {
            chatSession.SessionId,
            Profile = new
            {
                Id = chatSession.ProfileId,
                Type = profile.Type.ToString()
            },
            Messages = chatSession.Prompts.Select(message => new AIChatResponseMessageDetailed
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

    private async Task HandlePromptAsync(ChannelWriter<CompletionPartialMessage> writer, string profileId, string prompt, string sessionId, string sessionProfileId, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(profileId))
            {
                await Clients.Caller.ReceiveError(S["{0} is required.", nameof(sessionId)].Value);

                return;
            }

            var profile = await _profileManager.FindByIdAsync(profileId);

            if (profile is null)
            {
                await Clients.Caller.ReceiveError(S["Profile not found."].Value);

                return;
            }

            var httpContext = Context.GetHttpContext();

            if (!await _authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.QueryAnyAIProfile, profile))
            {
                await Clients.Caller.ReceiveError(S["You are not authorized to interact with the given profile."].Value);

                return;
            }

            if (profile.Type == AIProfileType.Utility)
            {
                if (string.IsNullOrWhiteSpace(prompt))
                {
                    await Clients.Caller.ReceiveError(S["{0} is required.", nameof(prompt)].Value);
                    return;
                }

                await ProcessUtilityAsync(writer, profile, prompt.Trim(), cancellationToken);

                // We don't need to save the session for utility profiles.
                return;
            }

            if (profile.Type == AIProfileType.TemplatePrompt)
            {
                if (string.IsNullOrWhiteSpace(sessionProfileId))
                {
                    await Clients.Caller.ReceiveError(S["{0} is required.", nameof(sessionProfileId)].Value);

                    return;
                }

                var parentProfile = await _profileManager.FindByIdAsync(sessionProfileId);

                if (parentProfile is null)
                {
                    await Clients.Caller.ReceiveError(S["Invalid value given to {0}.", nameof(sessionProfileId)].Value);

                    return;
                }

                await ProcessGeneratedPromptAsync(writer, profile, sessionId, parentProfile, cancellationToken);
            }
            else
            {
                // At this point, we are dealing with a chat profile.
                await ProcessChatPromptAsync(writer, profile, sessionId, prompt?.Trim(), cancellationToken);
            }

            await _session.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Don't write error messages if the operation was cancelled (e.g., user navigated away).
            if (ex is OperationCanceledException || (ex is TaskCanceledException && cancellationToken.IsCancellationRequested))
            {
                _logger.LogDebug("Chat prompt processing was cancelled.");
                return;
            }

            _logger.LogError(ex, "An error occurred while processing the chat prompt.");

            var errorMessage = new CompletionPartialMessage
            {
                SessionId = sessionId,
                MessageId = IdGenerator.GenerateId(),
                Content = AIHubErrorMessageHelper.GetFriendlyErrorMessage(ex, S).Value,
            };

            await writer.WriteAsync(errorMessage, cancellationToken);
        }
        finally
        {
            writer.Complete();
        }
    }


    private async Task<(AIChatSession ChatSession, bool IsNewSession)> GetSessionsAsync(IAIChatSessionManager sessionManager, string sessionId, AIProfile profile, string userPrompt)
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            var existingChatSession = await sessionManager.FindAsync(sessionId);

            if (existingChatSession != null && existingChatSession.ProfileId == profile.ItemId)
            {
                return (existingChatSession, false);
            }
        }

        // At this point, we need to create a new session.
        var chatSession = await sessionManager.NewAsync(profile, new NewAIChatSessionContext());

        if (profile.TitleType == AISessionTitleType.Generated)
        {
            chatSession.Title = await GetGeneratedTitleAsync(profile, userPrompt);
        }

        if (string.IsNullOrEmpty(chatSession.Title))
        {
            chatSession.Title = Str.Truncate(userPrompt, 255);
        }

        return (chatSession, true);
    }

    private async Task<string> GetGeneratedTitleAsync(AIProfile profile, string userPrompt)
    {
        var context = await _aICompletionContextBuilder.BuildAsync(profile, c =>
        {
            c.SystemMessage = AIConstants.TitleGeneratorSystemMessage;
            c.FrequencyPenalty = 0;
            c.PresencePenalty = 0;
            c.TopP = 1;
            c.Temperature = 0;
            c.MaxTokens = 64; // 64 token to generate about 255 characters.
            c.UserMarkdownInResponse = false;

            // Avoid using tools or any data sources when generating title to reduce the used tokens.
            c.DataSourceId = null;
            c.DataSourceType = null;
            c.DisableTools = true;
        });

        var titleResponse = await _completionService.CompleteAsync(profile.Source,
        [
            new (ChatRole.User, userPrompt),
        ], context);

        // If we fail to set an AI generated title to the session, we'll use the user's prompt at the title.
        return titleResponse.Messages.Count > 0
            ? Str.Truncate(titleResponse.Messages.First().Text, 255)
            : Str.Truncate(userPrompt, 255);
    }

    private async Task ProcessChatPromptAsync(ChannelWriter<CompletionPartialMessage> writer, AIProfile profile, string sessionId, string prompt, CancellationToken cancellationToken)
    {
        (var chatSession, var isNew) = await GetSessionsAsync(_sessionManager, sessionId, profile, prompt);

        chatSession.Prompts.Add(new AIChatSessionPrompt
        {
            Id = IdGenerator.GenerateId(),
            Role = ChatRole.User,
            Content = prompt,
        });

        var transcript = chatSession.Prompts
            .Where(x => !x.IsGeneratedPrompt)
            .Select(prompt => new ChatMessage(prompt.Role, prompt.Content));

        var assistantMessage = new AIChatSessionPrompt
        {
            Id = IdGenerator.GenerateId(),
            Role = ChatRole.Assistant,
            Title = profile.PromptSubject,
        };

        // Process prompt using intent-aware, strategy-based approach.
        var intentResult = await ReasonAsync(profile, chatSession.Prompts, prompt, cancellationToken);

        // Handle chart generation results.
        if (intentResult != null && intentResult.HasGeneratedChart)
        {
            var content = $"[chart:{intentResult.GeneratedChartConfig}]";
            await WritePartialMessageAsync(writer, chatSession.SessionId, assistantMessage.Id, content, cancellationToken);
            assistantMessage.Content = content;
            chatSession.Prompts.Add(assistantMessage);
            await _sessionManager.SaveAsync(chatSession);

            return;
        }

        // Handle chart generation errors.
        if (intentResult != null && intentResult.IsChartGenerationIntent && !intentResult.IsSuccess)
        {
            await WritePartialMessageAsync(writer, chatSession.SessionId, assistantMessage.Id, intentResult.ErrorMessage, cancellationToken);
            assistantMessage.Content = intentResult.ErrorMessage;
            chatSession.Prompts.Add(assistantMessage);
            await _sessionManager.SaveAsync(chatSession);

            return;
        }

        // Handle image generation results.
        if (intentResult != null && intentResult.HasGeneratedImages)
        {
            var content = BuildImageMarkdown(intentResult);
            await WritePartialMessageAsync(writer, chatSession.SessionId, assistantMessage.Id, content, cancellationToken);
            assistantMessage.Content = content;
            chatSession.Prompts.Add(assistantMessage);
            await _sessionManager.SaveAsync(chatSession);

            return;
        }

        // Handle image generation errors.
        if (intentResult != null && intentResult.IsImageGenerationIntent && !intentResult.IsSuccess)
        {
            await WritePartialMessageAsync(writer, chatSession.SessionId, assistantMessage.Id, intentResult.ErrorMessage, cancellationToken);
            assistantMessage.Content = intentResult.ErrorMessage;
            chatSession.Prompts.Add(assistantMessage);
            await _sessionManager.SaveAsync(chatSession);

            return;
        }

        var builder = new StringBuilder();

        var completionContext = await _aICompletionContextBuilder.BuildAsync(profile, c =>
        {
            c.AdditionalProperties["Session"] = chatSession;
            c.UserMarkdownInResponse = true;

            // Append additional context from intent processing to the system message.
            if (intentResult != null && intentResult.IsSuccess && intentResult.HasContext)
            {
                c.SystemMessage = (c.SystemMessage ?? string.Empty) + "\n\n" + intentResult.GetCombinedContext();
            }
        });

        var contentItemIds = new HashSet<string>();
        var references = new Dictionary<string, AICompletionReference>();

        await foreach (var chunk in _completionService.CompleteStreamingAsync(profile.Source, transcript, completionContext, cancellationToken))
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
                SessionId = chatSession.SessionId,
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

            chatSession.Prompts.Add(assistantMessage);
        }

        await _sessionManager.SaveAsync(chatSession);
    }

    private async Task ProcessGeneratedPromptAsync(ChannelWriter<CompletionPartialMessage> writer, AIProfile profile, string sessionId, AIProfile parentProfile, CancellationToken cancellationToken)
    {
        (var chatSession, _) = await GetSessionsAsync(_sessionManager, sessionId, parentProfile, userPrompt: profile.Name);

        var generatedPrompt = await _liquidTemplateManager.RenderStringAsync(profile.PromptTemplate, NullEncoder.Default,
            new Dictionary<string, FluidValue>()
            {
                ["Profile"] = new ObjectValue(profile),
                ["Session"] = new ObjectValue(chatSession),
            });

        var assistantMessage = new AIChatSessionPrompt
        {
            Id = IdGenerator.GenerateId(),
            Role = ChatRole.Assistant,
            IsGeneratedPrompt = true,
            Title = profile.PromptSubject,
        };

        var completionContext = await _aICompletionContextBuilder.BuildAsync(profile, c =>
        {
            c.UserMarkdownInResponse = true;
        });

        var builder = new StringBuilder();

        var contentItemIds = new HashSet<string>();
        var references = new Dictionary<string, AICompletionReference>();

        await foreach (var chunk in _completionService.CompleteStreamingAsync(profile.Source, [new ChatMessage(ChatRole.User, generatedPrompt)], completionContext, cancellationToken))
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
                SessionId = sessionId,
                MessageId = assistantMessage.Id,
                Content = chunk.Text,
                References = references,
            };

            await writer.WriteAsync(partialMessage, cancellationToken);
        }

        assistantMessage.Content = builder.ToString();
        assistantMessage.ContentItemIds = contentItemIds.ToList();
        assistantMessage.References = references;

        chatSession.Prompts.Add(assistantMessage);

        await _sessionManager.SaveAsync(chatSession);
    }

    private async Task ProcessUtilityAsync(ChannelWriter<CompletionPartialMessage> writer, AIProfile profile, string prompt, CancellationToken cancellationToken)
    {
        var messageId = IdGenerator.GenerateId();

        var completionContext = await _aICompletionContextBuilder.BuildAsync(profile, c =>
        {
            c.UserMarkdownInResponse = true;
        });

        var references = new Dictionary<string, AICompletionReference>();

        await foreach (var chunk in _completionService.CompleteStreamingAsync(profile.Source, [new ChatMessage(ChatRole.User, prompt)], completionContext, cancellationToken))
        {
            if (chunk.AdditionalProperties is not null)
            {
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

            var partialMessage = new CompletionPartialMessage
            {
                MessageId = messageId,
                Content = chunk.Text,
                References = references,
            };

            await writer.WriteAsync(partialMessage, cancellationToken);
        }
    }

    private async Task<IntentProcessingResult> ReasonAsync(AIProfile profile, IList<AIChatSessionPrompt> prompts, string prompt, CancellationToken cancellationToken)
    {
        var request = new PromptRoutingContext
        {
            Prompt = prompt,
            Source = profile.Source,
            ConnectionName = profile.ConnectionName,
            CompletionResource = profile,
            ConversationHistory = prompts
                .Where(p => !p.IsGeneratedPrompt)
                .Select(p => new ChatMessage(p.Role, p.Content))
                .ToList(),
        };

        return await _promptRouter.RouteAsync(request, cancellationToken);
    }

    private static async Task WritePartialMessageAsync(ChannelWriter<CompletionPartialMessage> writer, string sessionId, string messageId, string content, CancellationToken cancellationToken)
    {
        var partialMessage = new CompletionPartialMessage
        {
            SessionId = sessionId,
            MessageId = messageId,
            Content = content,
        };

        await writer.WriteAsync(partialMessage, cancellationToken);
    }

    private string BuildImageMarkdown(IntentProcessingResult result)
    {
        var contents = result?.GeneratedImages?.Contents;
        if (contents is null || contents.Count == 0)
        {
            return result?.ErrorMessage ?? S["No images were generated."].Value;
        }

        var messageBuilder = new StringBuilder();

        foreach (var contentItem in contents)
        {
            var imageUri = ExtractImageUri(contentItem);

            if (string.IsNullOrWhiteSpace(imageUri))
            {
                continue;
            }

            messageBuilder.AppendLine($"![Generated Image]({imageUri})");
            messageBuilder.AppendLine();
        }

        return messageBuilder.Length > 0
            ? messageBuilder.ToString()
            : (result?.ErrorMessage ?? S["No images were generated."].Value);
    }

    private static string ExtractImageUri(AIContent contentItem)
    {
        if (contentItem is null)
        {
            return null;
        }

        if (contentItem is UriContent uriContent)
        {
            return uriContent.Uri?.ToString();
        }

        if (contentItem is DataContent dataContent && dataContent.Uri is not null)
        {
            return dataContent.Uri.ToString();
        }

        return null;
    }
}
