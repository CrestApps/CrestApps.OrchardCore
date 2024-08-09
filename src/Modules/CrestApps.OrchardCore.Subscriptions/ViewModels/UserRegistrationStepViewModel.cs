using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

public class UserRegistrationStepViewModel
{
    public bool ContinueAsGuest { get; set; }

    [BindNever]
    public bool AllowGuestSignup { get; set; }

    [BindNever]
    public IShape SignupForm { get; set; }
}
