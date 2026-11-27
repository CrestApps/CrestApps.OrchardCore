using CrestApps.OrchardCore.Marketplace.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Marketplace.ViewModels;

public class VendorPartViewModel
{
    public string VendorId { get; set; }

    [BindNever]
    public IReadOnlyList<(string Id, string Name)> Vendors { get; set; } = [];
}

public class MarketplaceSettingsViewModel
{
    public decimal? CommissionPercent { get; set; }

    public int HoldDays { get; set; }

    public bool ReleaseOnFulfillment { get; set; }

    public bool AutomaticPayouts { get; set; }

    public string DefaultCountry { get; set; }

    public string PayoutProvider { get; set; }

    [BindNever]
    public IList<string> PayoutProviders { get; set; } = [];
}

public class VendorsIndexViewModel
{
    public string Search { get; set; }

    [BindNever]
    public AdminListContext<Vendor> List { get; set; }
}

/// <summary>
/// A vendor's editor. The commission is bound by the invariant number binder; empty means the store's.
/// </summary>
public class VendorEditViewModel
{
    public string Name { get; set; }

    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the usernames of the people who run it, one per line or separated by commas.
    /// </summary>
    public string UserNames { get; set; }

    public decimal? CommissionPercent { get; set; }

    public string Country { get; set; }

    public VendorStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the editor of the vendor, built by the display drivers of <see cref="VendorEditViewModel"/>.
    /// </summary>
    [BindNever]
    public IShape Editor { get; set; }
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

    /// <summary>
    /// Gets or sets the pending payouts whose transfer the provider hasn't shown for the review time, which staff check
    /// at the provider and mark as not made when none was.
    /// </summary>
    public IList<VendorPayout> PayoutsToCheck { get; set; } = [];

    public bool CanConnectStripe { get; set; }
}
