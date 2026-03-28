namespace CrestApps.OrchardCore.AI.Models;

public static class AIDeploymentTypeExtensions
{
    private static readonly AIDeploymentType _allSupportedTypes = Enum.GetValues<AIDeploymentType>()
        .Where(type => type != AIDeploymentType.None)
        .Aggregate(AIDeploymentType.None, static (current, type) => current | type);

    public static bool Supports(this AIDeploymentType value, AIDeploymentType type)
        => type != AIDeploymentType.None && (value & type) == type;

    public static bool IsValidSelection(this AIDeploymentType value)
        => value != AIDeploymentType.None && (value & ~_allSupportedTypes) == 0;

    public static IEnumerable<AIDeploymentType> GetSupportedTypes(this AIDeploymentType value)
        => Enum.GetValues<AIDeploymentType>().Where(type => value.Supports(type));
}
