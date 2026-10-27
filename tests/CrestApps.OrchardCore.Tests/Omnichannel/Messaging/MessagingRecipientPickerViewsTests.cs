namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// The new conversation and broadcast composers share one recipient picker script, which searches contacts only once it
/// knows the sending channel and offers the flag-and-number entry only when the page has its markup. A page that leaves
/// either out still renders, but its picker finds nobody and never offers a typed number.
/// </summary>
public sealed class MessagingRecipientPickerViewsTests
{
    [Theory]
    [InlineData("Admin", "_Compose.cshtml")]
    [InlineData("Broadcasts", "Create.cshtml")]
    public void Composer_WiresTheRecipientPickerToTheChannelAndTheNumberEntry(string folder, string fileName)
    {
        // Arrange
        var view = File.ReadAllText(Path.Combine(GetModuleRoot(), "Views", folder, fileName));

        // Assert
        Assert.Contains("data-messaging-contact-picker", view, StringComparison.Ordinal);
        Assert.Contains("data-endpoint-select=\"#endpoint-select\"", view, StringComparison.Ordinal);
        Assert.Contains("data-endpoint-channels=\"@JsonSerializer.Serialize(Model.EndpointChannels)\"", view, StringComparison.Ordinal);
        Assert.Contains("data-number-entry=\"#recipient-number-entry\"", view, StringComparison.Ordinal);
        Assert.Contains("id=\"recipient-number-entry\"", view, StringComparison.Ordinal);
        Assert.Contains("data-number-toggle=\"#recipient-number-toggle\"", view, StringComparison.Ordinal);
        Assert.Contains("id=\"recipient-number-toggle\"", view, StringComparison.Ordinal);
        Assert.Contains("<partial name=\"_RecipientNumberEntry\" />", view, StringComparison.Ordinal);
        Assert.Contains("<script asp-name=\"intl-tel-input\"", view, StringComparison.Ordinal);
        Assert.Contains("<style asp-name=\"intl-tel-input\"", view, StringComparison.Ordinal);
    }

    [Fact]
    public void RecipientNumberEntry_HasTheControlsThePickerScriptLooksFor()
    {
        // Arrange
        var moduleRoot = GetModuleRoot();
        var partial = File.ReadAllText(Path.Combine(moduleRoot, "Views", "Shared", "_RecipientNumberEntry.cshtml"));
        var script = File.ReadAllText(Path.Combine(moduleRoot, "Assets", "js", "messaging-contact-picker.js"));

        // Assert
        foreach (var attribute in new[] { "data-number-input", "data-number-add", "data-number-error" })
        {
            Assert.Contains(attribute, partial, StringComparison.Ordinal);
            Assert.Contains($"[{attribute}]", script, StringComparison.Ordinal);
        }
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
