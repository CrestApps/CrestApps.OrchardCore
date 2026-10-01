using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Contents.Services;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using YesSql;
using YesSql.Filters.Query;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Adds the lead and opportunity terms to the content list search box: <c>status:</c>, <c>source:</c>,
/// <c>list:</c>, <c>owner:</c>, <c>rating:</c> and <c>converted:</c> for leads, and <c>stage:</c>, <c>closed:</c>,
/// <c>won:</c> and <c>account:</c> for opportunities. A term joins the lead or opportunity index, so it narrows the
/// list to leads or opportunities.
/// </summary>
internal sealed class CrmContentsAdminListFilterProvider : IContentsAdminListFilterProvider
{
    public void Build(QueryEngineBuilder<ContentItem> builder)
    {
        builder
            .WithNamedTerm("status", term => term
                .OneCondition(async (value, query, context) =>
                {
                    var statusId = await ResolveCatalogIdAsync<LeadStatus>(value, context);

                    return query.With<LeadIndex>(index => index.StatusId == statusId);
                }))
            .WithNamedTerm("converted", term => term
                .OneCondition((value, query) =>
                {
                    var converted = ParseBoolean(value);

                    return query.With<LeadIndex>(index => index.IsConverted == converted);
                }))
            .WithNamedTerm("source", term => term
                .OneCondition(async (value, query, context) =>
                {
                    var sourceId = await ResolveLeadSourceIdAsync(value, context);

                    return query.With<LeadIndex>(index => index.SourceId == sourceId);
                }))
            .WithNamedTerm("list", term => term
                .OneCondition((value, query) => query.With<LeadIndex>(index => index.ListName == value)))
            .WithNamedTerm("owner", term => term
                .OneCondition(async (value, query, context) =>
                {
                    var ownerId = await ResolveUserIdAsync(value, context);

                    return query.With<LeadIndex>(index => index.OwnerId == ownerId);
                }))
            .WithNamedTerm("rating", term => term
                .OneCondition(async (value, query, context) =>
                {
                    var ratings = ((ContentQueryContext)context).ServiceProvider.GetRequiredService<LeadRatingProvider>();
                    var rating = await ratings.NormalizeAsync(value) ?? value;

                    return query.With<LeadIndex>(index => index.Rating == rating);
                }))
            .WithNamedTerm("stage", term => term
                .OneCondition(async (value, query, context) =>
                {
                    var stageId = await ResolveCatalogIdAsync<OpportunityStage>(value, context);

                    return query.With<OpportunityIndex>(index => index.StageId == stageId);
                }))
            .WithNamedTerm("closed", term => term
                .OneCondition((value, query) =>
                {
                    var closed = ParseBoolean(value);

                    return query.With<OpportunityIndex>(index => index.IsClosed == closed);
                }))
            .WithNamedTerm("won", term => term
                .OneCondition((value, query) =>
                {
                    var won = ParseBoolean(value);

                    return query.With<OpportunityIndex>(index => index.IsWon == won);
                }))
            .WithNamedTerm("account", term => term
                .OneCondition((value, query) => query.With<OpportunityIndex>(index => index.AccountContentItemId == value)));
    }

    // A term names a status or stage the way people read it; an identifier works too, for links built by code.
    private static async ValueTask<string> ResolveCatalogIdAsync<T>(string value, object context)
        where T : CrestApps.Core.Models.CatalogItem, CrestApps.Core.INameAwareModel
    {
        var catalog = ((ContentQueryContext)context).ServiceProvider.GetRequiredService<INamedCatalog<T>>();

        var entry = await catalog.FindByNameAsync(value);

        return entry?.ItemId ?? value;
    }

    // People type the name of a lead source; the index stores the id of its content item. An id works too.
    private static async ValueTask<string> ResolveLeadSourceIdAsync(string value, object context)
    {
        var sources = ((ContentQueryContext)context).ServiceProvider.GetRequiredService<LeadSourceProvider>();

        return await sources.FindIdAsync(value) ?? value;
    }

    // People type a user name; the index stores the user id. An id works too, for links built by code.
    private static async ValueTask<string> ResolveUserIdAsync(string value, object context)
    {
        var userManager = ((ContentQueryContext)context).ServiceProvider.GetRequiredService<UserManager<IUser>>();

        return await userManager.FindByNameAsync(value) is User user ? user.UserId : value;
    }

    private static bool ParseBoolean(string value)
        => value?.Trim().ToLowerInvariant() is "true" or "yes" or "1" or "y";
}
