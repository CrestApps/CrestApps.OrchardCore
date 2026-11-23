using System.Text.Json.Serialization;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.MailingLists.Models;

/// <summary>
/// One e-mail address on one mailing list, with its state and the evidence of the person's consent. The site keeps
/// this record whatever the list's provider is.
/// </summary>
public sealed class MailingListMember : CatalogItem, ITimestampedModel
{
    /// <summary>
    /// Gets or sets the YesSql document id. YesSql sets it, so a record saved again after a commit earlier in the same
    /// request (issuing a welcome coupon commits) updates its document instead of adding a second one. It belongs to the
    /// database it is stored in, so it is never written to the record's JSON or to an export.
    /// </summary>
    [JsonIgnore]
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the id of the list.
    /// </summary>
    public string ListId { get; set; }

    /// <summary>
    /// Gets or sets the person's contact details as they gave them; the e-mail is required, the name optional.
    /// </summary>
    public ContactInfo Contact { get; set; }

    /// <summary>
    /// Gets or sets the e-mail address in the form it is compared and indexed in (see <c>EmailAddresses.Normalize</c>).
    /// </summary>
    public string NormalizedEmail { get; set; }

    /// <summary>
    /// Gets or sets the member's state on the list.
    /// </summary>
    public MailingListMemberStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when the person proved they own the address (UTC), by a confirmation or claim link, a confirmed
    /// account e-mail or the provider's confirmation.
    /// </summary>
    public DateTime? EmailVerifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the value verification links carry; a new one is made for every link sent, so older links stop working.
    /// </summary>
    public string VerificationNonce { get; set; }

    /// <summary>
    /// Gets or sets how the member was added (see <see cref="MailingListMemberSources"/>).
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the id of the thing the member was added through, such as the sign-up widget's content item id.
    /// </summary>
    public string SourceId { get; set; }

    /// <summary>
    /// Gets or sets the id of the user who signed up while signed in, when there was one.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the evidence of the person's consent.
    /// </summary>
    public MailingListConsentEvidence Consent { get; set; }

    /// <summary>
    /// Gets or sets when the member was last subscribed (UTC).
    /// </summary>
    public DateTime? SubscribedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the member was last unsubscribed or cleaned (UTC).
    /// </summary>
    public DateTime? UnsubscribedUtc { get; set; }

    /// <summary>
    /// Gets or sets why the member left, when they said or the provider told.
    /// </summary>
    public string UnsubscribeReason { get; set; }

    /// <summary>
    /// Gets or sets when <see cref="Status"/> last changed (UTC); a provider change older than this is ignored.
    /// </summary>
    public DateTime StatusChangedUtc { get; set; }

    /// <summary>
    /// Gets or sets the member's id at the list's provider, when the provider has one.
    /// </summary>
    public string ProviderMemberId { get; set; }

    /// <summary>
    /// Gets or sets whether the last change reached the list's provider.
    /// </summary>
    public MailingListSyncState SyncState { get; set; }

    /// <summary>
    /// Gets or sets the last error the provider returned when the last change did not reach it, or why the change waits
    /// (the provider's feature is off, or the provider is not set up).
    /// </summary>
    public string SyncError { get; set; }

    /// <summary>
    /// Gets or sets the id of the welcome coupon code issued to the member, when one was issued.
    /// </summary>
    public string CouponCodeId { get; set; }

    /// <inheritdoc/>
    public DateTime CreatedUtc { get; set; }

    /// <inheritdoc/>
    public DateTime UpdatedUtc { get; set; }
}
