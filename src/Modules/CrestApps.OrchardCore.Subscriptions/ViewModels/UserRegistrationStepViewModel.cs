using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

/// <summary>
/// Represents the user registration step in a subscription flow.
/// </summary>
public class UserRegistrationStepViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether the subscriber continues checkout as a guest.
    /// </summary>
    public bool ContinueAsGuest { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether guest checkout is allowed.
    /// </summary>
    [BindNever]
    public bool AllowGuestSignup { get; set; }

    /// <summary>
    /// Gets or sets the rendered sign-up form shape.
    /// </summary>
    [BindNever]
    public IShape SignupForm { get; set; }
}
