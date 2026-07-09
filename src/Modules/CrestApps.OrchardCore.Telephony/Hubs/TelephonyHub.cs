using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Telephony.Hubs;

/// <summary>
/// SignalR hub that receives soft phone requests from the browser and routes them to the configured
/// telephony provider through <see cref="ITelephonyService"/>. Each invocation runs in its own
/// OrchardCore shell scope and is authorized against <see cref="TelephonyPermissions.UseSoftPhone"/>.
/// </summary>
[Authorize]
public sealed class TelephonyHub : Hub<ITelephonyClient>
{
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonyHub"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TelephonyHub(
        ILogger<TelephonyHub> logger,
        IStringLocalizer<TelephonyHub> stringLocalizer)
    {
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// Places an outbound call.
    /// </summary>
    /// <param name="request">The dial request.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    public Task<TelephonyResult> Dial(DialRequest request)
        => ExecuteAsync("Dial", () => DescribeDialRequest(request), (service, token) => service.DialAsync(request, token));

    /// <summary>
    /// Ends an active call.
    /// </summary>
    /// <param name="call">A reference to the call to end.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    public Task<TelephonyResult> Hangup(CallReference call)
        => ExecuteAsync("Hangup", () => DescribeCallReference(call), (service, token) => service.HangupAsync(call, token));

    /// <summary>
    /// Places an active call on hold.
    /// </summary>
    /// <param name="call">A reference to the call to place on hold.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    public Task<TelephonyResult> Hold(CallReference call)
        => ExecuteAsync("Hold", () => DescribeCallReference(call), (service, token) => service.HoldAsync(call, token));

    /// <summary>
    /// Resumes a call that is on hold.
    /// </summary>
    /// <param name="call">A reference to the call to resume.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    public Task<TelephonyResult> Resume(CallReference call)
        => ExecuteAsync("Resume", () => DescribeCallReference(call), (service, token) => service.ResumeAsync(call, token));

    /// <summary>
    /// Mutes the local audio of an active call.
    /// </summary>
    /// <param name="call">A reference to the call to mute.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    public Task<TelephonyResult> Mute(CallReference call)
        => ExecuteAsync("Mute", () => DescribeCallReference(call), (service, token) => service.MuteAsync(call, token));

    /// <summary>
    /// Unmutes the local audio of an active call.
    /// </summary>
    /// <param name="call">A reference to the call to unmute.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    public Task<TelephonyResult> Unmute(CallReference call)
        => ExecuteAsync("Unmute", () => DescribeCallReference(call), (service, token) => service.UnmuteAsync(call, token));

    /// <summary>
    /// Transfers an active call to another destination.
    /// </summary>
    /// <param name="request">The transfer request.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    public Task<TelephonyResult> Transfer(TransferRequest request)
        => ExecuteAsync("Transfer", () => DescribeTransferRequest(request), (service, token) => service.TransferAsync(request, token));

    /// <summary>
    /// Merges two active calls into a conference.
    /// </summary>
    /// <param name="request">The merge request.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    public Task<TelephonyResult> Merge(MergeRequest request)
        => ExecuteAsync("Merge", () => DescribeMergeRequest(request), (service, token) => service.MergeAsync(request, token));

    /// <summary>
    /// Sends DTMF digits to an active call.
    /// </summary>
    /// <param name="request">The send-digits request.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    public Task<TelephonyResult> SendDigits(SendDigitsRequest request)
        => ExecuteAsync("SendDigits", () => DescribeSendDigitsRequest(request), (service, token) => service.SendDigitsAsync(request, token));

    /// <summary>
    /// Answers a ringing inbound call.
    /// </summary>
    /// <param name="call">A reference to the inbound call to answer.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    public Task<TelephonyResult> Answer(CallReference call)
        => ExecuteAsync("Answer", () => DescribeCallReference(call), (service, token) => service.AnswerAsync(call, token));

    /// <summary>
    /// Rejects a ringing inbound call.
    /// </summary>
    /// <param name="call">A reference to the inbound call to reject.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    public Task<TelephonyResult> Reject(CallReference call)
        => ExecuteAsync("Reject", () => DescribeCallReference(call), (service, token) => service.RejectAsync(call, token));

    /// <summary>
    /// Sends a ringing inbound call to voicemail.
    /// </summary>
    /// <param name="call">A reference to the inbound call to send to voicemail.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    public Task<TelephonyResult> Voicemail(CallReference call)
        => ExecuteAsync("Voicemail", () => DescribeCallReference(call), (service, token) => service.SendToVoicemailAsync(call, token));

    /// <summary>
    /// Issues the bootstrap configuration the soft phone client needs to connect to the provider.
    /// </summary>
    /// <returns>The client credentials, or <see langword="null"/> when no provider is configured.</returns>
    public async Task<TelephonyClientCredentials> GetCredentials()
    {
        TelephonyClientCredentials credentials = null;
        LogHubActionStart("GetCredentials");

        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            if (!await AuthorizeAsync(scope.ServiceProvider))
            {
                LogHubActionUnauthorized("GetCredentials");
                return;
            }

            var service = scope.ServiceProvider.GetRequiredService<ITelephonyService>();
            credentials = await service.GetClientCredentialsAsync(Context.ConnectionAborted);
        });

        _logger.LogInformation(
            "Telephony hub action {Action} completed for user {UserId}. Provider={ProviderName}, HasCredentials={HasCredentials}.",
            "GetCredentials",
            Context.UserIdentifier ?? "(anonymous)",
            credentials?.ProviderName ?? "(none)",
            credentials is not null);

        return credentials;
    }

    /// <summary>
    /// Gets the connection status of the current user with the configured provider, used by the soft
    /// phone to decide whether to show the dialer, the "connect to provider" button, or an unconfigured state.
    /// </summary>
    /// <returns>The connection status.</returns>
    public async Task<TelephonyConnectionStatus> GetConnectionStatus()
    {
        var status = new TelephonyConnectionStatus
        {
            IsAvailable = false,
            RequiresAuthentication = false,
            IsConnected = false,
        };
        LogHubActionStart("GetConnectionStatus");

        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            if (!await AuthorizeAsync(scope.ServiceProvider))
            {
                LogHubActionUnauthorized("GetConnectionStatus");
                return;
            }

            var authenticationService = scope.ServiceProvider.GetService<ITelephonyAuthenticationService>();

            if (authenticationService is not null)
            {
                status = await authenticationService.GetStatusAsync(Context.ConnectionAborted);
            }
        });

        _logger.LogInformation(
            "Telephony hub action {Action} completed for user {UserId}. Provider={ProviderName}, Available={IsAvailable}, RequiresAuthentication={RequiresAuthentication}, Connected={IsConnected}.",
            "GetConnectionStatus",
            Context.UserIdentifier ?? "(anonymous)",
            status.ProviderName ?? "(none)",
            status.IsAvailable,
            status.RequiresAuthentication,
            status.IsConnected);

        return status;
    }

    /// <summary>
    /// Gets the current user's most recent interactions for the history panel.
    /// </summary>
    /// <param name="count">The maximum number of interactions to return.</param>
    /// <returns>The most recent interactions, newest first.</returns>
    public async Task<IReadOnlyList<TelephonyInteraction>> GetInteractions(int count)
    {
        IReadOnlyList<TelephonyInteraction> interactions = [];

        var take = count <= 0 ? 25 : Math.Min(count, 200);
        LogHubActionStart("GetInteractions", () => $"Count={take}");

        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            if (!await AuthorizeAsync(scope.ServiceProvider))
            {
                LogHubActionUnauthorized("GetInteractions");
                return;
            }

            var store = scope.ServiceProvider.GetService<ITelephonyInteractionStore>();
            var userId = Context.UserIdentifier;

            if (store is null || string.IsNullOrEmpty(userId))
            {
                return;
            }

            interactions = await store.GetRecentAsync(userId, take, Context.ConnectionAborted);
        });

        _logger.LogInformation(
            "Telephony hub action {Action} completed for user {UserId}. Requested={RequestedCount}, Returned={ReturnedCount}.",
            "GetInteractions",
            Context.UserIdentifier ?? "(anonymous)",
            take,
            interactions.Count);

        return interactions;
    }

    /// <summary>
    /// Gets the capabilities of the configured provider as a bit flag integer value.
    /// </summary>
    /// <returns>The provider capabilities as an integer.</returns>
    public async Task<int> GetCapabilities()
    {
        var capabilities = TelephonyCapabilities.None;
        LogHubActionStart("GetCapabilities");

        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            if (!await AuthorizeAsync(scope.ServiceProvider))
            {
                LogHubActionUnauthorized("GetCapabilities");
                return;
            }

            var service = scope.ServiceProvider.GetRequiredService<ITelephonyService>();
            capabilities = await service.GetCapabilitiesAsync(Context.ConnectionAborted);
        });

        _logger.LogInformation(
            "Telephony hub action {Action} completed for user {UserId}. Capabilities={Capabilities}.",
            "GetCapabilities",
            Context.UserIdentifier ?? "(anonymous)",
            capabilities);

        return (int)capabilities;
    }

    private async Task<TelephonyResult> ExecuteAsync(
        string actionName,
        Func<string> requestFactory,
        Func<ITelephonyService, CancellationToken, Task<TelephonyResult>> operation)
    {
        TelephonyResult result = null;
        LogHubActionStart(actionName, requestFactory);

        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            if (!await AuthorizeAsync(scope.ServiceProvider))
            {
                LogHubActionUnauthorized(actionName);
                result = TelephonyResult.Failed(S["You are not authorized to use the soft phone."].Value);

                return;
            }

            var service = scope.ServiceProvider.GetRequiredService<ITelephonyService>();

            try
            {
                result = await operation(service, Context.ConnectionAborted);
            }
            catch (Exception ex)
            {
                var request = BuildLogRequest(requestFactory);

                _logger.LogError(
                    ex,
                    "Telephony hub action {Action} failed for user {UserId} on connection {ConnectionId}. Request: {Request}.",
                    actionName,
                    Context.UserIdentifier ?? "(anonymous)",
                    Context.ConnectionId,
                    request ?? "(none)");

                result = TelephonyResult.Failed(S["An error occurred while processing your request."].Value);
            }

            if (result?.Call is not null)
            {
                await RecordInteractionAsync(scope.ServiceProvider, result.Call);
            }
        });

        if (result?.Call is not null)
        {
            await Clients.Caller.CallStateChanged(result.Call);
        }

        var completionRequest = BuildLogRequest(requestFactory);

        _logger.LogInformation(
            "Telephony hub action {Action} completed for user {UserId}. Request: {Request}. Succeeded={Succeeded}, Error={Error}, CallId={CallId}, CallState={CallState}.",
            actionName,
            Context.UserIdentifier ?? "(anonymous)",
            completionRequest,
            result?.Succeeded,
            result?.Error ?? "(none)",
            result?.Call?.CallId ?? "(none)",
            result?.Call?.State.ToString() ?? "(none)");

        return result;
    }

    private void LogHubActionStart(string actionName)
    {
        if (!_logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        _logger.LogInformation(
            "Telephony hub action {Action} started for user {UserId} on connection {ConnectionId}.",
            actionName,
            Context.UserIdentifier ?? "(anonymous)",
            Context.ConnectionId);
    }

    private void LogHubActionStart(string actionName, Func<string> requestFactory)
    {
        if (!_logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        _logger.LogInformation(
            "Telephony hub action {Action} started for user {UserId} on connection {ConnectionId}. Request: {Request}.",
            actionName,
            Context.UserIdentifier ?? "(anonymous)",
            Context.ConnectionId,
            BuildLogRequest(requestFactory));
    }

    private void LogHubActionUnauthorized(string actionName)
    {
        _logger.LogWarning(
            "Telephony hub action {Action} was denied for user {UserId} on connection {ConnectionId}.",
            actionName,
            Context.UserIdentifier ?? "(anonymous)",
            Context.ConnectionId);
    }

    private static string DescribeDialRequest(DialRequest request)
    {
        return request is null
            ? "(null)"
            : $"To={request.To ?? "(none)"}, From={request.From ?? "(none)"}";
    }

    private static string DescribeCallReference(CallReference call)
    {
        if (call is null)
        {
            return "(null)";
        }

        var metadata = call.Metadata is { Count: > 0 }
            ? string.Join(", ", call.Metadata.Select(entry => $"{entry.Key}={entry.Value}"))
            : "(none)";

        return $"CallId={call.CallId ?? "(none)"}, Metadata={metadata}";
    }

    private static string DescribeTransferRequest(TransferRequest request)
    {
        return request is null
            ? "(null)"
            : $"CallId={request.CallId ?? "(none)"}, To={request.To ?? "(none)"}, Mode={request.Mode}";
    }

    private static string DescribeMergeRequest(MergeRequest request)
    {
        return request is null
            ? "(null)"
            : $"PrimaryCallId={request.PrimaryCallId ?? "(none)"}, SecondaryCallId={request.SecondaryCallId ?? "(none)"}";
    }

    private static string DescribeSendDigitsRequest(SendDigitsRequest request)
    {
        return request is null
            ? "(null)"
            : $"CallId={request.CallId ?? "(none)"}, DigitsLength={request.Digits?.Length ?? 0}";
    }

    private static string BuildLogRequest(Func<string> requestFactory)
    {
        return requestFactory?.Invoke() ?? "(none)";
    }

    private async Task RecordInteractionAsync(IServiceProvider services, TelephonyCall call)
    {
        if (call is null || string.IsNullOrEmpty(call.CallId))
        {
            return;
        }

        var store = services.GetService<ITelephonyInteractionStore>();
        var userId = Context.UserIdentifier;

        if (store is null || string.IsNullOrEmpty(userId))
        {
            return;
        }

        var clock = services.GetService<IClock>();
        var now = clock?.UtcNow ?? DateTime.UtcNow;
        var userName = Context.GetHttpContext()?.User?.Identity?.Name;

        var existing = await store.FindByCallIdAsync(userId, call.CallId, Context.ConnectionAborted);

        if (existing is null)
        {
            if (call.State is CallState.Disconnected or CallState.Failed)
            {
                return;
            }

            var interaction = new TelephonyInteraction
            {
                InteractionId = IdGenerator.GenerateId(),
                CallId = call.CallId,
                ProviderName = call.ProviderName,
                UserId = userId,
                UserName = userName,
                From = call.From,
                To = call.To,
                Direction = call.Direction,
                Outcome = CallOutcome.InProgress,
                StartedUtc = call.StartedUtc?.UtcDateTime ?? now,
            };

            await store.CreateAsync(interaction, Context.ConnectionAborted);

            return;
        }

        if (call.State == CallState.Disconnected)
        {
            existing.Outcome = CallOutcome.Completed;
            existing.EndedUtc = now;
            existing.DurationSeconds = Math.Max(0, (now - existing.StartedUtc).TotalSeconds);
        }
        else if (call.State == CallState.Failed)
        {
            existing.Outcome = CallOutcome.Failed;
            existing.EndedUtc = now;
        }
        else
        {
            if (!string.IsNullOrEmpty(call.To))
            {
                existing.To = call.To;
            }

            if (!string.IsNullOrEmpty(call.From))
            {
                existing.From = call.From;
            }
        }

        await store.UpdateAsync(existing, Context.ConnectionAborted);
    }

    private async Task<bool> AuthorizeAsync(IServiceProvider services)
    {
        var httpContext = Context.GetHttpContext();

        if (httpContext?.User is null)
        {
            return false;
        }

        var authorizationService = services.GetRequiredService<IAuthorizationService>();

        return await authorizationService.AuthorizeAsync(httpContext.User, TelephonyPermissions.UseSoftPhone);
    }
}
