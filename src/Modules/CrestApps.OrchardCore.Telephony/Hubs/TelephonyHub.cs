using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.SignalR;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Environment.Shell;
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
    private readonly string _tenantName;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonyHub"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    /// <param name="shellSettings">The current Orchard shell settings.</param>
    public TelephonyHub(
        ILogger<TelephonyHub> logger,
        IStringLocalizer<TelephonyHub> stringLocalizer,
        ShellSettings shellSettings)
    {
        _logger = logger;
        _tenantName = shellSettings.Name;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;

        if (string.IsNullOrEmpty(userId))
        {
            Context.Abort();

            return;
        }

        var authorized = false;

        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            authorized = await AuthorizeAsync(scope.ServiceProvider);
        });

        if (!authorized)
        {
            Context.Abort();

            return;
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            TenantSignalRGroupName.ForUser(_tenantName, userId),
            Context.ConnectionAborted);
        await base.OnConnectedAsync();
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

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Telephony hub action {Action} completed for user {UserId}. Provider={ProviderName}, HasCredentials={HasCredentials}.",
                "GetCredentials",
                RedactedUserId(),
                credentials?.ProviderName ?? "(none)",
                credentials is not null);
        }

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

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Telephony hub action {Action} completed for user {UserId}. Provider={ProviderName}, Available={IsAvailable}, RequiresAuthentication={RequiresAuthentication}, Connected={IsConnected}.",
                "GetConnectionStatus",
                RedactedUserId(),
                status.ProviderName ?? "(none)",
                status.IsAvailable,
                status.RequiresAuthentication,
                status.IsConnected);
        }

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

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Telephony hub action {Action} completed for user {UserId}. Requested={RequestedCount}, Returned={ReturnedCount}.",
                "GetInteractions",
                RedactedUserId(),
                take,
                interactions.Count);
        }

        return interactions;
    }

    /// <summary>
    /// Gets the current user's active call directly from the configured telephony provider.
    /// </summary>
    /// <returns>The provider-authoritative call lookup result.</returns>
    public async Task<TelephonyCallLookupResult> GetActiveCall()
    {
        var result = new TelephonyCallLookupResult
        {
            Succeeded = false,
            Found = false,
            Error = S["Unable to determine the current call state."].Value,
        };
        LogHubActionStart("GetActiveCall");

        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            if (!await AuthorizeAsync(scope.ServiceProvider))
            {
                LogHubActionUnauthorized("GetActiveCall");
                result.Error = S["You are not authorized to use the soft phone."].Value;

                return;
            }

            var userId = Context.UserIdentifier;

            if (string.IsNullOrEmpty(userId))
            {
                return;
            }

            var synchronizationService = scope.ServiceProvider.GetRequiredService<ITelephonyInteractionSynchronizationService>();
            result = await synchronizationService.GetActiveCallAsync(userId, Context.ConnectionAborted);
        });

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Telephony hub action {Action} completed for user {UserId}. Succeeded={Succeeded}, Found={Found}, CallId={CallId}, CallState={CallState}, Error={Error}.",
                "GetActiveCall",
                RedactedUserId(),
                result.Succeeded,
                result.Found,
                OperationalLogRedactor.Redact(result.Call?.CallId, OperationalLogFieldKind.Identifier, OperationalLogIdentifierCategory.Call),
                result.Call?.State.ToString() ?? "(none)",
                OperationalLogRedactor.Redact(result.Error, OperationalLogFieldKind.FreeText));
        }

        return result;
    }

    /// <summary>
    /// Gets all active calls for the current user directly from their configured telephony providers.
    /// </summary>
    /// <returns>The provider-authoritative active call-list result.</returns>
    public async Task<TelephonyCallListLookupResult> GetActiveCalls()
    {
        var result = new TelephonyCallListLookupResult
        {
            Succeeded = false,
            Error = S["Unable to determine the active call state."].Value,
        };
        LogHubActionStart("GetActiveCalls");

        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            if (!await AuthorizeAsync(scope.ServiceProvider))
            {
                LogHubActionUnauthorized("GetActiveCalls");
                result.Error = S["You are not authorized to use the soft phone."].Value;

                return;
            }

            var userId = Context.UserIdentifier;

            if (string.IsNullOrEmpty(userId))
            {
                return;
            }

            var synchronizationService = scope.ServiceProvider.GetRequiredService<ITelephonyInteractionSynchronizationService>();
            result = await synchronizationService.GetActiveCallsAsync(userId, Context.ConnectionAborted);
        });

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Telephony hub action {Action} completed for user {UserId}. Succeeded={Succeeded}, Returned={ReturnedCount}, Error={Error}.",
                "GetActiveCalls",
                RedactedUserId(),
                result.Succeeded,
                result.Calls.Count,
                OperationalLogRedactor.Redact(result.Error, OperationalLogFieldKind.FreeText));
        }

        return result;
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

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Telephony hub action {Action} completed for user {UserId}. Capabilities={Capabilities}.",
                "GetCapabilities",
                RedactedUserId(),
                capabilities);
        }

        return (int)capabilities;
    }

    /// <summary>
    /// Gets transfer destinations from the configured provider directory.
    /// </summary>
    /// <returns>The provider directory lookup result.</returns>
    public async Task<TelephonyDirectoryResult> GetDirectory()
    {
        var result = new TelephonyDirectoryResult
        {
            Succeeded = false,
            Error = S["Unable to load the provider directory."].Value,
        };
        LogHubActionStart("GetDirectory");

        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            if (!await AuthorizeAsync(scope.ServiceProvider))
            {
                LogHubActionUnauthorized("GetDirectory");
                result.Error = S["You are not authorized to use the soft phone."].Value;

                return;
            }

            var service = scope.ServiceProvider.GetRequiredService<ITelephonyService>();
            result = await service.GetDirectoryAsync(Context.ConnectionAborted);
        });

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Telephony hub action {Action} completed for user {UserId}. Succeeded={Succeeded}, Returned={ReturnedCount}, Error={Error}.",
                "GetDirectory",
                RedactedUserId(),
                result.Succeeded,
                result.Entries.Count,
                OperationalLogRedactor.Redact(result.Error, OperationalLogFieldKind.FreeText));
        }

        return result;
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
                    OperationalLogRedactor.RedactException(ex),
                    "Telephony hub action {Action} failed for user {UserId} on connection {ConnectionId}. Request: {Request}.",
                    actionName,
                    RedactedUserId(),
                    OperationalLogRedactor.Pseudonymize(Context.ConnectionId, OperationalLogIdentifierCategory.Session),
                    request ?? "(none)");

                result = TelephonyResult.Failed(S["An error occurred while processing your request."].Value);
            }

            if (result?.Call is not null)
            {
                await RecordInteractionAsync(scope.ServiceProvider, actionName, result.Call);
            }
        });

        if (_logger.IsEnabled(LogLevel.Information))
        {
            var completionRequest = BuildLogRequest(requestFactory);

            _logger.LogInformation(
                "Telephony hub action {Action} completed for user {UserId}. Request: {Request}. Succeeded={Succeeded}, Error={Error}, CallId={CallId}, CallState={CallState}.",
                actionName,
                RedactedUserId(),
                completionRequest,
                result?.Succeeded,
                OperationalLogRedactor.Redact(result?.Error, OperationalLogFieldKind.FreeText),
                OperationalLogRedactor.Redact(result?.Call?.CallId, OperationalLogFieldKind.Identifier, OperationalLogIdentifierCategory.Call),
                result?.Call?.State.ToString() ?? "(none)");
        }

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
            RedactedUserId(),
            OperationalLogRedactor.Pseudonymize(Context.ConnectionId, OperationalLogIdentifierCategory.Session));
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
            RedactedUserId(),
            OperationalLogRedactor.Pseudonymize(Context.ConnectionId, OperationalLogIdentifierCategory.Session),
            BuildLogRequest(requestFactory));
    }

    private void LogHubActionUnauthorized(string actionName)
    {
        _logger.LogWarning(
            "Telephony hub action {Action} was denied for user {UserId} on connection {ConnectionId}.",
            actionName,
            RedactedUserId(),
            OperationalLogRedactor.Pseudonymize(Context.ConnectionId, OperationalLogIdentifierCategory.Session));
    }

    private static string DescribeDialRequest(DialRequest request)
    {
        return request is null
            ? "(null)"
            : $"To={OperationalLogRedactor.Redact(request.To, OperationalLogFieldKind.Address)}, From={OperationalLogRedactor.Redact(request.From, OperationalLogFieldKind.Address)}";
    }

    private static string DescribeCallReference(CallReference call)
    {
        if (call is null)
        {
            return "(null)";
        }

        return $"CallId={OperationalLogRedactor.Redact(call.CallId, OperationalLogFieldKind.Identifier, OperationalLogIdentifierCategory.Call)}, Metadata={OperationalLogRedactor.RedactMetadata(call.Metadata)}";
    }

    private static string DescribeTransferRequest(TransferRequest request)
    {
        return request is null
            ? "(null)"
            : $"CallId={OperationalLogRedactor.Redact(request.CallId, OperationalLogFieldKind.Identifier, OperationalLogIdentifierCategory.Call)}, To={OperationalLogRedactor.Redact(request.To, OperationalLogFieldKind.Address)}, Mode={request.Mode}";
    }

    private static string DescribeMergeRequest(MergeRequest request)
    {
        return request is null
            ? "(null)"
            : $"PrimaryCallId={OperationalLogRedactor.Redact(request.PrimaryCallId, OperationalLogFieldKind.Identifier, OperationalLogIdentifierCategory.Call)}, SecondaryCallId={OperationalLogRedactor.Redact(request.SecondaryCallId, OperationalLogFieldKind.Identifier, OperationalLogIdentifierCategory.Call)}";
    }

    private static string DescribeSendDigitsRequest(SendDigitsRequest request)
    {
        return request is null
            ? "(null)"
            : $"CallId={OperationalLogRedactor.Redact(request.CallId, OperationalLogFieldKind.Identifier, OperationalLogIdentifierCategory.Call)}, DigitsLength={request.Digits?.Length ?? 0}";
    }

    private static string BuildLogRequest(Func<string> requestFactory)
    {
        return requestFactory?.Invoke() ?? "(none)";
    }

    private string RedactedUserId()
    {
        return string.IsNullOrEmpty(Context.UserIdentifier)
            ? "(anonymous)"
            : OperationalLogRedactor.Pseudonymize(Context.UserIdentifier, OperationalLogIdentifierCategory.User);
    }

    private async Task RecordInteractionAsync(IServiceProvider services, string actionName, TelephonyCall call)
    {
        if (!string.Equals(actionName, "Dial", StringComparison.Ordinal) ||
            call is null ||
            string.IsNullOrEmpty(call.CallId))
        {
            return;
        }

        var store = services.GetService<ITelephonyInteractionStore>();
        var userId = Context.UserIdentifier;

        if (store is null || string.IsNullOrEmpty(userId))
        {
            return;
        }

        var clock = services.GetRequiredService<IClock>();
        var now = clock.UtcNow;
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

        if (!string.IsNullOrEmpty(call.To))
        {
            existing.To = call.To;
        }

        if (!string.IsNullOrEmpty(call.From))
        {
            existing.From = call.From;
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
