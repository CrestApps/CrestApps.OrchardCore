using System.Net;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// The outcome of one Telnyx API call.
/// <para>
/// Callers of this client are handling a live call, so a failure is returned rather than thrown: an exception on
/// a refused command would abandon a customer mid-flow, while a result the caller can inspect lets them fail the
/// command deliberately and tell the agent why.
/// </para>
/// </summary>
public class TelnyxApiResult
{
    /// <summary>
    /// Gets a value indicating whether the provider accepted the command.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Gets the status the provider answered with, when it answered at all.
    /// </summary>
    public HttpStatusCode? StatusCode { get; init; }

    /// <summary>
    /// Gets the provider's error body, or the transport failure, when the command did not succeed.
    /// </summary>
    public string ErrorBody { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="statusCode">The status the provider answered with.</param>
    public static TelnyxApiResult Success(HttpStatusCode statusCode)
        => new() { Succeeded = true, StatusCode = statusCode };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="statusCode">The status the provider answered with, when it answered.</param>
    /// <param name="errorBody">The provider's error body, or the transport failure.</param>
    public static TelnyxApiResult Failure(HttpStatusCode? statusCode, string errorBody)
        => new() { Succeeded = false, StatusCode = statusCode, ErrorBody = errorBody };
}

/// <summary>
/// The outcome of a call-originating command, carrying the provider's identifier for the new leg.
/// </summary>
public sealed class TelnyxCallApiResult : TelnyxApiResult
{
    /// <summary>
    /// Gets the provider's call control identifier for the leg the command created or acted on.
    /// </summary>
    public string CallControlId { get; init; }
}

/// <summary>
/// The outcome of a credential command, carrying what the caller needs to register a soft phone.
/// </summary>
public sealed class TelnyxCredentialApiResult : TelnyxApiResult
{
    /// <summary>
    /// Gets the provider's identifier for the credential.
    /// </summary>
    public string CredentialId { get; init; }

    /// <summary>
    /// Gets the SIP username the browser registers with.
    /// </summary>
    public string SipUsername { get; init; }

    /// <summary>
    /// Gets the SIP password the browser registers with.
    /// </summary>
    public string SipPassword { get; init; }
}

/// <summary>
/// What to dial, and as whom.
/// </summary>
public sealed class TelnyxOriginateRequest
{
    /// <summary>
    /// Gets or sets the destination.
    /// </summary>
    public string To { get; set; }

    /// <summary>
    /// Gets or sets the caller identity presented to the destination.
    /// </summary>
    public string From { get; set; }

    /// <summary>
    /// Gets or sets the Telnyx connection the call is placed on.
    /// </summary>
    public string ConnectionId { get; set; }

    /// <summary>
    /// Gets or sets the caller identity asserted to a SIP destination, used when the presented number and the
    /// authenticated identity differ.
    /// </summary>
    public string SipAuthUsername { get; set; }

    /// <summary>
    /// Gets or sets the opaque state Telnyx echoes back on every event for this leg, which is how the platform
    /// correlates a webhook to the work that started it.
    /// </summary>
    public string ClientState { get; set; }

    /// <summary>
    /// Gets or sets how long the destination may ring before the call is abandoned.
    /// </summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// Gets the provider fields this request carries that the client does not model.
    /// </summary>
    /// <remarks>
    /// Telnyx accepts far more on an origination than is worth naming here, and a caller that needs
    /// <c>outbound_voice_profile_id</c> or <c>from_display_name</c> should not have to reach for raw HTTP to
    /// send it — that is precisely how four copies of the transport came to exist. These are merged over the
    /// named fields, so a caller can also override one deliberately.
    /// </remarks>
    public IDictionary<string, object> AdditionalFields { get; } = new Dictionary<string, object>(StringComparer.Ordinal);
}

/// <summary>
/// The outcome of a conference command, carrying the conference the caller needs to join legs to.
/// </summary>
public sealed class TelnyxConferenceApiResult : TelnyxApiResult
{
    /// <summary>
    /// Gets the provider's identifier for the conference.
    /// </summary>
    public string ConferenceId { get; init; }
}

/// <summary>
/// The calls a provider currently has up on a connection.
/// </summary>
public sealed class TelnyxActiveCallListResult : TelnyxApiResult
{
    /// <summary>
    /// Gets the calls on this page. Empty when the listing failed.
    /// </summary>
    public IReadOnlyList<TelnyxActiveCall> Calls { get; init; } = [];

    /// <summary>
    /// Gets the cursor for the next page, or <see langword="null"/> when this is the last one.
    /// </summary>
    public string NextPageToken { get; init; }
}

/// <summary>
/// One call the provider reports as up.
/// </summary>
public sealed class TelnyxActiveCall
{
    /// <summary>
    /// Gets the provider's identifier for the call.
    /// </summary>
    public string CallControlId { get; init; }

    /// <summary>
    /// Gets the opaque state the platform attached when it placed the call, when it placed this one at all. An
    /// orphan with no client state was almost certainly inbound.
    /// </summary>
    public string ClientState { get; init; }

    /// <summary>
    /// Gets how long the call has been up, in seconds. A call that has only just started is far more likely to
    /// be one whose interaction is still being written than one that was lost.
    /// </summary>
    public int DurationSeconds { get; init; }
}
