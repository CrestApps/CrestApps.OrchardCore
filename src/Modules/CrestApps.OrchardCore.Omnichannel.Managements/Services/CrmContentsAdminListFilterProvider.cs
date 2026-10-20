using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.ContentManagement;
using OrchardCore.Contents.Services;
using YesSql.Filters.Builders;
using YesSql.Filters.Query;
using YesSql.Filters.Query.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Adds the lead and opportunity terms to the content list search box: <c>lead-status:</c>, <c>source:</c>,
/// <c>list:</c>, <c>owner:</c>, <c>rating:</c> and <c>converted:</c> for leads, and <c>stage:</c>, <c>closed:</c>,
/// <c>won:</c> and <c>account:</c> for opportunities. A term joins the lead or opportunity index, so it narrows the
/// list to leads or opportunities.
/// </summary>
/// <remarks>
/// A term must not reuse a name Orchard Core already registers on the content list (<c>status</c>, <c>sort</c>,
/// <c>type</c>, <c>stereotype</c>, <c>culture</c>), because the last provider to register a name replaces the earlier
/// one. Replacing <c>status</c> drops the term that always limits the list to the latest version of each item, so
/// every content list would then show every archived version as well.
/// </remarks>
internal sealed class CrmContentsAdminListFilterProvider : IContentsAdminListFilterProvider
{
    public const string LeadStatusTermName = LeadListTermPredicates.LeadStatusTermName;

    public void Build(QueryEngineBuilder<ContentItem> builder)
    {
        builder
            .WithNamedTerm(LeadStatusTermName, LeadTerm(LeadStatusTermName))
            .WithNamedTerm(LeadListTermPredicates.ConvertedTermName, LeadTerm(LeadListTermPredicates.ConvertedTermName))
            .WithNamedTerm(LeadListTermPredicates.SourceTermName, LeadTerm(LeadListTermPredicates.SourceTermName))
            .WithNamedTerm(LeadListTermPredicates.ListTermName, LeadTerm(LeadListTermPredicates.ListTermName))
            .WithNamedTerm(LeadListTermPredicates.OwnerTermName, LeadTerm(LeadListTermPredicates.OwnerTermName))
            .WithNamedTerm(LeadListTermPredicates.RatingTermName, LeadTerm(LeadListTermPredicates.RatingTermName))
            .WithNamedTerm("stage", term => term
                .OneCondition(async (value, query, context) =>
                {
                    var stageId = await LeadListTermPredicates.ResolveCatalogIdAsync<OpportunityStage>(
                        value,
                        ((ContentQueryContext)context).ServiceProvider);

                    return query.With<OpportunityIndex>(index => index.StageId == stageId);
                }))
            .WithNamedTerm("closed", term => term
                .OneCondition((value, query) =>
                {
                    var closed = LeadListTermPredicates.ParseBoolean(value);

                    return query.With<OpportunityIndex>(index => index.IsClosed == closed);
                }))
            .WithNamedTerm("won", term => term
                .OneCondition((value, query) =>
                {
                    var won = LeadListTermPredicates.ParseBoolean(value);

                    return query.With<OpportunityIndex>(index => index.IsWon == won);
                }))
            .WithNamedTerm("account", term => term
                .OneCondition((value, query) => query.With<OpportunityIndex>(index => index.AccountContentItemId == value)));
    }

    private static Action<NamedTermEngineBuilder<ContentItem, QueryTermOption<ContentItem>>> LeadTerm(string termName)
        => term => term
            .OneCondition(async (value, query, context) => query.With(await LeadListTermPredicates.BuildAsync(
                termName,
                value,
                ((ContentQueryContext)context).ServiceProvider)));
}
