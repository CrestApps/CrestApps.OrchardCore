using System.ComponentModel.DataAnnotations;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

/// <summary>
/// Represents user credentials collected during subscription registration.
/// </summary>
public class UserRegistrationViewModel
{
    /// <summary>
    /// Gets or sets the username for the registered subscription user.
    /// </summary>
    [Required(ErrorMessage = "Username is required.")]
    public string UserName { get; set; }

    /// <summary>
    /// Gets or sets the email address for the registered subscription user.
    /// </summary>
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid Email.")]
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the password entered for the registered subscription user.
    /// </summary>
    [DataType(DataType.Password)]
    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; }

    /// <summary>
    /// Gets or sets the password confirmation entered for the registered subscription user.
    /// </summary>
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "The new password and confirmation password do not match.")]
    public string ConfirmPassword { get; set; }
}
