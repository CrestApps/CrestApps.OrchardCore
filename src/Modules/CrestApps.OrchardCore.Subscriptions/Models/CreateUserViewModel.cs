using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Subscriptions.Models;

/// <summary>
/// Provides the editable user registration fields used during subscription sign-up.
/// </summary>
public class CreateUserViewModel
{
    /// <summary>
    /// Gets or sets the username for the new subscription user.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Gets or sets the email address for the new subscription user.
    /// </summary>
    [DataType(DataType.EmailAddress)]
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the password entered for the new subscription user.
    /// </summary>
    [DataType(DataType.Password)]
    public string Password { get; set; }

    /// <summary>
    /// Gets or sets the password confirmation entered for the new subscription user.
    /// </summary>
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the subscription session already has a saved password.
    /// </summary>
    [BindNever]
    public bool HasSavedPassword { get; set; }
}
