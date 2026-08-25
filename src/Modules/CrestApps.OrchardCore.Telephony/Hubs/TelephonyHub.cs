using CrestApps.Core.Support;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.SignalR.Core;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Compliance.Redaction;
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
    private readonly Redactor _addressRedactor;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonyHub"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    /// <param name="shellSettings">The current Orchard shell settings.</param>
    /// <param name="redactorProvider">The redactor provider used to redact sensitive values before logging.</param>
    public TelephonyHub(
        ILogger<TelephonyHub> logger,
        IStringLocalizer<TelephonyHub> stringLocalizer,
        ShellSettings shellSettings,
        IRedactorProvider redactorProvider)
    {
        _logger = logger;
        _tenantName = shellSettings.Name;
        _addressRedactor = redactorProvider.GetRedactor(LogDataClassifications.AddressSet);
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
            HubConnectionWork.MustComplete);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Places an outbound call.
    /// </summary>
    /// <param name="request">The dial request.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    public Task<TelephonyResult> Dial(DialRequest request)
    {
        if (request is not null && !string.IsNullOrEmpty(Context.UserIdentifier))
        {
            // Stamp the caller's identity so a provider that delivers audio to a per-user browser endpoint
            // (Telnyx WebRTC) can resolve this agent's live soft-phone registration and bridge the outbound
            // call to their browser. Providers without browser audio ignore the key.
            request.Metadata ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            request.Metadata[TelephonyConstants.RequestMetadata.SoftPhoneUserId] = Context.UserIdentifier;
        }

        return ExecuteAsync("Dial", () => DescribeDialRequest(request), (service, token) => service.DialAsync(request, token));
    }

    /// <summary>
    /// Ends an active call.
    /// </summary>
    /// <param name="call">A reference to the call to end.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    public Task<TelephonyResult> Hangup(CallReference call)
        => ExecuteAsync("Hangup", () => DescribeCallReference(call), (service, token) => service.HangupAsync(call, token), () => GetCallIds(call));

    /// <summary>
    /// Places an active call on hold.
    /// </summary>
    /// <param name="call">A reference to the call to place on hold.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    public Task<TelephonyResult> Hold(CallReference call)
        => ExecuteAsync("Hold", () => DescribeCallReference(call), (service, token) => service.HoldAsync(call, token), () => GetCallIds(call));

    /// <summary>
    /// Resumes a call that is on hold.
    /// </summary>
    /// <param name="call">A reference to the call to resume.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    public Task<TelephonyResult> Resume(CallReference call)
        => ExecuteAsync("Resume", () => DescribeCallReference(call), (service, token) => service.ResumeAsync(call, token), () => GetCallIds(call));

    /// <summary>
    /// Mutes the local audio of an active call.
    /// </summary>
    /// <param name="call">A reference to the call to mute.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    public Task<TelephonyResult> Mute(CallReference call)
        => ExecuteAsync("Mute", () => DescribeCallReference(call), (service, token) => service.MuteAsync(call, token), () => GetCallIds(call));

    /// <summary>
    /// Unmutes the local audio of an active call.
    /// </summary>
    /// <param name="call">A reference to the call to unmute.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    public Task<TelephonyResult> Unmute(CallReference call)
        => ExecuteAsync("Unmute", () => DescribeCallReference(call), (service, token) => service.UnmuteAsync(call, token), () => GetCallIds(call));

    /// <summary>
    /// Transfers an active call to another destination.
    /// </summary>
    /// <param name="request">The transfer request.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    public Task<TelephonyResult> Transfer(TransferRequest request)
        => ExecuteAsync("Transfer", () => DescribeTransferRequest(request), (service, token) => service.TransferAsync(request, token), () => GetCallIds(request));

    /// <summary>
    /// Merges two active calls into a conference.
    /// </summary>
    /// <param name="request">The merge request.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    public Task<TelephonyResult> Merge(MergeRequest request)
        => ExecuteAsync("Merge", () => DescribeMergeRequest(request), (service, token) => service.MergeAsync(request, token), () => GetCallIds(request));

    /// <summary>
    /// Sends DTMF digits to an active call.
    /// </summary>
    /// <param name="request">The send-digits request.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    public Task<TelephonyResult> SendDigits(SendDigitsRequest request)
        => ExecuteAsync("SendDigits", () => DescribeSendDigitsRequest(request), (service, token) => service.SendDigitsAsync(request, token), () => GetCallIds(request));

    /// <summary>
    /// Answers a ringing inbound call.
    /// </summary>
    /// <param name="call">A reference to the inbound call to answer.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    public Task<TelephonyResult> Answer(CallReference call)
        => ExecuteAsync("Answer", () => DescribeCallReference(call), (service, token) => service.AnswerAsync(call, token), () => GetCallIds(call));

    /// <summary>
    /// Rejects a ringing inbound call.
    /// </summary>
    /// <param name="call">A reference to the inbound call to reject.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    public Task<TelephonyResult> Reject(CallReference call)
        => ExecuteAsync("Reject", () => DescribeCallReference(call), (service, token) => service.RejectAsync(call, token), () => GetCallIds(call));

    /// <summary>
    /// Sends a ringing inbound call to voicemail.
    /// </summary>
    /// <param name="call">A reference to the inbound call to send to voicemail.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    public Task<TelephonyResult> Voicemail(CallReference call)
        => ExecuteAsync("Voicemail", () => DescribeCallReference(call), (service, token) => service.SendToVoicemailAsync(call, token), () => GetCallIds(call));

    /// <summary>
    /// Places a call to an internal extension.
    /// </summary>
    /// <param name="request">The extension dial request.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    public Task<TelephonyResult> DialExtension(ExtensionDialRequest request)
    {
        if (request is not null && !string.IsNullOrEmpty(Context.UserIdentifier))
        {
            // Stamp the caller's identity so a provider that delivers audio to a per-user browser endpoint can
            // resolve this agent's live soft-phone registration and bridge the internal call to their browser.
            request.CallerUserId = Context.UserIdentifier;
            // Carry the caller's display name so the target's ringing prompt can show who is calling instead of
            // only the internal caller-id number.
            request.CallerDisplayName = Context.GetHttpContext()?.User?.Identity?.Name;
            request.Metadata ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            request.Metadata[TelephonyConstants.RequestMetadata.SoftPhoneUserId] = Context.UserIdentifier;
        }

        return ExecuteAsync("DialExtension", () => DescribeExtensionDialRequest(request), (service, token) => service.DialExtensionAsync(request, token));
    }

    /// <summary>
    /// Adds an internal extension into an active call as a conference participant.
    /// </summary>
    /// <param name="request">The extension conference request.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    public Task<TelephonyResult> AddExtensionToConference(ExtensionConferenceRequest request)
    {
        if (request is not null && !string.IsNullOrEmpty(Context.UserIdentifier))
        {
            request.CallerUserId = Context.UserIdentifier;
            request.Metadata ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            request.Metadata[TelephonyConstants.RequestMetadata.SoftPhoneUserId] = Context.UserIdentifier;
        }

        return ExecuteAsync(
            "AddExtensionToConference",
            () => DescribeExtensionConferenceRequest(request),
            (service, token) => service.AddExtensionToConferenceAsync(request, token),
            () => GetCallIds(request?.ActiveCall));
    }

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
                // Reads the status but refreshes the provider's OAuth tokens on the way when they are near expiry,
                // and refresh-token rotation means the old token is already spent at the identity provider. Losing
                // the replacement to a cancelled connection locks the agent out until they authenticate again.
                status = await authenticationService.GetStatusAsync(HubConnectionWork.MustComplete);
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
    /// Gets the number of the current user's unread voicemails, for the soft phone's voicemail badge.
    /// </summary>
    /// <returns>The unread voicemail count.</returns>
    public async Task<int> GetUnreadVoicemailCount()
    {
        var count = 0;

        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            if (!await AuthorizeAsync(scope.ServiceProvider))
            {
                LogHubActionUnauthorized("GetUnreadVoicemailCount");
                return;
            }

            var store = scope.ServiceProvider.GetService<ITelephonyInteractionStore>();
            var userId = Context.UserIdentifier;

            if (store is null || string.IsNullOrEmpty(userId))
            {
                return;
            }

            count = await store.GetUnreadVoicemailCountAsync(userId, Context.ConnectionAborted);
        });

        return count;
    }

    /// <summary>
    /// Marks the voicemail identified by its provider call id as read for the current user.
    /// </summary>
    /// <param name="callId">The provider call id of the voicemail.</param>
    /// <returns>The remaining unread voicemail count after the mark.</returns>
    public async Task<int> MarkVoicemailRead(string callId)
    {
        var count = 0;

        if (string.IsNullOrEmpty(callId))
        {
            return count;
        }

        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            if (!await AuthorizeAsync(scope.ServiceProvider))
            {
                LogHubActionUnauthorized("MarkVoicemailRead");
                return;
            }

            var store = scope.ServiceProvider.GetService<ITelephonyInteractionStore>();
            var userId = Context.UserIdentifier;

            if (store is null || string.IsNullOrEmpty(userId))
            {
                return;
            }

            await store.MarkVoicemailReadAsync(userId, callId, DateTime.UtcNow, Context.ConnectionAborted);
            count = await store.GetUnreadVoicemailCountAsync(userId, Context.ConnectionAborted);
        });

        return count;
    }

    /// <summary>
    /// Marks all of the current user's voicemails as read (for example when they open the voicemail tab).
    /// </summary>
    /// <returns>The remaining unread voicemail count, which is zero on success.</returns>
    public async Task<int> MarkAllVoicemailsRead()
    {
        var count = 0;

        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            if (!await AuthorizeAsync(scope.ServiceProvider))
            {
                LogHubActionUnauthorized("MarkAllVoicemailsRead");
                return;
            }

            var store = scope.ServiceProvider.GetService<ITelephonyInteractionStore>();
            var userId = Context.UserIdentifier;

            if (store is null || string.IsNullOrEmpty(userId))
            {
                return;
            }

            await store.MarkAllVoicemailsReadAsync(userId, DateTime.UtcNow, Context.ConnectionAborted);
            count = await store.GetUnreadVoicemailCountAsync(userId, Context.ConnectionAborted);
        });

        return count;
    }

    /// <summary>
    /// Revokes a single browser credential the current user's soft phone just superseded during a renewal,
    /// so a renewed session does not leave its predecessor credential live until natural expiry (which would
    /// otherwise accumulate and, once the per-user cap is reached, evict a credential a live tab still uses).
    /// The revoke is scoped to the caller's own credentials, so a user can only revoke a credential they own.
    /// </summary>
    /// <param name="credentialId">The provider credential identifier to revoke.</param>
    public async Task RevokeSupersededCredential(string credentialId)
    {
        if (string.IsNullOrEmpty(credentialId))
        {
            return;
        }

        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            if (!await AuthorizeAsync(scope.ServiceProvider))
            {
                LogHubActionUnauthorized("RevokeSupersededCredential");
                return;
            }

            var userId = Context.UserIdentifier;

            if (string.IsNullOrEmpty(userId))
            {
                return;
            }

            // Offer the id to every registered revoker; each only revokes a credential it actually owns for
            // this user, so a provider that does not own it simply returns without doing anything.
            foreach (var revoker in scope.ServiceProvider.GetServices<ISoftPhoneCredentialRevoker>())
            {
                await revoker.RevokeCredentialAsync(userId, credentialId, "superseded", Context.ConnectionAborted);
            }
        });
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
            // This reads like a lookup but reconciles against the provider: it commits provider state back to each
            // interaction in its own session and deletes orphans, one commit per interaction. Abandoning it part-way
            // leaves some interactions reconciled and the rest not, and the caller is typically a client that has
            // just reconnected, so the connection token is exactly the wrong token to hand it.
            result = await synchronizationService.GetActiveCallAsync(userId, HubConnectionWork.MustComplete);
        });

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Telephony hub action {Action} completed for user {UserId}. Succeeded={Succeeded}, Found={Found}, CallId={CallId}, CallState={CallState}, Error={Error}.",
                "GetActiveCall",
                RedactedUserId(),
                result.Succeeded,
                result.Found,
                result.Call?.CallId.SanitizeLogValue(),
                result.Call?.State.ToString() ?? "(none)",
                result.Error.SanitizeLogValue());
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
            // This reads like a lookup but reconciles against the provider: it commits provider state back to each
            // interaction in its own session and deletes orphans, one commit per interaction. Abandoning it part-way
            // leaves some interactions reconciled and the rest not, and the caller is typically a client that has
            // just reconnected, so the connection token is exactly the wrong token to hand it.
            result = await synchronizationService.GetActiveCallsAsync(userId, HubConnectionWork.MustComplete);
        });

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Telephony hub action {Action} completed for user {UserId}. Succeeded={Succeeded}, Returned={ReturnedCount}, Error={Error}.",
                "GetActiveCalls",
                RedactedUserId(),
                result.Succeeded,
                result.Calls.Count,
                result.Error.SanitizeLogValue());
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
                result.Error.SanitizeLogValue());
        }

        return result;
    }

    private async Task<TelephonyResult> ExecuteAsync(
        string actionName,
        Func<string> requestFactory,
        Func<ITelephonyService, CancellationToken, Task<TelephonyResult>> operation,
        Func<IEnumerable<string>> callIdsFactory = null)
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

            if (!await AuthorizeReferencedCallsAsync(
                scope.ServiceProvider,
                actionName,
                callIdsFactory?.Invoke(),
                CancellationToken.None))
            {
                result = TelephonyResult.Failed(S["The requested call is not available."].Value);

                return;
            }

            var service = scope.ServiceProvider.GetRequiredService<ITelephonyService>();
            var commandExecutor = scope.ServiceProvider.GetRequiredService<ITelephonyCommandExecutor>();

            try
            {
                result = await commandExecutor.ExecuteAsync(
                    commandCancellationToken => operation(service, commandCancellationToken));

                if (result?.Call is not null)
                {
                    try
                    {
                        await RecordInteractionAsync(
                            scope.ServiceProvider,
                            actionName,
                            result.Call,
                            CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Telephony provider action {Action} completed for user {UserId}, but local interaction persistence failed.",
                            actionName,
                            RedactedUserId());

                        result = TelephonyResult.Unknown(
                            S["The telephony provider completed the operation, but the local interaction history could not be confirmed."].Value);
                    }
                }
            }
            catch (TimeoutException)
            {
                result = TelephonyResult.Unknown(
                    S["The telephony provider did not complete the operation within the server timeout."].Value);
            }
            catch (OperationCanceledException)
            {
                result = TelephonyResult.Unknown(
                    S["The telephony operation was interrupted before the provider outcome could be confirmed."].Value);
            }
            catch (Exception ex)
            {
                var request = BuildLogRequest(requestFactory);

                _logger.LogError(
                    ex,
                    "Telephony hub action {Action} failed for user {UserId} on connection {ConnectionId}. Request: {Request}.",
                    actionName,
                    RedactedUserId(),
                    Context.ConnectionId.SanitizeLogValue(),
                    request ?? "(none)");

                result = TelephonyResult.Failed(S["An error occurred while processing your request."].Value);
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
                result?.Error.SanitizeLogValue(),
                result?.Call?.CallId.SanitizeLogValue(),
                result?.Call?.State.ToString() ?? "(none)");
        }

        return result;
    }

    private async Task<bool> AuthorizeReferencedCallsAsync(
        IServiceProvider services,
        string actionName,
        IEnumerable<string> callIds,
        CancellationToken cancellationToken)
    {
        // A null collection indicates an action that does not operate on an existing call (for
        // example, Dial), so there is nothing to authorize. Every other action must reference at
        // least one call owned by the caller; a missing store, an unidentified caller, a blank
        // identifier, an empty set, or an unmatched call all fail closed.
        if (callIds is null)
        {
            return true;
        }

        var store = services.GetService<ITelephonyInteractionStore>();
        var userId = Context.UserIdentifier;

        if (store is null || string.IsNullOrWhiteSpace(userId))
        {
            LogHubActionCallUnavailable(actionName);

            return false;
        }

        var authorizedCallCount = 0;

        foreach (var callId in callIds)
        {
            if (string.IsNullOrWhiteSpace(callId))
            {
                LogHubActionCallUnavailable(actionName);

                return false;
            }

            var interaction = await store.FindByCallIdAsync(userId, callId, cancellationToken);

            if (interaction is null)
            {
                LogHubActionCallUnavailable(actionName);

                return false;
            }

            authorizedCallCount++;
        }

        if (authorizedCallCount == 0)
        {
            LogHubActionCallUnavailable(actionName);

            return false;
        }

        return true;
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
            Context.ConnectionId.SanitizeLogValue());
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
            Context.ConnectionId.SanitizeLogValue(),
            BuildLogRequest(requestFactory));
    }

    private void LogHubActionUnauthorized(string actionName)
    {
        _logger.LogWarning(
            "Telephony hub action {Action} was denied for user {UserId} on connection {ConnectionId}.",
            actionName,
            RedactedUserId(),
            Context.ConnectionId.SanitizeLogValue());
    }

    private void LogHubActionCallUnavailable(string actionName)
    {
        _logger.LogWarning(
            "Telephony hub action {Action} referenced a call that is not available to user {UserId} on connection {ConnectionId}.",
            actionName,
            RedactedUserId(),
            Context.ConnectionId.SanitizeLogValue());
    }

    private static IEnumerable<string> GetCallIds(CallReference call)
    {
        if (call is null)
        {
            return [];
        }

        return [call.CallId];
    }

    private static IEnumerable<string> GetCallIds(TransferRequest request)
    {
        if (request is null)
        {
            return [];
        }

        return [request.CallId];
    }

    private static IEnumerable<string> GetCallIds(MergeRequest request)
    {
        if (request is null)
        {
            return [];
        }

        return request.GetCallIds();
    }

    private static IEnumerable<string> GetCallIds(SendDigitsRequest request)
    {
        if (request is null)
        {
            return [];
        }

        return [request.CallId];
    }

    private string DescribeDialRequest(DialRequest request)
    {
        return request is null
            ? "(null)"
            : $"To={_addressRedactor.Redact(request.To)}, From={_addressRedactor.Redact(request.From)}";
    }

    private static string DescribeExtensionDialRequest(ExtensionDialRequest request)
    {
        return request is null
            ? "(null)"
            : $"Extension={request.Extension.SanitizeLogValue()}, TargetUserId={request.TargetUserId.SanitizeLogValue()}";
    }

    private static string DescribeExtensionConferenceRequest(ExtensionConferenceRequest request)
    {
        return request is null
            ? "(null)"
            : $"Extension={request.Extension.SanitizeLogValue()}, TargetUserId={request.TargetUserId.SanitizeLogValue()}, ActiveCallId={request.ActiveCall?.CallId.SanitizeLogValue()}";
    }

    private string DescribeCallReference(CallReference call)
    {
        if (call is null)
        {
            return "(null)";
        }

        return $"CallId={call.CallId.SanitizeLogValue()}, Metadata={DescribeMetadata(call.Metadata)}";
    }

    private string DescribeMetadata(IDictionary<string, object> metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return "(none)";
        }

        return string.Join(", ", metadata.Select(entry => $"{entry.Key.SanitizeLogValue()}={_addressRedactor.Redact(entry.Value?.ToString())}"));
    }

    private string DescribeTransferRequest(TransferRequest request)
    {
        return request is null
            ? "(null)"
            : $"CallId={request.CallId.SanitizeLogValue()}, To={_addressRedactor.Redact(request.To)}, Mode={request.Mode}";
    }

    private static string DescribeMergeRequest(MergeRequest request)
    {
        return request is null
            ? "(null)"
            : $"CallIds={string.Join(',', request.GetCallIds()).SanitizeLogValue()}";
    }

    private static string DescribeSendDigitsRequest(SendDigitsRequest request)
    {
        return request is null
            ? "(null)"
            : $"CallId={request.CallId.SanitizeLogValue()}, DigitsLength={request.Digits?.Length ?? 0}";
    }

    private static string BuildLogRequest(Func<string> requestFactory)
    {
        return requestFactory?.Invoke() ?? "(none)";
    }

    private string RedactedUserId()
    {
        return string.IsNullOrEmpty(Context.UserIdentifier)
            ? "(anonymous)"
            : Context.UserIdentifier.SanitizeLogValue();
    }

    /// <summary>
    /// Records a browser-originated outbound call in the caller's history. A client-originated provider (such as
    /// Telnyx) places the call directly from the browser, so it never passes through a server "Dial" action; the
    /// client reports it here so it still appears at the top of the Recent tab like any other outbound call.
    /// </summary>
    /// <param name="callId">The client call identifier.</param>
    /// <param name="to">The dialed destination (a number, or an extension's display name).</param>
    /// <param name="from">The presented caller id.</param>
    public async Task RecordBrowserCall(string callId, string to, string from)
    {
        if (string.IsNullOrEmpty(callId))
        {
            return;
        }

        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            if (!await AuthorizeAsync(scope.ServiceProvider))
            {
                LogHubActionUnauthorized("RecordBrowserCall");
                return;
            }

            var call = new TelephonyCall
            {
                CallId = callId,
                To = to,
                From = from,
                Direction = CallDirection.Outbound,
                State = CallState.Connecting,
            };

            await RecordInteractionAsync(scope.ServiceProvider, "Dial", call, Context.ConnectionAborted);
        });
    }

    /// <summary>
    /// Marks a browser-originated call ended in the caller's history. A client-originated provider (such as
    /// Telnyx) drives the call entirely in the browser, so the server never sees it end; without this the
    /// interaction stays "in progress" forever, which both keeps it out of completed history and leaves it to be
    /// reconciled away as an orphan (losing the entry). Reporting the end here settles it to a final outcome.
    /// </summary>
    /// <param name="callId">The client call identifier reported to <see cref="RecordBrowserCall"/>.</param>
    /// <param name="connected">Whether the call reached a connected state before it ended.</param>
    public async Task RecordBrowserCallEnded(string callId, bool connected)
    {
        if (string.IsNullOrEmpty(callId))
        {
            return;
        }

        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            if (!await AuthorizeAsync(scope.ServiceProvider))
            {
                LogHubActionUnauthorized("RecordBrowserCallEnded");
                return;
            }

            var store = scope.ServiceProvider.GetService<ITelephonyInteractionStore>();
            var userId = Context.UserIdentifier;

            if (store is null || string.IsNullOrEmpty(userId))
            {
                return;
            }

            var interaction = await store.FindByCallIdAsync(userId, callId, Context.ConnectionAborted);

            // Only settle an interaction still in progress; a real-time or reconciliation update may have already
            // given it a final outcome, which must not be overwritten.
            if (interaction is null || interaction.Outcome != CallOutcome.InProgress)
            {
                return;
            }

            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var endedUtc = clock.UtcNow;

            interaction.Outcome = connected ? CallOutcome.Completed : CallOutcome.Canceled;
            interaction.EndedUtc = endedUtc;
            interaction.DurationSeconds = Math.Max(0, (endedUtc - interaction.StartedUtc).TotalSeconds);

            await store.UpdateAsync(interaction, Context.ConnectionAborted);
        });
    }

    /// <summary>
    /// Receives a client-side diagnostic (an error, warning, or notable event surfaced in the browser) so
    /// failures that would otherwise only appear in the agent's console become alertable server-side. The client
    /// throttles and de-duplicates these before sending; the server maps the reported level to a log level and
    /// sanitizes the free-text fields against log injection.
    /// </summary>
    /// <param name="level">The severity the client reported (<c>error</c>, <c>warning</c>, or <c>info</c>).</param>
    /// <param name="code">A short, stable code identifying the kind of diagnostic (for example
    /// <c>mic-permission-denied</c>).</param>
    /// <param name="message">The human-readable message.</param>
    /// <param name="context">Optional extra context (for example the failing operation).</param>
    public async Task ReportClientDiagnostic(string level, string code, string message, string context)
    {
        if (string.IsNullOrEmpty(code) && string.IsNullOrEmpty(message))
        {
            return;
        }

        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            if (!await AuthorizeAsync(scope.ServiceProvider))
            {
                LogHubActionUnauthorized("ReportClientDiagnostic");
                return;
            }

            var logLevel = ResolveDiagnosticLogLevel(level);

            if (!_logger.IsEnabled(logLevel))
            {
                return;
            }

            _logger.Log(
                logLevel,
                "Telephony client diagnostic from user {UserId} on connection {ConnectionId}. Code={Code}, Message={Message}, Context={Context}.",
                RedactedUserId(),
                Context.ConnectionId.SanitizeLogValue(),
                code.SanitizeLogValue(),
                message.SanitizeLogValue(),
                context.SanitizeLogValue());
        });
    }

    private static LogLevel ResolveDiagnosticLogLevel(string level)
    {
        if (string.Equals(level, "error", StringComparison.OrdinalIgnoreCase))
        {
            return LogLevel.Error;
        }

        if (string.Equals(level, "info", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(level, "information", StringComparison.OrdinalIgnoreCase))
        {
            return LogLevel.Information;
        }

        // Default: treat anything else (including the common "warning") as a warning so a client problem is
        // visible without being escalated to an error alert.
        return LogLevel.Warning;
    }

    /// <summary>
    /// Receives a browser-measured media-quality sample (or an end-of-call summary) for the current user's
    /// call and logs it structured for observability and alerting. The server rates the report independently of
    /// the browser's own poor flag so alerting does not depend on a client-supplied value, and chooses the log
    /// severity from that rating so a poor connection surfaces as a warning without every periodic sample
    /// flooding the log.
    /// </summary>
    /// <param name="report">The measured media-quality report.</param>
    public async Task ReportCallQuality(CallQualityReport report)
    {
        if (report is null)
        {
            return;
        }

        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            if (!await AuthorizeAsync(scope.ServiceProvider))
            {
                LogHubActionUnauthorized("ReportCallQuality");
                return;
            }

            var rating = TelephonyCallQualityEvaluator.Evaluate(report);

            if (rating == CallQualityRating.Poor)
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning(
                        "Telephony call quality {Rating} for user {UserId}. CallId={CallId}, Mos={Mos:F2}, Loss={Loss:F1}%, Jitter={Jitter:F0}ms, Rtt={Rtt:F0}ms, BytesReceived={Bytes}, Codec={Codec}, Ice={LocalIce}/{RemoteIce}, Final={Final}.",
                        rating,
                        RedactedUserId(),
                        report.CallId.SanitizeLogValue(),
                        report.Mos,
                        report.LossPercent,
                        report.JitterMs,
                        report.RoundTripTimeMs,
                        report.BytesReceived,
                        report.Codec.SanitizeLogValue(),
                        report.LocalCandidateType.SanitizeLogValue(),
                        report.RemoteCandidateType.SanitizeLogValue(),
                        report.Final);
                }

                return;
            }

            if (report.Final)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(
                        "Telephony call quality summary ({Rating}) for user {UserId}. CallId={CallId}, AvgMos={AvgMos:F2}, MinMos={MinMos:F2}, MaxLoss={MaxLoss:F1}%, Samples={Samples}, DurationMs={Duration}, Codec={Codec}, Ice={LocalIce}/{RemoteIce}.",
                        rating,
                        RedactedUserId(),
                        report.CallId.SanitizeLogValue(),
                        report.AvgMos,
                        report.MinMos,
                        report.MaxLossPercent,
                        report.SampleCount,
                        report.DurationMs,
                        report.Codec.SanitizeLogValue(),
                        report.LocalCandidateType.SanitizeLogValue(),
                        report.RemoteCandidateType.SanitizeLogValue());
                }

                return;
            }

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Telephony call quality sample ({Rating}) for user {UserId}. CallId={CallId}, Mos={Mos:F2}, Loss={Loss:F1}%, Jitter={Jitter:F0}ms, Rtt={Rtt:F0}ms, BytesReceived={Bytes}, Codec={Codec}.",
                    rating,
                    RedactedUserId(),
                    report.CallId.SanitizeLogValue(),
                    report.Mos,
                    report.LossPercent,
                    report.JitterMs,
                    report.RoundTripTimeMs,
                    report.BytesReceived,
                    report.Codec.SanitizeLogValue());
            }
        });
    }

    private async Task RecordInteractionAsync(
        IServiceProvider services,
        string actionName,
        TelephonyCall call,
        CancellationToken cancellationToken)
    {
        // Record outbound-producing actions in call history. Both a server-placed "Dial" and an internal
        // "DialExtension" (and a browser-originated call the client reports through RecordBrowserCall, which
        // also routes here as "Dial") return a Call that should appear in the Recent tab.
        if ((!string.Equals(actionName, "Dial", StringComparison.Ordinal) &&
             !string.Equals(actionName, "DialExtension", StringComparison.Ordinal)) ||
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

        var existing = await store.FindByCallIdAsync(userId, call.CallId, cancellationToken);

        if (existing is null)
        {
            if (call.State is CallState.Disconnected or CallState.Failed)
            {
                return;
            }

            var extensionNumber = GetCallMetadataString(call, TelephonyConstants.CallMetadata.ExtensionNumber);

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
                IsExtension = !string.IsNullOrEmpty(extensionNumber),
                ExtensionNumber = extensionNumber,
                Outcome = CallOutcome.InProgress,
                StartedUtc = call.StartedUtc?.UtcDateTime ?? now,
            };

            await store.CreateAsync(interaction, cancellationToken);

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

        await store.UpdateAsync(existing, cancellationToken);
    }

    private static string GetCallMetadataString(TelephonyCall call, string key)
    {
        if (call?.Metadata is not null &&
            call.Metadata.TryGetValue(key, out var value) &&
            value is not null)
        {
            var text = value.ToString();

            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        return null;
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
