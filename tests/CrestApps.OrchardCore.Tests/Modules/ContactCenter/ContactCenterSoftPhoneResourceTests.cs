namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterSoftPhoneResourceTests
{
    [Fact]
    public void SoftPhonePicker_UsesSingleCompatibleResourceAndPreservesBulkActions()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var moduleRoot = Path.Combine(
            repositoryRoot,
            "src",
            "Modules",
            "CrestApps.OrchardCore.ContactCenter");
        var resourceConfiguration = File.ReadAllText(Path.Combine(
            moduleRoot,
            "Services",
            "ContactCenterSoftPhoneResourceConfiguration.cs"));
        var view = File.ReadAllText(Path.Combine(
            moduleRoot,
            "Views",
            "Items",
            "ContactCenterSoftPhoneWork.View.cshtml"));
        var script = File.ReadAllText(Path.Combine(
            moduleRoot,
            "wwwroot",
            "scripts",
            "contact-center-soft-phone.js"));

        // Act
        var bulkActionCount = view.Split("data-actions-box=\"true\"", StringSplitOptions.None).Length - 1;

        // Assert
        Assert.Contains(
            ".SetDependencies(\"telephony-soft-phone\", \"crestapps-bootstrap-select\")",
            resourceConfiguration,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            ".SetDependencies(\"telephony-soft-phone\", \"bootstrap-select\")",
            resourceConfiguration,
            StringComparison.Ordinal);
        Assert.Contains("<style asp-name=\"crestapps-bootstrap-select\">", view, StringComparison.Ordinal);
        Assert.DoesNotContain("<style asp-name=\"bootstrap-select\">", view, StringComparison.Ordinal);
        Assert.Equal(2, bulkActionCount);
        Assert.Contains("data-count-selected-text=\"{0} queues selected\"", view, StringComparison.Ordinal);
        Assert.Contains("data-count-selected-text=\"{0} campaigns selected\"", view, StringComparison.Ordinal);
        Assert.Contains("window.Selectpicker.getOrCreateInstance(select)", script, StringComparison.Ordinal);
        Assert.Contains("root.querySelectorAll('[data-contact-center-picker]')", script, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ??
            throw new InvalidOperationException("Unable to locate the repository root.");
    }
}
