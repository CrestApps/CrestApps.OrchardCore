using System.Linq.Expressions;
using System.Reflection;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.BackgroundTasks;
using CrestApps.OrchardCore.Omnichannel.Managements.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Contents;
using OrchardCore.Contents.Services;
using OrchardCore.Documents;
using YesSql;
using YesSql.Filters.Query;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

internal sealed class OmnichannelContactPhoneContentsAdminListFilterProvider : IContentsAdminListFilterProvider
{
    /// <summary>
    /// The fewest digits a search must hold to be compared with phone numbers. Fewer match a large part of any list.
    /// </summary>
    internal const int MinimumPhoneSearchDigits = 3;

    private const string _phoneSeparators = " -().";

    private static readonly MethodInfo _isInContactIndexMethod = typeof(DefaultQueryExtensionsIndex)
        .GetMethod(nameof(DefaultQueryExtensionsIndex.IsIn))
        .MakeGenericMethod(typeof(OmnichannelContactIndex));

    private static readonly MethodInfo _isInPhoneNumberIndexMethod = typeof(DefaultQueryExtensionsIndex)
        .GetMethod(nameof(DefaultQueryExtensionsIndex.IsIn))
        .MakeGenericMethod(typeof(OmnichannelContactPhoneNumberIndex));

    public void Build(QueryEngineBuilder<ContentItem> builder)
    {
        builder
            .WithNamedTerm("phone", term => term
                .OneCondition((value, query) => ApplyFilter(value, PhoneNumberMatchType.Contains, query)))
            .WithNamedTerm("phone-exact", term => term
                .OneCondition((value, query) => ApplyFilter(value, PhoneNumberMatchType.Exact, query)))
            .WithNamedTerm("phone-starts", term => term
                .OneCondition((value, query) => ApplyFilter(value, PhoneNumberMatchType.BeginsWith, query)))
            .WithNamedTerm("phone-ends", term => term
                .OneCondition((value, query) => ApplyFilter(value, PhoneNumberMatchType.EndsWith, query)))

            // Override the built-in free-text term. When the content list is scoped exclusively to omnichannel
            // contact content types, a search written like a phone number matches the contacts whose number starts
            // with it, and a search with letters in it matches the display text. On every other content list this
            // behaves like the framework default (a DisplayText match).
            .WithDefaultTerm(ContentsAdminListFilterOptions.DefaultTermName, term => term
                .ManyCondition(
                    async (value, query, context) =>
                    {
                        var serviceProvider = ((ContentQueryContext)context).ServiceProvider;
                        var useExactMatch = UseExactMatch(serviceProvider);

                        if (!TryParsePhoneLikeTerm(value, out var searchTerm))
                        {
                            return DisplayTextMatch(value, useExactMatch)(query);
                        }

                        if (await IsContactOnlyListAsync(serviceProvider))
                        {
                            return query.With(PhoneMatch(searchTerm, await IsPhoneNumberIndexReadyAsync(serviceProvider)));
                        }

                        // An exact E.164 number is unambiguous, so it finds the contact on any list, even one that
                        // also lists other content types, whose items are still matched by their display text.
                        return searchTerm.IsE164
                            ? query.With(DisplayTextOrPhoneMatch(value, useExactMatch, searchTerm, await IsPhoneNumberIndexReadyAsync(serviceProvider)))
                            : DisplayTextMatch(value, useExactMatch)(query);
                    },
                    async (value, query, context) =>
                    {
                        var serviceProvider = ((ContentQueryContext)context).ServiceProvider;
                        var useExactMatch = UseExactMatch(serviceProvider);

                        if (!IsExactE164(value) && !await IsContactOnlyListAsync(serviceProvider))
                        {
                            return DisplayTextNotMatch(value, useExactMatch)(query);
                        }

                        var predicates = new List<Func<IQuery<ContentItem>, IQuery<ContentItem>>>
                        {
                            DisplayTextNotMatch(value, useExactMatch),
                        };

                        AddPhoneNotContainsPredicate(value, predicates);

                        return query.All(predicates.ToArray());
                    }));
    }

    private static IQuery<ContentItem> ApplyFilter(
        string value,
        PhoneNumberMatchType matchType,
        IQuery<ContentItem> query)
    {
        if (!PhoneNumberSearchTerm.TryParse(value, out var searchTerm))
        {
            return query.With<OmnichannelContactIndex>(index => index.ContentItemId == string.Empty);
        }

        return query.With(OmnichannelContactPhonePredicates.Match(searchTerm, matchType));
    }

    /// <summary>
    /// Matches a content item that holds a number starting with the search. The search box of a contact or lead list
    /// uses it for a search written like a phone number.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The display text is left out on purpose. A display text match is a <c>LIKE '%…%'</c> that no index can answer,
    /// and joined to the number with "or" it kept the phone number indexes from narrowing the search too: every
    /// search by number read the display text of every contact or lead of the list, once for the page and again for
    /// its count, and timed out on a large tenant. A name holds no run of digits written like a phone number, and a
    /// record named after its number holds that number as well.
    /// </para>
    /// <para>
    /// The number is read from <see cref="OmnichannelContactPhoneNumberIndex"/>, which holds every number of the
    /// record, whatever its type, once <paramref name="usePhoneNumberIndex"/> says its backfill has finished. Until
    /// then it is read from the first cell and home columns of <see cref="OmnichannelContactIndex"/>, as every phone
    /// lookup does, so a record saved before the phone number index existed is still found.
    /// </para>
    /// </remarks>
    /// <param name="searchTerm">The parsed search term.</param>
    /// <param name="usePhoneNumberIndex">Whether the phone number index holds every record yet.</param>
    internal static Expression<Func<ContentItemIndex, bool>> PhoneMatch(PhoneNumberSearchTerm searchTerm, bool usePhoneNumberIndex)
    {
        Expression<Func<ContentItemIndex, bool>> anyDocument = index => index.DocumentId > 0;

        var parameter = anyDocument.Parameters[0];

        // YesSql joins the words of a search such as "(702) 555-9999" with an "or" that has no space after it, and
        // relies on every term opening with a parenthesis; a binary expression does, the subquery alone does not. The
        // DocumentId test matches every document, so it only gives the term its parenthesis.
        return Expression.Lambda<Func<ContentItemIndex, bool>>(
            Expression.AndAlso(anyDocument.Body, HasMatchingNumber(parameter, searchTerm, usePhoneNumberIndex)),
            parameter);
    }

    /// <summary>
    /// Matches a content item whose display text matches the search, or that holds a number starting with it.
    /// </summary>
    /// <remarks>
    /// The number is compared in a subquery (<c>DocumentId IN (SELECT DocumentId FROM ...)</c>) rather than by joining
    /// a phone index to the list. With the join, the "or" spanned two tables, so the database joined every contact of
    /// the list to its index row before it could test either half, on every search and again for the count of
    /// results. Only lists that are not scoped to contact types use it, for an exact E.164 search.
    /// </remarks>
    internal static Expression<Func<ContentItemIndex, bool>> DisplayTextOrPhoneMatch(
        string value,
        bool useExactMatch,
        PhoneNumberSearchTerm searchTerm,
        bool usePhoneNumberIndex)
    {
        Expression<Func<ContentItemIndex, bool>> displayText = useExactMatch
            ? index => index.DisplayText == value
            : index => index.DisplayText.Contains(value);

        var parameter = displayText.Parameters[0];

        return Expression.Lambda<Func<ContentItemIndex, bool>>(
            Expression.OrElse(displayText.Body, HasMatchingNumber(parameter, searchTerm, usePhoneNumberIndex)),
            parameter);
    }

    // DocumentId IN (SELECT DocumentId FROM <phone index> WHERE <the number starts with the search>).
    private static MethodCallExpression HasMatchingNumber(
        ParameterExpression contentItemIndex,
        PhoneNumberSearchTerm searchTerm,
        bool usePhoneNumberIndex)
    {
        var documentId = Expression.Convert(
            Expression.Property(contentItemIndex, nameof(ContentItemIndex.DocumentId)),
            typeof(object));

        if (usePhoneNumberIndex)
        {
            Expression<Func<OmnichannelContactPhoneNumberIndex, object>> phoneDocumentId = phone => phone.DocumentId;

            return Expression.Call(
                _isInPhoneNumberIndexMethod,
                documentId,
                Expression.Quote(phoneDocumentId),
                Expression.Quote(OmnichannelContactPhonePredicates.SearchBoxNumberMatch(searchTerm)));
        }

        Expression<Func<OmnichannelContactIndex, object>> contactDocumentId = contact => contact.DocumentId;

        return Expression.Call(
            _isInContactIndexMethod,
            documentId,
            Expression.Quote(contactDocumentId),
            Expression.Quote(OmnichannelContactPhonePredicates.SearchBoxMatch(searchTerm)));
    }

    /// <summary>
    /// Reads the search as a phone number only when it is written like one: digits with the usual separators and an
    /// optional leading plus, and at least <see cref="MinimumPhoneSearchDigits"/> digits.
    /// </summary>
    /// <remarks>
    /// A search with letters in it, such as <c>John 2</c>, is a name, so it is not also compared with every number
    /// that holds a <c>2</c>.
    /// </remarks>
    internal static bool TryParsePhoneLikeTerm(string value, out PhoneNumberSearchTerm searchTerm)
    {
        searchTerm = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        var digits = 0;

        for (var i = 0; i < trimmed.Length; i++)
        {
            var character = trimmed[i];

            if (char.IsAsciiDigit(character))
            {
                digits++;

                continue;
            }

            if ((character == '+' && i == 0) || _phoneSeparators.Contains(character))
            {
                continue;
            }

            return false;
        }

        return digits >= MinimumPhoneSearchDigits && PhoneNumberSearchTerm.TryParse(trimmed, out searchTerm);
    }

    private static Func<IQuery<ContentItem>, IQuery<ContentItem>> DisplayTextMatch(string value, bool useExactMatch)
        => query => useExactMatch
            ? query.With<ContentItemIndex>(index => index.DisplayText == value)
            : query.With<ContentItemIndex>(index => index.DisplayText.Contains(value));

    private static Func<IQuery<ContentItem>, IQuery<ContentItem>> DisplayTextNotMatch(string value, bool useExactMatch)
        => query => useExactMatch
            ? query.With<ContentItemIndex>(index => index.DisplayText != value)
            : query.With<ContentItemIndex>(index => index.DisplayText.NotContains(value));

    private static void AddPhoneNotContainsPredicate(string value, List<Func<IQuery<ContentItem>, IQuery<ContentItem>>> predicates)
    {
        if (!PhoneNumberSearchTerm.TryParse(value, out var searchTerm))
        {
            return;
        }

        predicates.Add(query => query.With(OmnichannelContactPhonePredicates.NotContains(searchTerm)));
    }

    private static bool IsExactE164(string value)
        => PhoneNumberSearchTerm.TryParse(value, out var searchTerm) && searchTerm.IsE164;

    private static bool UseExactMatch(IServiceProvider serviceProvider)
        => serviceProvider.GetService<IOptions<ContentsAdminListFilterOptions>>()?.Value.UseExactMatch ?? false;

    // The same test OmnichannelContactPhoneLookup makes before it reads the phone number index alone.
    private static async ValueTask<bool> IsPhoneNumberIndexReadyAsync(IServiceProvider serviceProvider)
    {
        var backfillState = serviceProvider.GetService<IDocumentManager<OmnichannelBackfillState>>();

        if (backfillState is null)
        {
            return false;
        }

        var state = await backfillState.GetOrCreateImmutableAsync();

        return state.Completed.Contains(ContactPhoneNumberIndexBackfillBackgroundTask.BackfillName);
    }

    private static ValueTask<bool> IsContactOnlyListAsync(IServiceProvider serviceProvider)
    {
        var httpContext = serviceProvider.GetService<IHttpContextAccessor>()?.HttpContext;
        var contentTypeProvider = serviceProvider.GetRequiredService<OmnichannelContentTypeProvider>();

        return OmnichannelContactListScope.IsContactOnlyListAsync(httpContext, contentTypeProvider);
    }
}
