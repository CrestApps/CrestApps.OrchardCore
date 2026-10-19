using System.Security.Cryptography;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Indexes;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using OrchardCore;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Customers.Services;

/// <summary>
/// Persists customers' referral links.
/// </summary>
public interface IReferralStore : ICatalog<ReferralProfile>
{
    Task<ReferralProfile> FindByCustomerAsync(string customerId, CancellationToken cancellationToken = default);

    Task<ReferralProfile> FindByCodeAsync(string code, CancellationToken cancellationToken = default);
}

/// <summary>
/// The default YesSql-backed <see cref="IReferralStore"/>.
/// </summary>
public sealed class ReferralStore : DocumentCatalog<ReferralProfile, ReferralProfileIndex>, IReferralStore
{
    private readonly IClock _clock;

    protected override bool CheckConcurrency => true;

    public ReferralStore(ISession session, IClock clock)
        : base(session)
    {
        CollectionName = CustomersConstants.CollectionName;
        _clock = clock;
    }

    public Task<ReferralProfile> FindByCustomerAsync(string customerId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(customerId)
            ? Task.FromResult<ReferralProfile>(null)
            : Session.Query<ReferralProfile, ReferralProfileIndex>(index => index.CustomerId == customerId, collection: CollectionName).FirstOrDefaultAsync(cancellationToken);

    public Task<ReferralProfile> FindByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        code = code?.Trim().ToUpperInvariant();

        return string.IsNullOrEmpty(code) || code.Length > 16
            ? Task.FromResult<ReferralProfile>(null)
            : Session.Query<ReferralProfile, ReferralProfileIndex>(index => index.Code == code, collection: CollectionName).FirstOrDefaultAsync(cancellationToken);
    }

    protected override ValueTask SavingAsync(ReferralProfile record)
    {
        var now = _clock.UtcNow;

        if (record.CreatedUtc == default)
        {
            record.CreatedUtc = now;
        }

        record.UpdatedUtc = now;

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Gives customers their referral link.
/// </summary>
public sealed class ReferralService
{
    /// <summary>
    /// The cookie a referral link keeps its code in until the friend checks out.
    /// </summary>
    public const string CookieName = "orchard-referral";

    // No 0, O, 1 or I, which people misread when they share a link by hand.
    private static readonly char[] _alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

    private readonly IReferralStore _store;
    private readonly ISession _session;

    public ReferralService(IReferralStore store, ISession session)
    {
        _store = store;
        _session = session;
    }

    /// <summary>
    /// Returns the customer's referral link, made the first time it's asked for.
    /// </summary>
    public async Task<ReferralProfile> GetOrCreateAsync(string customerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(customerId);

        if (await _store.FindByCustomerAsync(customerId, cancellationToken) is { } existing)
        {
            return existing;
        }

        string code;

        do
        {
            code = new string(RandomNumberGenerator.GetItems<char>(_alphabet, 8));
        }
        while (await _store.FindByCodeAsync(code, cancellationToken) is not null);

        var profile = new ReferralProfile { ItemId = IdGenerator.GenerateId(), CustomerId = customerId, Code = code };

        await _store.CreateAsync(profile, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        return profile;
    }
}
