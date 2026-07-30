using System.Text.Json;
using System.Text.Json.Serialization;
using OrchardCore.Json;

namespace CrestApps.OrchardCore.ContactCenter.Deployments;

/// <summary>
/// Serializes Contact Center configuration entries into the JSON shape a deployment plan carries.
/// </summary>
internal static class ContactCenterDeploymentSerializer
{
    /// <summary>
    /// The options used to export an entry. Null members are written rather than omitted, so a value cleared in the
    /// source environment clears wherever the plan is replayed instead of keeping its stale value.
    /// </summary>
    public static readonly JsonSerializerOptions Options = new(JOptions.Default)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };
}
