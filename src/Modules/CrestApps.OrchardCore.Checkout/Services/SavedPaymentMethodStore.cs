using CrestApps.OrchardCore.Checkout.Indexes;
using CrestApps.OrchardCore.Checkout.Models;
using OrchardCore;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// The default YesSql-backed <see cref="ISavedPaymentMethodStore"/>: one <see cref="CustomerPaymentMethod"/> document
/// per owner and kept payment method.
/// </summary>
internal sealed class SavedPaymentMethodStore : ISavedPaymentMethodStore
{
    // An owner keeps a handful of cards; this bounds a list read so a runaway owner can't load an unbounded set.
    internal const int MaxPerOwner = 100;

    private readonly ISession _session;
    private readonly IClock _clock;

    public SavedPaymentMethodStore(
        ISession session,
        IClock clock)
    {
        _session = session;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CustomerPaymentMethod>> ListAsync(string ownerId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(ownerId))
        {
            return [];
        }

        var methods = await _session
            .Query<CustomerPaymentMethod, CustomerPaymentMethodIndex>(index => index.OwnerId == ownerId)
            .OrderByDescending(index => index.CreatedUtc)
            .Take(MaxPerOwner)
            .ListAsync(cancellationToken);

        return [.. methods
            .OrderByDescending(method => method.IsDefault)
            .ThenByDescending(method => method.CreatedUtc)];
    }

    /// <inheritdoc/>
    public async Task<CustomerPaymentMethod> FindByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        return await _session
            .Query<CustomerPaymentMethod, CustomerPaymentMethodIndex>(index => index.ItemId == id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<CustomerPaymentMethod> FindAsync(string ownerId, string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(ownerId) || string.IsNullOrEmpty(id))
        {
            return null;
        }

        return await _session
            .Query<CustomerPaymentMethod, CustomerPaymentMethodIndex>(index => index.ItemId == id && index.OwnerId == ownerId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<CustomerPaymentMethod> GetDefaultAsync(string ownerId, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var methods = await ListAsync(ownerId, cancellationToken);

        // The list puts the default first, then the newest; an expired card is skipped because charging it can only fail.
        return methods.FirstOrDefault(method => method.PaymentMethod is not null && !method.PaymentMethod.IsExpired(now));
    }

    /// <inheritdoc/>
    public async Task<CustomerPaymentMethod> SaveAsync(string ownerId, SavedPaymentMethod paymentMethod, string sourceSessionId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(ownerId);
        ArgumentNullException.ThrowIfNull(paymentMethod);
        ArgumentException.ThrowIfNullOrEmpty(paymentMethod.ProviderKey);
        ArgumentException.ThrowIfNullOrEmpty(paymentMethod.PaymentMethodReference);

        var now = _clock.UtcNow;
        var providerKey = paymentMethod.ProviderKey;
        var reference = paymentMethod.PaymentMethodReference;

        var existing = await _session
            .Query<CustomerPaymentMethod, CustomerPaymentMethodIndex>(index =>
                index.OwnerId == ownerId &&
                index.ProviderKey == providerKey &&
                index.PaymentMethodReference == reference)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            // Kept again, for example by a second checkout with the same card: the provider's description is the newer
            // one (a renewed card has a new expiry), and the record is not duplicated.
            existing.PaymentMethod = paymentMethod;
            existing.UpdatedUtc = now;

            await _session.SaveAsync(existing, cancellationToken: cancellationToken);

            return existing;
        }

        var hasAny = await _session
            .QueryIndex<CustomerPaymentMethodIndex>(index => index.OwnerId == ownerId)
            .CountAsync(cancellationToken) > 0;

        var method = new CustomerPaymentMethod
        {
            ItemId = IdGenerator.GenerateId(),
            OwnerId = ownerId,
            PaymentMethod = paymentMethod,
            IsDefault = !hasAny,
            SourceSessionId = sourceSessionId,
            CreatedUtc = now,
            UpdatedUtc = now,
        };

        await _session.SaveAsync(method, cancellationToken: cancellationToken);

        return method;
    }

    /// <inheritdoc/>
    public async Task<bool> SetDefaultAsync(string ownerId, string id, CancellationToken cancellationToken = default)
    {
        var methods = await ListAsync(ownerId, cancellationToken);
        var chosen = methods.FirstOrDefault(method => string.Equals(method.ItemId, id, StringComparison.Ordinal));

        if (chosen is null)
        {
            return false;
        }

        var now = _clock.UtcNow;

        foreach (var method in methods)
        {
            var isDefault = ReferenceEquals(method, chosen);

            if (method.IsDefault == isDefault)
            {
                continue;
            }

            method.IsDefault = isDefault;
            method.UpdatedUtc = now;

            await _session.SaveAsync(method, cancellationToken: cancellationToken);
        }

        return true;
    }

    /// <inheritdoc/>
    public async Task MarkChargedAsync(string id, DateTime chargedUtc, CancellationToken cancellationToken = default)
    {
        var method = await FindByIdAsync(id, cancellationToken);

        if (method is null)
        {
            return;
        }

        method.LastChargedUtc = chargedUtc;

        await _session.SaveAsync(method, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> RemoveAsync(string ownerId, string id, CancellationToken cancellationToken = default)
    {
        var methods = await ListAsync(ownerId, cancellationToken);
        var removed = methods.FirstOrDefault(method => string.Equals(method.ItemId, id, StringComparison.Ordinal));

        if (removed is null)
        {
            return false;
        }

        _session.Delete(removed);

        if (removed.IsDefault && methods.FirstOrDefault(method => !ReferenceEquals(method, removed)) is { } next)
        {
            next.IsDefault = true;
            next.UpdatedUtc = _clock.UtcNow;

            await _session.SaveAsync(next, cancellationToken: cancellationToken);
        }

        return true;
    }

    /// <inheritdoc/>
    public async Task<int> RemoveAllAsync(string ownerId, CancellationToken cancellationToken = default)
    {
        var methods = await ListAsync(ownerId, cancellationToken);

        foreach (var method in methods)
        {
            _session.Delete(method);
        }

        return methods.Count;
    }
}
