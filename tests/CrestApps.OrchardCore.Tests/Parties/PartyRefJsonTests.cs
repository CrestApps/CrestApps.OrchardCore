using System.Text.Json;
using CrestApps.OrchardCore.Parties.Models;

namespace CrestApps.OrchardCore.Tests.Parties;

public sealed class PartyRefJsonTests
{
    // Regression: the computed IsEmpty was stored on every record that holds a party reference.
    [Fact]
    public void Serialize_WritesOnlyTheKindAndTheId_AndReadsThemBack()
    {
        // Arrange
        var party = new PartyRef("Staff", "s1");

        // Act
        var json = JsonSerializer.Serialize(party);
        var read = JsonSerializer.Deserialize<PartyRef>(json);

        // Assert
        Assert.DoesNotContain("IsEmpty", json, StringComparison.Ordinal);
        Assert.Equal(party, read);
    }
}
