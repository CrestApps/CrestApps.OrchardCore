using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.AI.Agents;

internal static class ProjectionExtensions
{
    public static object AsAIObject(this ShellSettings shellSettings)
    {
        return new
        {
            shellSettings.Name,
            Description = shellSettings["Description"],
            DatabaseProvider = shellSettings["DatabaseProvider"],
            RecipeName = shellSettings["RecipeName"],
            shellSettings.RequestUrlHost,
            shellSettings.RequestUrlPrefix,
            Category = shellSettings["Category"],
            TablePrefix = shellSettings["TablePrefix"],
            Schema = shellSettings["Schema"],
            Status = shellSettings.State.ToString(),
        };
    }

    public static object AsAIObject(this IFeatureInfo feature, bool isEnable)
    {
        return new
        {
            feature.Name,
            feature.Id,
            feature.Category,
            IsEnabled = isEnable,
            feature.IsAlwaysEnabled,
            feature.DefaultTenantOnly,
            feature.EnabledByDependencyOnly,
            feature.Dependencies,
        };
    }

    public static object AsAIObject(this User user)
    {
        return new
        {
            user.UserId,
            user.UserName,
            user.NormalizedUserName,
            user.RoleNames,
            user.IsEnabled,
            IsLockedOut = user.IsLockoutEnabled,
            user.Email,
            user.NormalizedEmail,
            user.EmailConfirmed,
            user.PhoneNumber,
            user.PhoneNumberConfirmed,
            user.TwoFactorEnabled,
        };
    }
}
