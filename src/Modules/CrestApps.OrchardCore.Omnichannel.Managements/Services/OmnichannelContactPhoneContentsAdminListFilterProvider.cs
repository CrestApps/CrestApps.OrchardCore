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
            // contact content types, the free-text box also matches contacts by phone number, so a plain digits
            // entry finds the contact whether the agent recalls the name or the number. On every other content
            // list this behaves exactly like the framework default (a DisplayText match).
            .WithDefaultTerm(ContentsAdminListFilterOptions.DefaultTermName, term => term
                .ManyCondition(
                    async (value, query, context) =>
                    {
                        var serviceProvider = ((ContentQueryContext)context).ServiceProvider;
                        var useExactMatch = UseExactMatch(serviceProvider);

                        // Always match by phone when the term is an exact E.164 number - it is unambiguous, so it
                        // should find the contact on any list, even when the list is not scoped to contact types.
                        if (!IsExactE164(value) && !await IsContactOnlyListAsync(serviceProvider))
                        {
                            return DisplayTextMatch(value, useExactMatch)(query);
                        }

                        var predicates = new List<Func<IQuery<ContentItem>, IQuery<ContentItem>>>
                        {
                            DisplayTextMatch(value, useExactMatch),
                        };

                        AddPhoneContainsPredicate(value, predicates);

                        return query.Any(predicates.ToArray());
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

    private static Func<IQuery<ContentItem>, IQuery<ContentItem>> DisplayTextMatch(string value, bool useExactMatch)
        => query => useExactMatch
            ? query.With<ContentItemIndex>(index => index.DisplayText == value)
            : query.With<ContentItemIndex>(index => index.DisplayText.Contains(value));

    private static Func<IQuery<ContentItem>, IQuery<ContentItem>> DisplayTextNotMatch(string value, bool useExactMatch)
        => query => useExactMatch
            ? query.With<ContentItemIndex>(index => index.DisplayText != value)
            : query.With<ContentItemIndex>(index => index.DisplayText.NotContains(value));

    private static void AddPhoneContainsPredicate(string value, List<Func<IQuery<ContentItem>, IQuery<ContentItem>>> predicates)
    {
        if (!PhoneNumberSearchTerm.TryParse(value, out var searchTerm))
        {
            return;
        }

        predicates.Add(query => query.With(OmnichannelContactPhonePredicates.Match(searchTerm, PhoneNumberMatchType.Contains)));
    }

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
