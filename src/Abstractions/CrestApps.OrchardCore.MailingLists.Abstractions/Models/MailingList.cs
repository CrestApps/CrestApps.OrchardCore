using System.Text.Json.Serialization;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.MailingLists.Models;

/// <summary>
/// A mailing list people sign up to, such as "Weekly news". Its provider decides where the list lives; provider
/// settings and the welcome coupon are kept in <see cref="CatalogItem.Properties"/> by their own display drivers.
/// </summary>
public sealed class MailingList : CatalogItem, INameAwareModel, IAuditedModel
{
    /// <summary>
    /// Gets or sets the YesSql document id. YesSql sets it, so a record saved again after a commit earlier in the same
    /// request (issuing a welcome coupon commits) updates its document instead of adding a second one. It belongs to the
    /// database it is stored in, so it is never written to the record's JSON or to an export.
    /// </summary>
    [JsonIgnore]
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the list, unique in the tenant.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the name people see on the sign-up form and the preferences page.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets what the list sends and how often, shown under the name.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the name of the provider that keeps the list; <see cref="MailingListsConstants.LocalProviderName"/> by default.
    /// </summary>
    public string ProviderName { get; set; } = MailingListsConstants.LocalProviderName;

    /// <summary>
    /// Gets or sets how a sign-up is confirmed.
    /// </summary>
    public MailingListConfirmation Confirmation { get; set; } = MailingListConfirmation.Email;

    /// <summary>
    /// Gets or sets whether a signed-in user whose account e-mail is confirmed is subscribed without a confirmation e-mail.
    /// </summary>
    public bool SkipConfirmationForVerifiedUsers { get; set; } = true;

    /// <summary>
    /// Gets or sets how long a confirmation link works.
    /// </summary>
    public TimeSpan ConfirmationLinkLifetime { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Gets or sets the consent text shown on the sign-up form and kept on each member as evidence of what they agreed to.
    /// </summary>
    public string ConsentText { get; set; }

    /// <summary>
    /// Gets or sets whether a welcome e-mail is sent when a person is subscribed.
    /// </summary>
    public bool SendWelcomeEmail { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the list is offered on the e-mail preferences page and in sign-up widgets.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <inheritdoc/>
    public DateTime CreatedUtc { get; set; }

    /// <inheritdoc/>
    public DateTime? ModifiedUtc { get; set; }

    /// <inheritdoc/>
    public string OwnerId { get; set; }

    /// <inheritdoc/>
    public string Author { get; set; }
}
