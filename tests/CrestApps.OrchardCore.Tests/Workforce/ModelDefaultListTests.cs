using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// Documents are read by filling the lists a new object already has. A list property whose default holds entries
/// would come back with those entries in front of the saved ones, so the saved values must read back exactly.
/// </summary>
public sealed class ModelDefaultListTests
{
    private static readonly JsonSerializerOptions _populate = new(JsonSerializerDefaults.Web)
    {
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
    };

    [Fact]
    public void TimeOffAllotment_WeekdaySlots_ReadBackAsSaved()
    {
        // Arrange
        var saved = new TimeOffAllotment { WeekdaySlots = [0, 1, 1, 1, 1, 1, 0] };

        // Act
        var read = JsonSerializer.Deserialize<TimeOffAllotment>(JsonSerializer.Serialize(saved, _populate), _populate);

        // Assert
        Assert.Equal([0, 1, 1, 1, 1, 1, 0], read.WeekdaySlots);
        Assert.Equal(1, read.SlotsOn(new DateOnly(2026, 11, 9)));
    }

    [Fact]
    public void Certification_ReminderDays_ReadBackAsSaved()
    {
        // Arrange
        var saved = new Certification { Name = "Gas safety", ReminderDays = [60, 7] };

        // Act
        var read = JsonSerializer.Deserialize<Certification>(JsonSerializer.Serialize(saved, _populate), _populate);

        // Assert
        Assert.Equal([60, 7], read.ReminderDays);
    }
}
