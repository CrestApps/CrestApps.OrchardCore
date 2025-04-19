using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;

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
}
