using System.Security.Claims;
using CrestApps.OrchardCore.Records;

namespace CrestApps.OrchardCore.Attachments.Services;

/// <summary>
/// Answers who may read and change a record's files, from the <see cref="IAttachmentAccessProvider"/> of the record's
/// kind. A kind without a provider is refused.
/// </summary>
public interface IAttachmentAccessService
{
    /// <summary>
    /// Gets whether a user may read a record's files.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="owner">The record.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>Whether the user may read them.</returns>
    Task<bool> CanViewAsync(ClaimsPrincipal user, RecordRef owner, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether a user may attach files to, and delete files from, a record.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="owner">The record.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>Whether the user may change them.</returns>
    Task<bool> CanManageAsync(ClaimsPrincipal user, RecordRef owner, CancellationToken cancellationToken = default);
}
