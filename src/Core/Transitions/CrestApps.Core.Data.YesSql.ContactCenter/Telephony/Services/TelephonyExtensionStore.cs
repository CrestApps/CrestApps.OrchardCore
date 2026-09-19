using CrestApps.Core.Data.YesSql.Telephony.Indexes;
using CrestApps.Core.Data.YesSql.Services;
using CrestApps.Core.Telephony.Models;
using CrestApps.Core.Telephony.Services;
using YesSql;

namespace CrestApps.Core.Data.YesSql.Telephony.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="ITelephonyExtensionStore"/>.
/// </summary>
public sealed class TelephonyExtensionStore : ConcurrentDocumentCatalog<TelephonyExtension, TelephonyExtensionIndex>, ITelephonyExtensionStore
{
    /// <inheritdoc/>
    protected override bool CheckConcurrency => true;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonyExtensionStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public TelephonyExtensionStore(ISession session)
        : base(session)
    {
    }

    /// <inheritdoc/>
    public async Task<TelephonyExtension> FindByNumberAsync(string number, CancellationToken cancellationToken = default)
    {
        var normalized = TelephonyExtension.NormalizeNumber(number);

        if (normalized is null)
        {
            return null;
        }

        return await Session.Query<TelephonyExtension, TelephonyExtensionIndex>(
            index => index.Number == normalized)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<TelephonyExtension> FindByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        return await Session.Query<TelephonyExtension, TelephonyExtensionIndex>(
            index => index.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
