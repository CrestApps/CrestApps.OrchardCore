namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Names the numbers customers dial to reach the contact center: every number an entry point answers, enabled or
/// not, because a disabled entry point's number still rings the tenant's own lines.
/// </summary>
public sealed class EntryPointOwnNumberSource : IContactCenterOwnNumberSource
{
    private readonly IContactCenterEntryPointManager _entryPointManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntryPointOwnNumberSource"/> class.
    /// </summary>
    /// <param name="entryPointManager">The entry point catalog.</param>
    public EntryPointOwnNumberSource(IContactCenterEntryPointManager entryPointManager)
    {
        _entryPointManager = entryPointManager;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<string>> GetOwnNumbersAsync(CancellationToken cancellationToken = default)
    {
        var entryPoints = await _entryPointManager.GetAllAsync(cancellationToken);

        return entryPoints
            .Where(entryPoint => entryPoint?.DialedNumbers is not null)
            .SelectMany(entryPoint => entryPoint.DialedNumbers)
            .Where(number => !string.IsNullOrWhiteSpace(number))
            .ToArray();
    }
}
