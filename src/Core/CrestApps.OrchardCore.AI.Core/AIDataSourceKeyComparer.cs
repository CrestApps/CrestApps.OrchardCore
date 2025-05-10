namespace CrestApps.OrchardCore.AI.Core;

public sealed class AIDataSourceKeyComparer : IEqualityComparer<AIDataSourceKey>
{
    public static readonly AIDataSourceKeyComparer Instance = new();

    public bool Equals(AIDataSourceKey x, AIDataSourceKey y)
    {
        return string.Equals(x.ProviderName, y.ProviderName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Type, y.Type, StringComparison.OrdinalIgnoreCase);
    }

    public int GetHashCode(AIDataSourceKey obj)
    {
        return HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.ProviderName),
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Type)
        );
    }
}
