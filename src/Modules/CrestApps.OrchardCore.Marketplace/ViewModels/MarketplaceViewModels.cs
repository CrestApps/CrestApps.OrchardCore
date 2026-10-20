using CrestApps.OrchardCore.Marketplace.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Marketplace.ViewModels;

public class VendorPartViewModel
{
    public string VendorId { get; set; }

    [BindNever]
    public IReadOnlyList<(string Id, string Name)> Vendors { get; set; } = [];
}

public class MarketplaceSettingsViewModel
{
    public string CommissionPercent { get; set; }

    public int HoldDays { get; set; }

    public bool ReleaseOnFulfillment { get; set; }

    public bool AutomaticPayouts { get; set; }

    public string DefaultCountry { get; set; }
}

public class VendorsIndexViewModel
{
    public string Search { get; set; }

    [BindNever]
    public AdminListContext<Vendor> List { get; set; }
}

/// <summary>
/// A vendor's editor. The commission is text, read in the invariant culture.
/// </summary>
public class VendorEditViewModel
{
    public string Name { get; set; }

    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the usernames of the people who run it, one per line or separated by commas.
    /// </summary>
    public string UserNames { get; set; }

    public string CommissionPercent { get; set; }

    public string Country { get; set; }

    public VendorStatus Status { get; set; }
}

/// <summary>
/// What a vendor has earned and been paid: on its admin page and on its own vendor page.
/// </summary>
public class VendorDashboardViewModel
{
    public Vendor Vendor { get; set; }

    public IList<VendorEarning> Earnings { get; set; } = [];

    public IList<VendorPayout> Payouts { get; set; } = [];

    /// <summary>
    /// Gets or sets the totals by currency: held in escrow, released and waiting, and paid out.
    /// </summary>
    public IList<(string Currency, decimal Held, decimal Available, decimal PaidOut)> Totals { get; set; } = [];

    public bool CanPayOut { get; set; }

    public bool CanConnectStripe { get; set; }
}
