using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Core;

public static class AIPermissions
{
    public static readonly Permission ManageAIProfiles = new("ManageAIProfiles", "Manage AI profiles");

    public static readonly Permission ManageModelDeployments = new("ManageAIDeployments", "Manage AI deployments");

    public static readonly Permission QueryAnyAIProfile = new("QueryAnyAIProfile", "Query any AI profile");

    private static readonly Permission _queryAIProfileTemplate = new("QueryAIProfiles_{0}", "Query AI profile - {0}", [QueryAnyAIProfile]);

    /// <summary>
    /// Generates a permission dynamically for a content type.
    /// </summary>
    public static Permission CreateDynamicPermission(string profileName)
    {
        ArgumentException.ThrowIfNullOrEmpty(profileName);

        return new Permission(
            string.Format(_queryAIProfileTemplate.Name, profileName),
            string.Format(_queryAIProfileTemplate.Description, profileName),
           _queryAIProfileTemplate.ImpliedBy ?? []
        );
    }
}
