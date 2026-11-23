using System.Security.Claims;
using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.Emails;
using CrestApps.OrchardCore.MailingLists.Core.Services;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.MailingLists.ContentTransfer;

/// <summary>
/// Imports the members of mailing lists from a file through ContentTransfer, one row per list and address. Every row is
/// signed up through <see cref="IMailingListMemberService.SubscribeAsync"/> as an import: silently, so no e-mail and no
/// welcome coupon goes out, with the row's status. An address that unsubscribed from the list, bounced, or asked to stop
/// all marketing e-mail is never subscribed by an import; its row is refused with the reason.
/// </summary>
internal sealed class MailingListMembersImportTarget : IContentTransferRecordImporter
{
    /// <summary>
    /// The name imports of mailing list members are filed under.
    /// </summary>
    public const string TargetName = "MailingLists.MailingListMember";

    private const string ListColumn = "List";
    private const string EmailColumn = "Email";
    private const string NameColumn = "Name";
    private const string StatusColumn = "Status";

    private static readonly string[] _listNames = ["ListId", "ListName", "MailingList"];
    private static readonly string[] _emailNames = ["EmailAddress", "E-mail"];
    private static readonly string[] _nameNames = ["FullName"];

    private readonly IMailingListManager _lists;
    private readonly IMailingListMemberStore _members;
    private readonly IMailingListMemberService _memberService;
    private readonly IConsentService _consent;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailingListMembersImportTarget"/> class.
    /// </summary>
    /// <param name="lists">The list manager, to find each row's list by id or name.</param>
    /// <param name="members">The member store, to find an address that unsubscribed.</param>
    /// <param name="memberService">The member service every row is signed up through.</param>
    /// <param name="consent">The consent service, to find an address that stopped all marketing e-mail.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public MailingListMembersImportTarget(
        IMailingListManager lists,
        IMailingListMemberStore members,
        IMailingListMemberService memberService,
        IConsentService consent,
        IStringLocalizer<MailingListMembersImportTarget> stringLocalizer)
    {
        _lists = lists;
        _members = members;
        _memberService = memberService;
        _consent = consent;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string Name => TargetName;

    /// <inheritdoc/>
    public string DisplayName => S["Mailing list members"];

    /// <inheritdoc/>
    public bool IsContentType => false;

    /// <inheritdoc/>
    public Task<bool> AuthorizeAsync(ClaimsPrincipal user, IAuthorizationService authorizationService)
    {
        ArgumentNullException.ThrowIfNull(authorizationService);

        return authorizationService.AuthorizeAsync(user, MailingListsPermissions.ManageMailingListMembers);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyCollection<ImportColumn>> GetColumnsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<ImportColumn>>(
        [
            new ImportColumn
            {
                Name = ListColumn,
                IsRequired = true,
                AdditionalNames = _listNames,
                Description = S["The mailing list the address joins: its technical name or its ID."],
            },
            new ImportColumn
            {
                Name = EmailColumn,
                IsRequired = true,
                AdditionalNames = _emailNames,
                Description = S["The e-mail address. An address already on the list stays as it is; an address that unsubscribed from the list, or bounced, is not imported."],
            },
            new ImportColumn
            {
                Name = NameColumn,
                AdditionalNames = _nameNames,
                Description = S["The person's name, optional."],
            },
            new ImportColumn
            {
                Name = StatusColumn,
                Description = S["Subscribed (the default when empty) or Pending. The import sends no e-mail: a pending member gets a confirmation e-mail only when you send one from the list's members page, and is removed when it is not confirmed in time."],
                ValidValues = [nameof(MailingListMemberStatus.Subscribed), nameof(MailingListMemberStatus.Pending)],
            },
        ]);

    /// <inheritdoc/>
    public async Task ImportAsync(ContentTransferImportBatch batch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var lists = new Dictionary<string, MailingList>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in batch.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var listValue = row.Get(ListColumn, _listNames);

            if (listValue is null)
            {
                row.Fail(S["Name the mailing list the address joins."], S["Did not pass validation"]);

                continue;
            }

            if (!lists.TryGetValue(listValue, out var list))
            {
                list = await _lists.FindByIdAsync(listValue, cancellationToken) ?? await _lists.FindByNameAsync(listValue, cancellationToken);
                lists[listValue] = list;
            }

            if (list is null)
            {
                row.Fail(S["No mailing list has the name or ID '{0}'.", listValue], S["Did not pass validation"]);

                continue;
            }

            var email = row.Get(EmailColumn, _emailNames);

            if (!EmailAddresses.IsValid(email))
            {
                row.Fail(S["The e-mail address '{0}' is not valid.", email ?? string.Empty], S["Did not pass validation"]);

                continue;
            }

            if (!TryReadStatus(row.Get(StatusColumn), out var status))
            {
                row.Fail(S["The status is Subscribed or Pending."], S["Did not pass validation"]);

                continue;
            }

            // The member service would move an unsubscribed address back onto the list; only the person may do that.
            var normalized = EmailAddresses.Normalize(email);
            var existing = await _members.FindAsync(list.ItemId, normalized, cancellationToken);

            if (existing?.Status == MailingListMemberStatus.Unsubscribed)
            {
                row.Fail(S["The address unsubscribed from the list. An import never subscribes it again; only the person can sign up again."], S["Unsubscribed"]);

                continue;
            }

            if (existing?.Status == MailingListMemberStatus.Cleaned)
            {
                row.Fail(S["Mail to the address bounced, so it was removed from the list. Only the person can sign up again."], S["Unsubscribed"]);

                continue;
            }

            // A person's "stop all marketing e-mail" is undone only by their own sign-up, which proves it is them; a
            // file proves nothing. The member service checks the opt-outs of every purpose itself.
            var marketing = await _consent.GetAsync(new ConsentQuery
            {
                Channel = ConsentChannels.Email,
                Address = normalized,
                Purpose = ConsentPurposes.Marketing,
            }, cancellationToken);

            if (marketing.HasOptedOut)
            {
                row.Fail(S["The address asked to stop all marketing e-mail. An import never subscribes it; only the person can sign up again."], S["Unsubscribed"]);

                continue;
            }

            var result = await _memberService.SubscribeAsync(new MailingListSubscribeRequest
            {
                ListId = list.ItemId,
                Email = email,
                Name = row.Get(NameColumn, _nameNames),
                Source = MailingListMemberSources.Import,
                SourceId = batch.Entry.EntryId,
                ConsentForm = S["Imported from the file {0}.", batch.Entry.UploadedFileName ?? string.Empty],
                Silent = true,
                Status = status,
            }, cancellationToken);

            if (!result.Succeeded)
            {
                row.Fail(S["The address could not be added: {0}", result.Error ?? string.Empty], S["Not imported"]);
            }
        }
    }

    private static bool TryReadStatus(string value, out MailingListMemberStatus status)
    {
        if (string.IsNullOrEmpty(value) || string.Equals(value, nameof(MailingListMemberStatus.Subscribed), StringComparison.OrdinalIgnoreCase))
        {
            status = MailingListMemberStatus.Subscribed;

            return true;
        }

        if (string.Equals(value, nameof(MailingListMemberStatus.Pending), StringComparison.OrdinalIgnoreCase))
        {
            status = MailingListMemberStatus.Pending;

            return true;
        }

        status = default;

        return false;
    }
}
