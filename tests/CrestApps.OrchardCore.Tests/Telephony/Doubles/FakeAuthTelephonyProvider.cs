using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Tests.Telephony.Doubles;

/// <summary>
/// A telephony provider that also supports OAuth, used to test the authentication service.
/// </summary>
internal sealed class FakeAuthTelephonyProvider : ITelephonyProvider, ITelephonyAuthenticationProvider
{
    public bool RequiresUserAuthentication { get; set; } = true;

    public string AuthenticationScheme { get; set; } = TelephonyAuthenticationSchemes.OAuth2;

    public bool SupportsProofKeyForCodeExchange { get; set; }

    public TelephonyUserTokens RefreshResult { get; set; }

    public TelephonyUserTokens RevokedTokens { get; private set; }

    public LocalizedString Name => new("FakeAuth", "FakeAuth");

    public TelephonyCapabilities Capabilities => TelephonyCapabilities.Dial;

    public Task<string> GetAuthorizationUrlAsync(TelephonyAuthorizationContext context, CancellationToken cancellationToken = default)
        => Task.FromResult($"https://provider.test/authorize?state={context.State}");

    public Task<TelephonyUserTokens> ExchangeCodeAsync(TelephonyCodeExchangeContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(new TelephonyUserTokens { AccessToken = "exchanged", RefreshToken = "refresh" });

    public Task<TelephonyUserTokens> RefreshTokensAsync(TelephonyUserTokens tokens, CancellationToken cancellationToken = default)
        => Task.FromResult(RefreshResult);

    public Task RevokeTokensAsync(TelephonyUserTokens tokens, CancellationToken cancellationToken = default)
    {
        RevokedTokens = tokens;

        return Task.CompletedTask;
    }

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

    public Task<TelephonyClientCredentials> GetClientCredentialsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new TelephonyClientCredentials { ProviderName = "FakeAuth" });
}
