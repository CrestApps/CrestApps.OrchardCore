using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Users.ViewModels;

public class AuthorNameViewModel : ShapeViewModel
{
    public string AuthorName { get; set; }

    public AuthorNameViewModel()
        : base("UserProfileTitle")
    {
    }
}
