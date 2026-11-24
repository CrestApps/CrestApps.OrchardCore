using CrestApps.Core.AI;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Marketing.AI;
using CrestApps.OrchardCore.Marketing.Models;
using CrestApps.OrchardCore.Marketing.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Marketing;

/// <summary>
/// Registers the marketing AI tools when the AI feature is on: the audience size of a segment, a test email, sending and
/// canceling a campaign. Segments and campaigns are listed and read through the generic record tools (kinds
/// <c>marketingSegment</c> and <c>marketingCampaign</c>), read-only, because their editors are admin controllers, not
/// display drivers the record tools can replay.
/// </summary>
[Feature(MarketingConstants.Feature)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class AIToolsStartup : StartupBase
{
    private const int BodyExcerptLength = 500;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolsStartup(IStringLocalizer<AIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<GetMarketingSegmentTool>(GetMarketingSegmentTool.TheName)
            .WithTitle(S["Get Marketing Segment"])
            .WithDescription(S["Returns one segment with its rules and two counts: customers in the segment and customers who can be emailed (accept marketing). Use it to size an audience before sending. Addresses that opted out are also skipped when a campaign is sent, so fewer may receive it."])
            .WithCategory(S["Marketing"])
            .Selectable();

        services.AddCoreAITool<SendMarketingCampaignTestTool>(SendMarketingCampaignTestTool.TheName)
            .WithTitle(S["Send Marketing Campaign Test"])
            .WithDescription(S["Sends one test email of a campaign, rendered with a sample customer from its segment, to the address given. Use it to check the content before sending the campaign. An address that opted out of email is refused."])
            .WithCategory(S["Marketing"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<SendMarketingCampaignTool>(SendMarketingCampaignTool.TheName)
            .WithTitle(S["Send Marketing Campaign"])
            .WithDescription(S["Starts sending a draft campaign to every reachable customer in its segment, or schedules it when its send time is in the future. Opted-out addresses are skipped. Requires the site's email and base URL to be set up; emails go out in batches and cannot be recalled."])
            .WithCategory(S["Marketing"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<CancelMarketingCampaignTool>(CancelMarketingCampaignTool.TheName)
            .WithTitle(S["Cancel Marketing Campaign"])
            .WithDescription(S["Cancels a scheduled or sending campaign and stops its outreach run. Emails already sent stay sent, and a canceled campaign cannot be restarted."])
            .WithCategory(S["Marketing"])
            .Selectable();

        services.AddAIRecordKind<Segment>("marketingSegment", kind => kind
            .WithTitle(S["Marketing segment"])
            .WithDescription(S["a group of the store's customers chosen by what they bought (order count, amount spent, days since the last or first order, country), which email campaigns are sent to; getMarketingSegment gives its audience size"])
            .WithPermission(MarketingPermissions.ManageMarketing)
            .FindUsing((serviceProvider, id, cancellationToken) => serviceProvider.GetRequiredService<ISegmentStore>().FindByIdAsync(id, cancellationToken))
            .ListUsing(ListSegmentsAsync)
            .WithProjection(segment => new
            {
                id = segment.ItemId,
                name = segment.Name,
                description = segment.Description,
                rules = segment.Rules is { } rules
                    ? new
                    {
                        minOrders = rules.MinOrders,
                        maxOrders = rules.MaxOrders,
                        minSpent = rules.MinSpent,
                        maxSpent = rules.MaxSpent,
                        lastOrderAtLeastDaysAgo = rules.LastOrderAtLeastDaysAgo,
                        lastOrderWithinDays = rules.LastOrderWithinDays,
                        firstOrderWithinDays = rules.FirstOrderWithinDays,
                        country = rules.Country,
                    }
                    : null,
            })
            .ReadOnly());

        services.AddAIRecordKind<Campaign>("marketingCampaign", kind => kind
            .WithTitle(S["Marketing campaign"])
            .WithDescription(S["an email campaign to a segment's customers, with its subject, the start of its Liquid body, its status (draft, scheduled, sending, sent, canceled) and how many emails were sent, skipped and failed; send, test and cancel it with sendMarketingCampaign, sendMarketingCampaignTest and cancelMarketingCampaign"])
            .WithPermission(MarketingPermissions.ManageMarketing)
            .FindUsing((serviceProvider, id, cancellationToken) => serviceProvider.GetRequiredService<ICampaignStore>().FindByIdAsync(id, cancellationToken))
            .ListUsing(ListCampaignsAsync)
            .WithProjection(campaign => new
            {
                id = campaign.ItemId,
                name = campaign.Name,
                segmentId = campaign.SegmentId,
                subject = campaign.Subject,
                bodyExcerpt = campaign.Body is { Length: > BodyExcerptLength } body ? body[..BodyExcerptLength] : campaign.Body,
                bodyLength = campaign.Body?.Length ?? 0,
                status = campaign.Status.ToString(),
                hasSendTime = campaign.ScheduledUtc.HasValue,
                sent = campaign.Sent,
                skipped = campaign.Skipped,
                failed = campaign.Failed,
                createdBy = campaign.CreatedBy,
            })
            .ReadOnly());
    }

    private static async ValueTask<PageResult<Segment>> ListSegmentsAsync(IServiceProvider services, AIRecordQuery query, CancellationToken cancellationToken)
    {
        var (count, segments) = await services.GetRequiredService<ISegmentStore>().PageAsync(query.Text, (query.Page - 1) * query.PageSize, query.PageSize, cancellationToken);

        return new PageResult<Segment> { Count = count, Entries = segments };
    }

    private static async ValueTask<PageResult<Campaign>> ListCampaignsAsync(IServiceProvider services, AIRecordQuery query, CancellationToken cancellationToken)
    {
        var (count, campaigns) = await services.GetRequiredService<ICampaignStore>().PageAsync(query.Text, (query.Page - 1) * query.PageSize, query.PageSize, cancellationToken);

        return new PageResult<Campaign> { Count = count, Entries = campaigns };
    }
}
