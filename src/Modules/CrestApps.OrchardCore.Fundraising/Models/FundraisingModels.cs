using CrestApps.Core.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Fundraising.Models;

/// <summary>
/// Makes a product a fundraising campaign: donors give the amount they choose toward a goal, until an end date.
/// </summary>
public sealed class FundraisingCampaignPart : ContentPart
{
    /// <summary>
    /// Gets or sets what the campaign raises toward, in the product's currency, or zero for no goal.
    /// </summary>
    public decimal Goal { get; set; }

    /// <summary>
    /// Gets or sets when it stops taking donations, or <see langword="null"/> for no end.
    /// </summary>
    public DateTime? EndsUtc { get; set; }

    /// <summary>
    /// Gets or sets the amounts offered as buttons, in the product's currency.
    /// </summary>
    public decimal[] SuggestedAmounts { get; set; } = [25m, 50m, 100m];

    /// <summary>
    /// Gets or sets the least a donor can give, in the product's currency.
    /// </summary>
    public decimal MinimumAmount { get; set; } = 1m;

    /// <summary>
    /// Gets or sets whether donations get a tax receipt.
    /// </summary>
    public bool TaxDeductible { get; set; } = true;
}

/// <summary>
/// One donation: who gave, how much, toward which campaign, with its receipt.
/// </summary>
public sealed class Donation : CatalogItem
{
    public string CampaignId { get; set; }

    public string CampaignTitle { get; set; }

    public string OrderId { get; set; }

    public string OrderNumber { get; set; }

    public string LineId { get; set; }

    public string DonorUserId { get; set; }

    public string DonorName { get; set; }

    public string DonorEmail { get; set; }

    /// <summary>
    /// Gets or sets what was given, in the currency it was paid in.
    /// </summary>
    public decimal Amount { get; set; }

    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets what was given, in the campaign's currency, toward its goal.
    /// </summary>
    public decimal CampaignAmount { get; set; }

    /// <summary>
    /// Gets or sets who it's given in honor or memory of, or <see langword="null"/>.
    /// </summary>
    public string Tribute { get; set; }

    public string ReceiptNumber { get; set; }

    public bool TaxDeductible { get; set; }

    /// <summary>
    /// Gets or sets how much of it was refunded.
    /// </summary>
    public decimal Refunded { get; set; }

    public DateTime CreatedUtc { get; set; }
}

/// <summary>
/// Who the receipts are from: the organization donations go to and what the receipt says.
/// </summary>
public sealed class FundraisingSettings
{
    public string OrganizationName { get; set; }

    /// <summary>
    /// Gets or sets the organization's tax or charity registration number, printed on receipts.
    /// </summary>
    public string TaxId { get; set; }

    /// <summary>
    /// Gets or sets what the receipt says about the donation, such as that no goods or services were given in return.
    /// </summary>
    public string ReceiptStatement { get; set; } = "No goods or services were provided in exchange for this contribution.";

    /// <summary>
    /// Gets or sets whether donors get their receipt by email when they give.
    /// </summary>
    public bool EmailReceipts { get; set; } = true;
}
