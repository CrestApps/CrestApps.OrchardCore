using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.MailingLists.Core.Indexes;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.MailingLists.Reports;

/// <summary>
/// One row per member of a mailing list, as the list's members page shows it: the list, the address and name, the
/// status and how the person signed up, the dates of the membership, whether the address was verified and whether the
/// provider has the change yet. The evidence of consent (the client address it was given from), the verification nonce
/// and provider errors are never exposed.
/// </summary>
public sealed class MailingListMembersReportDataSet : IndexedReportDataSet<MailingListMember, MailingListMemberIndex>
{
    /// <summary>
    /// The technical name of the data set, stored in report designs.
    /// </summary>
    public const string DataSetName = "MailingListMembers";

    /// <summary>
    /// The name of the field that holds the member's id.
    /// </summary>
    public const string ItemIdField = "ItemId";

    /// <summary>
    /// The name of the field that holds the id of the member's list.
    /// </summary>
    public const string ListIdField = "ListId";

    /// <summary>
    /// The name of the field that holds the name of the member's list.
    /// </summary>
    public const string ListField = "List";

    /// <summary>
    /// The name of the field that holds when the person signed up, the data set's main date.
    /// </summary>
    public const string SignedUpUtcField = "SignedUpUtc";

    private readonly IMailingListManager _lists;
    private Dictionary<string, string> _listNames;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailingListMembersReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session members are read from.</param>
    /// <param name="lists">The list manager, for the names of the lists.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public MailingListMembersReportDataSet(
        ISession session,
        IMailingListManager lists,
        IAuthorizationService authorizationService,
        IStringLocalizer<MailingListMembersReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                DataSetName,
                stringLocalizer["Mailing list members"],
                stringLocalizer["One row per member of a mailing list: the list, address, name, status, how they signed up, the dates of the membership, verification and the provider sync state."]),
            session,
            authorizationService,
            MailingListsConstants.CollectionName,
            MailingListsPermissions.ViewMailingListMembers)
    {
        _lists = lists;

        var S = stringLocalizer;
        var member = S["Member"].Value;
        var dates = S["Dates"].Value;

        AddField(ItemIdField, S["Member ID"], DataFieldType.Text, record => record.ItemId, member).Identifier();
        AddField(ListIdField, S["List ID"], DataFieldType.Text, record => record.ListId, member).Identifier();
        AddField(ListField, S["List"], DataFieldType.Text, record => ListName(record.ListId), member)
            .Description = S["The name people see for the list."].Value;
        AddField("Email", S["E-mail"], DataFieldType.Text, record => string.IsNullOrWhiteSpace(record.Contact?.Email) ? record.NormalizedEmail : record.Contact.Email, member);
        AddField("Name", S["Name"], DataFieldType.Text, record => record.Contact?.Name, member);
        AddField(nameof(MailingListMember.Status), S["Status"], DataFieldType.Text, record => record.Status, member)
            .Description = S["Pending (waiting for confirmation), Subscribed, Unsubscribed or Cleaned (mail bounced)."].Value;
        AddField(nameof(MailingListMember.Source), S["Source"], DataFieldType.Text, record => record.Source, member)
            .Description = S["How the person joined, such as Widget, Admin, Import or Provider."].Value;
        AddField("IsVerified", S["Verified"], DataFieldType.Boolean, record => record.EmailVerifiedUtc.HasValue, member)
            .Description = S["Whether the person proved they own the address, by a confirmation link or a confirmed account."].Value;
        AddField(nameof(MailingListMember.SyncState), S["Sync state"], DataFieldType.Text, record => record.SyncState, member)
            .Description = S["Whether the list's provider has the latest change: Synced, Pending or Failed."].Value;
        AddField("HasCoupon", S["Got a welcome coupon"], DataFieldType.Boolean, record => !string.IsNullOrEmpty(record.CouponCodeId), member);
        AddField(nameof(MailingListMember.UserId), S["User ID"], DataFieldType.Text, record => record.UserId, member).Identifier(ReportDataReferences.User());

        AddField(SignedUpUtcField, S["Signed up"], DataFieldType.DateTime, record => record.CreatedUtc, dates);
        AddField(nameof(MailingListMember.SubscribedUtc), S["Subscribed"], DataFieldType.DateTime, record => record.SubscribedUtc, dates);
        AddField(nameof(MailingListMember.UnsubscribedUtc), S["Unsubscribed"], DataFieldType.DateTime, record => record.UnsubscribedUtc, dates);
        AddField(nameof(MailingListMember.EmailVerifiedUtc), S["Verified on"], DataFieldType.DateTime, record => record.EmailVerifiedUtc, dates);
        AddField(nameof(MailingListMember.StatusChangedUtc), S["Status changed"], DataFieldType.DateTime, record => record.StatusChangedUtc, dates);

        CompleteDescription();

        // Members are read newest first by their index rows; a date range on when they signed up is filtered row by row.
        Descriptor.DefaultDateField = SignedUpUtcField;
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<MailingListMemberIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<MailingListMemberIndex, string>>>(StringComparer.Ordinal)
        {
            [ItemIdField] = index => index.ItemId,
            [ListIdField] = index => index.ListId,
            [nameof(MailingListMember.UserId)] = index => index.UserId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<MailingListMemberIndex> columns)
    {
        columns
            .Text(ListIdField, nameof(MailingListMemberIndex.ListId))
            .Enum(nameof(MailingListMember.Status), index => index.Status)
            .Enum(nameof(MailingListMember.SyncState), index => index.SyncState);
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, string> GetAggregateColumns()
    {
        // The index keeps the source cut to its column's length, so it is grouped from the members themselves.
        return base.GetAggregateColumns()
            .Where(column => !string.Equals(column.Key, nameof(MailingListMember.Source), StringComparison.Ordinal))
            .ToDictionary(column => column.Key, column => column.Value, StringComparer.Ordinal);
    }

    /// <inheritdoc/>
    protected override async IAsyncEnumerable<MailingListMember> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // The lists are few; their names are read once, before the first member.
        _listNames ??= (await _lists.GetAllAsync(cancellationToken))
            .Where(list => !string.IsNullOrEmpty(list.ItemId))
            .GroupBy(list => list.ItemId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().DisplayText ?? group.First().Name, StringComparer.Ordinal);

        await foreach (var member in base.LoadAsync(query, cancellationToken))
        {
            yield return member;
        }
    }

    private string ListName(string listId)
        => listId is not null && _listNames is not null && _listNames.TryGetValue(listId, out var name) ? name : null;
}
