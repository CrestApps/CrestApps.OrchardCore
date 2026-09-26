using System.Reflection;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Workflows.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Guards the Contact Center workflow event-type picker against drifting away from the canonical
/// <see cref="ContactCenterConstants.Events"/> constants. Every selectable value must map to a real
/// event constant, and every event constant must be selectable, so no-code authors can react to any event.
/// </summary>
public sealed class ContactCenterWorkflowEventTypeProviderTests
{
    /// <summary>
    /// The non-empty picker values must be exactly the set of declared event-type constants, with a single
    /// leading empty "Any event type" option and no duplicate values.
    /// </summary>
    [Fact]
    public void GetEventTypes_ValuesMatchEventConstantsExactly()
    {
        // Arrange
        var provider = new ContactCenterWorkflowEventTypeProvider(
            new PassThroughStringLocalizer<ContactCenterWorkflowEventTypeProvider>());

        var expected = typeof(ContactCenterConstants.Events)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue())
            .ToHashSet(StringComparer.Ordinal);

        // Act
        var items = provider.GetEventTypes();
        var emptyOptions = items.Where(item => string.IsNullOrEmpty(item.Value)).ToList();
        var values = items
            .Where(item => !string.IsNullOrEmpty(item.Value))
            .Select(item => item.Value)
            .ToList();

        // Assert
        Assert.Single(emptyOptions);
        Assert.Equal(values.Count, values.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(expected, values.ToHashSet(StringComparer.Ordinal));
    }
}
