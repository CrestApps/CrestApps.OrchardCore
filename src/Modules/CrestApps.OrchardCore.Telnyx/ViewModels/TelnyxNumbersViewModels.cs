using CrestApps.OrchardCore.Telnyx.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Telnyx.ViewModels;

/// <summary>
/// The account's numbers.
/// </summary>
public class TelnyxNumbersViewModel
{
    /// <summary>
    /// Gets or sets the numbers.
    /// </summary>
    [BindNever]
    public AdminListContext<TelnyxOwnedNumber> List { get; set; }
}

/// <summary>
/// The search for numbers for sale, and what it found.
/// </summary>
public class TelnyxNumberSearchViewModel
{
    /// <summary>Gets or sets a value indicating whether the form was sent.</summary>
    public bool Submitted { get; set; }

    /// <summary>Gets or sets the country code.</summary>
    public string CountryCode { get; set; } = "US";

    /// <summary>Gets or sets the area code.</summary>
    public string AreaCode { get; set; }

    /// <summary>Gets or sets the city.</summary>
    public string City { get; set; }

    /// <summary>Gets or sets the state or province.</summary>
    public string Region { get; set; }

    /// <summary>Gets or sets digits the number contains.</summary>
    public string Contains { get; set; }

    /// <summary>Gets or sets a value indicating whether the number must text too.</summary>
    public bool Sms { get; set; } = true;

    /// <summary>Gets or sets what was found.</summary>
    [BindNever]
    public AdminListContext<TelnyxAvailableNumber> List { get; set; }
}
