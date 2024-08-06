using System.ComponentModel.DataAnnotations;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

public class UserRegistrationViewModel
{
    [Required(ErrorMessage = "Username is required.")]
    public string UserName { get; set; }

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid Email.")]
    public string Email { get; set; }

    [DataType(DataType.Password)]
    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; }

    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "The new password and confirmation password do not match.")]
    public string ConfirmPassword { get; set; }
}
