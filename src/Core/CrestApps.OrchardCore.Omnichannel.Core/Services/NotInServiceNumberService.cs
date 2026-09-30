using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.PhoneNumbers;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// The YesSql-backed <see cref="INotInServiceNumberService"/>.
/// </summary>
public sealed class NotInServiceNumberService : INotInServiceNumberService
{
    private readonly ISession _session;
    private readonly IPhoneNumberService _phoneNumberService;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotInServiceNumberService"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="phoneNumberService">The phone number service used to put numbers in E.164 form.</param>
    /// <param name="clock">The clock used to stamp detections.</param>
    /// <param name="logger">The logger.</param>
    public NotInServiceNumberService(
        ISession session,
        IPhoneNumberService phoneNumberService,
        IClock clock,
        ILogger<NotInServiceNumberService> logger)
    {
        _session = session;
        _phoneNumberService = phoneNumberService;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> IsNotInServiceAsync(string phoneNumber, CancellationToken cancellationToken = default)
        => await FindAsync(phoneNumber, cancellationToken) is not null;

    /// <inheritdoc/>
    public async Task<IReadOnlySet<string>> GetNotInServiceAsync(IEnumerable<string> phoneNumbers, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(phoneNumbers);

        // Every form a caller passed in, by the number it stands for, so the answer can be given back in the
        // caller's own terms.
        var passedByNumber = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var phoneNumber in phoneNumbers)
        {
            var normalized = Normalize(phoneNumber);

            if (normalized is null)
            {
                continue;
            }

            if (!passedByNumber.TryGetValue(normalized, out var passed))
            {
                passed = [];
                passedByNumber[normalized] = passed;
            }

            passed.Add(phoneNumber);
        }

        var result = new HashSet<string>(StringComparer.Ordinal);

        if (passedByNumber.Count == 0)
        {
            return result;
        }

        var marked = await _session.QueryIndex<NotInServiceNumberIndex>(
            index => index.PhoneNumber.IsIn(passedByNumber.Keys),
            collection: OmnichannelConstants.CollectionName)
            .ListAsync(cancellationToken);

        foreach (var index in marked)
        {
            if (passedByNumber.TryGetValue(index.PhoneNumber, out var passed))
            {
                result.UnionWith(passed);
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<NotInServiceNumber> FindAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(phoneNumber);

        if (normalized is null)
        {
            return null;
        }

        return await _session.Query<NotInServiceNumber, NotInServiceNumberIndex>(
            index => index.PhoneNumber == normalized,
            collection: OmnichannelConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<NotInServiceNumber> MarkAsync(NotInServiceMark mark, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mark);

        var normalized = Normalize(mark.PhoneNumber);

        if (normalized is null)
        {
            _logger.LogWarning(
                "Could not mark '{PhoneNumber}' as not in service because it is not a phone number that can be read. Source={Source}, ActivityId={ActivityId}.",
                mark.PhoneNumber.SanitizeLogValue(),
                mark.Source.SanitizeLogValue(),
                mark.ActivityId.SanitizeLogValue());

            return null;
        }

        var now = _clock.UtcNow;
        var record = await FindAsync(normalized, cancellationToken);
        var isNew = record is null;

        record ??= new NotInServiceNumber
        {
            ItemId = UniqueId.GenerateId(),
            PhoneNumber = normalized,
            FirstDetectedUtc = now,
        };

        // The latest detection is what the record describes: the evidence someone checking the mark will want is
        // the most recent, and a number re-marked by a person after an automated detection now carries their name.
        record.Source = mark.Source;
        record.Reason = mark.Reason;
        record.ActivityId = mark.ActivityId;
        record.CampaignId = mark.CampaignId;
        record.ContactContentItemId = mark.ContactContentItemId;
        record.MarkedById = mark.MarkedById;
        record.MarkedByUsername = mark.MarkedByUsername;
        record.LastDetectedUtc = now;
        record.DetectionCount++;

        await _session.SaveAsync(record, collection: OmnichannelConstants.CollectionName, cancellationToken: cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "{Action} phone number '{PhoneNumber}' as not in service. Source={Source}, Reason={Reason}, ActivityId={ActivityId}, CampaignId={CampaignId}, Detections={DetectionCount}.",
                isNew ? "Marked" : "Re-marked",
                normalized.SanitizeLogValue(),
                mark.Source.SanitizeLogValue(),
                mark.Reason.SanitizeLogValue(),
                mark.ActivityId.SanitizeLogValue(),
                mark.CampaignId.SanitizeLogValue(),
                record.DetectionCount);
        }

        return record;
    }

    /// <inheritdoc/>
    public async Task<bool> ClearAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(phoneNumber);

        if (normalized is null)
        {
            return false;
        }

        // Two detections made at the same moment can each have created a record for the number; clearing it has
        // to clear them all, or the number would stay excluded.
        var records = await _session.Query<NotInServiceNumber, NotInServiceNumberIndex>(
            index => index.PhoneNumber == normalized,
            collection: OmnichannelConstants.CollectionName)
            .ListAsync(cancellationToken);

        var cleared = false;

        foreach (var record in records)
        {
            _session.Delete(record, OmnichannelConstants.CollectionName);
            cleared = true;
        }

        if (cleared && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Cleared the not-in-service mark from phone number '{PhoneNumber}'.", normalized.SanitizeLogValue());
        }

        return cleared;
    }

    /// <inheritdoc/>
    public async Task<PageResult<NotInServiceNumber>> PageAsync(int page, int pageSize, string search, CancellationToken cancellationToken = default)
    {
        var digits = string.IsNullOrWhiteSpace(search)
            ? null
            : new string(search.Where(char.IsAsciiDigit).ToArray());

        var query = string.IsNullOrEmpty(digits)
            ? _session.Query<NotInServiceNumber, NotInServiceNumberIndex>(collection: OmnichannelConstants.CollectionName)
            : _session.Query<NotInServiceNumber, NotInServiceNumberIndex>(index => index.PhoneNumber.Contains(digits), collection: OmnichannelConstants.CollectionName);

        var ordered = query.OrderByDescending(index => index.LastDetectedUtc);
        var skip = (Math.Max(page, 1) - 1) * pageSize;

        return new PageResult<NotInServiceNumber>
        {
            Count = await ordered.CountAsync(cancellationToken),
            Entries = (await ordered.Skip(skip).Take(pageSize).ListAsync(cancellationToken)).ToArray(),
        };
    }

    /// <inheritdoc/>
    public string Normalize(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return null;
        }

        if (_phoneNumberService.TryParse(phoneNumber, out PhoneNumber parsed) && parsed.HasValue)
        {
            return parsed.Value;
        }

        return PhoneNumber.TryFromE164(phoneNumber.Trim(), out var e164) && e164.HasValue
            ? e164.Value
            : null;
    }
}
