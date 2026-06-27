using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.ViewModels;

/// <summary>
/// View model for the phone number verification records queue, including search, filtering, and paging state.
/// </summary>
public class PhoneNumberVerificationRecordsViewModel
{
    /// <summary>
    /// Gets or sets the phone number search term.
    /// </summary>
    public string Q { get; set; }

    /// <summary>
    /// Gets or sets the selected status filter.
    /// </summary>
    public PhoneNumberVerificationRecordFilter Status { get; set; }

    /// <summary>
    /// Gets or sets the selected sort order.
    /// </summary>
    public PhoneNumberVerificationRecordSort Sort { get; set; }

    /// <summary>
    /// Gets or sets the records on the current page.
    /// </summary>
    [BindNever]
    public IList<PhoneNumberVerificationRecordEntry> Entries { get; set; } = [];

    /// <summary>
    /// Gets or sets the record counts per status filter, used to render the dashboard tiles.
    /// </summary>
    [BindNever]
    public IDictionary<PhoneNumberVerificationRecordFilter, int> Counts { get; set; } = new Dictionary<PhoneNumberVerificationRecordFilter, int>();

    /// <summary>
    /// Gets or sets the sort order options.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Sorts { get; set; } = [];

    /// <summary>
    /// Gets or sets the pager shape.
    /// </summary>
    [BindNever]
    public dynamic Pager { get; set; }
}
