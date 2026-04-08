using System.IO.Pipelines;
using System.Threading.Channels;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Chat.Hubs;
using CrestApps.Core.AI.Chat.Models;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Mvc.Web.Areas.ChatInteractions.Models;
using CrestApps.Core.Mvc.Web.Services;
using CrestApps.Core.Services;
using Cysharp.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;

#pragma warning disable MEAI001 // Speech APIs from Microsoft.Extensions.AI are preview and require explicit opt-in at each usage site.

namespace CrestApps.Core.Mvc.Web.Areas.ChatInteractions.Hubs;

[Authorize]
public sealed class ChatInteractionHub : ChatInteractionHubBase
{
    private readonly MvcCitationReferenceCollector _citationCollector;
    private readonly YesSql.ISession _session;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIClientFactory _aiClientFactory;
    private readonly AppDataSettingsService<ChatInteractionSettings> _chatInteractionSettingsService;
    private readonly AppDataSettingsService<DefaultAIDeploymentSettings> _defaultDeploymentSettingsService;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public ChatInteractionHub(
        ICatalogManager<ChatInteraction> interactionManager,
        IChatInteractionPromptStore promptStore,
        IOrchestrationContextBuilder orchestrationContextBuilder,
        IOrchestratorResolver orchestratorResolver,
        IEnumerable<IChatInteractionSettingsHandler> settingsHandlers,
        TimeProvider timeProvider,
        MvcCitationReferenceCollector citationCollector,
        YesSql.ISession session,
        IAIDeploymentManager deploymentManager,
        IAIClientFactory aiClientFactory,
        AppDataSettingsService<ChatInteractionSettings> chatInteractionSettingsService,
        AppDataSettingsService<DefaultAIDeploymentSettings> defaultDeploymentSettingsService,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ChatInteractionHub> logger)
        : base(interactionManager, promptStore, orchestrationContextBuilder, orchestratorResolver, settingsHandlers, timeProvider, logger)
    {
        _citationCollector = citationCollector;
        _session = session;
        _deploymentManager = deploymentManager;
        _aiClientFactory = aiClientFactory;
        _chatInteractionSettingsService = chatInteractionSettingsService;
        _defaultDeploymentSettingsService = defaultDeploymentSettingsService;
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override async Task CommitChangesAsync()
        => await _session.SaveChangesAsync();

    protected override void CollectPreemptiveReferences(
        OrchestrationContext context,
        Dictionary<string, AICompletionReference> references,
        HashSet<string> contentItemIds)
        => _citationCollector.CollectPreemptiveReferences(context, references, contentItemIds);

    protected override void CollectToolReferences(
        Dictionary<string, AICompletionReference> references,
        HashSet<string> contentItemIds)
        => _citationCollector.CollectToolReferences(references, contentItemIds);

    public async Task HandleNotificationAction(string itemId, string notificationType, string actionName)
    {
        if (string.IsNullOrWhiteSpace(itemId) || string.IsNullOrWhiteSpace(actionName))
        {
            return;
        }

        await using var scope = _serviceScopeFactory.CreateAsyncScope();

        try
        {
            var handler = scope.ServiceProvider.GetKeyedService<IChatNotificationActionHandler>(actionName);

            if (handler is null)
            {
                Logger.LogWarning("No notification action handler found for action '{ActionName}'.", actionName);
                return;
            }

            await handler.HandleAsync(new ChatNotificationActionContext
            {
                SessionId = itemId,
                NotificationType = notificationType,
                ActionName = actionName,
                ChatType = ChatContextType.ChatInteraction,
                ConnectionId = Context.ConnectionId,
                Services = scope.ServiceProvider,
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "An error occurred while handling notification action '{ActionName}'.", actionName);
            await Clients.Caller.ReceiveError("An error occurred while processing your action. Please try again.");
        }
    }

    public async Task StartConversation(string itemId, IAsyncEnumerable<string> audioChunks, string audioFormat = null, string language = null)
    {
        var interaction = await ValidateInteractionAsync(itemId);

        if (interaction is null)
        {
            return;
        }

        var chatInteractionSettings = await _chatInteractionSettingsService.GetAsync();

        if (chatInteractionSettings.ChatMode != ChatMode.Conversation)
        {
            await Clients.Caller.ReceiveError("Conversation mode is not enabled for chat interactions.");
            return;
        }

        var speechToTextDeployment = await _deploymentManager.ResolveOrDefaultAsync(AIDeploymentType.SpeechToText);

        if (speechToTextDeployment is null)
        {
            await Clients.Caller.ReceiveError("No speech-to-text deployment is configured or available.");
            return;
        }

        var textToSpeechDeployment = await _deploymentManager.ResolveOrDefaultAsync(AIDeploymentType.TextToSpeech);

        if (textToSpeechDeployment is null)
        {
            await Clients.Caller.ReceiveError("No text-to-speech deployment is configured or available.");
            return;
        }

        var deploymentDefaults = await _defaultDeploymentSettingsService.GetAsync();
        using var speechToTextClient = await _aiClientFactory.CreateSpeechToTextClientAsync(speechToTextDeployment);
        using var textToSpeechClient = await _aiClientFactory.CreateTextToSpeechClientAsync(textToSpeechDeployment);

        using var conversationCts = CancellationTokenSource.CreateLinkedTokenSource(Context.ConnectionAborted);
        SetConversationCancellation(conversationCts);

        try
        {
            await RunConversationLoopAsync(
                itemId,
                audioChunks,
                audioFormat,
                string.IsNullOrWhiteSpace(language) ? "en-US" : language,
                speechToTextClient,
                textToSpeechClient,
                deploymentDefaults.DefaultTextToSpeechVoiceId,
                conversationCts.Token);
        }
        catch (OperationCanceledException)
        {
            Logger.LogDebug("Conversation was cancelled.");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "An error occurred during conversation mode.");
            await Clients.Caller.ReceiveError("An error occurred during the conversation. Please try again.");
        }
        finally
        {
            ClearConversationCancellation();
        }
    }

    public async Task SendAudioStream(string itemId, IAsyncEnumerable<string> audioChunks, string audioFormat = null, string language = null)
    {
        var interaction = await ValidateInteractionAsync(itemId);

        if (interaction is null)
        {
            return;
        }

        var chatInteractionSettings = await _chatInteractionSettingsService.GetAsync();

        if (chatInteractionSettings.ChatMode == ChatMode.TextInput)
        {
            await Clients.Caller.ReceiveError("Audio input is not enabled for chat interactions.");
            return;
        }

        var speechToTextDeployment = await _deploymentManager.ResolveOrDefaultAsync(AIDeploymentType.SpeechToText);

        if (speechToTextDeployment is null)
        {
            await Clients.Caller.ReceiveError("No speech-to-text deployment is configured or available.");
            return;
        }

        try
        {
            using var speechToTextClient = await _aiClientFactory.CreateSpeechToTextClientAsync(speechToTextDeployment);

            await StreamTranscriptionAsync(
                speechToTextClient,
                itemId,
                audioChunks,
                audioFormat,
                string.IsNullOrWhiteSpace(language) ? "en-US" : language,
                Context.ConnectionAborted);
        }
        catch (OperationCanceledException)
        {
            Logger.LogDebug("Audio transcription was cancelled.");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "An error occurred while transcribing audio.");
            await Clients.Caller.ReceiveError(GetSpeechErrorMessage(ex, "speech-to-text", "An error occurred while transcribing the audio. Please try again."));
        }
    }

    public async Task SynthesizeSpeech(string itemId, string text, string voiceName = null)
    {
        var interaction = await ValidateInteractionAsync(itemId);

        if (interaction is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            await Clients.Caller.ReceiveError("text is required.");
            return;
        }

        var chatInteractionSettings = await _chatInteractionSettingsService.GetAsync();

        if (chatInteractionSettings.ChatMode != ChatMode.Conversation)
        {
            await Clients.Caller.ReceiveError("Text-to-speech is not enabled for chat interactions.");
            return;
        }

        var textToSpeechDeployment = await _deploymentManager.ResolveOrDefaultAsync(AIDeploymentType.TextToSpeech);

        if (textToSpeechDeployment is null)
        {
            await Clients.Caller.ReceiveError("No text-to-speech deployment is configured or available.");
            return;
        }

        try
        {
            var deploymentDefaults = await _defaultDeploymentSettingsService.GetAsync();
            using var textToSpeechClient = await _aiClientFactory.CreateTextToSpeechClientAsync(textToSpeechDeployment);

            await StreamSpeechAsync(
                textToSpeechClient,
                itemId,
                text,
                string.IsNullOrWhiteSpace(voiceName) ? deploymentDefaults.DefaultTextToSpeechVoiceId : voiceName,
                Context.ConnectionAborted);
        }
        catch (OperationCanceledException)
        {
            Logger.LogDebug("Speech synthesis was cancelled.");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "An error occurred while synthesizing speech.");
            await Clients.Caller.ReceiveError(GetSpeechErrorMessage(ex, "text-to-speech", "An error occurred while synthesizing speech. Please try again."));
        }
    }

    private async Task<ChatInteraction> ValidateInteractionAsync(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            await Clients.Caller.ReceiveError("itemId is required.");
            return null;
        }

        var interaction = await InteractionManager.FindByIdAsync(itemId);

        if (interaction is null)
        {
            await Clients.Caller.ReceiveError("Interaction not found.");
            return null;
        }

        if (!await AuthorizeAsync(interaction))
        {
            return null;
        }

        return interaction;
    }

    private async Task RunConversationLoopAsync(
        string itemId,
        IAsyncEnumerable<string> audioChunks,
        string audioFormat,
        string speechLanguage,
        ISpeechToTextClient speechToTextClient,
        ITextToSpeechClient textToSpeechClient,
        string voiceName,
        CancellationToken cancellationToken)
    {
        var pipe = new Pipe();
        using var errorCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var transcriptionTask = TranscribeConversationAsync(
            pipe.Reader,
            itemId,
            audioFormat,
            speechLanguage,
            speechToTextClient,
            textToSpeechClient,
            voiceName,
            errorCts,
            cancellationToken);

        try
        {
            await foreach (var base64Chunk in audioChunks.WithCancellation(errorCts.Token))
            {
                try
                {
                    await pipe.Writer.WriteAsync(Convert.FromBase64String(base64Chunk), errorCts.Token);
                }
                catch (FormatException)
                {
                }
            }
        }
        catch (OperationCanceledException) when (errorCts.IsCancellationRequested)
        {
        }

        await pipe.Writer.CompleteAsync();
        await transcriptionTask;
    }

    private async Task TranscribeConversationAsync(
        PipeReader pipeReader,
        string itemId,
        string audioFormat,
        string speechLanguage,
        ISpeechToTextClient speechToTextClient,
        ITextToSpeechClient textToSpeechClient,
        string voiceName,
        CancellationTokenSource errorCts,
        CancellationToken cancellationToken)
    {
        CancellationTokenSource currentResponseCts = null;
        Task currentResponseTask = null;

        try
        {
            await using var readerStream = pipeReader.AsStream();
            using var committedText = ZString.CreateStringBuilder();
            var sttOptions = new SpeechToTextOptions
            {
                SpeechLanguage = speechLanguage,
            };

            if (!string.IsNullOrWhiteSpace(audioFormat))
            {
                sttOptions.AdditionalProperties ??= [];
                sttOptions.AdditionalProperties["audioFormat"] = audioFormat;
            }

            await foreach (var update in speechToTextClient.GetStreamingTextAsync(readerStream, sttOptions, cancellationToken))
            {
                if (string.IsNullOrEmpty(update.Text))
                {
                    continue;
                }

                var isPartial = update.AdditionalProperties?.TryGetValue("isPartial", out var partial) == true && partial is true;

                if (isPartial)
                {
                    var display = committedText.Length > 0 ? committedText.ToString() + update.Text : update.Text;
                    await Clients.Caller.ReceiveTranscript(itemId, display, false);
                    continue;
                }

                if (currentResponseCts is not null)
                {
                    await currentResponseCts.CancelAsync();

                    if (currentResponseTask is not null)
                    {
                        try
                        {
                            await currentResponseTask;
                        }
                        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                        {
                        }
                    }

                    currentResponseCts.Dispose();
                    currentResponseCts = null;
                    currentResponseTask = null;
                }

                if (committedText.Length > 0)
                {
                    committedText.Append(' ');
                }

                committedText.Append(update.Text);

                var fullText = committedText.ToString().TrimEnd();

                await Clients.Caller.ReceiveTranscript(itemId, fullText, true);
                await Clients.Caller.ReceiveConversationUserMessage(itemId, fullText);

                committedText.Clear();
                currentResponseCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                currentResponseTask = ProcessConversationPromptAsync(itemId, fullText, textToSpeechClient, voiceName, currentResponseCts.Token);
            }

            if (currentResponseTask is not null)
            {
                try
                {
                    await currentResponseTask;
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                }

                currentResponseCts?.Dispose();
            }
        }
        catch (Exception)
        {
            await errorCts.CancelAsync();
            throw;
        }
    }

    private async Task ProcessConversationPromptAsync(
        string itemId,
        string prompt,
        ITextToSpeechClient textToSpeechClient,
        string voiceName,
        CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<CompletionPartialMessage>();
        var handleTask = HandlePromptAsync(channel.Writer, itemId, prompt, cancellationToken);
        var sentenceChannel = Channel.CreateUnbounded<string>();

        string messageId = null;
        string responseId = null;

        var ttsTask = StreamSentencesAsSpeechAsync(textToSpeechClient, () => itemId, sentenceChannel.Reader, voiceName, cancellationToken);
        using var sentenceBuffer = ZString.CreateStringBuilder();

        try
        {
            await foreach (var chunk in channel.Reader.ReadAllAsync(cancellationToken))
            {
                messageId ??= chunk.MessageId;
                responseId ??= chunk.ResponseId;

                if (string.IsNullOrEmpty(chunk.Content))
                {
                    continue;
                }

                await Clients.Caller.ReceiveConversationAssistantToken(itemId, messageId ?? string.Empty, chunk.Content, responseId ?? string.Empty);
                sentenceBuffer.Append(chunk.Content);

                if (!SentenceBoundaryDetector.EndsWithSentenceBoundary(chunk.Content))
                {
                    continue;
                }

                var sentence = sentenceBuffer.ToString().Trim();

                if (string.IsNullOrEmpty(sentence))
                {
                    continue;
                }

                await sentenceChannel.Writer.WriteAsync(sentence, cancellationToken);
                sentenceBuffer.Clear();
            }

            await handleTask;

            var remaining = sentenceBuffer.ToString().Trim();

            if (!string.IsNullOrEmpty(remaining))
            {
                await sentenceChannel.Writer.WriteAsync(remaining, cancellationToken);
            }

            sentenceChannel.Writer.Complete();
            await ttsTask;
        }
        finally
        {
            sentenceChannel.Writer.TryComplete();

            if (!string.IsNullOrEmpty(messageId))
            {
                try
                {
                    await Clients.Caller.ReceiveConversationAssistantComplete(itemId, messageId);
                }
                catch
                {
                }
            }
        }
    }

    private async Task StreamTranscriptionAsync(
        ISpeechToTextClient speechToTextClient,
        string itemId,
        IAsyncEnumerable<string> audioChunks,
        string audioFormat,
        string speechLanguage,
        CancellationToken cancellationToken)
    {
        var pipe = new Pipe();
        using var errorCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var transcriptionTask = TranscribeAudioInputAsync(
            itemId,
            pipe,
            audioFormat,
            speechLanguage,
            speechToTextClient,
            errorCts,
            cancellationToken);

        try
        {
            await foreach (var base64Chunk in audioChunks.WithCancellation(errorCts.Token))
            {
                try
                {
                    await pipe.Writer.WriteAsync(Convert.FromBase64String(base64Chunk), errorCts.Token);
                }
                catch (FormatException)
                {
                }
            }
        }
        catch (OperationCanceledException) when (errorCts.IsCancellationRequested)
        {
        }

        await pipe.Writer.CompleteAsync();
        await transcriptionTask;
    }

    private async Task TranscribeAudioInputAsync(
        string itemId,
        Pipe pipe,
        string audioFormat,
        string speechLanguage,
        ISpeechToTextClient speechToTextClient,
        CancellationTokenSource errorCts,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var readerStream = pipe.Reader.AsStream();
            using var committedText = ZString.CreateStringBuilder();
            var sttOptions = new SpeechToTextOptions
            {
                SpeechLanguage = speechLanguage,
            };

            if (!string.IsNullOrWhiteSpace(audioFormat))
            {
                sttOptions.AdditionalProperties ??= [];
                sttOptions.AdditionalProperties["audioFormat"] = audioFormat;
            }

            await foreach (var update in speechToTextClient.GetStreamingTextAsync(readerStream, sttOptions, cancellationToken))
            {
                if (string.IsNullOrEmpty(update.Text))
                {
                    continue;
                }

                var isPartial = update.AdditionalProperties?.TryGetValue("isPartial", out var partial) == true && partial is true;

                if (isPartial)
                {
                    var display = committedText.Length > 0 ? committedText.ToString() + update.Text : update.Text;
                    await Clients.Caller.ReceiveTranscript(itemId, display, false);
                    continue;
                }

                if (committedText.Length > 0)
                {
                    committedText.Append(' ');
                }

                committedText.Append(update.Text);
                await Clients.Caller.ReceiveTranscript(itemId, committedText.ToString(), false);
            }

            var finalText = committedText.ToString().TrimEnd();

            if (!string.IsNullOrEmpty(finalText))
            {
                await Clients.Caller.ReceiveTranscript(itemId, finalText, true);
            }
        }
        catch (Exception)
        {
            await errorCts.CancelAsync();
            throw;
        }
    }

    private static string GetSpeechErrorMessage(Exception ex, string capabilityName, string fallbackMessage)
    {
        var message = ex?.ToString();

        if (string.IsNullOrWhiteSpace(message))
        {
            return fallbackMessage;
        }

        if (message.Contains("AuthenticationFailure", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Authentication error (401)", StringComparison.OrdinalIgnoreCase)
            || message.Contains("check subscription information and region name", StringComparison.OrdinalIgnoreCase))
        {
            return $"{capabilityName} authentication failed. Check the configured speech deployment credentials and region.";
        }

        return fallbackMessage;
    }
}

#pragma warning restore MEAI001
