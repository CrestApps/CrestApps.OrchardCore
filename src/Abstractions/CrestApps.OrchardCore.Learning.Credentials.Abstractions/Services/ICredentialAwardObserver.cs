using CrestApps.OrchardCore.Learning.Credentials.Models;

namespace CrestApps.OrchardCore.Learning.Credentials.Services;

/// <summary>
/// Hears credential awards after they are saved.
/// </summary>
public interface ICredentialAwardObserver
{
    /// <summary>
    /// Called after a credential was awarded.
    /// </summary>
    /// <param name="award">The award.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task AwardedAsync(CredentialAward award, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Called after an award was revoked.
    /// </summary>
    /// <param name="award">The award.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task RevokedAsync(CredentialAward award, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
