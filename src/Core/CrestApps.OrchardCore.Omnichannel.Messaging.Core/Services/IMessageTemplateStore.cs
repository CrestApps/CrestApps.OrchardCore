using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// The persistence contract for <see cref="MessageTemplate"/>.
/// </summary>
public interface IMessageTemplateStore : ICatalog<MessageTemplate>
{
    /// <summary>
    /// Lists the templates of one scope, by name, from the index.
    /// </summary>
    /// <param name="scope">The scope, or <see langword="null"/> for the messaging workspace's templates.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The templates.</returns>
    Task<IReadOnlyList<MessageTemplate>> ListByScopeAsync(string scope, CancellationToken cancellationToken = default);
}
