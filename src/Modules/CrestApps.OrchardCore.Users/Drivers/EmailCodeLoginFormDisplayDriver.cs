using CrestApps.OrchardCore.Users.Services;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Users.Drivers;

/// <summary>
/// Offers signing in with an emailed code on the login page, when the site offers it (see
/// <see cref="EmailSignInAvailability"/>).
/// </summary>
public sealed class EmailCodeLoginFormDisplayDriver : DisplayDriver<LoginForm>
{
    private readonly ISiteService _siteService;

    public EmailCodeLoginFormDisplayDriver(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public override async Task<IDisplayResult> EditAsync(LoginForm model, BuildEditorContext context)
    {
        // The link leads to the code pages, which are not there on a site that turned off local sign-in.
        if (!await EmailSignInAvailability.IsAvailableAsync(_siteService))
        {
            return null;
        }

        return View("LoginFormEmailCode", model).Location("Links:15");
    }
}
