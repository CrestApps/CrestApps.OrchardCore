using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Users.ViewModels;

/// <summary>
/// The page that asks for the signed-in person's password again.
/// </summary>
public class PasswordConfirmationViewModel
{
    /// <summary>
    /// Gets or sets the password typed.
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// Gets or sets the local page to return to.
    /// </summary>
    public string ReturnUrl { get; set; }

    /// <summary>
    /// Gets or sets whether the person's account has a password.
    /// </summary>
    [BindNever]
    public bool HasPassword { get; set; }

    /// <summary>
    /// Gets or sets how many minutes a confirmation lasts.
    /// </summary>
    [BindNever]
    public int Minutes { get; set; }
}
