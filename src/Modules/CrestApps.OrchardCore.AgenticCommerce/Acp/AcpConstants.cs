namespace CrestApps.OrchardCore.AgenticCommerce.Acp;

/// <summary>
/// The names, addresses and headers of the Agentic Commerce Protocol.
/// </summary>
public static class AcpConstants
{
    /// <summary>
    /// The path of the REST binding, after the tenant's base address.
    /// </summary>
    public const string RoutePrefix = "acp";

    /// <summary>
    /// The path of the store's discovery document.
    /// </summary>
    public const string DiscoveryPath = ".well-known/acp.json";

    /// <summary>
    /// The header the agent names the version in.
    /// </summary>
    public const string VersionHeader = "API-Version";

    /// <summary>
    /// The versions the store serves, oldest first, as ACP's discovery lists them.
    /// </summary>
    public static readonly IReadOnlyList<string> Versions = [AgenticCommerceConstants.Versions.Acp20260130, AgenticCommerceConstants.Versions.Acp20260417];

    /// <summary>
    /// The storage key of the platform an ACP order update is for, on its delivery.
    /// </summary>
    public const string PlatformIdData = "platformId";
}
