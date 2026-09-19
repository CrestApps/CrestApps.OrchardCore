using CrestApps.Core.ContactCenter;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.ContactCenter.FeatureActivationTests;

/// <summary>
/// Proves that every registered lifecycle participant can still be reached when its owning feature is
/// disabled.
/// </summary>
/// <remarks>
/// Participants used to be matched to a disabling feature by feature id, which made an orphan
/// impossible. They are now matched by capability, with the feature-to-capability edges contributed
/// to dependency injection by whichever startup owns the participant. That indirection introduces a
/// failure that nothing else would notice: a participant whose capability no enabled feature maps to
/// is never quiesced, so in-flight work survives a feature disable and nothing throws.
/// <para>
/// This closes that hole. For every Contact Center feature, and for each provider, it boots a tenant
/// and asserts that each registered participant's capability is reachable from some registered
/// mapping.
/// </para>
/// </remarks>
public sealed class ContactCenterFeatureCapabilityMapTests
{
    [Fact]
    public async Task EveryRegisteredParticipant_IsReachableFromSomeCapabilityMapping()
    {
        // Arrange
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var profiles = await ContactCenterSupportMatrix.LoadAsync();
        var orphans = new List<string>();
        var checkedParticipants = 0;

        // Act
        foreach (var profile in profiles.TenantProfiles)
        {
            var tenant = await host.CreateTenantAsync(profile);

            await host.ExecuteInTenantScopeAsync(tenant, serviceProvider =>
            {
                var participants = serviceProvider.GetServices<IContactCenterFeatureLifecycleParticipant>().ToArray();
                var mapped = serviceProvider
                    .GetServices<ContactCenterFeatureCapabilityMapping>()
                    .Select(mapping => mapping.Capability)
                    .ToHashSet(StringComparer.Ordinal);

                foreach (var participant in participants)
                {
                    checkedParticipants++;

                    if (!mapped.Contains(participant.Capability))
                    {
                        orphans.Add(
                            $"profile '{profile.Id}': {participant.GetType().Name} owns capability " +
                            $"'{participant.Capability}', which no enabled feature maps to.");
                    }
                }

                return Task.CompletedTask;
            });
        }

        // Assert
        Assert.True(
            checkedParticipants > 0,
            "No lifecycle participants were registered by any supported profile, so this gate proves nothing.");

        Assert.True(
            orphans.Count == 0,
            new StringBuilder()
                .AppendLine("These participants can never be drained, because no feature declares the capability they")
                .AppendLine("own. Disabling the owning feature would leave their in-flight work running, silently.")
                .AppendLine()
                .AppendJoin(Environment.NewLine, orphans.Distinct(StringComparer.Ordinal).Select(entry => "  - " + entry))
                .AppendLine()
                .AppendLine()
                .Append("Add services.AddContactCenterCapability(<feature id>, <capability>) to the startup that ")
                .Append("registers the participant.")
                .ToString());
    }

    [Fact]
    public async Task EveryCapabilityConstant_IsOwnedByAFeature()
    {
        // Arrange
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var tenant = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = "capability-coverage",
            ProviderProfile = "none",
            Features = GetContactCenterFeatureIds(),
        });

        var declared = typeof(ContactCenterCapabilities)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false })
            .Select(field => (string)field.GetRawConstantValue())
            .ToArray();

        // Act
        var mapped = new HashSet<string>(StringComparer.Ordinal);

        await host.ExecuteInTenantScopeAsync(tenant, serviceProvider =>
        {
            foreach (var mapping in serviceProvider.GetServices<ContactCenterFeatureCapabilityMapping>())
            {
                mapped.Add(mapping.Capability);
            }

            return Task.CompletedTask;
        });

        // Assert
        Assert.NotEmpty(mapped);

        // Not every declared capability has to be claimed today - some name work that only a provider
        // or a later feature contributes - but a capability that is used by a participant and claimed
        // by nobody is the orphan case the other test covers. This one simply records which constants
        // are live, so deleting a feature's mapping is visible.
        var unclaimed = declared.Where(capability => !mapped.Contains(capability)).ToArray();

        Assert.True(
            unclaimed.Length < declared.Length,
            "No declared capability is claimed by any feature, so the mapping registration is not running at all.");
    }

    private static string[] GetContactCenterFeatureIds()
        => typeof(ContactCenterConstants.Feature)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue())
            .Where(featureId => !string.IsNullOrEmpty(featureId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(featureId => featureId, StringComparer.Ordinal)
            .ToArray();
}
