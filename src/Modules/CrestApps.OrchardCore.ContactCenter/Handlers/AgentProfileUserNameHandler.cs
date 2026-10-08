using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.AspNetCore.Identity;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

/// <summary>
/// Keeps an agent profile's user name filled in from the user it belongs to.
/// </summary>
/// <remarks>
/// A profile made when an agent first signs in or changes presence carries only the user's id, so every report that
/// names agents by their profile listed them as unknown. The name is read from the user account whenever a profile
/// without one is created or saved, and <see cref="Services.AgentProfileUserNameBackfill"/> fills the profiles saved
/// before this existed.
/// </remarks>
internal sealed class AgentProfileUserNameHandler : CatalogEntryHandlerBase<AgentProfile>
{
    private readonly UserManager<IUser> _userManager;

    public AgentProfileUserNameHandler(UserManager<IUser> userManager)
    {
        _userManager = userManager;
    }

    public override Task CreatingAsync(CreatingContext<AgentProfile> context, CancellationToken cancellationToken = default)
        => FillAsync(context.Model);

    public override Task UpdatingAsync(UpdatingContext<AgentProfile> context, CancellationToken cancellationToken = default)
        => FillAsync(context.Model);

    private async Task FillAsync(AgentProfile profile)
    {
        if (!string.IsNullOrEmpty(profile.UserName) || string.IsNullOrEmpty(profile.UserId))
        {
            return;
        }

        var user = await _userManager.FindByIdAsync(profile.UserId);

        if (user is not null)
        {
            profile.UserName = user.UserName;
        }
    }
}
