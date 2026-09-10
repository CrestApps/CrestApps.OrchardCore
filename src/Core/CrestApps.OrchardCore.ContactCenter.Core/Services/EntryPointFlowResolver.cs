using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Finds the menu a call in progress is in, from the entry point recorded on the interaction when the menu
/// started.
/// </summary>
/// <remarks>
/// The entry point is recorded rather than re-derived from the dialled number: a number can be re-pointed at a
/// different entry point while somebody is still listening to the old one's menu, and continuing that caller
/// through a menu they never heard the top of is worse than either outcome.
/// </remarks>
public sealed class EntryPointFlowResolver : IEntryPointFlowResolver
{
    /// <summary>
    /// The technical-metadata key holding the entry point whose menu the caller is in.
    /// </summary>
    public const string EntryPointMetadataKey = "ivrEntryPointId";

    private readonly IContactCenterEntryPointManager _entryPointManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntryPointFlowResolver"/> class.
    /// </summary>
    /// <param name="entryPointManager">The entry point manager.</param>
    public EntryPointFlowResolver(IContactCenterEntryPointManager entryPointManager)
    {
        _entryPointManager = entryPointManager;
    }

    /// <inheritdoc/>
    public async Task<IvrFlow> FindFlowAsync(Interaction interaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        if (interaction.TechnicalMetadata is null ||
            !interaction.TechnicalMetadata.TryGetValue(EntryPointMetadataKey, out var value) ||
            value?.ToString() is not { Length: > 0 } entryPointId)
        {
            return null;
        }

        var entryPoint = await _entryPointManager.FindByIdAsync(entryPointId, cancellationToken);

        return entryPoint?.IvrFlow;
    }
}
