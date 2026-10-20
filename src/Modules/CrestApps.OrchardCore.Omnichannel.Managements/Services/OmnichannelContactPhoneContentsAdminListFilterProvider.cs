using System.Linq.Expressions;
using System.Reflection;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Contents;
using OrchardCore.Contents.Services;
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
            // contact content types, a search that looks like a phone number also matches contacts whose number
            // starts with it, so the agent finds the contact whether they recall the name or the number. On every
            // other content list this behaves exactly like the framework default (a DisplayText match).
            .WithDefaultTerm(ContentsAdminListFilterOptions.DefaultTermName, term => term
                .ManyCondition(
                    async (value, query, context) =>
                    {
                        var serviceProvider = ((ContentQueryContext)context).ServiceProvider;
                        var useExactMatch = UseExactMatch(serviceProvider);

                        // Always match by phone when the term is an exact E.164 number - it is unambiguous, so it
                        // should find the contact on any list, even when the list is not scoped to contact types.
                        if (!TryParsePhoneLikeTerm(value, out var searchTerm) ||
                            (!searchTerm.IsE164 && !await IsContactOnlyListAsync(serviceProvider)))
                        {
                            return DisplayTextMatch(value, useExactMatch)(query);
                        }

                        return query.With(DisplayTextOrPhoneMatch(value, useExactMatch, searchTerm));
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
    /// Matches a content item whose display text matches the search, or whose contact index row holds a number that
    /// starts with it.
    /// </summary>
    /// <remarks>
    /// The number is compared in a subquery on the contact index (<c>DocumentId IN (SELECT DocumentId FROM ...)</c>)
    /// rather than by joining that index to the list. With the join, the "or" spanned two tables, so the database
    /// joined every contact of the list to its index row before it could test either half, on every search and again
    /// for the count of results. The subquery seeks the phone number indexes once and the list reads only the content
    /// item index.
    /// </remarks>
    internal static Expression<Func<ContentItemIndex, bool>> DisplayTextOrPhoneMatch(
        string value,
        bool useExactMatch,
        PhoneNumberSearchTerm searchTerm)
    {
        Expression<Func<ContentItemIndex, bool>> displayText = useExactMatch
            ? index => index.DisplayText == value
            : index => index.DisplayText.Contains(value);

        Expression<Func<OmnichannelContactIndex, object>> documentId = contact => contact.DocumentId;

        var parameter = displayText.Parameters[0];
        var hasMatchingNumber = Expression.Call(
            _isInContactIndexMethod,
            Expression.Convert(Expression.Property(parameter, nameof(ContentItemIndex.DocumentId)), typeof(object)),
            Expression.Quote(documentId),
            Expression.Quote(OmnichannelContactPhonePredicates.SearchBoxMatch(searchTerm)));

        return Expression.Lambda<Func<ContentItemIndex, bool>>(
            Expression.OrElse(displayText.Body, hasMatchingNumber),
            parameter);
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

    private static ValueTask<bool> IsContactOnlyListAsync(IServiceProvider serviceProvider)
    {
        var httpContext = serviceProvider.GetService<IHttpContextAccessor>()?.HttpContext;
        var contentTypeProvider = serviceProvider.GetRequiredService<OmnichannelContentTypeProvider>();

        return OmnichannelContactListScope.IsContactOnlyListAsync(httpContext, contentTypeProvider);
    }
}
