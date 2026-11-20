using System.Security.Claims;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Attachments.Services;

namespace CrestApps.OrchardCore.Attachments.Core.Services;

/// <summary>
/// The default <see cref="IAttachmentAccessService"/>: asks the access provider of the owner's kind, and refuses a kind
/// that has none.
/// </summary>
public sealed class DefaultAttachmentAccessService : IAttachmentAccessService
{
    private readonly IEnumerable<IAttachmentAccessProvider> _providers;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAttachmentAccessService"/> class.
    /// </summary>
    /// <param name="providers">The access providers.</param>
    public DefaultAttachmentAccessService(IEnumerable<IAttachmentAccessProvider> providers)
    {
        _providers = providers;
    }

    /// <inheritdoc/>
    public Task<bool> CanViewAsync(ClaimsPrincipal user, RecordRef owner, CancellationToken cancellationToken = default)
    {
        var provider = Find(owner);

        return user is null || provider is null
            ? Task.FromResult(false)
            : provider.CanViewAsync(user, owner, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> CanManageAsync(ClaimsPrincipal user, RecordRef owner, CancellationToken cancellationToken = default)
    {
        var provider = Find(owner);

        return user is null || provider is null
            ? Task.FromResult(false)
            : provider.CanManageAsync(user, owner, cancellationToken);
    }

    private IAttachmentAccessProvider Find(RecordRef owner)
        => owner.IsEmpty
            ? null
            : _providers.FirstOrDefault(provider => string.Equals(provider.OwnerKind, owner.Kind, StringComparison.Ordinal));
}
