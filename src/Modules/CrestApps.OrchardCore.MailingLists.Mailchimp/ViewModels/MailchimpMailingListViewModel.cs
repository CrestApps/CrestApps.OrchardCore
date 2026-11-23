using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.MailingLists.Mailchimp.ViewModels;

/// <summary>
/// The Mailchimp card of the mailing list editor.
/// </summary>
public class MailchimpMailingListViewModel
{
    /// <summary>
    /// Gets or sets whether the card was on the posted form; a list switched to Mailchimp in the same post has no card yet.
    /// </summary>
    public bool Rendered { get; set; }

    /// <summary>
    /// Gets or sets the id of the Mailchimp audience.
    /// </summary>
    public string AudienceId { get; set; }

    /// <summary>
    /// Gets or sets the tags added to each member, separated by commas.
    /// </summary>
    public string Tags { get; set; }

    /// <summary>
    /// Gets or sets whether the member's name is sent in the FNAME and LNAME merge fields.
    /// </summary>
    public bool SendNameMergeFields { get; set; }

    /// <summary>
    /// Gets or sets the tag of the merge field the welcome coupon code is written to.
    /// </summary>
    public string CouponMergeTag { get; set; }

    /// <summary>
    /// Gets or sets the list's id, for the card's actions.
    /// </summary>
    [BindNever]
    public string ListId { get; set; }

    /// <summary>
    /// Gets or sets the audiences to choose from; empty when they could not be read.
    /// </summary>
    [BindNever]
    public List<SelectListItem> Audiences { get; set; } = [];

    /// <summary>
    /// Gets or sets why the audiences could not be read, when they could not.
    /// </summary>
    [BindNever]
    public string AudiencesError { get; set; }

    /// <summary>
    /// Gets or sets whether Mailchimp is set up, so the card's actions can run.
    /// </summary>
    [BindNever]
    public bool IsConfigured { get; set; }

    /// <summary>
    /// Gets or sets whether the list's confirmation is the provider's, so Mailchimp sends the confirmation e-mail.
    /// </summary>
    [BindNever]
    public bool ProviderConfirms { get; set; }

    /// <summary>
    /// Gets or sets whether the list's welcome coupon is delivered by the provider, through the coupon merge field.
    /// </summary>
    [BindNever]
    public bool CouponDeliveredByProvider { get; set; }

    /// <summary>
    /// Gets or sets whether a webhook is registered.
    /// </summary>
    [BindNever]
    public bool WebhookRegistered { get; set; }

    /// <summary>
    /// Gets or sets whether the registered webhook's requests are signed.
    /// </summary>
    [BindNever]
    public bool WebhookSigned { get; set; }

    /// <summary>
    /// Gets or sets when the webhook was registered, in the user's time zone.
    /// </summary>
    [BindNever]
    public DateTimeOffset? WebhookRegisteredLocal { get; set; }
}
