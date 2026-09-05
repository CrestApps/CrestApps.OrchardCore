namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Answers which content types carry the Omnichannel Contact part. Channels that search or list contacts depend
/// on this contract instead of the Omnichannel Management administration, so a tenant can run a channel workspace
/// without the CRM administration screens.
/// </summary>
public interface IOmnichannelContactTypeProvider
{
    /// <summary>
    /// Gets the technical names of the content types that have the Omnichannel Contact part attached.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The contact content type names, or an empty collection when the tenant defines none.</returns>
    ValueTask<IReadOnlyCollection<string>> GetContactContentTypesAsync(CancellationToken cancellationToken = default);
}
