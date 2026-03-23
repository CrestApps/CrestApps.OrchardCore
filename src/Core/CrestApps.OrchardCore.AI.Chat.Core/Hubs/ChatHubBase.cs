using System.Threading.Channels;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;

#pragma warning disable MEAI001 // Text-to-speech APIs from Microsoft.Extensions.AI are preview and require explicit opt-in at each usage site.
namespace CrestApps.OrchardCore.AI.Chat.Core.Hubs;

/// <summary>
/// Base class for chat hubs that provides shared functionality for speech synthesis,
/// audio streaming, and conversation management.
/// </summary>
public abstract class ChatHubBase<TClient> : Hub<TClient>
    where TClient : class, IChatHubClient
{
    private const string _conversationCtsKey = "ConversationCts";

    protected readonly ILogger Logger;
    protected readonly IStringLocalizer S;

    protected ChatHubBase(ILogger logger, IStringLocalizer stringLocalizer)
    {
        Logger = logger;
        S = stringLocalizer;
    }

    public Task StopConversation()
    {
        if (Context.Items.TryGetValue(_conversationCtsKey, out var value) && value is CancellationTokenSource cts)
        {
            cts.Cancel();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles a user-initiated action on a chat notification system message.
    /// Dispatches to registered <see cref="IChatNotificationActionHandler"/> implementations.
    /// </summary>
    public async Task HandleNotificationAction(string sessionId, string notificationType, string actionName)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(actionName))
        {
            return;
        }

        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            try
            {
                var handler = scope.ServiceProvider.GetKeyedService<IChatNotificationActionHandler>(actionName);

                if (handler is null)
                {
                    Logger.LogWarning("No notification action handler found for action '{ActionName}'.", actionName);
                    return;
                }

                var context = new ChatNotificationActionContext
                {
                    SessionId = sessionId,
                    NotificationType = notificationType,
                    ActionName = actionName,
                    ChatType = GetChatType(),
                    ConnectionId = Context.ConnectionId,
                    Services = scope.ServiceProvider,
                };

                await handler.HandleAsync(context);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "An error occurred while handling notification action '{ActionName}'.", actionName);

                try
                {
                    await Clients.Caller.ReceiveError(S["An error occurred while processing your action. Please try again."].Value);
                }
                catch
                {
                    // Best-effort error reporting.
                }
            }
        });
    }

    /// <summary>
    /// Gets the chat context type for this hub. Used by <see cref="HandleNotificationAction"/>
    /// to build the action context.
    /// </summary>
    protected abstract ChatContextType GetChatType();

    /// <summary>
    /// Gets the key used to store the conversation cancellation token source in the hub context items.
    /// </summary>
    protected static string ConversationCtsKey => _conversationCtsKey;

    /// <summary>
    /// Synthesizes the given text as speech and streams audio chunks to the caller.
    /// </summary>
    protected async Task StreamSpeechAsync(
        ITextToSpeechClient ttsClient,
        string identifier,
        string text,
        string voiceName,
        CancellationToken cancellationToken)
    {
        var options = new TextToSpeechOptions();

        if (!string.IsNullOrWhiteSpace(voiceName))
        {
            options.VoiceId = voiceName;
        }

        var speechText = SpeechTextSanitizer.Sanitize(text);

        if (string.IsNullOrWhiteSpace(speechText))
        {
            await Clients.Caller.ReceiveAudioComplete(identifier);
            return;
        }

        await foreach (var update in ttsClient.GetStreamingAudioAsync(speechText, options, cancellationToken))
        {
            var audioContent = update.Contents.OfType<DataContent>().FirstOrDefault();
            if (audioContent?.Data is not { Length: > 0 } audioData)
            {
                continue;
            }

            var base64Audio = Convert.ToBase64String(audioData.ToArray());
            await Clients.Caller.ReceiveAudioChunk(identifier, base64Audio, audioContent.MediaType ?? "audio/mp3");
        }

        await Clients.Caller.ReceiveAudioComplete(identifier);
    }

    /// <summary>
    /// Reads sentences from a channel and synthesizes each as speech, streaming audio chunks to the caller.
    /// </summary>
    protected async Task StreamSentencesAsSpeechAsync(
        ITextToSpeechClient ttsClient,
        Func<string> getIdentifier,
        ChannelReader<string> sentenceReader,
        string voiceName,
        CancellationToken cancellationToken)
    {
        var options = new TextToSpeechOptions();

        if (!string.IsNullOrWhiteSpace(voiceName))
        {
            options.VoiceId = voiceName;
        }

        await foreach (var sentence in sentenceReader.ReadAllAsync(cancellationToken))
        {
            var identifier = getIdentifier();
            var speechText = SpeechTextSanitizer.Sanitize(sentence);

            if (string.IsNullOrWhiteSpace(speechText))
            {
                continue;
            }

            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("StreamSentencesAsSpeechAsync: Synthesizing sentence ({Length} chars).", speechText.Length);
            }

            // Only stream audio — text tokens were already sent immediately in ProcessConversationPromptAsync.
            await foreach (var update in ttsClient.GetStreamingAudioAsync(speechText, options, cancellationToken))
            {
                var audioContent = update.Contents.OfType<DataContent>().FirstOrDefault();
                if (audioContent?.Data is not { Length: > 0 } audioData)
                {
                    continue;
                }

                var base64Audio = Convert.ToBase64String(audioData.ToArray());
                await Clients.Caller.ReceiveAudioChunk(identifier, base64Audio, audioContent.MediaType ?? "audio/mp3");
            }

            // Signal that this sentence's audio is ready for playback immediately,
            // so the client can start playing each sentence as it's synthesized
            // rather than waiting for all sentences to finish.
            await Clients.Caller.ReceiveAudioComplete(identifier);
        }
    }
}
