using CrestApps.OrchardCore.Users.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Users.Core.ViewModels;

public class UserFullNamePartViewModel
{
    public string FirstName { get; set; }

    public string MiddleName { get; set; }

    public string LastName { get; set; }

    public string DisplayName { get; set; }

    [BindNever]
    public User User { get; set; }

    [BindNever]
    public DisplayNameSettings Settings { get; set; }
}
