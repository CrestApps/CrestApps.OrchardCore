using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.ContentManagement;
using OrchardCore.Contents.Services;
using YesSql;
using YesSql.Filters.Query;

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
                .OneCondition((value, query) => ApplyFilter(value, PhoneNumberMatchType.EndsWith, query)));
    }

    private static IQuery<ContentItem> ApplyFilter(
        string value,
        PhoneNumberMatchType matchType,
        IQuery<ContentItem> query)
    {
        if (!PhoneNumberSearchTerm.TryParse(value, out var searchTerm))
        {
            return query.With<OmnichannelContactPhoneIndex>(index => index.ContentItemId == string.Empty);
        }

        if (searchTerm.IsE164)
        {
            return matchType switch
            {
                PhoneNumberMatchType.Exact => query.With<OmnichannelContactPhoneIndex>(index =>
                    index.E164PrimaryCellPhoneNumber == searchTerm.Value ||
                    index.E164PrimaryHomePhoneNumber == searchTerm.Value),
                PhoneNumberMatchType.BeginsWith => query.With<OmnichannelContactPhoneIndex>(index =>
                    index.E164PrimaryCellPhoneNumber.StartsWith(searchTerm.Value) ||
                    index.E164PrimaryHomePhoneNumber.StartsWith(searchTerm.Value)),
                PhoneNumberMatchType.EndsWith => query.With<OmnichannelContactPhoneIndex>(index =>
                    index.E164PrimaryCellPhoneNumber.EndsWith(searchTerm.Value) ||
                    index.E164PrimaryHomePhoneNumber.EndsWith(searchTerm.Value)),
                PhoneNumberMatchType.Contains => query.With<OmnichannelContactPhoneIndex>(index =>
                    index.E164PrimaryCellPhoneNumber.Contains(searchTerm.Value) ||
                    index.E164PrimaryHomePhoneNumber.Contains(searchTerm.Value)),
                _ => throw new ArgumentOutOfRangeException(nameof(matchType), matchType, "Unsupported phone number match type."),
            };
        }

        return matchType switch
        {
            PhoneNumberMatchType.Exact => query.With<OmnichannelContactPhoneIndex>(index =>
                index.NationalPrimaryCellPhoneNumber == searchTerm.Value ||
                index.NationalPrimaryHomePhoneNumber == searchTerm.Value),
            PhoneNumberMatchType.BeginsWith => query.With<OmnichannelContactPhoneIndex>(index =>
                index.NationalPrimaryCellPhoneNumber.StartsWith(searchTerm.Value) ||
                index.NationalPrimaryHomePhoneNumber.StartsWith(searchTerm.Value)),
            PhoneNumberMatchType.EndsWith => query.With<OmnichannelContactPhoneIndex>(index =>
                index.NationalPrimaryCellPhoneNumber.EndsWith(searchTerm.Value) ||
                index.NationalPrimaryHomePhoneNumber.EndsWith(searchTerm.Value)),
            PhoneNumberMatchType.Contains => query.With<OmnichannelContactPhoneIndex>(index =>
                index.NationalPrimaryCellPhoneNumber.Contains(searchTerm.Value) ||
                index.NationalPrimaryHomePhoneNumber.Contains(searchTerm.Value)),
            _ => throw new ArgumentOutOfRangeException(nameof(matchType), matchType, "Unsupported phone number match type."),
        };
    }
}
