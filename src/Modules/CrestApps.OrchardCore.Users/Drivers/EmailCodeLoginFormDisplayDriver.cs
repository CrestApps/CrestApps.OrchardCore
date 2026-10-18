using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Users.Drivers;

/// <summary>
/// Offers signing in with an emailed code on the login page.
/// </summary>
public sealed class EmailCodeLoginFormDisplayDriver : DisplayDriver<LoginForm>
{
    public override IDisplayResult Edit(LoginForm model, BuildEditorContext context)
        => View("LoginFormEmailCode", model).Location("Links:15");
}
