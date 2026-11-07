using System.Globalization;
using CrestApps.OrchardCore.Classifieds.Indexes;
using CrestApps.OrchardCore.Classifieds.Models;
using CrestApps.OrchardCore.PersonalData;
using OrchardCore.ContentManagement;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Classifieds.Services;

/// <summary>
/// Exports and anonymizes the classified listings a person posted. Erasing takes a listing down (it expires and is
/// unpublished), removes where it was offered, and scrubs the person's details from its title; the listing stays for the
/// store's records of what was paid for.
/// </summary>
public sealed class ClassifiedPersonalDataContributor : IPersonalDataContributor
{
    /// <summary>The record kind of a classified listing.</summary>
    public const string ClassifiedKind = "Classified";

    // A privacy request reads at most this many listings of each account.
    private const int MaxListings = 1000;

    private readonly ISession _session;
    private readonly IContentManager _contentManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClassifiedPersonalDataContributor"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="contentManager">The content manager.</param>
    public ClassifiedPersonalDataContributor(
        ISession session,
        IContentManager contentManager)
    {
        _session = session;
        _contentManager = contentManager;
    }

    /// <inheritdoc/>
    public async Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var item in await ListAsync(context.Subject, cancellationToken))
        {
            item.TryGet<ClassifiedPart>(out var part);
            var record = context.Add(ClassifiedKind, item.ContentItemId, item.DisplayText, item.CreatedUtc);

            record.Fields[nameof(ContentItem.ContentType)] = item.ContentType;
            record.Fields[nameof(ContentItem.DisplayText)] = item.DisplayText;
            record.Fields[nameof(ClassifiedPart.Status)] = part?.Status.ToString();
            record.Fields[nameof(ClassifiedPart.Place)] = part?.Place;
            record.Fields[nameof(ClassifiedPart.PostedUtc)] = part?.PostedUtc?.ToString("O", CultureInfo.InvariantCulture);
            record.Fields[nameof(ClassifiedPart.ExpiresUtc)] = part?.ExpiresUtc?.ToString("O", CultureInfo.InvariantCulture);
        }
    }

    /// <inheritdoc/>
    public async Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var item in await ListAsync(context.Subject, cancellationToken))
        {
            item.Alter<ClassifiedPart>(part =>
            {
                part.Status = ClassifiedStatus.Expired;
                part.Place = null;
                part.Latitude = null;
                part.Longitude = null;
            });

            item.DisplayText = context.Scrub(item.DisplayText);

            await _contentManager.UpdateAsync(item);

            if (item.Published)
            {
                await _contentManager.UnpublishAsync(item);
            }

            context.Result.Count(ClassifiedKind);
        }
    }

    private async Task<IReadOnlyList<ContentItem>> ListAsync(PersonalDataSubject subject, CancellationToken cancellationToken)
    {
        var owners = subject.UserIds.ToArray();

        if (owners.Length == 0)
        {
            return [];
        }

        return [.. await _session.Query<ContentItem, ClassifiedIndex>(index => index.Owner.IsIn(owners) && index.Latest)
            .OrderByDescending(index => index.SortUtc)
            .Take(MaxListings)
            .ListAsync(cancellationToken)];
    }
}
