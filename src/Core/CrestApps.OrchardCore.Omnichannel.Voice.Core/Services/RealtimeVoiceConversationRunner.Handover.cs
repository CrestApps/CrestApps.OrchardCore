using CrestApps.Core;
using CrestApps.Core.AI.Capabilities;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Handing a live call to another AI profile: the old profile finishes what it was saying, the caller hears the call
/// being put through, and a session of the new profile's own -- its model, its voice, its instructions and the call's
/// guidance -- picks the call up and greets them.
/// </summary>
/// <remarks>
/// A live session cannot change its voice once it has spoken, so changing the profile inside it had the new profile
/// speak in the old one's voice, and the caller could not tell they had been transferred.
/// </remarks>
public sealed partial class RealtimeVoiceConversationRunner
{
    /// <summary>
    /// How long the line must have been quiet before the old profile's session is closed. A tool's result is answered
    /// by a reply of its own a moment after the tool's response completes, and that reply is the old profile saying it
    /// is putting the caller through.
    /// </summary>
    internal static readonly TimeSpan HandoverSettle = TimeSpan.FromMilliseconds(1200);

    /// <summary>
    /// The longest the caller hears the call being put through. The new profile normally speaks well before.
    /// </summary>
    internal static readonly TimeSpan TransferToneLimit = TimeSpan.FromSeconds(6);

    private AIProfile _handoverProfile;
    private int _handoverInProgress;
    private CancellationTokenSource _transferTone;
    private Task _transferToneTask = Task.CompletedTask;

    /// <summary>
    /// Gets whether the assistant is busy: answering, or being handed to another profile. The idle watchdog does not
    /// ask a caller whether they are still there while it is.
    /// </summary>
    internal bool IsBusy
        => Volatile.Read(ref _responseInFlight) == 1 || Volatile.Read(ref _handoverInProgress) == 1;

    /// <summary>
    /// Queues the call's handover to a profile, and closes the current session once the old profile has finished.
    /// </summary>
    private void QueueHandover(LiveConversation live, AIProfile profile, CancellationToken callToken)
    {
        Volatile.Write(ref _handoverInProgress, 1);
        Volatile.Write(ref _handoverProfile, profile);

        _ = Task.Run(() => EndSessionForHandoverAsync(live, callToken), CancellationToken.None);
    }

    private AIProfile TakeHandover()
        => Interlocked.Exchange(ref _handoverProfile, null);

    // The old profile is let finish: no reply running, none ended a moment ago, and nothing it said still playing.
    private async Task EndSessionForHandoverAsync(LiveConversation live, CancellationToken callToken)
    {
        try
        {
            var giveUpAt = DateTime.UtcNow + SupervisorNoteWait;

            while (DateTime.UtcNow < giveUpAt && !IsLineQuiet())
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), callToken);
            }

            live.EndSession();
        }
        catch (OperationCanceledException)
        {
            // The call ended before the handover.
        }
    }

    private bool IsLineQuiet()
    {
        var now = DateTime.UtcNow.Ticks;

        return Volatile.Read(ref _responseInFlight) == 0 &&
            now - Interlocked.Read(ref _lastResponseEndedTicks) >= HandoverSettle.Ticks &&
            now >= Interlocked.Read(ref _assistantSpeechEndsTicks);
    }

    /// <summary>
    /// Opens the new profile's session, seeded with the conversation so far, and has it greet the caller.
    /// </summary>
    /// <returns><see langword="true"/> when the new profile's session carries the call.</returns>
    private async Task<bool> HandOverAsync(
        IContactCenterVoiceMediaSession media,
        LiveConversation live,
        RealtimeVoiceConversationContext context,
        AIProfile profile,
        CancellationToken callToken)
    {
        var previousSwitch = context.Activity is not null && context.Activity.TryGet<AIProfileSwitch>(out var kept) ? kept : null;
        var previous = new CallProfile(context.Profile, context.RealtimeDeploymentName, context.Activity?.AIProfileId, previousSwitch);

        StartTransferTone(media, callToken);

        try
        {
            context.RealtimeDeploymentName = await ResolveRealtimeDeploymentNameAsync(profile, callToken) ?? context.RealtimeDeploymentName;
            PutOnProfile(context, profile, profile.ItemId, new AIProfileSwitch { ProfileId = profile.ItemId });

            await live.DetachAsync();

            var next = await StartConversationAsync(context, await DescribeConversationSoFarAsync(context, callToken, handedOver: true), callToken);

            if (next is null)
            {
                await GoBackAsync(context, previous);

                return false;
            }

            try
            {
                await ApplyTelephonyTurnDetectionAsync(next, callToken);

                // A fresh session has no reply running, so the greeting is asked for straight away, from a note in
                // the conversation rather than with instructions of its own, which had the model repeat its opening.
                await next.AddMessageAsync(ChatRole.System, VoiceAgentConversationLoop.ProfileSwitchedTurn, callToken);
                await next.RequestUnpromptedResponseAsync(cancellationToken: callToken);
            }
            catch (Exception ex)
            {
                if (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "The session of the profile a live AI call was handed to opened but could not be started.");
                }

                await next.DisposeAsync();
                await GoBackAsync(context, previous);

                return false;
            }

            live.Attach(next, callToken);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Handed a live AI call on activity '{ActivityId}' to profile '{ProfileId}' in a session of its own.",
                    context.Activity?.ItemId.SanitizeLogValue(),
                    profile.ItemId.SanitizeLogValue());
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            await GoBackAsync(context, previous);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "A live AI call could not be handed to profile '{ProfileId}'.", profile.ItemId.SanitizeLogValue());
            await GoBackAsync(context, previous);

            return false;
        }
        finally
        {
            Volatile.Write(ref _handoverInProgress, 0);
        }
    }

    private sealed record CallProfile(AIProfile Profile, string RealtimeDeploymentName, string ActivityProfileId, AIProfileSwitch Switch);

    // The call follows the profile everywhere it is read from: its guardrails, its tools and the turns it saves.
    private void PutOnProfile(RealtimeVoiceConversationContext context, AIProfile profile, string activityProfileId, AIProfileSwitch profileSwitch)
    {
        context.Profile = profile;

        if (_callConversation is not null)
        {
            _callConversation.Profile = profile;
        }

        if (context.Session is not null && profile is not null)
        {
            context.Session.ProfileId = profile.ItemId;
        }

        if (context.Activity is not null)
        {
            context.Activity.AIProfileId = activityProfileId;
            context.Activity.Put(profileSwitch ?? new AIProfileSwitch());
        }
    }

    // The new profile's session did not open: the call goes back to the profile it was on, which the recovery that
    // follows reopens.
    private async Task GoBackAsync(RealtimeVoiceConversationContext context, CallProfile previous)
    {
        await StopTransferToneAsync();
        context.RealtimeDeploymentName = previous.RealtimeDeploymentName;
        PutOnProfile(context, previous.Profile, previous.ActivityProfileId, previous.Switch);
    }

    // The new profile's own realtime model, when it names one; otherwise the call keeps the one it has.
    private static async Task<string> ResolveRealtimeDeploymentNameAsync(AIProfile profile, CancellationToken cancellationToken)
    {
        var services = ShellScope.Services;
        var capabilityService = services?.GetService<IAIDeploymentCapabilityService>();
        var deploymentManager = services?.GetService<IAIDeploymentManager>();

        if (capabilityService is null || deploymentManager is null)
        {
            return null;
        }

        var deployment = await VoiceAgentConversationLoop.ResolveRealtimeDeploymentAsync(
            VoiceAgentConversationLoop.GetConversationDeploymentName(profile),
            profile.ChatDeploymentName,
            capabilityService,
            deploymentManager,
            cancellationToken);

        return deployment?.Name;
    }

    /// <summary>
    /// Plays the transfer tone on the line until the new profile speaks, the caller talks, or it has rung long enough.
    /// </summary>
    private void StartTransferTone(IContactCenterVoiceMediaSession media, CancellationToken callToken)
    {
        if (media is null || _outgoing is null)
        {
            return;
        }

        var stop = CancellationTokenSource.CreateLinkedTokenSource(callToken);
        stop.CancelAfter(TransferToneLimit);

        _transferToneTask = PlayTransferToneAsync(media, stop.Token);
        Volatile.Write(ref _transferTone, stop);
    }

    /// <summary>
    /// Stops the transfer tone, if it is playing, before anything else is written to the line.
    /// </summary>
    private async Task StopTransferToneAsync()
    {
        if (Interlocked.Exchange(ref _transferTone, null) is not { } stop)
        {
            return;
        }

        await stop.CancelAsync();

        try
        {
            await _transferToneTask;
        }
        catch (Exception)
        {
            // The tone is cosmetic; whatever stopped it, the call goes on.
        }

        stop.Dispose();

        // What the new profile says starts fresh rather than on the tone's last sample.
        _outgoing?.Reset();
    }

    private async Task PlayTransferToneAsync(IContactCenterVoiceMediaSession media, CancellationToken cancellationToken)
    {
        var samplesPerFrame = RealtimeAudioConverter.RealtimeSampleRate * AmbienceFrameMilliseconds / 1000;
        var tone = new TransferTone(RealtimeAudioConverter.RealtimeSampleRate);

        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(AmbienceFrameMilliseconds));

            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                // Counted as the assistant's audio: the room bed waits behind it, and the silence clocks do not run.
                Interlocked.Exchange(ref _lastAssistantAudioTicks, DateTime.UtcNow.Ticks);

                await WriteToLineAsync(media, _outgoing.Encode(tone.NextPcmBytes(samplesPerFrame)), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Stopped: the new profile spoke, the caller talked, or it rang long enough.
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "The transfer tone stopped early; the handover goes on without it.");
        }
    }
}
