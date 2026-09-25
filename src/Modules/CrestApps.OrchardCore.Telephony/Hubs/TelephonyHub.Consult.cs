using CrestApps.Core.Support;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.SignalR;

namespace CrestApps.OrchardCore.Telephony.Hubs;

/// <summary>
/// Following and finishing a transfer the provider carries out as a call of its own: a warm transfer's consult, and a
/// blind transfer that rings its destination before the call is handed over.
/// </summary>
/// <remarks>
/// The transfer's own leg rings somebody else -- a colleague, or an outside number -- so it is never in the caller's
/// history. Only the call being transferred is authorized against it; the provider checks that the leg the request
/// names was rung for that call.
/// </remarks>
public sealed partial class TelephonyHub
{
    /// <summary>
    /// Reports where a transfer stands.
    /// </summary>
    /// <param name="request">The call being transferred and its transfer's leg.</param>
    /// <returns>A <see cref="TelephonyResult"/> whose call carries the transfer's state.</returns>
    public Task<TelephonyResult> GetConsult(ConsultTransferRequest request)
        => ExecuteAsync("GetConsult", () => DescribeConsultRequest(request), (service, token) => service.GetConsultAsync(request, token), () => [request?.CallId]);

    /// <summary>
    /// Hands the call to the destination the agent is consulting.
    /// </summary>
    /// <param name="request">The call being transferred and its consult's leg.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    public Task<TelephonyResult> CompleteConsult(ConsultTransferRequest request)
        => ExecuteAsync("CompleteConsult", () => DescribeConsultRequest(request), (service, token) => service.CompleteConsultAsync(request, token), () => [request?.CallId]);

    /// <summary>
    /// Calls a transfer off, leaving the call with the agent.
    /// </summary>
    /// <param name="request">The call being transferred and its transfer's leg.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    public Task<TelephonyResult> CancelConsult(ConsultTransferRequest request)
        => ExecuteAsync("CancelConsult", () => DescribeConsultRequest(request), (service, token) => service.CancelConsultAsync(request, token), () => [request?.CallId]);

    // Who is transferring, stamped here rather than taken from the client: a provider that rings the agent's own phone
    // for a consult, or tells a colleague who handed them the call, must not be told by the client whose phone it is.
    private TransferRequest StampTransferCaller(TransferRequest request)
    {
        if (request is null || string.IsNullOrEmpty(Context.UserIdentifier))
        {
            return request;
        }

        request.Metadata ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        request.Metadata[TelephonyConstants.RequestMetadata.SoftPhoneUserId] = Context.UserIdentifier;
        request.Metadata.Remove(TelephonyConstants.RequestMetadata.SoftPhoneUserDisplayName);

        var displayName = Context.GetHttpContext()?.User?.Identity?.Name;

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            request.Metadata[TelephonyConstants.RequestMetadata.SoftPhoneUserDisplayName] = displayName;
        }

        return request;
    }

    // A dial places a new call on this phone, and so does a warm transfer: its consult is a call of the agent's own,
    // which the agent holds, hangs up and completes from here like any other.
    private static bool PlacesNewCall(string actionName, TelephonyCall call)
        => string.Equals(actionName, "Dial", StringComparison.Ordinal) ||
            string.Equals(actionName, "DialExtension", StringComparison.Ordinal) ||
            (string.Equals(actionName, "Transfer", StringComparison.Ordinal) &&
             call?.Metadata is not null &&
             call.Metadata.TryGetValue(TelephonyConstants.CallMetadata.ConsultOf, out var consultOf) &&
             !string.IsNullOrWhiteSpace(consultOf?.ToString()));

    private static string DescribeConsultRequest(ConsultTransferRequest request)
        => request is null
            ? "(none)"
            : $"CallId={request.CallId.SanitizeLogValue()}, ConsultCallId={request.ConsultCallId.SanitizeLogValue()}";
}
