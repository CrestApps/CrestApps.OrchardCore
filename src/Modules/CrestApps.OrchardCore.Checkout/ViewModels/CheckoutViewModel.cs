using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Checkout.ViewModels;

/// <summary>
/// The model behind the public checkout page. The step content is a shape built by the registered step
/// drivers, so the page itself stays agnostic of what any particular checkout collects.
/// </summary>
public sealed class CheckoutViewModel
{
    /// <summary>
    /// Gets or sets the checkout session id.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the key of the step being displayed.
    /// </summary>
    public string Step { get; set; }

    /// <summary>
    /// Gets or sets the rendered step content.
    /// </summary>
    public IShape Content { get; set; }
}
