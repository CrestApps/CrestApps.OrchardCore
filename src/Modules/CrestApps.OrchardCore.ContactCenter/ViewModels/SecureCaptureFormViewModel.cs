using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// The view model rendered on the customer secure capture page. It carries only the metadata needed to render the
/// form and never carries a submitted value back to the server through a round-trip that the agent could observe.
/// </summary>
public class SecureCaptureFormViewModel
{
    /// <summary>
    /// Gets or sets the raw access token that authorizes the page, echoed back on submission.
    /// </summary>
    public string Token { get; set; }

    /// <summary>
    /// Gets or sets the sensitive field kinds the customer is asked to provide.
    /// </summary>
    public IReadOnlyList<SecureCaptureField> Fields { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the submission completed successfully.
    /// </summary>
    public bool Completed { get; set; }

    /// <summary>
    /// Gets or sets a customer-safe error message to display, when the submission failed.
    /// </summary>
    public string ErrorMessage { get; set; }
}
