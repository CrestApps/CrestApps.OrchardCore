using System.Security.Claims;
using CrestApps.OrchardCore.Records;

namespace CrestApps.OrchardCore.Attachments.Services;

/// <summary>
/// Says who may read, attach and delete the files of one kind of record. The module that owns the record kind
/// registers it with <see cref="AttachmentsServiceCollectionExtensions.AddAttachmentAccessProvider{TProvider}"/>; the
/// files of a record kind without a provider can't be read or attached through the shared pages.
/// </summary>
public interface IAttachmentAccessProvider
{
    /// <summary>
    /// Gets the record kind the provider answers for.
    /// </summary>
    string OwnerKind { get; }

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
