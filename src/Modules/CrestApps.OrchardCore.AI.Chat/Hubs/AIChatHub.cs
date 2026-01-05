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
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Entities;
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
    private readonly IAIClientFactory _clientFactory;
    private readonly AIProviderOptions _aiProviderOptions;
    private readonly IAICompletionContextBuilder _aICompletionContextBuilder;
    private readonly ILogger<AIChatHub> _logger;

    protected readonly IStringLocalizer S;

    public AIChatHub(
        IAuthorizationService authorizationService,
        IAIProfileManager profileManager,
        IAIChatSessionManager sessionManager,
        ILiquidTemplateManager liquidTemplateManager,
        ISession session,
        IAICompletionService completionService,
        IAIClientFactory clientFactory,
        IOptions<AIProviderOptions> aiProviderOptions,
        IAICompletionContextBuilder aICompletionContextBuilder,
        ILogger<AIChatHub> logger,
        IStringLocalizer<AIChatHub> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _profileManager = profileManager;
        _sessionManager = sessionManager;
        _liquidTemplateManager = liquidTemplateManager;
        _session = session;
        _completionService = completionService;
        _clientFactory = clientFactory;
        _aiProviderOptions = aiProviderOptions.Value;
        _aICompletionContextBuilder = aICompletionContextBuilder;
        _logger = logger;
        S = stringLocalizer;
    }

    public sealed class TranscriptResponse
    {
        public string Transcript { get; set; }
        public string SessionId { get; set; }
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

    // Accept a client-to-server streamed parameter of base64-encoded audio chunks
    public async Task SendAudioChunk(string profileId, string sessionId, IAsyncEnumerable<string> stream)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(profileId))
            {
                return;
            }

            if (stream == null)
            {
                return;
            }

            var profile = await _profileManager.FindByIdAsync(profileId);

            if (profile is null)
            {
                return;
            }

            var httpContext = Context.GetHttpContext();

            if (!await _authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.QueryAnyAIProfile, profile))
            {
                return;
            }

            // Get the speech-to-text client using the dedicated connection from profile metadata
            var speechToTextMetadata = profile.As<SpeechToTextMetadata>();
            var speechToTextConnection = speechToTextMetadata?.ConnectionName ?? profile.ConnectionName;

#pragma warning disable MEAI001
            ISpeechToTextClient speechToTextClient;
#pragma warning restore MEAI001
            try
            {
                if (!_aiProviderOptions.Providers.TryGetValue(profile.Source, out var provider))
                {
                    _logger.LogWarning("AI provider '{ProviderName}' not found", profile.Source);
                    return;
                }

                if (!provider.Connections.TryGetValue(speechToTextConnection, out var connection))
                {
                    _logger.LogWarning("Speech-to-text connection '{ConnectionName}' not found for provider '{ProviderName}'", speechToTextConnection, profile.Source);
                    return;
                }

                speechToTextClient = await _clientFactory.CreateSpeechToTextClientAsync(profile.Source, speechToTextConnection, connection.GetDefaultDeploymentName() ?? connection.GetDefaultSpeechToTextDeploymentName());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create speech-to-text client for chunk");
                return;
            }

            try
            {
                // Accumulate the streamed base64 chunks into a single audio stream
                using var audioStream = new MemoryStream();
                long totalBytes = 0;

                await foreach (var base64 in stream.WithCancellation(Context.ConnectionAborted))
                {
                    if (string.IsNullOrWhiteSpace(base64))
                    {
                        continue;
                    }

                    byte[] bytes;
                    try
                    {
                        bytes = Convert.FromBase64String(base64);
                    }
                    catch (FormatException)
                    {
                        // Ignore malformed chunk
                        continue;
                    }

                    totalBytes += bytes.Length;

                    await audioStream.WriteAsync(bytes, Context.ConnectionAborted);
                }

                audioStream.Position = 0;

                var builder = new StringBuilder();
                var messageId = IdGenerator.GenerateId();

                AIChatSession chatSession = null;
                var isNewSession = false;

                if (!string.IsNullOrWhiteSpace(sessionId))
                {
                    chatSession = await _sessionManager.FindAsync(sessionId);
                }

                if (chatSession is null)
                {
                    chatSession = await _sessionManager.NewAsync(profile, new NewAIChatSessionContext());
                    isNewSession = true;
                }

                await foreach (var update in speechToTextClient.GetStreamingTextAsync(audioStream, cancellationToken: Context.ConnectionAborted))
                {
                    if (update is not null && !string.IsNullOrEmpty(update.Text))
                    {
                        builder.Append(update.Text);
                        // Optionally stream partial transcript to caller here
                    }
                }

                var transcribedText = builder.ToString().Trim() ?? string.Empty;

                chatSession.Prompts.Add(new AIChatSessionPrompt
                {
                    Id = messageId,
                    Role = ChatRole.User,
                    Content = transcribedText,
                });

                if (isNewSession)
                {
                    await SetTitleAsync(profile, chatSession, transcribedText);
                }

                await _sessionManager.SaveAsync(chatSession);

            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error transcribing audio chunk");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing audio chunk");
        }
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
                await ProcessChatPromptAsync(writer, profile, sessionId, prompt.Trim(), cancellationToken);
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
        // Handle explicit HttpRequestException with known status codes.
        if (ex is HttpRequestException httpEx)
        {
            // Some HttpRequestExceptions don't have StatusCode populated (e.g., socket errors)
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

            // If no status code, it might be a network or DNS-level failure.
            return S["Unable to reach the provider. Please check your connection or endpoint URL."];
        }

        var message = ex.Message ?? string.Empty;

        // Authentication errors.
        if (message.Contains("401", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("403", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("invalid api key", StringComparison.OrdinalIgnoreCase) ||
            ex.GetType().Name.Contains("Authentication", StringComparison.OrdinalIgnoreCase))
        {
            return S["Authentication failed. Please check your API credentials."];
        }

        // Bad request / invalid parameters
        if (message.Contains("bad request", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("invalid request", StringComparison.OrdinalIgnoreCase))
        {
            return S["Invalid request. Please verify your profile configuration or parameters."];
        }

        // Not found errors.
        if (message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("endpoint not found", StringComparison.OrdinalIgnoreCase))
        {
            return S["The provider endpoint could not be found. Please verify the API URL."];
        }

        // Rate limit / too many requests.
        if (message.Contains("too many requests", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
        {
            return S["Rate limit reached. Please wait and try again later."];
        }

        // Connectivity / timeout issues.
        if (ex is TimeoutException || ex is TaskCanceledException)
        {
            return S["The request timed out. Please try again later."];
        }

        if (ex.InnerException is System.Net.Sockets.SocketException ||
            message.Contains("connection refused", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("name or service not known", StringComparison.OrdinalIgnoreCase))
        {
            return S["Unable to reach the provider. Please check your connection or endpoint URL."];
        }

        // Fallback generic error.
        return S["Our service is currently unavailable. Please try again later."];
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

        await SetTitleAsync(profile, chatSession, userPrompt);

        return (chatSession, true);
    }

    private async Task SetTitleAsync(AIProfile profile, AIChatSession chatSession, string userPrompt)
    {
        if (profile.TitleType == AISessionTitleType.Generated)
        {
            chatSession.Title = await GetGeneratedTitleAsync(profile, userPrompt);
        }

        if (string.IsNullOrEmpty(chatSession.Title))
        {
            chatSession.Title = Str.Truncate(userPrompt, 255);
        }
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

        var builder = new StringBuilder();

        var completionContext = await _aICompletionContextBuilder.BuildAsync(profile, c =>
        {
            c.Session = chatSession;
            c.UserMarkdownInResponse = true;
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
}
