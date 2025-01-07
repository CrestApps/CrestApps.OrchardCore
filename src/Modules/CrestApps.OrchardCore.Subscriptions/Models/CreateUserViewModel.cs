using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Subscriptions.Models;

public class CreateUserViewModel
{
    public string UserName { get; set; }

    [DataType(DataType.EmailAddress)]
    public string Email { get; set; }

    [DataType(DataType.Password)]
    public string Password { get; set; }

    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; }

    [BindNever]
    public bool HasSavedPassword { get; set; }
}
