using CrestApps.Core;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Indexes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Deliverability;

/// <summary>
/// The email addresses the business no longer sends to.
/// </summary>
public interface IEmailSuppressionList
{
    /// <summary>
    /// Finds the suppression of an address.
    /// </summary>
    /// <param name="address">The address, in any spelling.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The suppression, or <see langword="null"/> when the address is not suppressed.</returns>
    Task<EmailSuppression> FindAsync(string address, CancellationToken cancellationToken = default);

    /// <summary>
    /// Suppresses an address. An address already suppressed keeps its first reason.
    /// </summary>
    /// <param name="address">The address, in any spelling.</param>
    /// <param name="reason">Why.</param>
    /// <param name="detail">What the server or provider said.</param>
    /// <param name="createdBy">The user suppressing it by hand, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the address was added; <see langword="false"/> when it was already there.</returns>
    Task<bool> SuppressAsync(string address, EmailSuppressionReason reason, string detail, string createdBy = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes an address off the list.
    /// </summary>
    /// <param name="address">The address, in any spelling.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when it was on the list.</returns>
    Task<bool> RemoveAsync(string address, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists suppressed addresses, newest first.
    /// </summary>
    /// <param name="search">Part of an address to look for, or <see langword="null"/>.</param>
    /// <param name="page">The page, from one.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The page and the total count.</returns>
    Task<(IReadOnlyList<EmailSuppression> Items, int Total)> PageAsync(string search, int page, int pageSize, CancellationToken cancellationToken = default);
}

/// <summary>
/// The default <see cref="IEmailSuppressionList"/>, stored as documents in the deliverability collection.
/// </summary>
public sealed class EmailSuppressionList : IEmailSuppressionList
{
    private const string Collection = EmailChannelConstants.DeliverabilityCollectionName;

    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public EmailSuppressionList(
        ISession session,
        IClock clock,
        ILogger<EmailSuppressionList> logger)
    {
        _session = session;
        _clock = clock;
        _logger = logger;
    }

    public async Task<EmailSuppression> FindAsync(string address, CancellationToken cancellationToken = default)
    {
        var key = ToKey(address);

        if (key is null)
        {
            return null;
        }

        return await _session.Query<EmailSuppression, EmailSuppressionIndex>(index => index.Address == key, collection: Collection)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> SuppressAsync(string address, EmailSuppressionReason reason, string detail, string createdBy = null, CancellationToken cancellationToken = default)
    {
        var key = ToKey(address);

        if (key is null || await FindAsync(key, cancellationToken) is not null)
        {
            return false;
        }

        await _session.SaveAsync(
            new EmailSuppression
            {
                ItemId = UniqueId.GenerateId(),
                Address = key,
                Reason = reason,
                Detail = detail is { Length: > 500 } ? detail[..500] : detail,
                CreatedUtc = _clock.UtcNow,
                CreatedBy = createdBy,
            },
            collection: Collection,
            cancellationToken: cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("An email address was added to the suppression list ({Reason}).", reason);
        }

        return true;
    }

    public async Task<bool> RemoveAsync(string address, CancellationToken cancellationToken = default)
    {
        var suppression = await FindAsync(address, cancellationToken);

        if (suppression is null)
        {
            return false;
        }

        _session.Delete(suppression, Collection);

        return true;
    }

    public async Task<(IReadOnlyList<EmailSuppression> Items, int Total)> PageAsync(string search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var term = search?.Trim().ToLowerInvariant();
        var size = Math.Clamp(pageSize, 1, 200);
        var skip = (Math.Max(1, page) - 1) * size;

        var query = string.IsNullOrEmpty(term)
            ? _session.Query<EmailSuppression, EmailSuppressionIndex>(collection: Collection)
            : _session.Query<EmailSuppression, EmailSuppressionIndex>(index => index.Address.Contains(term), collection: Collection);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(index => index.CreatedUtc)
            .Skip(skip)
            .Take(size)
            .ListAsync(cancellationToken);

        return (items.ToArray(), total);
    }

    private static string ToKey(string address)
    {
        var normalized = OmnichannelEmailAddress.Normalize(address);

        return OmnichannelEmailAddress.IsValid(normalized)
            ? EmailDeliveryLogIndexProvider.Truncate(normalized, EmailDeliveryLogIndex.RecipientLength)
            : null;
    }
}
