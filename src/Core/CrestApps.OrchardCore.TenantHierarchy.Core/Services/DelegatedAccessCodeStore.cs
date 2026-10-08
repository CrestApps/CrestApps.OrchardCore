using CrestApps.OrchardCore.TenantHierarchy.Core.Indexes;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using YesSql;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Services;

/// <summary>
/// Stores the one-time codes in the database of the current parent tenant.
/// </summary>
public sealed class DelegatedAccessCodeStore
{
    private const string Collection = TenantHierarchyConstants.CollectionName;

    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="DelegatedAccessCodeStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session of the parent tenant.</param>
    public DelegatedAccessCodeStore(ISession session)
    {
        _session = session;
    }

    /// <summary>
    /// Finds a code by its hash.
    /// </summary>
    /// <param name="codeHash">The hash of the code.</param>
    public Task<DelegatedAccessCode> FindByHashAsync(string codeHash)
    {
        if (string.IsNullOrEmpty(codeHash))
        {
            return Task.FromResult<DelegatedAccessCode>(null);
        }

        return _session.Query<DelegatedAccessCode, DelegatedAccessCodeIndex>(index => index.CodeHash == codeHash, Collection)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Saves a new code.
    /// </summary>
    /// <param name="code">The code.</param>
    public Task CreateAsync(DelegatedAccessCode code)
    {
        ArgumentNullException.ThrowIfNull(code);

        return _session.SaveAsync(code, checkConcurrency: false, Collection);
    }

    /// <summary>
    /// Marks a code as redeemed and commits at once with a concurrency check, so when two redemptions race, the
    /// second one fails with a concurrency exception.
    /// </summary>
    /// <param name="code">The code.</param>
    public async Task MarkRedeemedAsync(DelegatedAccessCode code)
    {
        ArgumentNullException.ThrowIfNull(code);

        code.Redeemed = true;
        await _session.SaveAsync(code, checkConcurrency: true, Collection);
        await _session.SaveChangesAsync();
    }

    /// <summary>
    /// Deletes the codes that expired before the given time.
    /// </summary>
    /// <param name="utcNow">The current time.</param>
    public async Task<int> DeleteExpiredAsync(DateTime utcNow)
    {
        var expired = await _session.Query<DelegatedAccessCode, DelegatedAccessCodeIndex>(index => index.ExpiresUtc < utcNow, Collection)
            .Take(200)
            .ListAsync();

        var count = 0;

        foreach (var code in expired)
        {
            _session.Delete(code, Collection);
            count++;
        }

        return count;
    }
}
