using System.Text.Json;
using CrestApps.OrchardCore.Users.ViewComponents;

namespace CrestApps.OrchardCore.Tests.Modules.Users;

/// <summary>
/// The users a picker opens with are the ones already chosen.
/// </summary>
/// <remarks>
/// The item selector script keeps an initial item only when it is marked selected. The picker sent its saved users
/// without the mark, so an editor opened on a saved value showed nobody, and saving the form again cleared it.
/// </remarks>
public sealed class UserPickerItemTests
{
    [Fact]
    public void AnInitialItem_IsSentMarkedSelected()
    {
        // Act
        var json = JsonSerializer.Serialize(new UserPickerItem { Value = "user-1", Text = "Agent One" });

        // Assert
        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.GetProperty("selected").GetBoolean());
    }
}
