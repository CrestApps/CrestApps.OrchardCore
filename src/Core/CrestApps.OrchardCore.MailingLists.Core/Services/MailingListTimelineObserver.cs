using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Timeline;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.MailingLists.Core.Services;

/// <summary>
/// Writes every change of a member on its timeline (record kind <see cref="MailingListsConstants.MemberRecordKind"/>),
/// seen by the people who may see the list's members, and deletes the timeline when the member is erased.
/// </summary>
public sealed class MailingListTimelineObserver : MailingListMemberObserverBase
{
    private readonly ITimelineService _timeline;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailingListTimelineObserver"/> class.
    /// </summary>
    /// <param name="timeline">The timeline service.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public MailingListTimelineObserver(
        ITimelineService timeline,
        IClock clock,
        IStringLocalizer<MailingListTimelineObserver> stringLocalizer)
    {
        _timeline = timeline;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task SignedUpAsync(MailingListMemberContext context)
        => context.Member.Status == MailingListMemberStatus.Pending
            ? AddAsync(context, S["Signed up; waiting for the confirmation of the address. Source: {0}.", Source(context)])
            : Task.CompletedTask;

    /// <inheritdoc/>
    public override Task SubscribedAsync(MailingListMemberContext context)
        => AddAsync(context, S["Subscribed. Source: {0}.", Source(context)]);

    /// <inheritdoc/>
    public override Task VerifiedAsync(MailingListMemberContext context)
        => AddAsync(context, context.IssuedCoupon is null ? S["The address was verified."] : S["The address was verified; the welcome code {0} was given.", context.IssuedCoupon.Code]);

    /// <inheritdoc/>
    public override Task UnsubscribedAsync(MailingListMemberContext context)
        => AddAsync(context, context.Member.Status == MailingListMemberStatus.Cleaned
            ? S["Cleaned: mail to the address bounces. Source: {0}.", Source(context)]
            : S["Unsubscribed. Source: {0}.", Source(context)]);

    /// <inheritdoc/>
    public override async Task ErasedAsync(MailingListMemberContext context)
        => await _timeline.DeleteAllAsync(Subject(context.Member));

    private async Task AddAsync(MailingListMemberContext context, string body)
        => await _timeline.AddAsync(new TimelineEntry
        {
            Subject = Subject(context.Member),
            Kind = TimelineConstants.Kinds.Event,
            Visibility = TimelineVisibility.Internal,
            Body = body,
            CreatedUtc = _clock.UtcNow,
        });

    private static RecordRef Subject(MailingListMember member)
        => new(MailingListsConstants.MemberRecordKind, member.ItemId);

    private static string Source(MailingListMemberContext context)
        => string.IsNullOrWhiteSpace(context.Source) ? "-" : context.Source;
}
