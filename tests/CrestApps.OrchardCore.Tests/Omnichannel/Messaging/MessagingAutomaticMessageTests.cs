using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// An out-of-office or other automatic message showed in the thread like anything the contact wrote. The channel-neutral
/// flag lets a channel's receiver mark it, and the bubble shows the mark on every channel.
/// </summary>
public sealed class MessagingAutomaticMessageTests
{
    [Fact]
    public void IsAutomatic_OnAMessageNothingMarked_IsFalse()
    {
        // Arrange
        var message = new OmnichannelMessage { Id = "m1" };

        // Act
        var isAutomatic = message.IsAutomatic();

        // Assert
        Assert.False(isAutomatic);
    }

    [Fact]
    public void SetAutomatic_True_MarksTheMessage()
    {
        // Arrange
        var message = new OmnichannelMessage { Id = "m1" };

        // Act
        message.SetAutomatic(true);

        // Assert
        Assert.True(message.IsAutomatic());
    }

    [Fact]
    public void SetAutomatic_False_OnAMarkedMessage_ClearsTheMark()
    {
        // Arrange
        var message = new OmnichannelMessage { Id = "m1" };
        message.SetAutomatic(true);

        // Act
        message.SetAutomatic(false);

        // Assert
        Assert.False(message.IsAutomatic());
    }

    // Most messages are written by a person; marking them as such stores nothing on them.
    [Fact]
    public void SetAutomatic_False_OnAnUnmarkedMessage_StoresNothing()
    {
        // Arrange
        var message = new OmnichannelMessage { Id = "m1" };

        // Act
        message.SetAutomatic(false);

        // Assert
        Assert.False(message.Properties.TryGetPropertyValue(nameof(MessagingMessageOrigin), out _));
    }

    // The bubble is rendered on the first load of a thread and again for every message appended live, so the mark is read
    // from the message in the bubble itself rather than from whichever path built its view model.
    [Fact]
    public void MessageBubble_MarksAutomaticMessages_AndTheAutoReply()
    {
        // Arrange
        var bubble = File.ReadAllText(Path.Combine(GetModuleRoot(), "Views", "Admin", "_MessageBubble.cshtml"));

        // Act & Assert
        Assert.Contains("message.IsAutomatic()", bubble, StringComparison.Ordinal);
        Assert.Contains("data-message-automatic", bubble, StringComparison.Ordinal);
        Assert.Contains("data-message-auto-reply", bubble, StringComparison.Ordinal);
    }

    private static string GetModuleRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Unable to locate the repository root.");
        }

        return Path.Combine(directory.FullName, "src", "Modules", "CrestApps.OrchardCore.Omnichannel.Messaging");
    }
}
