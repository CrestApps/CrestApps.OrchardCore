using CrestApps.OrchardCore.Skills.Models;

namespace CrestApps.OrchardCore.Skills.Services;

/// <summary>
/// Hears about certificates issued, renewed and revoked, after the change is committed. A failing observer is logged
/// and does not undo the change or stop the other observers.
/// </summary>
public interface ICertificationRecordObserver
{
    /// <summary>
    /// A certificate was issued for the first time in its chain.
    /// </summary>
    /// <param name="context">The certificate and its certification.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task IssuedAsync(CertificationRecordContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// A certificate was renewed by a new one; <see cref="CertificationRecordContext.PreviousRecord"/> is the one renewed.
    /// </summary>
    /// <param name="context">The new certificate, the earlier one and their certification.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task RenewedAsync(CertificationRecordContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// A certificate was revoked.
    /// </summary>
    /// <param name="context">The certificate and its certification.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task RevokedAsync(CertificationRecordContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
