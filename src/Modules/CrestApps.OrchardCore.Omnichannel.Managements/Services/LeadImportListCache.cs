using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Keeps the file imports <see cref="LeadImportProvider"/> read, between requests. It is registered as a per-tenant
/// singleton, so the imports it holds belong to one tenant, and each instance of the application keeps its own. An
/// instance tells kept imports from current ones by the import history it reads from the database, so a change made
/// on one instance is seen by the others without a signal.
/// </summary>
public sealed class LeadImportListCache
{
    /// <summary>
    /// How long kept imports are used before they are read again even though the import history has not changed, so
    /// leads deleted since are reflected.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private readonly IClock _clock;

    private Entry _entry;

    /// <summary>
    /// Lets one request at a time read the imports from the database, so several editors opened together, or opened
    /// right after the kept imports expire, share one read instead of each paying for it.
    /// </summary>
    internal SemaphoreSlim ReadGate { get; } = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="LeadImportListCache"/> class.
    /// </summary>
    /// <param name="clock">The clock the lifetime is measured with.</param>
    public LeadImportListCache(IClock clock)
    {
        _clock = clock;
    }

    /// <summary>
    /// Returns the kept imports when they were read from the given import history and are still within their lifetime.
    /// </summary>
    /// <param name="version">Identifies the import history the imports must have been read from.</param>
    /// <param name="imports">The kept imports, when they can be used.</param>
    /// <returns><see langword="true"/> when the kept imports can be used.</returns>
    internal bool TryGet(string version, out IReadOnlyList<LeadImportSummary> imports)
    {
        var entry = Volatile.Read(ref _entry);

        if (entry is not null &&
            string.Equals(entry.Version, version, StringComparison.Ordinal) &&
            _clock.UtcNow < entry.ExpiresUtc)
        {
            imports = entry.Imports;

            return true;
        }

        imports = null;

        return false;
    }

    /// <summary>
    /// Keeps the imports read from the given import history.
    /// </summary>
    /// <param name="version">Identifies the import history the imports were read from.</param>
    /// <param name="imports">The imports.</param>
    internal void Set(string version, IReadOnlyList<LeadImportSummary> imports)
        => Volatile.Write(ref _entry, new Entry(version, imports, _clock.UtcNow.Add(Lifetime)));

    private sealed record Entry(string Version, IReadOnlyList<LeadImportSummary> Imports, DateTime ExpiresUtc);
}
