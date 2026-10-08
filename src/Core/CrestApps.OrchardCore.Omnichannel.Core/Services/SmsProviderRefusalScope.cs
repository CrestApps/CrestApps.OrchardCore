namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Carries a provider's machine-readable reason for refusing an SMS back to the code that sent it.
/// </summary>
/// <remarks>
/// Some providers report a refused message only as "not sent" and discard the response that says why, while the
/// reason decides what the sender should do next: a recipient who opted out with the carrier must be recorded as
/// opted out, not retried. The provider-specific code that can read the response (an HTTP handler on the
/// provider's client, for example) reports the reason as one of <see cref="OmnichannelConstants.SmsErrorCodes"/>
/// to the scope the sender opened around the send.
/// </remarks>
public sealed class SmsProviderRefusalScope : IDisposable
{
    private static readonly AsyncLocal<SmsProviderRefusalScope> _current = new();

    private readonly SmsProviderRefusalScope _parent;

    private SmsProviderRefusalScope(SmsProviderRefusalScope parent)
    {
        _parent = parent;
    }

    /// <summary>
    /// Gets the reason the provider gave for refusing the send, as one of
    /// <see cref="OmnichannelConstants.SmsErrorCodes"/>, or <see langword="null"/> when it gave none.
    /// </summary>
    public string ErrorCode { get; private set; }

    /// <summary>
    /// Opens a scope around one send. Dispose it once the send has returned.
    /// </summary>
    /// <returns>The scope.</returns>
    public static SmsProviderRefusalScope Begin()
    {
        var scope = new SmsProviderRefusalScope(_current.Value);

        _current.Value = scope;

        return scope;
    }

    /// <summary>
    /// Reports why the provider refused the send in progress. Does nothing when no scope is open.
    /// </summary>
    /// <param name="errorCode">The reason, as one of <see cref="OmnichannelConstants.SmsErrorCodes"/>.</param>
    public static void Report(string errorCode)
    {
        if (_current.Value is { } scope)
        {
            scope.ErrorCode = errorCode;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
        => _current.Value = _parent;
}
