using CrestApps.OrchardCore.Addresses.Core.Editing;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Shipping.ViewModels;

public class ShippingPartViewModel
{
    public decimal? Weight { get; set; }

    public string ShippingClass { get; set; }

    public List<ShippingVariantWeightViewModel> Variants { get; set; } = [];

    [BindNever]
    public WeightUnit WeightUnit { get; set; }

    [BindNever]
    public IReadOnlyList<string> ShippingClasses { get; set; } = [];
}

public class ShippingVariantWeightViewModel
{
    public string VariantId { get; set; }

    public decimal? Weight { get; set; }

    [BindNever]
    public string Title { get; set; }
}

public class ShippingSettingsViewModel
{
    public WeightUnit WeightUnit { get; set; }

    public string ShippingClasses { get; set; }

    public string Carriers { get; set; }

    public bool SendShippedEmails { get; set; }

    public List<PickupLocationViewModel> PickupLocations { get; set; } = [];
}

public class PickupLocationViewModel
{
    public string LocationId { get; set; }

    public string Name { get; set; }

    public string AddressLine1 { get; set; }

    public string City { get; set; }

    public string Region { get; set; }

    public string PostalCode { get; set; }

    public string Country { get; set; }

    public string Instructions { get; set; }

    public decimal Fee { get; set; }

    /// <summary>
    /// Gets or sets whether the location is to be removed.
    /// </summary>
    public bool Remove { get; set; }
}

public class LocalDeliverySettingsViewModel
{
    public string PostalCodes { get; set; }

    public decimal Amount { get; set; }

    public decimal? FreeFromSubtotal { get; set; }

    public decimal MinimumSubtotal { get; set; }
}

public class OrderShipmentsViewModel
{
    public Order Order { get; set; }

    public IReadOnlyList<Shipment> Shipments { get; set; } = [];

    public IReadOnlyList<ShipmentLineChoice> Remaining { get; set; } = [];

    public bool CanFulfill { get; set; }

    public IReadOnlyList<string> Carriers { get; set; } = [];
}

public class ShipmentLineChoice
{
    public string LineId { get; set; }

    public string Title { get; set; }

    public string Sku { get; set; }

    public int Remaining { get; set; }
}

public class CreateShipmentViewModel
{
    public List<ShipmentLineQuantityViewModel> Lines { get; set; } = [];

    public string Carrier { get; set; }

    public string TrackingNumber { get; set; }

    public string TrackingUrl { get; set; }

    public string Note { get; set; }

    public bool Ship { get; set; }
}

public class ShipmentLineQuantityViewModel
{
    public string LineId { get; set; }

    public int Quantity { get; set; }
}

public class ShipShipmentViewModel
{
    public string Carrier { get; set; }

    public string TrackingNumber { get; set; }

    public string TrackingUrl { get; set; }
}

public class ShippingZoneViewModel
{
    public string Name { get; set; }

    public int Position { get; set; }

    public bool RestOfWorld { get; set; }

    public string[] Countries { get; set; } = [];

    public string[] Regions { get; set; } = [];

    [BindNever]
    public bool IsNew { get; set; }

    [BindNever]
    public IReadOnlyList<SelectListItem> AllCountries { get; set; } = [];

    [BindNever]
    public IReadOnlyList<ShippingZoneCountryRegions> CountryRegions { get; set; } = [];
}

public class ShippingZoneCountryRegions
{
    public string CountryCode { get; set; }

    public string CountryName { get; set; }

    public IReadOnlyList<SelectListItem> Regions { get; set; } = [];
}

public class ShippingZoneMethodsViewModel
{
    public ShippingZone Zone { get; set; }

    public IReadOnlyList<ShippingZoneMethodRow> Methods { get; set; } = [];

    public IReadOnlyList<IShippingRateProvider> Providers { get; set; } = [];
}

public class ShippingZoneMethodRow
{
    public ShippingMethod Method { get; set; }

    public string ProviderName { get; set; }

    public string Summary { get; set; }
}

public class ShippingMethodViewModel
{
    public string Name { get; set; }

    public string Description { get; set; }

    public bool Enabled { get; set; }

    public int Position { get; set; }

    public string Currency { get; set; }

    public string TaxCategoryCode { get; set; }

    public List<ShippingClassSurchargeViewModel> Surcharges { get; set; } = [];

    [BindNever]
    public string ProviderDisplayName { get; set; }

    [BindNever]
    public IReadOnlyList<SelectListItem> Currencies { get; set; } = [];
}

public class ShippingClassSurchargeViewModel
{
    public string ShippingClass { get; set; }

    public decimal? Amount { get; set; }
}

public class FlatRateSettingsViewModel
{
    public decimal Amount { get; set; }

    public decimal PerItemAmount { get; set; }
}

public class TableRateSettingsViewModel
{
    public TableRateBasis Basis { get; set; }

    public decimal? Maximum { get; set; }

    public List<TableRateRowViewModel> Rows { get; set; } = [];

    [BindNever]
    public WeightUnit WeightUnit { get; set; }
}

public class TableRateRowViewModel
{
    public decimal? From { get; set; }

    public decimal? Amount { get; set; }
}

public class FreeShippingSettingsViewModel
{
    public decimal MinimumSubtotal { get; set; }
}

public class EditShippingMethodViewModel
{
    public string ZoneId { get; set; }

    public string ZoneName { get; set; }

    public string MethodId { get; set; }

    public string ProviderName { get; set; }

    public string ProviderDisplayName { get; set; }

    public dynamic Editor { get; set; }
}

public class ShippingCheckoutStepViewModel
{
    public string AddressId { get; set; }

    /// <summary>
    /// Gets or sets the store location chosen to pick the order up from, or empty to have it shipped.
    /// </summary>
    public string PickupLocationId { get; set; }

    [BindNever]
    public IReadOnlyList<PickupLocation> PickupLocations { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the ways to ship were just shown for the address, for the buyer to choose from.
    /// </summary>
    [BindNever]
    public bool IsChoosing { get; set; }

    public AddressEditorViewModel Address { get; set; } = new();

    public string RateId { get; set; }

    public bool SaveAddress { get; set; }

    [BindNever]
    public bool IsSignedIn { get; set; }

    [BindNever]
    public bool CanSaveAddress { get; set; }

    [BindNever]
    public IReadOnlyList<ShippingAddressSummary> SavedAddresses { get; set; } = [];

    [BindNever]
    public IShape AddressEditor { get; set; }

    [BindNever]
    public IReadOnlyList<ShippingRate> Rates { get; set; } = [];

    [BindNever]
    public bool HasZone { get; set; }

    [BindNever]
    public string Currency { get; set; }
}

public class ShippingAddressSummary
{
    public string AddressId { get; set; }

    public string Label { get; set; }

    public IReadOnlyList<string> Lines { get; set; } = [];

    public bool IsDefault { get; set; }
}

public class ShippingRatesTestViewModel
{
    public string Country { get; set; }

    public string Region { get; set; }

    public string Currency { get; set; }

    public decimal Weight { get; set; }

    public decimal Subtotal { get; set; }

    public int Items { get; set; } = 1;

    public string ShippingClass { get; set; }

    [BindNever]
    public bool Submitted { get; set; }

    [BindNever]
    public ShippingZone Zone { get; set; }

    [BindNever]
    public IReadOnlyList<ShippingRate> Rates { get; set; } = [];

    [BindNever]
    public IReadOnlyList<SelectListItem> Countries { get; set; } = [];

    [BindNever]
    public IReadOnlyList<SelectListItem> Currencies { get; set; } = [];

    [BindNever]
    public IReadOnlyList<string> ShippingClasses { get; set; } = [];

    [BindNever]
    public WeightUnit WeightUnit { get; set; }
}
