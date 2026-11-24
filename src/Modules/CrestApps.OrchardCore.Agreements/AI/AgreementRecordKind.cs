using System.Globalization;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Agreements.Core.Indexes;
using CrestApps.OrchardCore.Agreements.Models;
using Microsoft.Extensions.DependencyInjection;
using YesSql;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Agreements.AI;

/// <summary>
/// How agreements are exposed to the generic record tools: read-only, listed newest number first and found by number,
/// name or account, with an allow-listed projection.
/// </summary>
internal static class AgreementRecordKind
{
    /// <summary>
    /// The kind's name.
    /// </summary>
    public const string Name = "agreement";

    /// <summary>
    /// The most characters of an agreement's notes returned.
    /// </summary>
    public const int NotesExcerptLength = 500;

    /// <summary>
    /// Lists agreements, newest number first. A text that is a status (Draft, Active, Paused, Expired or Canceled) lists
    /// the agreements in it; any other text matches an agreement whose number or name contains it, whose account has that
    /// ID, or whose account the user with that ID is a member of (with Account Members on). The default list would ignore
    /// the text, since the agreement index has no name column the catalog knows.
    /// </summary>
    /// <param name="services">The services of the invocation.</param>
    /// <param name="query">The text and the page.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The page.</returns>
    public static async ValueTask<PageResult<Agreement>> ListAsync(IServiceProvider services, AIRecordQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(query);

        var agreements = services.GetRequiredService<YesSqlSession>().Query<Agreement, AgreementIndex>(collection: AgreementsConstants.CollectionName);

        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            var text = query.Text.Trim();

            if (!char.IsAsciiDigit(text[0]) && Enum.TryParse<AgreementStatus>(text, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
            {
                var status = parsed.ToString();

                agreements = agreements.With<AgreementIndex>(index => index.Status == status);
            }
            else
            {
                // A customer is found through the account they are a member of: the agreement's account is indexed, its
                // field-service customer is not.
                var memberAccountId = services.GetService<IAccountMemberService>() is { } members
                    ? (await members.FindAccountAsync(text, cancellationToken))?.ContentItemId ?? text
                    : text;

                // A text can't seek: grouped by document, the database reads the matches and sorts them.
                agreements = agreements.NoIndexOptimizations().With<AgreementIndex>(index => index.Number.Contains(text) || index.Name.Contains(text) || index.AccountId == text || index.AccountId == memberAccountId);
            }
        }

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Max(1, query.PageSize);
        var count = await agreements.CountAsync(cancellationToken);
        var items = await agreements.OrderByDescending(index => index.Number).Skip((page - 1) * pageSize).Take(pageSize).ListAsync(cancellationToken);

        return new PageResult<Agreement>
        {
            Count = count,
            Entries = [.. items],
        };
    }

    /// <summary>
    /// The fields of an agreement the record tools return. The terms other features keep on it (a contact's phone and
    /// e-mail, an address, a price) are named only, and the bookkeeping of its notices is left out.
    /// </summary>
    /// <param name="agreement">The agreement.</param>
    /// <returns>The fields.</returns>
    public static object Project(Agreement agreement)
    {
        ArgumentNullException.ThrowIfNull(agreement);

        var hours = agreement.Hours ?? new AgreementHours();
        var notes = agreement.Notes;

        return new
        {
            id = agreement.ItemId,
            number = agreement.Number,
            name = agreement.Name,
            status = agreement.Status.ToString(),
            accountId = agreement.AccountId,
            startDate = Day(agreement.StartDate),
            endDate = Day(agreement.EndDate),
            timeZone = agreement.TimeZoneId,
            hours = new
            {
                kind = hours.Kind.ToString(),
                hours = hours.Hours,
                roundingMinutes = hours.RoundingMinutes,
            },
            expiringNoticeDays = agreement.ExpiringNoticeDays,
            notesExcerpt = notes is { Length: > NotesExcerptLength } ? notes[..NotesExcerptLength] : notes,
            notesLength = notes?.Length ?? 0,
            otherTerms = agreement.Properties?.Keys.Order(StringComparer.Ordinal).ToList() ?? [],
        };
    }

    private static string Day(DateOnly? day)
        => day?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
