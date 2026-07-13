using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Default <see cref="ITelephonyService"/> implementation that resolves the configured default
/// provider and delegates each operation to it.
/// </summary>
public sealed class DefaultTelephonyService : ITelephonyService
{
    private readonly ITelephonyProviderResolver _resolver;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTelephonyService"/> class.
    /// </summary>
    /// <param name="resolver">The provider resolver.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DefaultTelephonyService(
        ITelephonyProviderResolver resolver,
        IStringLocalizer<DefaultTelephonyService> stringLocalizer)
    {
        _resolver = resolver;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public Task<TelephonyResult> DialAsync(DialRequest request, CancellationToken cancellationToken = default)
        => InvokeAsync((provider, token) => provider.DialAsync(request, token), cancellationToken);

    /// <inheritdoc/>
    public Task<TelephonyResult> HangupAsync(CallReference call, CancellationToken cancellationToken = default)
        => InvokeAsync((provider, token) => provider.HangupAsync(call, token), cancellationToken);

    /// <inheritdoc/>
    public Task<TelephonyResult> HoldAsync(CallReference call, CancellationToken cancellationToken = default)
        => InvokeAsync((provider, token) => provider.HoldAsync(call, token), cancellationToken);

    /// <inheritdoc/>
    public Task<TelephonyResult> ResumeAsync(CallReference call, CancellationToken cancellationToken = default)
        => InvokeAsync((provider, token) => provider.ResumeAsync(call, token), cancellationToken);

    /// <inheritdoc/>
    public Task<TelephonyResult> MuteAsync(CallReference call, CancellationToken cancellationToken = default)
        => InvokeAsync((provider, token) => provider.MuteAsync(call, token), cancellationToken);

    /// <inheritdoc/>
    public Task<TelephonyResult> UnmuteAsync(CallReference call, CancellationToken cancellationToken = default)
        => InvokeAsync((provider, token) => provider.UnmuteAsync(call, token), cancellationToken);

    /// <inheritdoc/>
    public Task<TelephonyResult> TransferAsync(TransferRequest request, CancellationToken cancellationToken = default)
        => InvokeAsync((provider, token) => provider.TransferAsync(request, token), cancellationToken);

    /// <inheritdoc/>
    public Task<TelephonyResult> MergeAsync(MergeRequest request, CancellationToken cancellationToken = default)
        => InvokeAsync((provider, token) => provider.MergeAsync(request, token), cancellationToken);

    /// <inheritdoc/>
    public Task<TelephonyResult> SendDigitsAsync(SendDigitsRequest request, CancellationToken cancellationToken = default)
        => InvokeAsync((provider, token) => provider.SendDigitsAsync(request, token), cancellationToken);

    /// <inheritdoc/>
    public Task<TelephonyResult> AnswerAsync(CallReference call, CancellationToken cancellationToken = default)
        => InvokeAsync((provider, token) => provider.AnswerAsync(call, token), cancellationToken);

    /// <inheritdoc/>
    public Task<TelephonyResult> RejectAsync(CallReference call, CancellationToken cancellationToken = default)
        => InvokeAsync((provider, token) => provider.RejectAsync(call, token), cancellationToken);

    /// <inheritdoc/>
    public Task<TelephonyResult> SendToVoicemailAsync(CallReference call, CancellationToken cancellationToken = default)
        => InvokeAsync((provider, token) => provider.SendToVoicemailAsync(call, token), cancellationToken);

    /// <inheritdoc/>
    public async Task<TelephonyClientCredentials> GetClientCredentialsAsync(CancellationToken cancellationToken = default)
    {
        var provider = await _resolver.GetAsync();

        if (provider is null)
        {
            return null;
        }

        return await provider.GetClientCredentialsAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<TelephonyDirectoryResult> GetDirectoryAsync(CancellationToken cancellationToken = default)
    {
        var provider = await _resolver.GetAsync();

        if (provider is null)
        {
            return new TelephonyDirectoryResult
            {
                Succeeded = false,
                Error = S["No telephony provider is configured."].Value,
            };
        }

        if (!provider.Capabilities.HasFlag(TelephonyCapabilities.Directory) ||
            provider is not ITelephonyDirectoryProvider directoryProvider)
        {
            return new TelephonyDirectoryResult
            {
                Succeeded = false,
                Error = S["The configured telephony provider does not support directory lookup."].Value,
            };
        }

        return await directoryProvider.GetDirectoryAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<TelephonyCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        var provider = await _resolver.GetAsync();

        return provider?.Capabilities ?? TelephonyCapabilities.None;
    }

    private async Task<TelephonyResult> InvokeAsync(
        Func<ITelephonyProvider, CancellationToken, Task<TelephonyResult>> operation,
        CancellationToken cancellationToken)
    {
        var provider = await _resolver.GetAsync();

        if (provider is null)
        {
            return TelephonyResult.Failed(S["No telephony provider is configured."].Value);
        }

        return await operation(provider, cancellationToken);
    }
}
