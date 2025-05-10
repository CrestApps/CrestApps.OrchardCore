using System.Diagnostics.CodeAnalysis;

namespace CrestApps.OrchardCore.AI.Core;

public readonly record struct AIDataSourceKey
{
    [SetsRequiredMembers]
    public AIDataSourceKey(string providerName, string type)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerName);
        ArgumentException.ThrowIfNullOrEmpty(type);

        ProviderName = providerName;
        Type = type;
    }

    public required string ProviderName { get; init; }

    public required string Type { get; init; }
}
