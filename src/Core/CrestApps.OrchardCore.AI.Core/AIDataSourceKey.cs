using System.Diagnostics.CodeAnalysis;

namespace CrestApps.OrchardCore.AI.Core;

public readonly record struct AIDataSourceKey
{
    [SetsRequiredMembers]
    public AIDataSourceKey(string profileSource, string type)
    {
        ArgumentException.ThrowIfNullOrEmpty(profileSource);
        ArgumentException.ThrowIfNullOrEmpty(type);

        ProfileSource = profileSource;
        Type = type;
    }

    public required string ProfileSource { get; init; }

    public required string Type { get; init; }
}
