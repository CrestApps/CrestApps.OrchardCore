using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Workflows.Tasks;
using CrestApps.OrchardCore.Omnichannel.Managements.Workflows.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Workflows.Drivers;

internal sealed class TryAgainActivityTaskDisplayDriver : ActivityDisplayDriver<TryAgainActivityTask, TryAgainActivityTaskViewModel>
{
    private readonly UserManager<IUser> _userManager;
    private readonly IDisplayNameProvider _displayNameProvider;

    internal readonly IStringLocalizer S;

    public TryAgainActivityTaskDisplayDriver(
        UserManager<IUser> userManager,
        IDisplayNameProvider displayNameProvider,
        IStringLocalizer<TryAgainActivityTaskDisplayDriver> stringLocalizer)
    {
        _userManager = userManager;
        _displayNameProvider = displayNameProvider;
        S = stringLocalizer;
    }

    protected override async ValueTask EditActivityAsync(TryAgainActivityTask activity, TryAgainActivityTaskViewModel model)
    {
        model.UrgencyLevel = activity.UrgencyLevel;
        model.MaxAttempt = activity.MaxAttempt;
        model.NormalizedUserName = activity.NormalizedUserName;
        model.DefaultScheduleHours = activity.DefaultScheduleHours;
        model.UrgencyLevels =
        [
            new(S["Normal"], nameof(ActivityUrgencyLevel.Normal)),
            new(S["Very low"], nameof(ActivityUrgencyLevel.VeryLow)),
            new(S["Low"], nameof(ActivityUrgencyLevel.Low)),
            new(S["Medium"], nameof(ActivityUrgencyLevel.Medium)),
            new(S["High"], nameof(ActivityUrgencyLevel.High)),
            new(S["Very high"], nameof(ActivityUrgencyLevel.VeryHigh)),
        ];

        var users = await _userManager.GetUsersInRoleAsync(OmnichannelConstants.AgentRole);

        var userItems = new List<SelectListItem>();

        foreach (var user in users)
        {
            var displayName = await _displayNameProvider.GetAsync(user);

            var userId = user is User su ? su.UserId : _userManager.NormalizeName(user.UserName);

            userItems.Add(new SelectListItem
            {
                Text = displayName,
                Value = userId,
            });
        }

        model.Users = userItems.OrderBy(x => x.Text);
    }

    public override async Task<IDisplayResult> UpdateAsync(TryAgainActivityTask activity, UpdateEditorContext context)
    {
        var model = new TryAgainActivityTaskViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.MaxAttempt.HasValue && model.MaxAttempt < 1)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.MaxAttempt), S["Max attempt field must have min of {0}", 1]);
        }

        if (model.DefaultScheduleHours.HasValue && model.DefaultScheduleHours < 1)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DefaultScheduleHours), S["Default schedule field must have min of {0}", 1]);
        }

        activity.UrgencyLevel = model.UrgencyLevel;
        activity.MaxAttempt = model.MaxAttempt;
        activity.NormalizedUserName = model.NormalizedUserName?.Trim();
        activity.DefaultScheduleHours = model.DefaultScheduleHours;

        return await EditAsync(activity, context);
    }
}
