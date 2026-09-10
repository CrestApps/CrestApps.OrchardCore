using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Tests.Telephony.Doubles;

/// <summary>
/// A telephony provider that also supports OAuth, used to test the authentication service.
/// </summary>
internal sealed class FakeAuthTelephonyProvider :
    ITelephonyProvider,
    ITelephonyCallControlProvider,
    ITelephonyInboundCallProvider,
    ITelephonyHoldProvider,
    ITelephonyMuteProvider,
    ITelephonyTransferProvider,
    ITelephonyAttendedTransferProvider,
    ITelephonyConferenceProvider,
    ITelephonyDtmfProvider,
    ITelephonyVoicemailProvider,
    ITelephonySoftPhoneCredentialsProvider,
    ITelephonyAuthenticationProvider,
    ITelephonyUserConnectionMetadataProvider
{
    public bool RequiresUserAuthentication { get; set; } = true;

    public string AuthenticationScheme { get; set; } = TelephonyConstants.AuthenticationSchemes.OAuth2;

    public bool SupportsProofKeyForCodeExchange { get; set; }

    public TelephonyUserTokens RefreshResult { get; set; }

    private int _refreshCount;

    /// <summary>
    /// Gets the number of times <see cref="RefreshTokensAsync"/> was invoked. Incremented atomically so a
    /// concurrency test can assert an exact count without a data race of its own.
    /// </summary>
    public int RefreshCount => Volatile.Read(ref _refreshCount);

    /// <summary>
    /// Gets or sets an optional gate awaited inside <see cref="RefreshTokensAsync"/>. A test sets this so the
    /// first caller holds the refresh lock while a second caller is provably contending for it, exercising the
    /// serialization path rather than a coincidentally sequential run.
    /// </summary>
    public Task RefreshGate { get; set; }

    /// <summary>
    /// Gets a task that completes the first time <see cref="RefreshTokensAsync"/> starts, letting a test wait
    /// until the lock holder is inside the critical section before releasing the gate.
    /// </summary>
    public Task RefreshStarted => _refreshStarted.Task;

    private readonly TaskCompletionSource _refreshStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TelephonyUserTokens RevokedTokens { get; private set; }

    public TelephonyResult RevokeResult { get; set; } = TelephonyResult.Success();

    public Exception RevokeException { get; set; }

    public TelephonyUserTokens EnrichedTokensResult { get; set; }

    public LocalizedString Name => new("FakeAuth", "FakeAuth");

    public TelephonyCapabilities Capabilities => TelephonyCapabilities.Dial;

    public Task<string> GetAuthorizationUrlAsync(TelephonyAuthorizationContext context, CancellationToken cancellationToken = default)
        => Task.FromResult($"https://provider.test/authorize?state={context.State}");

    public Task<TelephonyUserTokens> ExchangeCodeAsync(TelephonyCodeExchangeContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(new TelephonyUserTokens { AccessToken = "exchanged", RefreshToken = "refresh" });

    public async Task<TelephonyUserTokens> RefreshTokensAsync(TelephonyUserTokens tokens, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _refreshCount);
        _refreshStarted.TrySetResult();

        if (RefreshGate is not null)
        {
            await RefreshGate;
        }

        return RefreshResult;
    }

    public Task<TelephonyResult> RevokeTokensAsync(TelephonyUserTokens tokens, CancellationToken cancellationToken = default)
    {
        RevokedTokens = tokens;

        if (RevokeException is not null)
        {
            throw RevokeException;
        }

        return Task.FromResult(RevokeResult);
    }

    public Task<TelephonyUserTokens> EnrichTokensAsync(TelephonyUserTokens tokens, CancellationToken cancellationToken = default)
        => Task.FromResult(EnrichedTokensResult ?? tokens);

    public Task<TelephonyResult> DialAsync(DialRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(TelephonyResult.Success());

    public Task<TelephonyResult> HangupAsync(CallReference call, CancellationToken cancellationToken = default)
        => Task.FromResult(TelephonyResult.Success());

    public Task<TelephonyResult> HoldAsync(CallReference call, CancellationToken cancellationToken = default)
        => Task.FromResult(TelephonyResult.Success());

    public Task<TelephonyResult> ResumeAsync(CallReference call, CancellationToken cancellationToken = default)
        => Task.FromResult(TelephonyResult.Success());

    public Task<TelephonyResult> MuteAsync(CallReference call, CancellationToken cancellationToken = default)
        => Task.FromResult(TelephonyResult.Success());

    public Task<TelephonyResult> UnmuteAsync(CallReference call, CancellationToken cancellationToken = default)
        => Task.FromResult(TelephonyResult.Success());

    public Task<TelephonyResult> StartAttendedTransferAsync(TransferRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(TelephonyResult.Success());

    public Task<TelephonyResult> TransferAsync(TransferRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(TelephonyResult.Success());

    public Task<TelephonyResult> MergeAsync(MergeRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(TelephonyResult.Success());

    public Task<TelephonyResult> SendDigitsAsync(SendDigitsRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(TelephonyResult.Success());

    public Task<TelephonyResult> AnswerAsync(CallReference call, CancellationToken cancellationToken = default)
        => Task.FromResult(TelephonyResult.Success());

    public Task<TelephonyResult> RejectAsync(CallReference call, CancellationToken cancellationToken = default)
        => Task.FromResult(TelephonyResult.Success());

    public Task<TelephonyResult> SendToVoicemailAsync(CallReference call, CancellationToken cancellationToken = default)
        => Task.FromResult(TelephonyResult.Success());

    public Task<TelephonyClientCredentials> GetClientCredentialsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new TelephonyClientCredentials { ProviderName = "FakeAuth" });
}
