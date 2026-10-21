using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Checkout.ViewModels;

/// <summary>
/// A checkout laid out on one page: a section per step, payment last.
/// </summary>
public sealed class CheckoutOnePageViewModel
{
    /// <summary>
    /// Gets or sets the checkout session id.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the checkout flow.
    /// </summary>
    public CheckoutFlow Flow { get; set; }

    /// <summary>
    /// Gets the page's sections, in the steps' order.
    /// </summary>
    public List<CheckoutOnePageSection> Sections { get; } = [];

    /// <summary>
    /// Gets or sets whether payment is on the page: it shows once every step before it is complete.
    /// </summary>
    public bool ShowsPayment { get; set; }

    /// <summary>
    /// Gets or sets whether what is on the page has to be saved before paying, such as after a mistake was pointed
    /// out.
    /// </summary>
    public bool NeedsUpdate { get; set; }

    /// <summary>
    /// Gets or sets the step frame whose side column (the order summary, and a promotion code box) the page shows.
    /// </summary>
    public IShape Aside { get; set; }
}

/// <summary>
/// One step's section of a one-page checkout.
/// </summary>
public sealed class CheckoutOnePageSection
{
    /// <summary>
    /// Gets or sets the step.
    /// </summary>
    public CheckoutFlowStep Step { get; set; }

    /// <summary>
    /// Gets or sets the step's frame, whose zones its section shows.
    /// </summary>
    public IShape Shape { get; set; }

    /// <summary>
    /// Gets or sets whether this is the payment step.
    /// </summary>
    public bool IsPayment { get; set; }
}
