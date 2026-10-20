using CrestApps.OrchardCore.Fundraising.Models;
using CrestApps.OrchardCore.Fundraising.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Fundraising.ViewModels;

/// <summary>
/// The Fundraising campaign part's editor. Amounts are text, read in the invariant culture.
/// </summary>
public class FundraisingCampaignPartViewModel
{
    public string Goal { get; set; }

    public DateTime? EndsUtc { get; set; }

    public string SuggestedAmounts { get; set; }

    public string MinimumAmount { get; set; }

    public bool TaxDeductible { get; set; }
}

public class CampaignProgressViewModel
{
    public CampaignProgress Progress { get; set; }

    public bool Ended { get; set; }
}

/// <summary>
/// The donate form on a campaign's page, whose fields post with its Add to cart form.
/// </summary>
public class DonateFormViewModel
{
    public string FormId { get; set; }

    public IList<(decimal Amount, string Label)> Amounts { get; set; } = [];

    public string Currency { get; set; }

    public decimal Minimum { get; set; }

    public bool TaxDeductible { get; set; }

    public bool Ended { get; set; }
}

public class AccountDonationsViewModel
{
    public IList<Donation> Donations { get; set; } = [];
}

public class DonationReceiptViewModel
{
    public Donation Donation { get; set; }

    [BindNever]
    public FundraisingSettings Settings { get; set; }

    public string SiteName { get; set; }
}

public class FundraisingSettingsViewModel
{
    public string OrganizationName { get; set; }

    public string TaxId { get; set; }

    public string ReceiptStatement { get; set; }

    public bool EmailReceipts { get; set; }
}
