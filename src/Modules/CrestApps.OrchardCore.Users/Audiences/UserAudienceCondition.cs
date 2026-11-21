using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Rules;

namespace CrestApps.OrchardCore.Users.Audiences;

/// <summary>
/// A rule condition that is met when the signed-in user is in an audience, so a layer can show widgets to it.
/// </summary>
public sealed class UserAudienceCondition : Condition
{
    /// <summary>
    /// Gets or sets the audience.
    /// </summary>
    public string AudienceName { get; set; }
}

/// <summary>
/// Evaluates <see cref="UserAudienceCondition"/> for the signed-in user; an anonymous visitor is in no audience.
/// </summary>
internal sealed class UserAudienceConditionEvaluator : ConditionEvaluator<UserAudienceCondition>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IUserAudienceService _audiences;

    public UserAudienceConditionEvaluator(IHttpContextAccessor httpContextAccessor, IUserAudienceService audiences)
    {
        _httpContextAccessor = httpContextAccessor;
        _audiences = audiences;
    }

    public override async ValueTask<bool> EvaluateAsync(UserAudienceCondition condition)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var userId = httpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(condition.AudienceName))
        {
            return false;
        }

        return await _audiences.IsMemberAsync(condition.AudienceName, userId, httpContext.RequestAborted);
    }
}

/// <summary>
/// The editor of <see cref="UserAudienceCondition"/>.
/// </summary>
internal sealed class UserAudienceConditionDisplayDriver : DisplayDriver<Condition, UserAudienceCondition>
{
    private readonly IUserAudienceService _audiences;

    public UserAudienceConditionDisplayDriver(IUserAudienceService audiences)
    {
        _audiences = audiences;
    }

    public override Task<IDisplayResult> DisplayAsync(UserAudienceCondition condition, BuildDisplayContext context)
        => CombineAsync(
            View("UserAudienceCondition_Fields_Summary", condition).Location(OrchardCoreConstants.DisplayType.Summary, "Content"),
            View("UserAudienceCondition_Fields_Thumbnail", condition).Location("Thumbnail", "Content"));

    public override IDisplayResult Edit(UserAudienceCondition condition, BuildEditorContext context)
        => Initialize<UserAudienceConditionViewModel>("UserAudienceCondition_Fields_Edit", async model =>
        {
            model.AudienceName = condition.AudienceName;
            model.Audiences = (await _audiences.ListAsync())
                .Select(audience => new SelectListItem(audience.DisplayName, audience.Name, string.Equals(audience.Name, condition.AudienceName, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }).Location("Content");

    public override async Task<IDisplayResult> UpdateAsync(UserAudienceCondition condition, UpdateEditorContext context)
    {
        var model = new UserAudienceConditionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix, value => value.AudienceName);

        condition.AudienceName = model.AudienceName?.Trim();

        return Edit(condition, context);
    }
}

/// <summary>
/// The fields of the <see cref="UserAudienceCondition"/> editor.
/// </summary>
public class UserAudienceConditionViewModel
{
    /// <summary>
    /// Gets or sets the audience.
    /// </summary>
    public string AudienceName { get; set; }

    /// <summary>
    /// Gets or sets the audiences to choose from.
    /// </summary>
    public IList<SelectListItem> Audiences { get; set; } = [];
}
