using CrestApps.Core;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Tools;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.ContactCenter;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// The tools a turn-based call offers the model on each turn.
/// </summary>
public sealed partial class VoiceAgentConversationLoop
{
    /// <summary>
    /// How long a tool runs before the caller hears a short filler line, so a lookup is not heard as a dropped call.
    /// </summary>
    internal static readonly TimeSpan ToolFillerDelay = TimeSpan.FromSeconds(1.5);

    // The call's own tools, offered alongside the profile's. The automated conversation calls the completion service
    // directly rather than through the orchestrator, so the tools are attached to the context for this turn only:
    // the profile's tools, tool instances, MCP connections and agents come from the same registry the orchestrator
    // reads (see AIConversationTools), and the call's system tools are added after them. The profile's own tool list
    // skips system tools, which these are, so they could never be enabled through it.
    private Task<IReadOnlyList<ToolRegistryEntry>> AttachCallToolsAsync(AICompletionContext context, bool offerTransfer, CancellationToken cancellationToken)
    {
        // The end-call tool is offered on every turn, and the transfer tool only when this call has somewhere to
        // transfer to. Ending the call needs no such condition: the model can always be finished talking, and a
        // call it cannot end is one that ends when the customer works out that nobody is going to hang up.
        var systemTools = new List<ToolRegistryEntry>
        {
            AIConversationTools.SystemTool(EndCallTool.ToolName, "Ends the phone call once the conversation has genuinely finished."),
            AIConversationTools.SystemTool(PressKeysTool.ToolName, "Presses keys on the phone keypad to get through a phone menu."),
        };

        // Secure keypad entry, where secure data capture is on to take what the caller types.
        if (OffersSecureKeypad(ShellScope.Services))
        {
            systemTools.Add(AIConversationTools.SystemTool(
                CollectSecureDigitsTool.ToolName,
                "Makes the caller's keypad private before they type a card, account or identity number."));
        }

        if (offerTransfer)
        {
            systemTools.Add(AIConversationTools.SystemTool(
                OmnichannelHandoffHelper.TransferToAgentToolName,
                "Transfers the current conversation to a live human agent."));
        }

        return AIConversationTools.AttachAsync(_toolRegistry, context, systemTools, cancellationToken);
    }

    /// <summary>
    /// Whether calls offer the AI secure keypad entry: only where secure data capture can take what the caller types.
    /// </summary>
    /// <param name="services">The tenant's services, or <see langword="null"/> outside a shell scope.</param>
    /// <returns><see langword="true"/> when the tool is offered.</returns>
    internal static bool OffersSecureKeypad(IServiceProvider services)
        => services?.GetService<ISecureCaptureTokenSink>() is not null;

    /// <summary>
    /// Puts a time limit on this turn's tools and has the caller hear a short filler line when one runs long.
    /// </summary>
    /// <param name="context">The turn's completion context, with its tools attached.</param>
    /// <param name="activity">The call's activity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The filler for this turn; dispose it once the completion returns.</returns>
    private async Task<ToolFiller> LimitCallToolsAsync(AICompletionContext context, OmnichannelActivity activity, CancellationToken cancellationToken)
    {
        IVoiceAgentMediaProvider media = null;
        CallSpeech speech = null;
        string providerCallId = null;

        if (activity is not null && activity.TryGet<AILiveCall>(out var liveCall) && !string.IsNullOrEmpty(liveCall.ProviderCallId))
        {
            media = _mediaResolver.Get(liveCall.ProviderName) ?? _mediaResolver.GetDefault();
            providerCallId = liveCall.ProviderCallId;

            // Chosen now, on this request's own thread: the filler is spoken while a tool may be using the session.
            speech = media is null ? null : await GetCallSpeechAsync(media, activity, cancellationToken);
        }

        var filler = new ToolFiller(
            media is null || speech is null
                ? null
                : token => SpeakFillerAsync(media, providerCallId, activity, speech, token),
            ToolFillerDelay);

        AIConversationTools.LimitRunTime(context, AIConversationTools.DefaultCallToolTimeout, filler.Started, filler.Finished);

        return filler;
    }

    private async Task SpeakFillerAsync(IVoiceAgentMediaProvider media, string providerCallId, OmnichannelActivity activity, CallSpeech speech, CancellationToken cancellationToken)
    {
        var line = FillerLine(speech.SpeakLanguage);

        // Recorded first: the line can end before the provider has answered the request to speak it.
        TurnBasedToolFillers.Spoke(providerCallId, _clock.UtcNow);

        if (!await media.SpeakAsync(providerCallId, line, voice: speech.Voice, language: speech.SpeakLanguage, cancellationToken: cancellationToken))
        {
            TurnBasedToolFillers.TryClaim(providerCallId, _clock.UtcNow);

            return;
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "A tool on automated call activity '{ActivityId}' was slow; the caller heard a filler line.",
                activity.ItemId.SanitizeLogValue());
        }
    }

    /// <summary>
    /// The filler line in the call's language, in English when the language has none here.
    /// </summary>
    /// <param name="language">The language the call speaks, such as <c>es-MX</c>.</param>
    /// <returns>The line.</returns>
    internal static string FillerLine(string language)
    {
        var code = string.IsNullOrWhiteSpace(language)
            ? "en"
            : language.Split('-', '_')[0].ToLowerInvariant();

        return code switch
        {
            "es" => "Un momento, por favor.",
            "fr" => "Un instant, s'il vous plaît.",
            "de" => "Einen Moment, bitte.",
            "it" => "Un momento, per favore.",
            "pt" => "Um momento, por favor.",
            "nl" => "Een moment, alstublieft.",
            "ar" => "لحظة من فضلك.",
            "el" => "Μια στιγμή, παρακαλώ.",
            _ => "One moment, please.",
        };
    }

    /// <summary>
    /// Speaks one filler line on a turn when a tool has been running longer than the delay.
    /// </summary>
    internal sealed class ToolFiller : IDisposable
    {
        private readonly Func<CancellationToken, Task> _speak;
        private readonly TimeSpan _delay;
        private readonly CancellationTokenSource _turn = new();
        private int _running;
        private int _scheduled;
        private int _spoken;

        /// <summary>
        /// Initializes a new instance of the <see cref="ToolFiller"/> class.
        /// </summary>
        /// <param name="speak">Speaks the line; <see langword="null"/> when the call cannot be spoken to.</param>
        /// <param name="delay">How long a tool runs before the line is spoken.</param>
        public ToolFiller(Func<CancellationToken, Task> speak, TimeSpan delay)
        {
            _speak = speak;
            _delay = delay;
        }

        /// <summary>
        /// Gets a value indicating whether the line was spoken, or is being spoken, on this turn.
        /// </summary>
        public bool Spoken => Volatile.Read(ref _spoken) == 1;

        /// <summary>
        /// Gets the task speaking the line once it is scheduled. For tests.
        /// </summary>
        internal Task Pending { get; private set; } = Task.CompletedTask;

        /// <summary>
        /// A tool started running.
        /// </summary>
        /// <param name="toolName">The tool's name.</param>
        public void Started(string toolName)
        {
            Interlocked.Increment(ref _running);

            // One line per turn, however many tools the model calls.
            if (_speak is null || Interlocked.Exchange(ref _scheduled, 1) == 1)
            {
                return;
            }

            Pending = SpeakWhenSlowAsync();
        }

        /// <summary>
        /// A tool stopped running.
        /// </summary>
        /// <param name="toolName">The tool's name.</param>
        public void Finished(string toolName)
            => Interlocked.Decrement(ref _running);

        /// <inheritdoc/>
        public void Dispose()
        {
            _turn.Cancel();
            _turn.Dispose();
        }

        private async Task SpeakWhenSlowAsync()
        {
            try
            {
                await Task.Delay(_delay, _turn.Token);

                if (Volatile.Read(ref _running) > 0 && !_turn.IsCancellationRequested)
                {
                    Volatile.Write(ref _spoken, 1);
                    await _speak(_turn.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // The turn ended first: nothing to say.
            }
            catch (ObjectDisposedException)
            {
                // The turn ended while the delay was being set up.
            }
        }
    }
}
