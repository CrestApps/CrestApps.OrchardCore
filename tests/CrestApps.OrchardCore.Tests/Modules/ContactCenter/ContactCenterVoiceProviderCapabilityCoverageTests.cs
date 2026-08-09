using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.DialPad.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Enforces the capability-honesty principle: every flag defined by
/// <see cref="ContactCenterVoiceProviderCapabilities"/> must either be advertised by an in-tree voice provider
/// or be explicitly listed as a reserved, not-yet-implemented extension point. This replaces the bespoke
/// governance gate: a capability that no provider implements and that is not acknowledged as reserved would
/// advertise a feature the platform cannot deliver.
/// </summary>
public sealed class ContactCenterVoiceProviderCapabilityCoverageTests
{
    private static readonly ContactCenterVoiceProviderCapabilities _reserved =
        ContactCenterVoiceProviderCapabilities.AgentCallAssignment |
        ContactCenterVoiceProviderCapabilities.ProviderQueue |
        ContactCenterVoiceProviderCapabilities.QueueEvents |
        ContactCenterVoiceProviderCapabilities.AgentPresenceSync |
        ContactCenterVoiceProviderCapabilities.SecureCapture |
        ContactCenterVoiceProviderCapabilities.SecureCaptureMasking;

    [Fact]
    public void EveryCapabilityBit_IsAdvertisedByAnInTreeProvider_OrExplicitlyReserved()
    {
        // Arrange
        var declaredByProviders = DiscoverInTreeProviderCapabilities();

        var allBits = ContactCenterVoiceProviderCapabilities.None;

        foreach (ContactCenterVoiceProviderCapabilities bit in Enum.GetValues<ContactCenterVoiceProviderCapabilities>())
        {
            allBits |= bit;
        }

        // Act
        var covered = declaredByProviders | _reserved;
        var uncovered = allBits & ~covered;
        var reservedButAdvertised = _reserved & declaredByProviders;

        // Assert
        Assert.Equal(ContactCenterVoiceProviderCapabilities.None, uncovered);
        Assert.Equal(ContactCenterVoiceProviderCapabilities.None, reservedButAdvertised);
    }

    private static ContactCenterVoiceProviderCapabilities DiscoverInTreeProviderCapabilities()
    {
        var providerAssemblies = new[]
        {
            typeof(AsteriskContactCenterVoiceProvider).Assembly,
            typeof(DialPadContactCenterVoiceProvider).Assembly,
        };

        var capabilities = ContactCenterVoiceProviderCapabilities.None;

        foreach (var type in providerAssemblies.SelectMany(assembly => assembly.GetTypes()))
        {
            if (type.IsAbstract || type.IsInterface || !typeof(IContactCenterVoiceProvider).IsAssignableFrom(type))
            {
                continue;
            }

            // The Capabilities getters are pure declarations of advertised flags, so they can be read from an
            // uninitialized instance without running a constructor that would demand injected dependencies.
            var provider = (IContactCenterVoiceProvider)RuntimeHelpers.GetUninitializedObject(type);

            capabilities |= provider.Capabilities;
        }

        return capabilities;
    }
}
