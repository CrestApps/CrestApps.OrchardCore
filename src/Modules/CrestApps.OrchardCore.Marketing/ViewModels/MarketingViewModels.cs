using CrestApps.OrchardCore.Marketing.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Marketing.ViewModels;

public class SegmentsIndexViewModel
{
    public AdminListContext<Segment> List { get; set; }

    public string Search { get; set; }
}

public class SegmentEditViewModel
{
    [BindNever]
    public string ItemId { get; set; }

    public bool IsNew
        => string.IsNullOrEmpty(ItemId);

    public string Name { get; set; }

    public string Description { get; set; }

    public int? MinOrders { get; set; }

    public int? MaxOrders { get; set; }

    /// <summary>
    /// Gets or sets the least spent, as text, so a page in a language with a decimal comma reads "50.5" right.
    /// </summary>
    public string MinSpent { get; set; }

    public string MaxSpent { get; set; }

    public int? LastOrderAtLeastDaysAgo { get; set; }

    public int? LastOrderWithinDays { get; set; }

    public int? FirstOrderWithinDays { get; set; }

    public string Country { get; set; }

    [BindNever]
    public string StoreCurrency { get; set; }

    [BindNever]
    public int? Count { get; set; }

    [BindNever]
    public int? Reachable { get; set; }
}

public class CampaignsIndexViewModel
{
    public AdminListContext<Campaign> List { get; set; }

    public string Search { get; set; }
}

public class CampaignEditViewModel
{
    [BindNever]
    public string ItemId { get; set; }

    public bool IsNew
        => string.IsNullOrEmpty(ItemId);

    public string Name { get; set; }

    /// <summary>
    /// Gets or sets who the campaign goes to: a segment or a mailing list.
    /// </summary>
    public string AudienceKind { get; set; } = CampaignAudienceKinds.Segment;

    public string SegmentId { get; set; }

    /// <summary>
    /// Gets or sets the mailing list the campaign goes to, when it goes to one.
    /// </summary>
    public string MailingListId { get; set; }

    public string Subject { get; set; }

    public string Body { get; set; }

    /// <summary>
    /// Gets or sets when to start sending, in the site's time zone, or none for at once.
    /// </summary>
    public DateTime? ScheduledLocal { get; set; }

    public string TestEmail { get; set; }

    [BindNever]
    public Campaign Campaign { get; set; }

    [BindNever]
    public IList<SelectListItem> Segments { get; set; } = [];

    /// <summary>
    /// Gets or sets the enabled mailing lists the campaign can go to, while the Mailing Lists feature is on.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> MailingLists { get; set; } = [];

    /// <summary>
    /// Gets or sets whether a campaign can go to a mailing list: the Mailing Lists feature is on.
    /// </summary>
    [BindNever]
    public bool CanSendToMailingLists { get; set; }

    /// <summary>
    /// Gets whether the campaign goes to a mailing list.
    /// </summary>
    [BindNever]
    public bool SendsToMailingList
        => CampaignAudienceKinds.IsMailingList(AudienceKind);

    [BindNever]
    public bool CanEmail { get; set; }

    [BindNever]
    public bool HasBaseUrl { get; set; }

    [BindNever]
    public bool CanChange
        => Campaign is null || Campaign.Status is CampaignStatus.Draft or CampaignStatus.Scheduled;
}

public class UnsubscribeViewModel
{
    public string Token { get; set; }

    public bool IsValid { get; set; }

    public string Email { get; set; }

    public bool IsUnsubscribed { get; set; }
}
